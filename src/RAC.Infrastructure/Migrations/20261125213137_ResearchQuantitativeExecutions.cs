using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RAC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ResearchQuantitativeExecutions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ResearchQuantitativeExecutions",
                schema: "rac",
                columns: table => new
                {
                    ExecutionId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ConfigurationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SourceSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Month = table.Column<DateOnly>(type: "date", nullable: false),
                    EngineVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ParametersJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ResultJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RecordedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResearchQuantitativeExecutions", x => x.ExecutionId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ResearchQuantitativeExecutions_ConfigurationId_Month",
                schema: "rac",
                table: "ResearchQuantitativeExecutions",
                columns: new[] { "ConfigurationId", "Month" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ResearchQuantitativeExecutions",
                schema: "rac");
        }
    }
}
