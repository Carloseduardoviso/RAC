using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RAC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ResearchSourcesMvp1631425 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ResearchSources",
                schema: "rac",
                columns: table => new
                {
                    Host = table.Column<string>(type: "nvarchar(253)", maxLength: 253, nullable: false),
                    EvidenceUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    DiscoveredFrom = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DiscoveredUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResearchSources", x => x.Host);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ResearchSources",
                schema: "rac");
        }
    }
}
