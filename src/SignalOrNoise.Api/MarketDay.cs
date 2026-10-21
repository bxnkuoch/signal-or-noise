namespace SignalOrNoise.Api;

// The digest groups stories by US/Eastern calendar day, the day a trader in New York would see them.
public static class MarketDay
{
    private static readonly TimeZoneInfo Eastern = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

    public static DateOnly Today(DateTime utcNow) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utcNow, Eastern));

    // Start (inclusive) and end (exclusive) of an Eastern day in UTC. Daylight saving days are 23 or 25 hours.
    public static (DateTime FromUtc, DateTime ToUtc) UtcRange(DateOnly day) =>
    (
        TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue), Eastern),
        TimeZoneInfo.ConvertTimeToUtc(day.AddDays(1).ToDateTime(TimeOnly.MinValue), Eastern)
    );
}
