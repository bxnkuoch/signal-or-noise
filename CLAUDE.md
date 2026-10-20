# CLAUDE.md

Behavioral guidelines to reduce common LLM coding mistakes. Merge with project-specific instructions as needed.

**Tradeoff:** These guidelines bias toward caution over speed. For trivial tasks, use judgment.

## 1. Think Before Coding

**Don't assume. Don't hide confusion. Surface tradeoffs.**

Before implementing:
- State your assumptions explicitly. If uncertain, ask.
- If multiple interpretations exist, present them - don't pick silently.
- If a simpler approach exists, say so. Push back when warranted.
- If something is unclear, stop. Name what's confusing. Ask.

## 2. Simplicity First

**Minimum code that solves the problem. Nothing speculative.**

- No features beyond what was asked.
- No abstractions for single-use code.
- No "flexibility" or "configurability" that wasn't requested.
- No error handling for impossible scenarios.
- If you write 200 lines and it could be 50, rewrite it.

Ask yourself: "Would a senior engineer say this is overcomplicated?" If yes, simplify.

## 3. Surgical Changes

**Touch only what you must. Clean up only your own mess.**

When editing existing code:
- Don't "improve" adjacent code, comments, or formatting.
- Don't refactor things that aren't broken.
- Match existing style, even if you'd do it differently.
- If you notice unrelated dead code, mention it - don't delete it.

When your changes create orphans:
- Remove imports/variables/functions that YOUR changes made unused.
- Don't remove pre-existing dead code unless asked.

The test: Every changed line should trace directly to the user's request.

## 4. Goal-Driven Execution

**Define success criteria. Loop until verified.**

Transform tasks into verifiable goals:
- "Add validation" → "Write tests for invalid inputs, then make them pass"
- "Fix the bug" → "Write a test that reproduces it, then make it pass"
- "Refactor X" → "Ensure tests pass before and after"

For multi-step tasks, state a brief plan:
```
1. [Step] → verify: [check]
2. [Step] → verify: [check]
3. [Step] → verify: [check]
```

Strong success criteria let you loop independently. Weak criteria ("make it work") require constant clarification.

---

**These guidelines are working if:** fewer unnecessary changes in diffs, fewer rewrites due to overcomplication, and clarifying questions come before implementation rather than after mistakes.

---

# Project: Signal or Noise

Portfolio project for a SIG technology co-op application. [PLAN.md](PLAN.md) is the spec: stack, pipeline, eval design, and milestones. Read it before starting work, and update it when a decision changes the plan.

## Working agreement
- Work one milestone at a time (M0–M5). Stop at the end of each so I can review and understand the code. Briefly explain key design choices; I need to be able to defend them in interviews.
- Commit only at the end of a milestone, after I've reviewed it.
- Ask before adding any NuGet, npm, or pip package. Already approved: EF Core (SqlServer, Design, dotnet-ef), Microsoft.Extensions.Hosting, xUnit, Polly, the official Anthropic C# SDK; pandas, scipy, yfinance (eval); React, Vite, TypeScript (web).
- Interfaces only where they help testing: LLM client, feed reader, clock. No other abstractions.

## Layout
- `src/SignalOrNoise.Core`: entities, `NewsDbContext`, EF migrations
- `src/SignalOrNoise.Ingestor`: worker service (RSS ingest, later clustering and scoring)
- `src/SignalOrNoise.Api`: Minimal API
- `tests/SignalOrNoise.Tests`: xUnit
- `web/` (M3), `eval/` (M4)

Shared build settings (net10.0, nullable, implicit usings) live in `Directory.Build.props`, not in each `.csproj`. The solution file is `SignalOrNoise.slnx`.

## Commands
```bash
docker compose up -d                       # SQL Server (needs .env; see .env.example)
dotnet tool restore                        # dotnet-ef, pinned in .config/dotnet-tools.json
dotnet build
dotnet test
dotnet ef migrations add <Name> --project src/SignalOrNoise.Core
dotnet ef database update --project src/SignalOrNoise.Core
```
Apps and EF tooling read the `ConnectionStrings__Default` environment variable.

## Conventions
- Store all timestamps in UTC (`DateTime` named `...Utc`). Convert to US/Eastern only where the eval maps times to market sessions.
- Never commit secrets: no API keys, passwords, or connection strings with passwords in code, appsettings, or commits. Use env vars / user-secrets.
- Unit tests must not need SQL Server or the network. Use fakes for the LLM client, feed reader, and clock, so CI runs without a database container.
- Schema changes go through EF migrations, never hand-edited SQL.
- Dev machine is an Apple Silicon Mac; SQL Server runs under Rosetta (`platform: linux/amd64`).

## Eval integrity (non-negotiable)
- No lookahead: the scorer sees only headlines published at or before the story's `ScoredAsOfUtc`, and returns are measured from after it.
- Never write, estimate, or round up eval numbers by hand. Every number in the README, results.md, or the resume bullet must come from an actual eval run.
- Report live (post-model-cutoff) and historical-dataset results separately, and keep the training-data contamination caveat.
- If a result is weak or not significant, say so plainly.
