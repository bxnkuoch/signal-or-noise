using Microsoft.EntityFrameworkCore;

namespace SignalOrNoise.Core;

public class NewsDbContext(DbContextOptions<NewsDbContext> options) : DbContext(options)
{
    public DbSet<Headline> Headlines => Set<Headline>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var headline = modelBuilder.Entity<Headline>();
        headline.Property(h => h.Source).HasMaxLength(100);
        headline.Property(h => h.Url).HasMaxLength(2048);
        headline.Property(h => h.UrlHash).HasMaxLength(64).IsFixedLength();
        headline.Property(h => h.Title).HasMaxLength(1000);
        headline.Property(h => h.TitleHash).HasMaxLength(64).IsFixedLength();
        headline.Property(h => h.Tickers).HasMaxLength(200);

        headline.HasIndex(h => h.UrlHash).IsUnique();
        headline.HasIndex(h => h.TitleHash);
        headline.HasIndex(h => h.PublishedUtc);
    }
}
