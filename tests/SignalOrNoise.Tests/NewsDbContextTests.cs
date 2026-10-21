using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SignalOrNoise.Core;

namespace SignalOrNoise.Tests;

// These build the EF model without opening a connection, so they run in CI with no SQL Server.
public class NewsDbContextTests
{
    private static NewsDbContext CreateContext() => new NewsDbContextFactory().CreateDbContext([]);

    private static IIndex IndexOn(NewsDbContext db, string propertyName) =>
        db.Model.FindEntityType(typeof(Headline))!
            .GetIndexes()
            .Single(i => i.Properties.Single().Name == propertyName);

    [Fact]
    public void Migrations_match_the_model()
    {
        using var db = CreateContext();

        Assert.False(db.Database.HasPendingModelChanges(),
            "The model changed without a migration. Run `dotnet ef migrations add <Name> --project src/SignalOrNoise.Core`.");
    }

    [Fact]
    public void UrlHash_index_is_unique()
    {
        using var db = CreateContext();

        Assert.True(IndexOn(db, nameof(Headline.UrlHash)).IsUnique);
    }

    [Fact]
    public void Timestamps_read_from_the_database_are_marked_utc()
    {
        using var db = CreateContext();
        var converter = db.Model.FindEntityType(typeof(Headline))!
            .FindProperty(nameof(Headline.PublishedUtc))!
            .GetValueConverter()!;

        var fromDb = (DateTime)converter.ConvertFromProvider(new DateTime(2026, 10, 20, 14, 5, 0, DateTimeKind.Unspecified))!;

        Assert.Equal(DateTimeKind.Utc, fromDb.Kind);
        Assert.Equal(new DateTime(2026, 10, 20, 14, 5, 0, DateTimeKind.Utc), fromDb);
    }

    [Fact]
    public void TitleHash_index_is_not_unique()
    {
        // The same title under different URLs is merged in code, not rejected by the database.
        using var db = CreateContext();

        Assert.False(IndexOn(db, nameof(Headline.TitleHash)).IsUnique);
    }
}
