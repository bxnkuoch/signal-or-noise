using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace SignalOrNoise.Core;

public class NewsDbContext(DbContextOptions<NewsDbContext> options) : DbContext(options)
{
    public DbSet<Headline> Headlines => Set<Headline>();
    public DbSet<PollRun> PollRuns => Set<PollRun>();
    public DbSet<Story> Stories => Set<Story>();
    public DbSet<StoryScore> StoryScores => Set<StoryScore>();

    // SQL Server's datetime2 has no time zone, so EF reads values back as DateTimeKind.Unspecified.
    // Every timestamp here is stored in UTC, so mark it as UTC on read (JSON then ends in "Z").
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    private class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        toDb => toDb,
        fromDb => DateTime.SpecifyKind(fromDb, DateTimeKind.Utc));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var headline = modelBuilder.Entity<Headline>();
        headline.Property(h => h.Source).HasMaxLength(100);
        headline.Property(h => h.Url).HasMaxLength(2048);
        headline.Property(h => h.UrlHash).HasMaxLength(64).IsFixedLength();
        headline.Property(h => h.Title).HasMaxLength(1000);
        headline.Property(h => h.TitleHash).HasMaxLength(64).IsFixedLength();
        headline.Property(h => h.Tickers).HasMaxLength(500);

        headline.HasIndex(h => h.UrlHash).IsUnique();
        headline.HasIndex(h => h.TitleHash);
        headline.HasIndex(h => h.PublishedUtc);

        var pollRun = modelBuilder.Entity<PollRun>();
        pollRun.Property(p => p.Feed).HasMaxLength(100);
        pollRun.HasIndex(p => p.StartedUtc);

        var story = modelBuilder.Entity<Story>();
        story.HasMany(s => s.Headlines).WithOne(h => h.Story).HasForeignKey(h => h.StoryId).OnDelete(DeleteBehavior.SetNull);
        story.HasMany(s => s.Scores).WithOne(sc => sc.Story).HasForeignKey(sc => sc.StoryId);
        story.HasIndex(s => s.FirstPublishedUtc);
        story.HasIndex(s => s.ScoredAsOfUtc);

        var score = modelBuilder.Entity<StoryScore>();
        score.Property(sc => sc.InputHash).HasMaxLength(64).IsFixedLength();
        score.Property(sc => sc.Model).HasMaxLength(50);
        score.Property(sc => sc.PromptVersion).HasMaxLength(20);
        score.Property(sc => sc.EventType).HasMaxLength(30);
        score.Property(sc => sc.PrimaryTicker).HasMaxLength(10);
        score.Property(sc => sc.Summary).HasMaxLength(500);
        score.Property(sc => sc.CostUsd).HasPrecision(12, 8);
        score.HasIndex(sc => sc.InputHash);
    }
}
