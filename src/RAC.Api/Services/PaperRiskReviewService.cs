using Microsoft.EntityFrameworkCore;
using RAC.Application;
using RAC.Infrastructure;

namespace RAC.Api.Services;

/// <summary>
/// Authenticated, read-only PAPER incident dossier. It cannot review, unlock,
/// restart, close positions, or submit any order. Values are persisted snapshots,
/// not live executable quotes. Never inject an order client here.
/// </summary>
public sealed class PaperRiskReviewService(IDbContextFactory<RacDbContext> factory)
{
    public sealed record PositionEvidence(string Symbol, decimal Quantity,
        decimal EntryPriceBrl, decimal LastPriceBrl, decimal StopPriceBrl,
        DateTimeOffset OpenedUtc, decimal LastRecordedValueBrl);
    public sealed record IncidentEvidence(string Type, string Description, DateTimeOffset CreatedUtc);
    public sealed record ReviewDossier(
        string Mode, string Status, bool PaperRunning, bool PaperRiskLocked,
        bool ReviewApproved, bool UnlockPermitted, bool RealOrdersEnabled,
        bool TestnetOrdersEnabled, string RiskReason, DateTimeOffset ObservedUtc,
        DateTimeOffset? LastPaperCycleUtc, bool ValuationStale,
        decimal CashBrl, decimal LastRecordedEquityBrl,
        decimal? DailyLossPercent, decimal? DrawdownPercent,
        int Fills, int BuyFills, int SellFills,
        int TestnetOrders, bool TestnetEnabled,
        IReadOnlyList<PositionEvidence> Positions,
        IReadOnlyList<IncidentEvidence> Incidents,
        string RequiredNextStep);

    public async Task<ReviewDossier> SnapshotAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var now = DateTimeOffset.UtcNow;
        var account = await db.PaperAccounts.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == 1, ct);
        var positions = await db.PaperPositions.AsNoTracking()
            .OrderBy(x => x.Symbol).ToArrayAsync(ct);
        var fills = await db.PaperFills.AsNoTracking()
            .GroupBy(x => x.Side)
            .Select(x => new { Side = x.Key, Count = x.Count() })
            .ToListAsync(ct);
        var alerts = await db.AuditEvents.AsNoTracking()
            .Where(x => x.EventType == "PAPER_RISK_LOCK")
            .OrderByDescending(x => x.CreatedUtc).Take(5)
            .Select(x => new IncidentEvidence(x.EventType, x.Description, x.CreatedUtc))
            .ToArrayAsync(ct);
        var testnet = await db.TestnetExecutionStates.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == 1, ct);
        var testnetOrders = await db.TestnetOrders.CountAsync(ct);
        var buyFills = fills.Where(x => x.Side == "BUY").Sum(x => x.Count);
        var sellFills = fills.Where(x => x.Side == "SELL").Sum(x => x.Count);
        var stale = account?.LastCycleUtc is not { } last ||
            last > now || now - last > TimeSpan.FromMinutes(5);
        return new ReviewDossier(
            Mode: "READ_ONLY_PAPER_RISK_REVIEW",
            Status: account is null ? "ACCOUNT_MISSING" :
                account.RiskLocked ? "MANUAL_REVIEW_REQUIRED" :
                "NO_ACTIVE_PAPER_LOCK",
            PaperRunning: account?.Running ?? false,
            PaperRiskLocked: account?.RiskLocked ?? false,
            ReviewApproved: false, UnlockPermitted: false,
            RealOrdersEnabled: false, TestnetOrdersEnabled: false,
            RiskReason: account?.RiskReason ?? "Conta PAPER indisponível.",
            ObservedUtc: now, LastPaperCycleUtc: account?.LastCycleUtc,
            ValuationStale: stale,
            CashBrl: account?.CashBrl ?? 0m,
            LastRecordedEquityBrl: account?.LastEquityBrl ?? 0m,
            DailyLossPercent: account is null ? null :
                PaperRiskDiagnostics.DailyLossPercent(
                    account.LastEquityBrl, account.DailyStartEquityBrl),
            DrawdownPercent: account is null ? null :
                PaperRiskDiagnostics.DailyLossPercent(
                    account.LastEquityBrl, account.PeakEquityBrl),
            Fills: fills.Sum(x => x.Count), BuyFills: buyFills,
            SellFills: sellFills, TestnetOrders: testnetOrders,
            TestnetEnabled: testnet?.Enabled ?? false,
            Positions: positions.Select(x => new PositionEvidence(
                x.Symbol, x.Quantity, x.EntryPriceBrl, x.LastPriceBrl,
                x.StopPriceBrl, x.OpenedUtc, x.Quantity * x.LastPriceBrl)).ToArray(),
            Incidents: alerts,
            RequiredNextStep: "Investigar incidente e posição; revisão humana com justificativa. Esta rota não desbloqueia PAPER.");
    }
}
