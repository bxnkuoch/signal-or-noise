#!/usr/bin/env bash
# Dumps what the eval needs from SQL Server into eval/data/*.tsv, using the sqlcmd that ships
# in the SQL Server container (no Python database driver needed). Run from anywhere:
#   eval/export.sh
set -euo pipefail
cd "$(dirname "$0")/.."
set -a; source .env; set +a
mkdir -p eval/data

# $1 = output file, $2 = header line, $3 = query. -h -1 drops sqlcmd's own header; -W trims padding.
export_tsv() {
  {
    printf '%s\n' "$2"
    docker compose exec -T -e SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" sqlserver \
      /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -d SignalOrNoise \
      -h -1 -W -s $'\t' -Q "SET NOCOUNT ON; $3"
  } > "eval/data/$1"
  echo "eval/data/$1: $(( $(wc -l < "eval/data/$1") - 1 )) rows"
}

# Every score row; eval.py picks the latest one per story.
export_tsv scores.tsv $'StoryId\tCreatedUtc\tScoredAsOfUtc\tPrimaryTicker\tEventType\tMateriality\tDirection\tHeadlineCount\tModel\tPromptVersion\tCostUsd\tLatencyMs\tFromCache' \
  "SELECT StoryId, CreatedUtc, ScoredAsOfUtc, ISNULL(PrimaryTicker, 'NULL'), EventType, Materiality, Direction,
          HeadlineCount, Model, PromptVersion, CostUsd, LatencyMs, CAST(FromCache AS int)
   FROM StoryScores ORDER BY Id"

# Tabs and line breaks are replaced in titles so every row stays on one line with the right number of fields.
export_tsv headlines.tsv $'Id\tStoryId\tPublishedUtc\tIngestedUtc\tTitle' \
  "SELECT Id, ISNULL(CAST(StoryId AS varchar(20)), 'NULL'), PublishedUtc, IngestedUtc,
          REPLACE(REPLACE(REPLACE(Title, CHAR(9), ' '), CHAR(10), ' '), CHAR(13), ' ')
   FROM Headlines ORDER BY Id"

export_tsv pollruns.tsv $'StartedUtc\tFeed\tItemsFetched\tNewHeadlines\tFailed' \
  "SELECT StartedUtc, Feed, ItemsFetched, NewHeadlines, CASE WHEN Error IS NULL THEN 0 ELSE 1 END
   FROM PollRuns ORDER BY Id"
