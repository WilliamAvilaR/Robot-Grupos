using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scraper.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialScraperTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OtpChallenge",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    AccountId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UsedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OtpChallenge", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScrapeResult",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    AccountId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Url = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CapturedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    PayloadJson = table.Column<string>(type: "NVARCHAR(MAX)", nullable: true),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    HtmlSnapshotPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScrapeResult", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OtpChallenge_AccountId",
                table: "OtpChallenge",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_OtpChallenge_ExpiresAt",
                table: "OtpChallenge",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_OtpChallenge_Status",
                table: "OtpChallenge",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ScrapeResult_AccountId",
                table: "ScrapeResult",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ScrapeResult_CapturedAt",
                table: "ScrapeResult",
                column: "CapturedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ScrapeResult_ContentHash",
                table: "ScrapeResult",
                column: "ContentHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OtpChallenge");

            migrationBuilder.DropTable(
                name: "ScrapeResult");
        }
    }
}
