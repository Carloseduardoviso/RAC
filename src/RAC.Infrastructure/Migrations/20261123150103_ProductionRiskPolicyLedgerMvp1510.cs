using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RAC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ProductionRiskPolicyLedgerMvp1510 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductionRiskPolicies",
                schema: "rac",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Version = table.Column<int>(type: "int", nullable: false),
                    LimitsJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    LimitsSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ProposedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ProposedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReviewedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ReviewedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReviewReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionRiskPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductionRiskPolicyAudits",
                schema: "rac",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PolicyId = table.Column<long>(type: "bigint", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    LimitsSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionRiskPolicyAudits", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRiskPolicies_Version",
                schema: "rac",
                table: "ProductionRiskPolicies",
                column: "Version",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRiskPolicyAudits_PolicyId_CreatedUtc",
                schema: "rac",
                table: "ProductionRiskPolicyAudits",
                columns: new[] { "PolicyId", "CreatedUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductionRiskPolicies",
                schema: "rac");

            migrationBuilder.DropTable(
                name: "ProductionRiskPolicyAudits",
                schema: "rac");
        }
    }
}
