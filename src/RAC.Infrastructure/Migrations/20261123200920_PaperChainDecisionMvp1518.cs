using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RAC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PaperChainDecisionMvp1518 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaperChainDecisions",
                schema: "rac",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Symbol = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    CandleClosedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RecordedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Approved = table.Column<bool>(type: "bit", nullable: false),
                    Enforced = table.Column<bool>(type: "bit", nullable: false),
                    FirstBlockedBy = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    FirstBlockReason = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    ResultsJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaperChainDecisions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaperChainDecisions_RecordedUtc",
                schema: "rac",
                table: "PaperChainDecisions",
                column: "RecordedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PaperChainDecisions_Symbol_CandleClosedUtc",
                schema: "rac",
                table: "PaperChainDecisions",
                columns: new[] { "Symbol", "CandleClosedUtc" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaperChainDecisions",
                schema: "rac");
        }
    }
}
