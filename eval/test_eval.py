"""Tests for the parts of the eval where lookahead could sneak in. No network or database.

    eval/.venv/bin/python -m unittest discover eval
"""
import unittest

import numpy as np
import pandas as pd

import eval as ev

# Mon Oct 19 - Fri Oct 23 2026 and Mon Oct 26. US/Eastern is UTC-4 (EDT) on these dates.
DAYS = pd.DatetimeIndex(["2026-10-19", "2026-10-20", "2026-10-21", "2026-10-22", "2026-10-23", "2026-10-26"])


def utc(s):
    return pd.Timestamp(s, tz="UTC")


class EventSessionTests(unittest.TestCase):
    def check(self, t, expected, days=DAYS):
        got = ev.event_session(utc(t), days)
        self.assertEqual(got, None if expected is None else pd.Timestamp(expected))

    def test_premarket_belongs_to_same_day(self):
        self.check("2026-10-20 12:00", "2026-10-20")  # 08:00 ET

    def test_during_session_belongs_to_same_day(self):
        self.check("2026-10-20 19:59", "2026-10-20")  # 15:59 ET

    def test_at_close_belongs_to_next_day(self):
        self.check("2026-10-20 20:00", "2026-10-21")  # 16:00 ET: the close isn't after the cutoff

    def test_late_evening_utc_rolls_over_correctly(self):
        self.check("2026-10-21 02:00", "2026-10-21")  # Oct 20 22:00 ET, already Oct 21 in UTC

    def test_friday_evening_and_weekend_map_to_monday(self):
        self.check("2026-10-23 22:00", "2026-10-26")
        self.check("2026-10-24 15:00", "2026-10-26")

    def test_session_not_closed_yet_is_none(self):
        self.check("2026-10-23 21:00", None, days=DAYS[:5])  # Friday after close, Monday not in the data


class AddReturnsTests(unittest.TestCase):
    def setUp(self):
        idx = pd.bdate_range("2026-09-01", periods=25)
        self.values = np.random.default_rng(1).normal(0, 0.01, len(idx))
        self.values[21] = 0.5  # a huge event-day move must not leak into sigma
        self.ar = pd.DataFrame({"AAA": self.values}, index=idx)

    def test_sigma_uses_only_the_20_sessions_before_the_event(self):
        ts = pd.DataFrame({"Ticker": ["AAA"], "Session": [self.ar.index[21]]})
        row = ev.add_returns(ts, self.ar).iloc[0]
        self.assertAlmostEqual(row.sigma, np.std(self.values[1:21], ddof=1))
        self.assertAlmostEqual(row.ar_event, 0.5)
        self.assertAlmostEqual(row.ar_next, self.values[22])
        self.assertAlmostEqual(row.z_event, 0.5 / row.sigma)

    def test_short_history_is_dropped_and_missing_next_session_is_nan(self):
        ts = pd.DataFrame({"Ticker": ["AAA", "AAA"], "Session": [self.ar.index[19], self.ar.index[24]]})
        out = ev.add_returns(ts, self.ar)
        self.assertEqual(list(out.Session), [self.ar.index[24]])
        self.assertTrue(np.isnan(out.iloc[0].ar_next))


class TickerSessionTests(unittest.TestCase):
    def test_stories_collapse_to_one_row_per_ticker_session(self):
        stories = pd.DataFrame({
            "ScoredAsOfUtc": [utc("2026-10-20 14:00"), utc("2026-10-20 15:00"), utc("2026-10-20 16:00"),
                              utc("2026-10-20 14:00")],
            "PrimaryTicker": ["AAA", "AAA", "AAA", None],
            "Materiality": [3, 7, 7, 9],
            "Direction": [1, -1, 1, 1],
            "HeadlineCount": [1, 2, 1, 1],
            "Earnings": [False, True, False, True],
        })
        ts = ev.ticker_sessions(stories, DAYS)
        self.assertEqual(len(ts), 1)  # the NULL-ticker story is excluded
        row = ts.iloc[0]
        self.assertEqual((row.Ticker, row.Materiality, row.HeadlineCount, row.Stories), ("AAA", 7, 4, 3))
        self.assertTrue(row.Earnings)
        self.assertEqual(row.Direction, -1)  # tie on materiality 7: the earlier story wins


class EarningsFlagTests(unittest.TestCase):
    def test_only_headlines_before_the_cutoff_count(self):
        scores = pd.DataFrame({"StoryId": [1, 2], "ScoredAsOfUtc": [utc("2026-10-20 14:30")] * 2})
        headlines = pd.DataFrame({
            "StoryId": [1, 1, 2],
            "PublishedUtc": [utc("2026-10-20 14:00"), utc("2026-10-20 15:00"), utc("2026-10-20 14:00")],
            "Title": ["Apple unveils phone", "Apple Q3 earnings beat", "Nike EPS tops estimates"],
        })
        self.assertEqual(list(ev.earnings_flags(scores, headlines)), [False, True])

    def test_keyword_needs_word_boundaries(self):
        self.assertIsNone(ev.EARNINGS.search("Steps toward a deal"))
        self.assertIsNotNone(ev.EARNINGS.search("Q2 results due Thursday"))


class PipelineStatsTests(unittest.TestCase):
    def test_day_with_a_collection_gap_is_partial(self):
        # Polls every 15 min from Oct 20 00:00 ET to Oct 23 00:00 ET, except a 2 h gap on Oct 21.
        polls = pd.date_range(utc("2026-10-20 04:00"), utc("2026-10-23 04:00"), freq="15min")
        polls = polls[(polls < utc("2026-10-21 16:00")) | (polls > utc("2026-10-21 18:00"))]
        polls = pd.DataFrame({"StartedUtc": polls, "Failed": 0})
        published = [utc("2026-10-20 15:00"), utc("2026-10-21 15:00"), utc("2026-10-22 15:00")]
        headlines = pd.DataFrame({"StoryId": [1, 2, 3], "PublishedUtc": published})
        scores = pd.DataFrame({"CreatedUtc": published, "CostUsd": 0.0, "FromCache": 0, "LatencyMs": 1})
        days = ev.pipeline_stats(scores, headlines, polls)["days"]
        self.assertEqual(list(days.coverage), ["full", "partial", "full"])


if __name__ == "__main__":
    unittest.main()
