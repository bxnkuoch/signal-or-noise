I want to build a portfolio project called **Signal or Noise** (a trader news digest). It collapses a day of financial headlines into ~30 ranked stories, each with an LLM materiality score and a one-line summary, and then measures whether the scores actually predict price moves. It's for my resume (SIG technology co-op), so it should show C#/.NET, a real data pipeline, SQL Server, and LLM engineering with an honest eval.

Realistic size: ~25–30 hours of build time, plus 1–2 weeks of unattended data collection.

## Stack (keep it to this)
- .NET 10 (current LTS) solution, C#: a Worker ingestion service + ASP.NET Core Minimal API
- SQL Server 2022 in Docker, EF Core with migrations. On Apple Silicon the image runs under Rosetta (`platform: linux/amd64`); confirm `docker compose up` works before writing EF code
- LLM: Claude API via the official Anthropic C# SDK (fall back to HttpClient if needed). Use a cheap model (Haiku) by default, model name in config, API key from env var / user-secrets, never committed
- Python 3 only for the eval (pandas, scipy, yfinance), in /eval
- React + Vite + TypeScript thin frontend in /web (one page, no UI library needed)
- docker-compose for SQL Server; README with exact run commands

## Pipeline
1. **Ingest**: poll RSS feeds on a timer: Yahoo Finance per-ticker headline RSS for 50 large-cap tickers, plus CNBC top news as a general market feed. Feed choices (checked Oct 20, 2026):
   - Yahoo per-ticker RSS works, but each feed holds only ~13–20 items (about a day), so history builds up only by running continuously.
   - Yahoo's general `rssindex` feed is dead (returns HTML).
   - Nasdaq per-ticker RSS blocks non-browser User-Agents. Not used: we send an honest User-Agent and don't impersonate a browser.
   - Google News RSS has months of history, but its terms allow only rendering in a personal feed reader. Not used. Normalize, dedupe on URL + normalized title hash, store `Headline(Id, Source, Url, Title, Summary, PublishedUtc, IngestedUtc, Tickers)`. Polly retries, structured logging, idempotent upserts. Record each poll run (start time, feed, items fetched) so collection gaps (laptop asleep, network down) are visible and can be reported.
2. **Cluster**: group headlines about the same event. Start simple: same ticker + 24h window + title token overlap (Jaccard). Add an LLM confirm/split step **only if** the simple grouping is visibly bad on real data. Store `Story` with its member headlines.
3. **Score**: per story, one LLM call returning strict JSON: `{event_type, primary_ticker, materiality 0-10, direction -1/0/1, one_line_summary, rationale}`. `primary_ticker` comes from the headline text (one of the 50 tickers, or NONE) because Yahoo's feed tags are noisy; the eval uses it, not the feed tags. Use the API's structured output / tool-use schema so the model is forced into the shape; keep schema validation and a retry as a backstop. Cache by story hash so reruns don't re-bill. Log tokens and cost per call. Prompt and model version stored on every score row.
   - **No lookahead inside a cluster.** A story's score uses only the headlines published within a fixed scoring window after its first headline (start with 30 minutes). Store that cutoff time `ScoredAsOfUtc` on the score row. Later headlines can still join the story for display, but they are never shown to the scorer. The prompt also tells the model to ignore any headline text that reports the stock's price move itself.
4. **Digest API**: `GET /digest?date=` returns the top 30 stories by materiality; `GET /stories/{id}` returns member headlines.
5. **Web**: one page listing the digest: score, ticker, summary, source links, date picker.

## Eval (the part that makes this resume-worthy)
- For each scored story with a single primary ticker, compute the **abnormal return** = stock return minus SPY return, over **two windows**, reported separately:
  - **Event session**: from the last close before `ScoredAsOfUtc` to the next close after it (captures the same-day reaction for headlines during market hours).
  - **Next session**: the full trading session after the event session (tests whether the score predicts moves the market hasn't priced in yet).
  - After-hours/weekend headlines map to the next open.
- **Volatility-normalize**: divide each abnormal return by the stock's trailing 20-trading-day standard deviation of abnormal returns (computed only from data before the event). This compares surprises, not how volatile each stock normally is.
- No lookahead: the LLM only sees headline text published at or before `ScoredAsOfUtc`, and returns are measured from after it.
- Report, for each window:
  - Spearman correlation between materiality and |normalized abnormal return|, with a bootstrap 95% CI and n.
  - Mean |normalized abnormal return| for top vs bottom materiality quintile.
  - **Baselines the LLM has to beat**: (a) number of headlines in the cluster; (b) an earnings-only flag (is this story about earnings, yes/no). If the LLM doesn't beat (b), say so.
  - **Direction hit rate**: for stories with direction ≠ 0, the fraction where sign(direction) matches sign(abnormal return), with n and a CI.
- Also report pipeline stats: headlines in → stories out per day, cost per day, p50 latency per LLM call, and collection gaps.
- Power: a few hundred stories can only detect a correlation of roughly 0.1–0.15 or larger. A null result is likely and fine; report it plainly.
- Output a markdown report to /eval/results.md and a scatter plot PNG.
- Data volume: live RSS only goes back days, so the ingester runs continuously for 1–2 weeks to collect a few hundred stories. If I want results sooner, add an optional importer for a public historical financial-headlines dataset (with timestamps) and label which data each number came from. **Historical data published before the model's training cutoff is contaminated**: the model may already know what happened to those stocks. Report live and historical results separately and explain this in the README; only live, post-cutoff data counts as a clean test.

## Milestones (each ends with a commit and a working state)
- **M0 (1–2h)**: repo scaffold, solution, docker-compose SQL Server (verified on Apple Silicon), EF migrations, .gitignore, README skeleton
- **M1 (4–5h)**: RSS ingestion running end to end into SQL Server, with dedupe, poll-run logging, and unit tests (xUnit) for normalization and dedupe. **Then start the ingester running continuously right away** (keep the Mac awake with `caffeinate`, or use a cheap VM). The data collection clock starts here; scoring stored headlines later is fine because scoring only uses text up to `ScoredAsOfUtc`.
- **M2 (5–7h)**: clustering + scoring with the LLM, structured output + validation, caching, cost logging; tests using a fake LLM client behind an interface
- **M3 (3–4h)**: Minimal API + React digest page
- **M4 (5–6h)**: Python eval producing results.md and the plot (both windows, volatility normalization, both baselines, direction hit rate)
- **M5 (2h)**: README polish: architecture diagram (mermaid), screenshot, how to run, the eval numbers, limitations

## Definition of done
- Public GitHub repo, clean commit history, `docker compose up` + documented commands run it from scratch
- Tests pass in GitHub Actions (`dotnet test`). Unit tests don't need SQL Server (fakes / in-memory), so CI needs no database container
- README states: headlines/day processed, stories/day, LLM cost/day, and the eval numbers (correlation, CI, n, both baselines, both windows), plus the training-data contamination caveat
- No secrets in the repo

## How to work with me
- Start with M0 and stop after each milestone so I can review and understand the code before moving on. Explain key design choices briefly.
- Prefer simple, idiomatic C# over clever abstractions. Interfaces only where they help testing (LLM client, feed reader, clock). In M1, parsing, normalization and dedupe are pure functions tested directly, so no feed-reader or clock interface was needed.
- Ask me before adding any dependency not listed above.
- At the end, draft one resume bullet that uses only numbers the eval actually produced.
