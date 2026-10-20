using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SignalOrNoise.Core;

// Used only by the `dotnet ef` CLI so migrations can be created and applied from this project.
public class NewsDbContextFactory : IDesignTimeDbContextFactory<NewsDbContext>
{
    public NewsDbContext CreateDbContext(string[] args)
    {
        // `migrations add` never connects, so a placeholder is fine there; `database update` needs the real one.
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Server=localhost,1433;Database=SignalOrNoise;TrustServerCertificate=True";

        var options = new DbContextOptionsBuilder<NewsDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new NewsDbContext(options);
    }
}
