using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RAC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ResearchSourceReviewsMvp1631428 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ResearchSourceReviews",
                schema: "rac",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Host = table.Column<string>(type: "nvarchar(253)", maxLength: 253, nullable: false),
                    ReviewedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EvidenceJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    RubricVersion = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResearchSourceReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResearchSourceReviews_ResearchSources_Host",
                        column: x => x.Host,
                        principalSchema: "rac",
                        principalTable: "ResearchSources",
                        principalColumn: "Host",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ResearchSourceReviews_Host_ReviewedUtc",
                schema: "rac",
                table: "ResearchSourceReviews",
                columns: new[] { "Host", "ReviewedUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ResearchSourceReviews",
                schema: "rac");
        }
    }
}
