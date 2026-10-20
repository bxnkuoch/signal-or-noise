using Microsoft.EntityFrameworkCore;
using SignalOrNoise.Core;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Set the ConnectionStrings__Default environment variable (see README).");
builder.Services.AddDbContext<NewsDbContext>(options => options.UseSqlServer(connectionString));

var app = builder.Build();

app.MapGet("/health", async (NewsDbContext db) =>
    await db.Database.CanConnectAsync() ? Results.Ok("ok") : Results.Problem("Database unreachable"));

app.Run();
