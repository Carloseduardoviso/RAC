using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RAC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DurableSimulatorState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SimulationKey",
                schema: "rac",
                table: "Positions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SimulationCheckpoints",
                schema: "rac",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StateJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    Tick = table.Column<int>(type: "int", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SimulationCheckpoints", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Positions_SimulationKey",
                schema: "rac",
                table: "Positions",
                column: "SimulationKey",
                unique: true,
                filter: "[SimulationKey] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SimulationCheckpoints",
                schema: "rac");

            migrationBuilder.DropIndex(
                name: "IX_Positions_SimulationKey",
                schema: "rac",
                table: "Positions");

            migrationBuilder.DropColumn(
                name: "SimulationKey",
                schema: "rac",
                table: "Positions");
        }
    }
}
