using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RAC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PaperAutoTradingMvp11 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaperAccounts",
                schema: "rac",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    CashBrl = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: false),
                    TotalContributionsBrl = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: false),
                    PeakEquityBrl = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: false),
                    DailyStartEquityBrl = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: false),
                    CurrentDayUtc = table.Column<DateOnly>(type: "date", nullable: false),
                    NextContributionUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Running = table.Column<bool>(type: "bit", nullable: false),
                    RiskLocked = table.Column<bool>(type: "bit", nullable: false),
                    RiskReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    LastEquityBrl = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: false),
                    EntriesToday = table.Column<int>(type: "int", nullable: false),
                    LastCycleUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaperAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaperAgentCursors",
                schema: "rac",
                columns: table => new
                {
                    Symbol = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    LastCandleUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastSignal = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaperAgentCursors", x => x.Symbol);
                });

            migrationBuilder.CreateTable(
                name: "PaperFills",
                schema: "rac",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Symbol = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Side = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: false),
                    PriceBrl = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: false),
                    FeeBrl = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: false),
                    ExecutedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SignalClosedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RealizedPnlBrl = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaperFills", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaperPositions",
                schema: "rac",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Symbol = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: false),
                    EntryPriceBrl = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: false),
                    StopPriceBrl = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: false),
                    EntryFeeBrl = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: false),
                    LastPriceBrl = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: false),
                    OpenedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SignalClosedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaperPositions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaperFills_ExecutedUtc",
                schema: "rac",
                table: "PaperFills",
                column: "ExecutedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PaperFills_Symbol_Side_SignalClosedUtc",
                schema: "rac",
                table: "PaperFills",
                columns: new[] { "Symbol", "Side", "SignalClosedUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaperPositions_Symbol",
                schema: "rac",
                table: "PaperPositions",
                column: "Symbol",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaperAccounts",
                schema: "rac");

            migrationBuilder.DropTable(
                name: "PaperAgentCursors",
                schema: "rac");

            migrationBuilder.DropTable(
                name: "PaperFills",
                schema: "rac");

            migrationBuilder.DropTable(
                name: "PaperPositions",
                schema: "rac");
        }

    }
}
