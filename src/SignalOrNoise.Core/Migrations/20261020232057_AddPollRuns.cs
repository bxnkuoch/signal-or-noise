using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SignalOrNoise.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddPollRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Tickers",
                table: "Headlines",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.CreateTable(
                name: "PollRuns",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Feed = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StartedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ItemsFetched = table.Column<int>(type: "int", nullable: false),
                    NewHeadlines = table.Column<int>(type: "int", nullable: false),
                    Error = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PollRuns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PollRuns_StartedUtc",
                table: "PollRuns",
                column: "StartedUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PollRuns");

            migrationBuilder.AlterColumn<string>(
                name: "Tickers",
                table: "Headlines",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);
        }
    }
}
