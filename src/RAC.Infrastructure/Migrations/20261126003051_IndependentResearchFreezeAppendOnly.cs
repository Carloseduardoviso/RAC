using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RAC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class IndependentResearchFreezeAppendOnly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("EXEC(N'CREATE TRIGGER [rac].[TR_IndependentResearchFreezes_AppendOnly] ON [rac].[IndependentResearchFreezes] AFTER UPDATE, DELETE AS BEGIN SET NOCOUNT ON; IF EXISTS (SELECT 1 FROM deleted) THROW 51001, ''Frozen research records are append-only.'', 1; END')");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER [rac].[TR_IndependentResearchFreezes_AppendOnly]");

        }
    }
}
