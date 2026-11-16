using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RAC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RealSpotPortfolioHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SpotCashFlowNotes",
                schema: "rac",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AmountBrl = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    OccurredOn = table.Column<DateOnly>(type: "date", nullable: false),
                    RecordedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpotCashFlowNotes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SpotInvestmentPlans",
                schema: "rac",
                columns: table => new
                {
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MonthlyContributionBrl = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    ReinvestRealizedProfitPercent = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpotInvestmentPlans", x => x.OwnerUserId);
                });

            migrationBuilder.CreateTable(
                name: "SpotPortfolioSnapshots",
                schema: "rac",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WalletObservedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    QuotesObservedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UsdtBrlRate = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: true),
                    ValuedSubsetBrl = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: false),
                    TotalEstimatedBrl = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: true),
                    CompleteValuation = table.Column<bool>(type: "bit", nullable: false),
                    PricedAssetCount = table.Column<int>(type: "int", nullable: false),
                    UnpricedAssetCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpotPortfolioSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SpotPortfolioHoldings",
                schema: "rac",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SnapshotId = table.Column<long>(type: "bigint", nullable: false),
                    Asset = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Free = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: false),
                    Locked = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: false),
                    UnitPriceBrl = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: true),
                    EstimatedValueBrl = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: true),
                    PriceSource = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpotPortfolioHoldings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SpotPortfolioHoldings_SpotPortfolioSnapshots_SnapshotId",
                        column: x => x.SnapshotId,
                        principalSchema: "rac",
                        principalTable: "SpotPortfolioSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SpotCashFlowNotes_OwnerUserId_OccurredOn",
                schema: "rac",
                table: "SpotCashFlowNotes",
                columns: new[] { "OwnerUserId", "OccurredOn" });

            migrationBuilder.CreateIndex(
                name: "IX_SpotPortfolioHoldings_SnapshotId_Asset",
                schema: "rac",
                table: "SpotPortfolioHoldings",
                columns: new[] { "SnapshotId", "Asset" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SpotPortfolioSnapshots_OwnerUserId_WalletObservedUtc",
                schema: "rac",
                table: "SpotPortfolioSnapshots",
                columns: new[] { "OwnerUserId", "WalletObservedUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SpotCashFlowNotes",
                schema: "rac");

            migrationBuilder.DropTable(
                name: "SpotInvestmentPlans",
                schema: "rac");

            migrationBuilder.DropTable(
                name: "SpotPortfolioHoldings",
                schema: "rac");

            migrationBuilder.DropTable(
                name: "SpotPortfolioSnapshots",
                schema: "rac");
        }
    }
}
