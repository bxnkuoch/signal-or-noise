# Signal or Noise: eval results

Generated 2026-10-30 01:22 UTC by `eval/eval.py` from `eval/data/` (exported with `eval/export.sh`). Every number below comes from that run; nothing is edited by hand.

## Data

- **Source: live RSS only** (Yahoo per-ticker + CNBC), polled from 2026-10-20 23:21 UTC to 2026-10-29 19:44 UTC. No historical dataset is used, so there are no historical-dataset results to report separately.
- Scored by `claude-haiku-5-5` (prompt `v1`). Live headlines are only a clean test if they were published after the model's training cutoff; otherwise the model may already know what happened.
- 5364 scored stories, 2596 with a primary ticker, collapsed to 388 ticker-sessions whose event session has closed; 388 of those have the 20-session volatility history needed to normalize.
- Prices: adjusted daily closes from yfinance, through 2026-10-28.

## Method

- **Unit:** one primary ticker × one event session; materiality is the max over its stories, the headline count is the sum of headlines the scorer saw, and direction comes from the top story.
- **Event session:** close before the scoring cutoff → next close after it. For headlines during market hours this window starts *before* the headline, so it measures the reaction, including moves that may have caused the news. **Next session** (the following close-to-close) is the clean predictive test.
- **Abnormal return** = stock − SPY, divided by the stock's standard deviation of abnormal returns over the 20 sessions before the event session. The target is the absolute value of that.
- **Spearman ρ** with a 2000-resample percentile bootstrap 95% CI (seed 0), resampling ticker-sessions. Baselines are tested on the same resamples, so the LLM − baseline difference has a CI.
- **Baseline (b)** matches the visible headlines against `\b(?:earnings|eps|quarterly results|q[1-4] (?:results|earnings))\b` (case-insensitive).

## Event session

n = 388 ticker-sessions.

| Predictor of \|normalized abnormal return\| | Spearman ρ | 95% CI | p |
|---|---|---|---|
| Materiality (LLM) | 0.088 | [-0.009, 0.182] | 0.085 |
| Headline count (baseline a) | 0.025 | [-0.076, 0.124] | 0.623 |
| Earnings keyword (baseline b) | 0.138 | [0.044, 0.236] | 0.006 |

Materiality CI includes 0: no evidence that materiality predicts move size.

| LLM − baseline | Δρ | 95% CI | LLM beats it? |
|---|---|---|---|
| vs Headline count (baseline a) | 0.063 | [-0.031, 0.151] | not shown (CI includes 0) |
| vs Earnings keyword (baseline b) | -0.050 | [-0.170, 0.060] | not shown (CI includes 0) |

| Materiality quintile | n | Materiality range | Mean \|normalized abnormal return\| |
|---|---|---|---|
| Top | 78 | 5–8 | 1.24 |
| Bottom | 78 | 1–2 | 0.80 |

**Direction hit rate** (top story's direction ≠ 0): 89/176 = 50.6%, Wilson 95% CI [43.2%, 57.9%], binomial p = 0.940 vs 50%.

## Next session

n = 342 ticker-sessions.

| Predictor of \|normalized abnormal return\| | Spearman ρ | 95% CI | p |
|---|---|---|---|
| Materiality (LLM) | 0.041 | [-0.065, 0.147] | 0.452 |
| Headline count (baseline a) | 0.051 | [-0.058, 0.156] | 0.351 |
| Earnings keyword (baseline b) | 0.118 | [0.016, 0.227] | 0.028 |

Materiality CI includes 0: no evidence that materiality predicts move size.

| LLM − baseline | Δρ | 95% CI | LLM beats it? |
|---|---|---|---|
| vs Headline count (baseline a) | -0.010 | [-0.108, 0.082] | not shown (CI includes 0) |
| vs Earnings keyword (baseline b) | -0.078 | [-0.204, 0.049] | not shown (CI includes 0) |

| Materiality quintile | n | Materiality range | Mean \|normalized abnormal return\| |
|---|---|---|---|
| Top | 69 | 5–8 | 1.01 |
| Bottom | 69 | 1–2 | 0.86 |

**Direction hit rate** (top story's direction ≠ 0): 77/150 = 51.3%, Wilson 95% CI [43.4%, 59.2%], binomial p = 0.807 vs 50%.

## Pipeline

- 16799 feed polls, 9 failed. LLM cost 1.3939 USD in total; p50 latency per (non-cached) LLM call 1810 ms.
- No fully covered day yet, so no per-day averages.

| ET date | Coverage | Headlines | Stories | LLM cost (USD) |
|---|---|---|---|---|
| 2026-10-14 | partial | 4 | 4 | 0.0000 |
| 2026-10-15 | partial | 18 | 18 | 0.0000 |
| 2026-10-16 | partial | 24 | 22 | 0.0000 |
| 2026-10-17 | partial | 8 | 8 | 0.0000 |
| 2026-10-18 | partial | 8 | 8 | 0.0000 |
| 2026-10-19 | partial | 179 | 174 | 0.0000 |
| 2026-10-20 | partial | 552 | 506 | 0.0582 |
| 2026-10-21 | partial | 991 | 820 | 0.3421 |
| 2026-10-22 | partial | 1011 | 866 | 0.2272 |
| 2026-10-23 | partial | 537 | 473 | 0.1067 |
| 2026-10-24 | partial | 38 | 38 | 0.0000 |
| 2026-10-25 | partial | 41 | 41 | 0.0000 |
| 2026-10-26 | partial | 494 | 468 | 0.0773 |
| 2026-10-27 | partial | 993 | 838 | 0.2960 |
| 2026-10-28 | partial | 727 | 655 | 0.1700 |
| 2026-10-29 | partial | 676 | 465 | 0.1164 |

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
- 2026-10-23 05:07 → 2026-10-23 06:14 UTC (1.1 h without a poll)
- 2026-10-23 17:13 → 2026-10-27 03:27 UTC (82.2 h without a poll)
- 2026-10-27 22:13 → 2026-10-27 22:44 UTC (0.5 h without a poll)
- 2026-10-28 00:30 → 2026-10-28 01:50 UTC (1.3 h without a poll)
- 2026-10-28 06:21 → 2026-10-28 14:07 UTC (7.8 h without a poll)
- 2026-10-28 14:23 → 2026-10-28 23:04 UTC (8.7 h without a poll)
- 2026-10-28 23:37 → 2026-10-29 00:07 UTC (0.5 h without a poll)
- 2026-10-29 07:08 → 2026-10-29 09:07 UTC (2.0 h without a poll)
- 2026-10-29 09:53 → 2026-10-29 10:26 UTC (0.5 h without a poll)
- 2026-10-29 11:42 → 2026-10-29 14:01 UTC (2.3 h without a poll)
- 2026-10-29 14:22 → 2026-10-29 14:59 UTC (0.6 h without a poll)
- 2026-10-29 16:10 → 2026-10-29 17:22 UTC (1.2 h without a poll)
- 2026-10-29 17:22 → 2026-10-29 17:53 UTC (0.5 h without a poll)
- 2026-10-29 18:21 → 2026-10-29 18:55 UTC (0.6 h without a poll)

![Materiality vs move size](scatter.png)
