using Microsoft.EntityFrameworkCore;
using SignalOrNoise.Core;
using SignalOrNoise.Ingestor;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Set the ConnectionStrings__Default environment variable (see README).");
builder.Services.AddDbContext<NewsDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddHostedService<IngestWorker>();

builder.Build().Run();
