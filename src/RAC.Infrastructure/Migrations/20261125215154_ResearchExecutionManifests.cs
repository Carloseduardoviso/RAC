using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RAC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ResearchExecutionManifests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ResearchExecutionManifests",
                schema: "rac",
                columns: table => new
                {
                    ManifestId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    EngineVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Assets = table.Column<int>(type: "int", nullable: false),
                    Executions = table.Column<int>(type: "int", nullable: false),
                    EvidenceJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RecordedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResearchExecutionManifests", x => x.ManifestId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ResearchExecutionManifests_EngineVersion_RecordedUtc",
                schema: "rac",
                table: "ResearchExecutionManifests",
                columns: new[] { "EngineVersion", "RecordedUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ResearchExecutionManifests",
                schema: "rac");
        }
    }
}
