using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RAC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ResearchStrategyCatalogMvp16314250 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ResearchStrategyCatalogs",
                schema: "rac",
                columns: table => new
                {
                    ContentSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SourceSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CatalogJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArchivedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResearchStrategyCatalogs", x => x.ContentSha256);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ResearchStrategyCatalogs_SourceSha256",
                schema: "rac",
                table: "ResearchStrategyCatalogs",
                column: "SourceSha256");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ResearchStrategyCatalogs",
                schema: "rac");
        }
    }
}
