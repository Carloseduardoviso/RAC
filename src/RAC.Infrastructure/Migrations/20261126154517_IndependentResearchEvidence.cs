using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RAC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class IndependentResearchEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IndependentResearchEvidence",
                schema: "rac",
                columns: table => new
                {
                    ExecutionId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FreezeId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SourceSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Digest = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Month = table.Column<DateOnly>(type: "date", nullable: false),
                    EvidenceJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RecordedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IndependentResearchEvidence", x => x.ExecutionId);
                    table.ForeignKey(
                        name: "FK_IndependentResearchEvidence_IndependentResearchFreezes_FreezeId",
                        column: x => x.FreezeId,
                        principalSchema: "rac",
                        principalTable: "IndependentResearchFreezes",
                        principalColumn: "FreezeId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IndependentResearchEvidence_FreezeId_Month",
                schema: "rac",
                table: "IndependentResearchEvidence",
                columns: new[] { "FreezeId", "Month" },
                unique: true);
            migrationBuilder.Sql("EXEC(N'CREATE TRIGGER [rac].[TR_IndependentResearchEvidence_AppendOnly] ON [rac].[IndependentResearchEvidence] AFTER UPDATE, DELETE AS BEGIN SET NOCOUNT ON; IF EXISTS (SELECT 1 FROM deleted) THROW 51002, ''Independent research evidence is append-only.'', 1; END')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IndependentResearchEvidence",
                schema: "rac");
        }
    }
}
