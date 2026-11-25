using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RAC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DanielDiscoveries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DanielDiscoveries",
                schema: "rac",
                columns: table => new
                {
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObservedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CandidatesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CandidateCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DanielDiscoveries", x => x.TaskId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DanielDiscoveries_ObservedUtc",
                schema: "rac",
                table: "DanielDiscoveries",
                column: "ObservedUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DanielDiscoveries",
                schema: "rac");
        }
    }
}
