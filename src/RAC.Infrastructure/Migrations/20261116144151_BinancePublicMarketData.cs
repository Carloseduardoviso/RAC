using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RAC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BinancePublicMarketData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MarketCandles",
                schema: "rac",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Symbol = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Interval = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    OpenUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CloseUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Open = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: false),
                    High = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: false),
                    Low = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: false),
                    Close = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: false),
                    Volume = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: false),
                    QuoteVolume = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: false),
                    Trades = table.Column<long>(type: "bigint", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketCandles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarketQuotes",
                schema: "rac",
                columns: table => new
                {
                    Symbol = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    PriceUsdt = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: false),
                    ObservedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketQuotes", x => x.Symbol);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MarketCandles_CloseUtc",
                schema: "rac",
                table: "MarketCandles",
                column: "CloseUtc");

            migrationBuilder.CreateIndex(
                name: "IX_MarketCandles_Symbol_Interval_OpenUtc",
                schema: "rac",
                table: "MarketCandles",
                columns: new[] { "Symbol", "Interval", "OpenUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketQuotes_ObservedUtc",
                schema: "rac",
                table: "MarketQuotes",
                column: "ObservedUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MarketCandles",
                schema: "rac");

            migrationBuilder.DropTable(
                name: "MarketQuotes",
                schema: "rac");
        }
    }
}
