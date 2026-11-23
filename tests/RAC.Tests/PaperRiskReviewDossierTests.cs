using Microsoft.EntityFrameworkCore;
using RAC.Api.Services;
using RAC.Domain;
using RAC.Infrastructure;

namespace RAC.Tests;

public sealed class PaperRiskReviewDossierTests
{
    private sealed class Factory(DbContextOptions<RacDbContext> options)
        : IDbContextFactory<RacDbContext>
    {
        public RacDbContext CreateDbContext() => new(options);
    }

    [Fact]
    public async Task Locked_paper_review_is_read_only_and_never_authorizes_unlock()
    {
        if (!OperatingSystem.IsWindows()) return;
        var name = "RAC_MVP1517_REVIEW_" + Guid.NewGuid().ToString("N");
        var connection = @"Server=(localdb)\MSSQLLocalDB;Database=" + name +
            ";Integrated Security=True;Encrypt=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<RacDbContext>()
            .UseSqlServer(connection).Options;
        var now = DateTimeOffset.UtcNow;
        try
        {
            await using (var db = new RacDbContext(options))
            {
                await db.Database.EnsureCreatedAsync();
                db.PaperAccounts.Add(new PaperAccount
                {
                    Id = 1, RiskLocked = true, Running = false,
                    RiskReason = "Carlos: revisão obrigatória.",
                    CashBrl = 79m, LastEquityBrl = 99m,
                    DailyStartEquityBrl = 100m, PeakEquityBrl = 102m,
                    LastCycleUtc = now.AddMinutes(-15)
                });
                db.PaperPositions.Add(new PaperPosition
                {
                    Symbol = "DOTUSDT", Quantity = 2m, EntryPriceBrl = 10m,
                    LastPriceBrl = 9m, StopPriceBrl = 8m,
                    OpenedUtc = now, SignalClosedUtc = now
                });
                db.PaperFills.Add(new PaperFill
                {
                    Symbol = "DOTUSDT", Side = "BUY", Reason = "PAPER",
                    Quantity = 2m, PriceBrl = 10m,
                    ExecutedUtc = now, SignalClosedUtc = now
                });
                db.AuditEvents.Add(new AuditEvent
                {
                    EventType = "PAPER_RISK_LOCK",
                    Description = "Falha genérica anterior. Revisão obrigatória.",
                    CreatedUtc = now.AddMinutes(-10)
                });
                db.TestnetExecutionStates.Add(new TestnetExecutionState
                {
                    Id = 1, Enabled = false
                });
                await db.SaveChangesAsync();
            }
            var service = new PaperRiskReviewService(new Factory(options));
            var first = await service.SnapshotAsync(CancellationToken.None);
            var second = await service.SnapshotAsync(CancellationToken.None);
            Assert.Equal("READ_ONLY_PAPER_RISK_REVIEW", first.Mode);
            Assert.Equal("MANUAL_REVIEW_REQUIRED", first.Status);
            Assert.True(first.PaperRiskLocked);
            Assert.False(first.PaperRunning);
            Assert.False(first.ReviewApproved);
            Assert.False(first.UnlockPermitted);
            Assert.False(first.RealOrdersEnabled);
            Assert.False(first.TestnetOrdersEnabled);
            Assert.False(first.TestnetEnabled);
            Assert.Equal(0, first.TestnetOrders);
            Assert.Equal(1, first.Fills);
            Assert.Equal(1, first.BuyFills);
            Assert.Equal(0, first.SellFills);
            Assert.Single(first.Positions);
            Assert.Equal("DOTUSDT", first.Positions[0].Symbol);
            Assert.Equal(18m, first.Positions[0].LastRecordedValueBrl);
            Assert.True(first.ValuationStale);
            Assert.Equal(1m, first.DailyLossPercent);
            Assert.Single(first.Incidents);
            Assert.Equal(first.RiskReason, second.RiskReason);

            await using var verify = new RacDbContext(options);
            var account = await verify.PaperAccounts.AsNoTracking().SingleAsync();
            Assert.True(account.RiskLocked);
            Assert.False(account.Running);
            Assert.Equal("Carlos: revisão obrigatória.", account.RiskReason);
            Assert.Single(await verify.PaperPositions.ToArrayAsync());
            Assert.Single(await verify.PaperFills.ToArrayAsync());
            Assert.Single(await verify.AuditEvents.ToArrayAsync());
            Assert.Empty(await verify.TestnetOrders.ToArrayAsync());
        }
        finally
        {
            await using var cleanup = new RacDbContext(options);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }
}
