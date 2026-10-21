using SignalOrNoise.Api;

namespace SignalOrNoise.Tests;

public class MarketDayTests
{
    private static DateTime Utc(int month, int day, int hour) => new(2026, month, day, hour, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Daylight_time_day_runs_from_4am_to_4am_utc()
    {
        var (from, to) = MarketDay.UtcRange(new DateOnly(2026, 10, 20));

        Assert.Equal(Utc(10, 20, 4), from);
        Assert.Equal(Utc(10, 21, 4), to);
    }

    [Fact]
    public void Day_daylight_saving_ends_is_25_hours()
    {
        // Clocks fall back on Sunday, November 1, 2026.
        var (from, to) = MarketDay.UtcRange(new DateOnly(2026, 11, 1));

        Assert.Equal(Utc(11, 1, 4), from);
        Assert.Equal(Utc(11, 2, 5), to);
    }

    [Fact]
    public void Day_daylight_saving_starts_is_23_hours()
    {
        // Clocks spring forward on Sunday, March 8, 2026.
        var (from, to) = MarketDay.UtcRange(new DateOnly(2026, 3, 8));

        Assert.Equal(Utc(3, 8, 5), from);
        Assert.Equal(Utc(3, 9, 4), to);
    }

    [Fact]
    public void Today_uses_the_eastern_date_not_the_utc_date()
    {
        // 02:00 UTC on Oct 21 is still 10 PM on Oct 20 in New York.
        Assert.Equal(new DateOnly(2026, 10, 20), MarketDay.Today(Utc(10, 21, 2)));
    }
}
