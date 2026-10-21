# Signal or Noise

Collapses a day of financial headlines into ~30 ranked stories, each with an LLM materiality score and a one-line summary, then measures whether those scores actually predict price moves.

> Status: M0 (scaffold). See [PLAN.md](PLAN.md) for the full spec and milestones.

## Architecture

_TODO (M5): mermaid diagram._

| Project | Role |
|---|---|
| `src/SignalOrNoise.Core` | EF Core entities, `NewsDbContext`, migrations |
| `src/SignalOrNoise.Ingestor` | Worker service: polls RSS, dedupes, stores headlines |
| `src/SignalOrNoise.Api` | ASP.NET Core Minimal API serving the digest |
| `tests/SignalOrNoise.Tests` | xUnit tests |
| `web/` | React + Vite + TypeScript digest page _(M3)_ |
| `eval/` | Python eval of scores vs. abnormal returns _(M4)_ |

## Prerequisites

- .NET 10 SDK
- Docker Desktop (on Apple Silicon: enable "Use Rosetta for x86_64/amd64 emulation")

## Configuration and secrets

No secret is stored in the repo. Each one lives outside git:

| Setting | Used by | Where it lives |
|---|---|---|
| `MSSQL_SA_PASSWORD` | Docker (SQL Server) | `.env` in the repo root (gitignored; copy from `.env.example`) |
| `ConnectionStrings__Default` | Ingestor, API, `dotnet ef` | Environment variable, exported per terminal (step 2 below) |
| `ANTHROPIC_API_KEY` | Story scoring (Claude API) | Environment variable exported from `~/.zshrc`, outside the repo |

To set the API key, add this line to the bottom of `~/.zshrc`, then run `source ~/.zshrc` (new terminals load it automatically):

```bash
export ANTHROPIC_API_KEY="sk-ant-..."
```

Check it without printing the key: `echo ${ANTHROPIC_API_KEY:0:7}` should print `sk-ant-`. The .NET apps don't read `.env`, so the key doesn't go there.

## Run locally

```bash
# 1. Start SQL Server
cp .env.example .env            # then edit the password
docker compose up -d
docker compose ps               # wait until sqlserver shows (healthy)

# 2. Point the apps and EF tooling at it (same password as .env)
export ConnectionStrings__Default="Server=localhost,1433;Database=SignalOrNoise;User Id=sa;Password=<your password>;TrustServerCertificate=True"

# 3. Create the schema
dotnet tool restore
dotnet ef database update --project src/SignalOrNoise.Core

# 4. Run
dotnet run --project src/SignalOrNoise.Ingestor   # polls all feeds every 15 minutes until Ctrl+C
dotnet run --project src/SignalOrNoise.Api        # then: curl http://localhost:<port>/health

# Tests
dotnet test
```

## Collect data continuously

The eval needs days of headlines, and each Yahoo feed only holds about a day's worth, so leave the Ingestor running. On a Mac, `caffeinate -i` keeps the machine from idle-sleeping while it runs. Keep it plugged in; closing the lid still sleeps it.

```bash
caffeinate -i dotnet run --project src/SignalOrNoise.Ingestor
```

Every poll of every feed is recorded in the `PollRuns` table, including failures, so gaps in collection show up in the data.

## Results

_TODO (M4/M5): headlines/day, stories/day, LLM cost/day, eval numbers (correlation, CI, n, baselines, both return windows)._

## Limitations

_TODO (M5)._
