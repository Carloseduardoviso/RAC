using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RAC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PaperMonitoringMvp111 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaperDecisionAudits",
                schema: "rac",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Symbol = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    CandleClosedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RecordedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Signal = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    PlacedRealOrder = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaperDecisionAudits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaperMonitorAlerts",
                schema: "rac",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(350)", maxLength: 350, nullable: false),
                    OpenedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastObservedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ResolvedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Observations = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaperMonitorAlerts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaperMonitorSamples",
                schema: "rac",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecordedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EquityBrl = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: false),
                    CashBrl = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: false),
                    ExposureBrl = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: false),
                    TotalContributionsBrl = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: false),
                    DrawdownPercent = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: false),
                    ReadyAgents = table.Column<int>(type: "int", nullable: false),
                    BlockedAgents = table.Column<int>(type: "int", nullable: false),
                    OpenPositions = table.Column<int>(type: "int", nullable: false),
                    Running = table.Column<bool>(type: "bit", nullable: false),
                    RiskLocked = table.Column<bool>(type: "bit", nullable: false),
                    LastPaperCycleUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaperMonitorSamples", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaperDecisionAudits_RecordedUtc",
                schema: "rac",
                table: "PaperDecisionAudits",
                column: "RecordedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PaperDecisionAudits_Symbol_CandleClosedUtc",
                schema: "rac",
                table: "PaperDecisionAudits",
                columns: new[] { "Symbol", "CandleClosedUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaperMonitorAlerts_Code_ResolvedUtc",
                schema: "rac",
                table: "PaperMonitorAlerts",
                columns: new[] { "Code", "ResolvedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PaperMonitorAlerts_OpenedUtc",
                schema: "rac",
                table: "PaperMonitorAlerts",
                column: "OpenedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PaperMonitorSamples_RecordedUtc",
                schema: "rac",
                table: "PaperMonitorSamples",
                column: "RecordedUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaperDecisionAudits",
                schema: "rac");

            migrationBuilder.DropTable(
                name: "PaperMonitorAlerts",
                schema: "rac");

            migrationBuilder.DropTable(
                name: "PaperMonitorSamples",
                schema: "rac");
        }
    }
}
