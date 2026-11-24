using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RAC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ResearchKnowledgeMvp16314225 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ResearchKnowledge",
                schema: "rac",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Doi = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Term = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Evidence = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    EvidenceLevel = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ExtractorVersion = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ExtractedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResearchKnowledge", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResearchKnowledge_ResearchPublications_Doi",
                        column: x => x.Doi,
                        principalSchema: "rac",
                        principalTable: "ResearchPublications",
                        principalColumn: "Doi",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ResearchKnowledge_Doi_Category_Term_ExtractorVersion",
                schema: "rac",
                table: "ResearchKnowledge",
                columns: new[] { "Doi", "Category", "Term", "ExtractorVersion" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ResearchKnowledge",
                schema: "rac");
        }
    }
}
