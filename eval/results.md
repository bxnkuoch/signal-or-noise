# Signal or Noise: eval results

Generated 2026-10-23 04:24 UTC by `eval/eval.py` from `eval/data/` (exported with `eval/export.sh`). Every number below comes from that run; nothing is edited by hand.

## Data

- **Source: live RSS only** (Yahoo per-ticker + CNBC), polled from 2026-10-20 23:21 UTC to 2026-10-23 04:22 UTC. No historical dataset is used, so there are no historical-dataset results to report separately.
- Scored by `claude-haiku-5-5` (prompt `v1`). Live headlines are only a clean test if they were published after the model's training cutoff; otherwise the model may already know what happened.
- 2412 scored stories, 1201 with a primary ticker, collapsed to 196 ticker-sessions whose event session has closed; 196 of those have the 20-session volatility history needed to normalize.
- Prices: adjusted daily closes from yfinance, through 2026-10-22.

## Method

- **Unit:** one primary ticker × one event session; materiality is the max over its stories, the headline count is the sum of headlines the scorer saw, and direction comes from the top story.
- **Event session:** close before the scoring cutoff → next close after it. For headlines during market hours this window starts *before* the headline, so it measures the reaction, including moves that may have caused the news. **Next session** (the following close-to-close) is the clean predictive test.
- **Abnormal return** = stock − SPY, divided by the stock's standard deviation of abnormal returns over the 20 sessions before the event session. The target is the absolute value of that.
- **Spearman ρ** with a 2000-resample percentile bootstrap 95% CI (seed 0), resampling ticker-sessions. Baselines are tested on the same resamples, so the LLM − baseline difference has a CI.
- **Baseline (b)** matches the visible headlines against `\b(?:earnings|eps|quarterly results|q[1-4] (?:results|earnings))\b` (case-insensitive).

## Event session

n = 196 ticker-sessions.

| Predictor of \|normalized abnormal return\| | Spearman ρ | 95% CI | p |
|---|---|---|---|
| Materiality (LLM) | 0.103 | [-0.047, 0.245] | 0.152 |
| Headline count (baseline a) | 0.089 | [-0.062, 0.238] | 0.213 |
| Earnings keyword (baseline b) | 0.217 | [0.075, 0.356] | 0.002 |

Materiality CI includes 0: no evidence that materiality predicts move size.

| LLM − baseline | Δρ | 95% CI | LLM beats it? |
|---|---|---|---|
| vs Headline count (baseline a) | 0.013 | [-0.115, 0.137] | not shown (CI includes 0) |
| vs Earnings keyword (baseline b) | -0.115 | [-0.281, 0.043] | not shown (CI includes 0) |

| Materiality quintile | n | Materiality range | Mean \|normalized abnormal return\| |
|---|---|---|---|
| Top | 39 | 4–8 | 1.37 |
| Bottom | 40 | 1–2 | 0.92 |

**Direction hit rate** (top story's direction ≠ 0): 43/84 = 51.2%, Wilson 95% CI [40.7%, 61.6%], binomial p = 0.913 vs 50%.

## Next session

n = 148 ticker-sessions.

| Predictor of \|normalized abnormal return\| | Spearman ρ | 95% CI | p |
|---|---|---|---|
| Materiality (LLM) | 0.026 | [-0.142, 0.199] | 0.750 |
| Headline count (baseline a) | 0.099 | [-0.057, 0.249] | 0.229 |
| Earnings keyword (baseline b) | 0.196 | [0.028, 0.348] | 0.017 |

Materiality CI includes 0: no evidence that materiality predicts move size.

| LLM − baseline | Δρ | 95% CI | LLM beats it? |
|---|---|---|---|
| vs Headline count (baseline a) | -0.073 | [-0.220, 0.078] | not shown (CI includes 0) |
| vs Earnings keyword (baseline b) | -0.170 | [-0.354, 0.022] | not shown (CI includes 0) |

| Materiality quintile | n | Materiality range | Mean \|normalized abnormal return\| |
|---|---|---|---|
| Top | 30 | 4–8 | 1.16 |
| Bottom | 30 | 1–2 | 1.01 |

**Direction hit rate** (top story's direction ≠ 0): 25/56 = 44.6%, Wilson 95% CI [32.4%, 57.6%], binomial p = 0.504 vs 50%.

## Pipeline

- 6845 feed polls, 9 failed. LLM cost 0.6288 USD in total; p50 latency per (non-cached) LLM call 1800 ms.
- Fully covered days: 2; per day on average 994 headlines → 837 stories, 0.2847 USD.

| ET date | Coverage | Headlines | Stories | LLM cost (USD) |
|---|---|---|---|---|
| 2026-10-14 | partial | 4 | 4 | 0.0000 |
| 2026-10-15 | partial | 18 | 18 | 0.0000 |
| 2026-10-16 | partial | 24 | 22 | 0.0000 |
| 2026-10-17 | partial | 8 | 8 | 0.0000 |
| 2026-10-18 | partial | 8 | 8 | 0.0000 |
| 2026-10-19 | partial | 179 | 174 | 0.0000 |
| 2026-10-20 | partial | 552 | 506 | 0.0582 |
| 2026-10-21 | full | 991 | 820 | 0.3421 |
| 2026-10-22 | full | 998 | 854 | 0.2272 |
| 2026-10-23 | partial | 1 | 0 | 0.0013 |

Partial days only contain what was still in the feeds at the first poll, or what was collected before the last poll. Cost is by the day the score was made, so the first day includes the backfill.

### Collection gaps

- 2026-10-21 00:26 → 2026-10-21 02:54 UTC (2.5 h without a poll)
- 2026-10-21 14:42 → 2026-10-21 18:03 UTC (3.4 h without a poll)
- 2026-10-21 21:28 → 2026-10-21 23:18 UTC (1.8 h without a poll)
- 2026-10-21 23:27 → 2026-10-22 02:37 UTC (3.2 h without a poll)
- 2026-10-22 07:53 → 2026-10-22 08:52 UTC (1.0 h without a poll)
- 2026-10-22 09:38 → 2026-10-22 10:19 UTC (0.7 h without a poll)
- 2026-10-22 12:36 → 2026-10-22 13:12 UTC (0.6 h without a poll)
- 2026-10-22 16:16 → 2026-10-22 16:49 UTC (0.5 h without a poll)
- 2026-10-22 18:03 → 2026-10-22 23:49 UTC (5.8 h without a poll)
- 2026-10-23 01:05 → 2026-10-23 02:35 UTC (1.5 h without a poll)
- 2026-10-23 02:51 → 2026-10-23 03:36 UTC (0.8 h without a poll)

![Materiality vs move size](scatter.png)
