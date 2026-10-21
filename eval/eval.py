"""Does LLM materiality predict price moves? Reads eval/data/*.tsv (from export.sh), downloads daily
closes with yfinance, and writes eval/results.md and eval/scatter.png. See PLAN.md "Eval" for the design.

    eval/.venv/bin/python eval/eval.py
"""
import csv
import re
import warnings
from datetime import time, timedelta
from pathlib import Path

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np
import pandas as pd
import yfinance as yf
from scipy import stats

EVAL_DIR = Path(__file__).resolve().parent
DATA_DIR = EVAL_DIR / "data"
ET = "America/New_York"
CLOSE = time(16, 0)
VOL_WINDOW = 20         # trading days of abnormal returns used for the volatility estimate
BOOTSTRAP = 2000
SEED = 0                # fixed so reruns on the same data give the same CIs
MIN_N = 10              # below this, stats for a window are not reported
POLL_GAP = pd.Timedelta(minutes=30)  # polls run every 15 min, so a longer silence is a collection gap
# Baseline (b). Fixed before looking at any returns; deliberately plain, like a rule a desk could write in a minute.
EARNINGS = re.compile(r"\b(?:earnings|eps|quarterly results|q[1-4] (?:results|earnings))\b", re.IGNORECASE)
WINDOWS = {"event": "Event session", "next": "Next session"}


def read_tsv(name):
    df = pd.read_csv(DATA_DIR / name, sep="\t", quoting=csv.QUOTE_NONE, keep_default_na=False, na_values=["NULL"])
    for col in [c for c in df.columns if c.endswith("Utc")]:
        df[col] = pd.to_datetime(df[col]).dt.tz_localize("UTC")
    return df


def latest_scores(scores):
    """One score per story: the most recent (a story can be rescored under a new model or prompt version)."""
    return scores.sort_values("CreatedUtc").groupby("StoryId").tail(1)


def earnings_flags(scores, headlines):
    """True per story if any headline the scorer could see (published at or before its cutoff) mentions earnings."""
    h = headlines.merge(scores[["StoryId", "ScoredAsOfUtc"]], on="StoryId")
    h = h[h.PublishedUtc <= h.ScoredAsOfUtc]
    flags = h.assign(hit=h.Title.str.contains(EARNINGS)).groupby("StoryId").hit.any()
    return scores.StoryId.map(flags).fillna(False).astype(bool)


def event_session(t_utc, trading_days):
    """The first trading day whose 4pm ET close is strictly after t: news before the close belongs to that
    day's session, news after it (evening, weekend, holiday) to the next session. None if that session
    isn't in trading_days yet (it hasn't closed)."""
    t = t_utc.tz_convert(ET)
    day = pd.Timestamp(t.date())
    if t.time() >= CLOSE:
        day += pd.Timedelta(days=1)
    i = trading_days.searchsorted(day)
    return trading_days[i] if i < len(trading_days) else None


def ticker_sessions(stories, trading_days):
    """Collapse scored stories to one row per primary ticker x event session (the unit of analysis), so
    rewordings of one event that share a single stock return aren't counted as separate observations."""
    s = stories[stories.PrimaryTicker.notna()].copy()
    s["Session"] = [event_session(t, trading_days) for t in s.ScoredAsOfUtc]
    s = s[s.Session.notna()]
    # The top story (highest materiality, earliest cutoff on ties) supplies the direction call.
    s = s.sort_values(["Materiality", "ScoredAsOfUtc"], ascending=[False, True])
    g = s.groupby(["PrimaryTicker", "Session"], sort=True)
    return pd.DataFrame({
        "Materiality": g.Materiality.max(),
        "Direction": g.Direction.first(),
        "HeadlineCount": g.HeadlineCount.sum(),  # only headlines the scorer saw, so no lookahead
        "Earnings": g.Earnings.any(),
        "Stories": g.size(),
    }).reset_index().rename(columns={"PrimaryTicker": "Ticker"})


def abnormal_returns(closes):
    """Daily stock return minus SPY return, one column per ticker, indexed by trading day."""
    r = closes.pct_change(fill_method=None).iloc[1:]
    return r.drop(columns="SPY").sub(r.SPY, axis=0)


def add_returns(ts, ar):
    """Attach event- and next-session abnormal returns, each divided by the stock's standard deviation of
    abnormal returns over the VOL_WINDOW sessions before the event session (none of the event's own data).
    Rows without a full volatility history or an event-session return are dropped; the next-session
    return is NaN until that session has closed."""
    rows = []
    for r in ts.itertuples():
        col = ar[r.Ticker]
        i = ar.index.get_loc(r.Session)
        history = col.iloc[max(0, i - VOL_WINDOW):i]
        sigma = history.std() if history.notna().sum() == VOL_WINDOW else np.nan
        rows.append((col.iloc[i], col.iloc[i + 1] if i + 1 < len(col) else np.nan, sigma))
    out = ts.copy()
    out[["ar_event", "ar_next", "sigma"]] = pd.DataFrame(rows, index=ts.index, columns=range(3), dtype=float)
    out = out[out.sigma.notna() & out.ar_event.notna()].copy()
    for w in WINDOWS:
        out[f"z_{w}"] = out[f"ar_{w}"] / out.sigma
    return out


def load_closes(tickers, start):
    """Adjusted daily closes for tickers + SPY, keeping only sessions that have closed: during the
    trading day yfinance returns today's row with the live price in it."""
    px = yf.download([*tickers, "SPY"], start=start, auto_adjust=True, progress=False)["Close"]
    # yfinance logs a failed ticker and returns an empty column; fail instead of silently shrinking n.
    missing = [t for t in [*tickers, "SPY"] if t not in px or px[t].isna().all()]
    if missing:
        raise SystemExit(f"No prices downloaded for {missing}; rerun.")
    now = pd.Timestamp.now(tz=ET)
    last_closed = now.date() if now.time() >= time(16, 30) else now.date() - timedelta(days=1)
    return px[px.index <= pd.Timestamp(last_closed)].dropna(how="all")


def spearman(x, y):
    with warnings.catch_warnings():
        warnings.simplefilter("ignore")  # a resample can be constant; that rho is NaN and skipped
        return stats.spearmanr(x, y).statistic


def window_stats(df, w, rng):
    d = df[df[f"z_{w}"].notna()].reset_index(drop=True)
    n = len(d)
    out = {"n": n}
    if n < MIN_N:
        return out
    y = d[f"z_{w}"].abs().to_numpy()
    preds = {"Materiality (LLM)": d.Materiality.to_numpy(),
             "Headline count (baseline a)": d.HeadlineCount.to_numpy(),
             "Earnings keyword (baseline b)": d.Earnings.astype(int).to_numpy()}
    # Paired bootstrap over ticker-sessions: the same resamples feed every predictor, so the
    # LLM-minus-baseline difference gets an honest CI too.
    idx = rng.integers(0, n, size=(BOOTSTRAP, n))
    boot = {k: np.array([spearman(x[i], y[i]) for i in idx]) for k, x in preds.items()}
    llm = "Materiality (LLM)"
    out["rho"] = {}
    for k, x in preds.items():
        res = stats.spearmanr(x, y)
        out["rho"][k] = (res.statistic, *np.nanpercentile(boot[k], [2.5, 97.5]), res.pvalue)
    out["diff"] = {k: (out["rho"][llm][0] - out["rho"][k][0], *np.nanpercentile(boot[llm] - boot[k], [2.5, 97.5]))
                   for k in preds if k != llm}

    # Materiality has many ties, so quintiles are cut on a rank that breaks ties by ticker then session
    # (deterministic, not data-driven). The materiality range of each quintile is reported alongside.
    d["q"] = pd.qcut(d.Materiality.rank(method="first"), 5, labels=False)
    d["absz"] = y
    out["quintiles"] = {q: (len(g), g.Materiality.min(), g.Materiality.max(), g.absz.mean())
                        for q, g in d.groupby("q") if q in (0, 4)}

    calls = d[(d.Direction != 0) & (d[f"ar_{w}"] != 0)]
    hits = int((np.sign(calls.Direction) == np.sign(calls[f"ar_{w}"])).sum())
    if len(calls):
        bt = stats.binomtest(hits, len(calls), 0.5)
        ci = bt.proportion_ci(method="wilson")
        out["direction"] = (hits, len(calls), ci.low, ci.high, bt.pvalue)
    return out


def pipeline_stats(scores, headlines, polls):
    et = lambda s: s.dt.tz_convert(ET).dt.date
    first_poll, last_poll = polls.StartedUtc.min(), polls.StartedUtc.max()
    story_start = headlines[headlines.StoryId.notna()].groupby("StoryId").PublishedUtc.min()
    days = pd.DataFrame({
        "headlines": headlines.groupby(et(headlines.PublishedUtc)).size(),
        "stories": story_start.groupby(et(story_start)).size(),
        "cost": scores.groupby(et(scores.CreatedUtc)).CostUsd.sum(),
    }).fillna(0).sort_index()
    # A day counts as fully covered only if polling was running across the whole ET day. Earlier days
    # only have whatever was still in the feeds at the first poll.
    start = lambda d: pd.Timestamp(d).tz_localize(ET)
    days["coverage"] = ["full" if first_poll <= start(d) and last_poll >= start(d) + pd.Timedelta(days=1)
                        else "partial" for d in days.index]
    batches = polls.StartedUtc.sort_values().drop_duplicates()
    gaps = batches.diff()
    gaps = [(batches.iloc[i - 1], batches.iloc[i], gaps.iloc[i]) for i in np.flatnonzero(gaps > POLL_GAP)]
    live = scores[scores.FromCache == 0]
    return {"days": days, "gaps": gaps, "first_poll": first_poll, "last_poll": last_poll,
            "p50_latency": live.LatencyMs.median(), "total_cost": scores.CostUsd.sum(),
            "feed_failures": int(polls.Failed.sum()), "polls": len(polls)}


def plot(df, path):
    fig, axes = plt.subplots(1, 2, figsize=(10, 4), sharey=True)
    rng = np.random.default_rng(SEED)
    for ax, (w, label) in zip(axes, WINDOWS.items()):
        d = df[df[f"z_{w}"].notna()]
        jitter = rng.uniform(-0.15, 0.15, len(d))  # materiality is an integer; jitter only so dots don't stack
        ax.scatter(d.Materiality + jitter, d[f"z_{w}"].abs(), s=18, color="#2a78d6", alpha=0.6, linewidths=0)
        rho = round(spearman(d.Materiality, d[f"z_{w}"].abs()), 2) + 0.0 if len(d) >= MIN_N else float("nan")  # + 0.0: no "-0.00"
        ax.set_title(f"{label}: Spearman ρ = {rho:.2f}, n = {len(d)}", fontsize=10, color="#0b0b0b")
        ax.set_xlabel("LLM materiality (0–10)", color="#52514e")
        ax.set_xticks(range(0, 11))
        ax.grid(color="#e5e4e0", linewidth=0.6)
        ax.set_axisbelow(True)
        for side in ("top", "right"):
            ax.spines[side].set_visible(False)
    axes[0].set_ylabel("|abnormal return| / trailing 20-day σ", color="#52514e")
    fig.tight_layout()
    fig.savefig(path, dpi=150)


def fmt_p(p):
    return "<0.001" if p < 0.001 else f"{p:.3f}"


def report(meta, results, pipe):
    L = [
        "# Signal or Noise: eval results",
        "",
        f"Generated {meta['generated']} by `eval/eval.py` from `eval/data/` (exported with `eval/export.sh`). "
        "Every number below comes from that run; nothing is edited by hand.",
        "",
        "## Data",
        "",
        f"- **Source: live RSS only** (Yahoo per-ticker + CNBC), polled from {pipe['first_poll']:%Y-%m-%d %H:%M} UTC "
        f"to {pipe['last_poll']:%Y-%m-%d %H:%M} UTC. No historical dataset is used, so there are no "
        "historical-dataset results to report separately.",
        f"- Scored by {meta['models']} (prompt {meta['prompts']}). Live headlines are only a clean test if they were "
        "published after the model's training cutoff; otherwise the model may already know what happened.",
        f"- {meta['stories']} scored stories, {meta['with_ticker']} with a primary ticker, collapsed to "
        f"{meta['sessions']} ticker-sessions whose event session has closed; {meta['usable']} of those have the "
        f"{VOL_WINDOW}-session volatility history needed to normalize.",
        f"- Prices: adjusted daily closes from yfinance, through {meta['last_close']:%Y-%m-%d}.",
        "",
        "## Method",
        "",
        "- **Unit:** one primary ticker × one event session; materiality is the max over its stories, the headline "
        "count is the sum of headlines the scorer saw, and direction comes from the top story.",
        "- **Event session:** close before the scoring cutoff → next close after it. For headlines during market "
        "hours this window starts *before* the headline, so it measures the reaction, including moves that may have "
        "caused the news. **Next session** (the following close-to-close) is the clean predictive test.",
        f"- **Abnormal return** = stock − SPY, divided by the stock's standard deviation of abnormal returns over the "
        f"{VOL_WINDOW} sessions before the event session. The target is the absolute value of that.",
        f"- **Spearman ρ** with a {BOOTSTRAP}-resample percentile bootstrap 95% CI (seed {SEED}), resampling "
        "ticker-sessions. Baselines are tested on the same resamples, so the LLM − baseline difference has a CI.",
        f"- **Baseline (b)** matches the visible headlines against `{EARNINGS.pattern}` (case-insensitive).",
        "",
    ]
    for w, label in WINDOWS.items():
        r = results[w]
        L += [f"## {label}", ""]
        if r["n"] < MIN_N:
            L += [f"n = {r['n']} ticker-sessions, below the minimum of {MIN_N}. Not reported yet.", ""]
            continue
        L += [f"n = {r['n']} ticker-sessions.", "",
              "| Predictor of \\|normalized abnormal return\\| | Spearman ρ | 95% CI | p |", "|---|---|---|---|"]
        for k, (rho, lo, hi, p) in r["rho"].items():
            L.append(f"| {k} | {rho:.3f} | [{lo:.3f}, {hi:.3f}] | {fmt_p(p)} |")
        rho, lo, hi, _ = r["rho"]["Materiality (LLM)"]
        verdict = "excludes 0" if lo > 0 or hi < 0 else "includes 0: no evidence that materiality predicts move size"
        L += ["", f"Materiality CI {verdict}.", "",
              "| LLM − baseline | Δρ | 95% CI | LLM beats it? |", "|---|---|---|---|"]
        llm_signal = r["rho"]["Materiality (LLM)"][1] > 0
        for k, (dd, lo, hi) in r["diff"].items():
            if lo > 0:
                # A positive gap over a baseline that is itself negatively correlated isn't a win for the LLM.
                beats = "yes" if llm_signal else "Δρ > 0 only because the baseline is lower; materiality's own CI includes 0"
            else:
                beats = "no, baseline is better" if hi < 0 else "not shown (CI includes 0)"
            L.append(f"| vs {k} | {dd:.3f} | [{lo:.3f}, {hi:.3f}] | {beats} |")
        L += ["", "| Materiality quintile | n | Materiality range | Mean \\|normalized abnormal return\\| |",
              "|---|---|---|---|"]
        for q, name in ((4, "Top"), (0, "Bottom")):
            n, lo, hi, m = r["quintiles"][q]
            L.append(f"| {name} | {n} | {lo}–{hi} | {m:.2f} |")
        if "direction" in r:
            hits, n, lo, hi, p = r["direction"]
            L += ["", f"**Direction hit rate** (top story's direction ≠ 0): {hits}/{n} = {hits / n:.1%}, "
                      f"Wilson 95% CI [{lo:.1%}, {hi:.1%}], binomial p = {fmt_p(p)} vs 50%."]
        L.append("")
    days = pipe["days"]
    full = days[days.coverage == "full"]
    L += ["## Pipeline", "",
          f"- {pipe['polls']} feed polls, {pipe['feed_failures']} failed. LLM cost {pipe['total_cost']:.4f} USD in "
          f"total; p50 latency per (non-cached) LLM call {pipe['p50_latency']:.0f} ms.",
          (f"- Fully covered days: {len(full)}; per day on average {full.headlines.mean():.0f} headlines → "
           f"{full.stories.mean():.0f} stories, {full.cost.mean():.4f} USD."
           if len(full) else "- No fully covered day yet, so no per-day averages."),
          "", "| ET date | Coverage | Headlines | Stories | LLM cost (USD) |", "|---|---|---|---|---|"]
    for d, row in days.iterrows():
        L.append(f"| {d} | {row.coverage} | {row.headlines:.0f} | {row.stories:.0f} | {row.cost:.4f} |")
    L += ["", "Partial days only contain what was still in the feeds at the first poll, or what was collected "
              "before the last poll. Cost is by the day the score was made, so the first day includes the backfill.",
          "", "### Collection gaps", ""]
    L += ([f"- {a:%Y-%m-%d %H:%M} → {b:%Y-%m-%d %H:%M} UTC ({g.total_seconds() / 3600:.1f} h without a poll)"
           for a, b, g in pipe["gaps"]] or ["- None."])
    L += ["", "![Materiality vs move size](scatter.png)", ""]
    return "\n".join(L)


def main():
    scores, headlines, polls = read_tsv("scores.tsv"), read_tsv("headlines.tsv"), read_tsv("pollruns.tsv")
    stories = latest_scores(scores)
    stories = stories.assign(Earnings=earnings_flags(stories, headlines).to_numpy())

    first = stories.ScoredAsOfUtc.min().tz_convert(ET).date()
    tickers = sorted(stories.PrimaryTicker.dropna().unique())
    closes = load_closes(tickers, start=first - timedelta(days=60))
    ar = abnormal_returns(closes)
    ts = ticker_sessions(stories, ar.index)
    df = add_returns(ts, ar)

    rng = np.random.default_rng(SEED)
    results = {w: window_stats(df, w, rng) for w in WINDOWS}
    pipe = pipeline_stats(scores, headlines, polls)
    meta = {
        "generated": pd.Timestamp.now(tz="UTC").strftime("%Y-%m-%d %H:%M UTC"),
        "models": ", ".join(f"`{m}`" for m in stories.Model.unique()),
        "prompts": ", ".join(f"`{p}`" for p in stories.PromptVersion.unique()),
        "stories": len(stories), "with_ticker": int(stories.PrimaryTicker.notna().sum()),
        "sessions": len(ts), "usable": len(df), "last_close": ar.index.max(),
    }
    plot(df, EVAL_DIR / "scatter.png")
    (EVAL_DIR / "results.md").write_text(report(meta, results, pipe))
    print(f"Wrote {EVAL_DIR / 'results.md'} and scatter.png "
          f"(event n={results['event']['n']}, next n={results['next']['n']})")


if __name__ == "__main__":
    main()
