using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RAC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class IndependentResearchFreezes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IndependentResearchFreezes",
                schema: "rac",
                columns: table => new
                {
                    FreezeId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ConfigurationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ManifestId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    EngineVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ParametersJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FrozenUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IndependentResearchFreezes", x => x.FreezeId);
                    table.ForeignKey(
                        name: "FK_IndependentResearchFreezes_ResearchExecutionManifests_ManifestId",
                        column: x => x.ManifestId,
                        principalSchema: "rac",
                        principalTable: "ResearchExecutionManifests",
                        principalColumn: "ManifestId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IndependentResearchFreezes_ConfigurationId",
                schema: "rac",
                table: "IndependentResearchFreezes",
                column: "ConfigurationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IndependentResearchFreezes_ManifestId",
                schema: "rac",
                table: "IndependentResearchFreezes",
                column: "ManifestId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IndependentResearchFreezes",
                schema: "rac");
        }
    }
}
