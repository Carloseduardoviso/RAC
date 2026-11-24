using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RAC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ResearchExecutionHistoryMvp16314218 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ResearchExecutionHistory",
                schema: "rac",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Topic = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StartedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ElapsedMs = table.Column<long>(type: "bigint", nullable: false),
                    PublicationsFound = table.Column<int>(type: "int", nullable: true),
                    PublicationsSaved = table.Column<int>(type: "int", nullable: true),
                    CandidateDomains = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ErrorCategory = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResearchExecutionHistory", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ResearchExecutionHistory_Topic_StartedUtc",
                schema: "rac",
                table: "ResearchExecutionHistory",
                columns: new[] { "Topic", "StartedUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ResearchExecutionHistory",
                schema: "rac");
        }
    }
}
