using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SignalOrNoise.Core.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Headlines",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Source = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Url = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    UrlHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    TitleHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PublishedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IngestedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Tickers = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Headlines", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Headlines_PublishedUtc",
                table: "Headlines",
                column: "PublishedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Headlines_TitleHash",
                table: "Headlines",
                column: "TitleHash");

            migrationBuilder.CreateIndex(
                name: "IX_Headlines_UrlHash",
                table: "Headlines",
                column: "UrlHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Headlines");
        }
    }
}
