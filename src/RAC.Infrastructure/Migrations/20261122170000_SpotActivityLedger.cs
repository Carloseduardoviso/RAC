using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RAC.Infrastructure.Migrations;

[DbContext(typeof(RacDbContext))]
[Migration("20261122170000_SpotActivityLedger")]
public partial class SpotActivityLedger : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "SpotActivities",
            schema: "rac",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Kind = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                ReferenceId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                Symbol = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                Asset = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                Side = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                Quantity = table.Column<decimal>(type: "decimal(28,12)", nullable: false),
                Price = table.Column<decimal>(type: "decimal(28,12)", nullable: false),
                QuoteQuantity = table.Column<decimal>(type: "decimal(28,12)", nullable: false),
                Fee = table.Column<decimal>(type: "decimal(28,12)", nullable: false),
                FeeAsset = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                OccurredUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                ImportedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_SpotActivities", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_SpotActivities_OwnerUserId_Kind_Symbol_ReferenceId",
            schema: "rac", table: "SpotActivities",
            columns: new[] { "OwnerUserId", "Kind", "Symbol", "ReferenceId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_SpotActivities_OwnerUserId_OccurredUtc",
            schema: "rac", table: "SpotActivities",
            columns: new[] { "OwnerUserId", "OccurredUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "SpotActivities", schema: "rac");
    }
}
