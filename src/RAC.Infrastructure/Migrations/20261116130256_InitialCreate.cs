using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RAC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "rac");

            migrationBuilder.CreateTable(
                name: "Agents",
                schema: "rac",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Agents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditEvents",
                schema: "rac",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Contributions",
                schema: "rac",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AmountBrl = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    IsVirtual = table.Column<bool>(type: "bit", nullable: false),
                    OccurredUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contributions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EquitySnapshots",
                schema: "rac",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EquityBrl = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    TotalContributionsBrl = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    DrawdownPercent = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    RecordedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquitySnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                schema: "rac",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientOrderId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Side = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Mode = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Positions",
                schema: "rac",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: false),
                    AverageEntry = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: false),
                    StopPrice = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: true),
                    IsOpen = table.Column<bool>(type: "bit", nullable: false),
                    Mode = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Positions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RiskLocks",
                schema: "rac",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReleasedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskLocks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Strategies",
                schema: "rac",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Strategies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Executions",
                schema: "rac",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: false),
                    Fee = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: false),
                    ExecutedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Executions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Executions_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "rac",
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Agents_Name",
                schema: "rac",
                table: "Agents",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_CreatedUtc",
                schema: "rac",
                table: "AuditEvents",
                column: "CreatedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_EquitySnapshots_RecordedUtc",
                schema: "rac",
                table: "EquitySnapshots",
                column: "RecordedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Executions_OrderId",
                schema: "rac",
                table: "Executions",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_ClientOrderId",
                schema: "rac",
                table: "Orders",
                column: "ClientOrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CreatedUtc",
                schema: "rac",
                table: "Orders",
                column: "CreatedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Positions_Symbol_IsOpen",
                schema: "rac",
                table: "Positions",
                columns: new[] { "Symbol", "IsOpen" });

            migrationBuilder.CreateIndex(
                name: "IX_RiskLocks_Active",
                schema: "rac",
                table: "RiskLocks",
                column: "Active");

            migrationBuilder.CreateIndex(
                name: "IX_Strategies_Name_Version",
                schema: "rac",
                table: "Strategies",
                columns: new[] { "Name", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Agents",
                schema: "rac");

            migrationBuilder.DropTable(
                name: "AuditEvents",
                schema: "rac");

            migrationBuilder.DropTable(
                name: "Contributions",
                schema: "rac");

            migrationBuilder.DropTable(
                name: "EquitySnapshots",
                schema: "rac");

            migrationBuilder.DropTable(
                name: "Executions",
                schema: "rac");

            migrationBuilder.DropTable(
                name: "Positions",
                schema: "rac");

            migrationBuilder.DropTable(
                name: "RiskLocks",
                schema: "rac");

            migrationBuilder.DropTable(
                name: "Strategies",
                schema: "rac");

            migrationBuilder.DropTable(
                name: "Orders",
                schema: "rac");
        }
    }
}
