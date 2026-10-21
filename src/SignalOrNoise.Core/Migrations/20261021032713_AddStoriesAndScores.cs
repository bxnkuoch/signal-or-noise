using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SignalOrNoise.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddStoriesAndScores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "StoryId",
                table: "Headlines",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Stories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FirstPublishedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ScoredAsOfUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StoryScores",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoryId = table.Column<long>(type: "bigint", nullable: false),
                    InputHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PromptVersion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ScoredAsOfUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HeadlineCount = table.Column<int>(type: "int", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PrimaryTicker = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Materiality = table.Column<int>(type: "int", nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Rationale = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    InputTokens = table.Column<int>(type: "int", nullable: false),
                    OutputTokens = table.Column<int>(type: "int", nullable: false),
                    CostUsd = table.Column<decimal>(type: "decimal(12,8)", precision: 12, scale: 8, nullable: false),
                    LatencyMs = table.Column<int>(type: "int", nullable: false),
                    FromCache = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoryScores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StoryScores_Stories_StoryId",
                        column: x => x.StoryId,
                        principalTable: "Stories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Headlines_StoryId",
                table: "Headlines",
                column: "StoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Stories_FirstPublishedUtc",
                table: "Stories",
                column: "FirstPublishedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Stories_ScoredAsOfUtc",
                table: "Stories",
                column: "ScoredAsOfUtc");

            migrationBuilder.CreateIndex(
                name: "IX_StoryScores_InputHash",
                table: "StoryScores",
                column: "InputHash");

            migrationBuilder.CreateIndex(
                name: "IX_StoryScores_StoryId",
                table: "StoryScores",
                column: "StoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_Headlines_Stories_StoryId",
                table: "Headlines",
                column: "StoryId",
                principalTable: "Stories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Headlines_Stories_StoryId",
                table: "Headlines");

            migrationBuilder.DropTable(
                name: "StoryScores");

            migrationBuilder.DropTable(
                name: "Stories");

            migrationBuilder.DropIndex(
                name: "IX_Headlines_StoryId",
                table: "Headlines");

            migrationBuilder.DropColumn(
                name: "StoryId",
                table: "Headlines");
        }
    }
}
