using Microsoft.EntityFrameworkCore;
using RAC.Application;
using RAC.Domain;
using RAC.Infrastructure;

namespace RAC.Api.Services;

public sealed record SpotContributionRequest(
    string Kind, decimal AmountBrl, DateOnly OccurredOn, string? Description);
public sealed record SpotPlanRequest(
    decimal MonthlyContributionBrl, decimal ReinvestRealizedProfitPercent);

public sealed class RacSpotPortfolioService(
    IDbContextFactory<RacDbContext> database,
    BinanceSpotAccountClient account,
    BinancePortfolioPricesClient quotes)
{
    private readonly SemaphoreSlim _captureGate = new(1, 1);

    public async Task<object> CaptureAsync(Guid ownerId, CancellationToken ct)
    {
        await _captureGate.WaitAsync(ct);
        try
        {
            await using var db = await database.CreateDbContextAsync(ct);
            var newest = await db.SpotPortfolioSnapshots.AsNoTracking()
                .Where(x => x.OwnerUserId == ownerId).OrderByDescending(x => x.Id)
                .Select(x => (DateTimeOffset?)x.WalletObservedUtc).FirstOrDefaultAsync(ct);
            if (newest.HasValue && DateTimeOffset.UtcNow - newest.Value < TimeSpan.FromSeconds(45))
                throw new InvalidOperationException("Aguarde 45 segundos entre registros de patrimônio real.");

            // Only manual requests read the private Binance wallet.
            var wallet = await account.GetWalletAsync(ct);
            var prices = await quotes.GetPricesAsync(ct);
            var amounts = wallet.Assets.Select(x =>
                new SpotAssetAmount(x.Asset, x.Free, x.Locked)).ToArray();
            var valuation = SpotValuationCalculator.Calculate(amounts,
                prices.Prices, prices.ObservedUtc);

            var snapshot = new SpotPortfolioSnapshot
            {
                OwnerUserId = ownerId,
                WalletObservedUtc = wallet.ObservedUtc,
                QuotesObservedUtc = valuation.QuotesObservedUtc,
                UsdtBrlRate = valuation.UsdtBrlRate,
                ValuedSubsetBrl = valuation.ValuedSubsetBrl,
                TotalEstimatedBrl = valuation.TotalEstimatedBrl,
                CompleteValuation = valuation.CompleteValuation,
                PricedAssetCount = valuation.PricedAssetCount,
                UnpricedAssetCount = valuation.UnpricedAssetCount,
                Holdings = valuation.Holdings.Select(x => new SpotPortfolioHolding
                {
                    Asset = x.Asset,
                    Free = x.Free,
                    Locked = x.Locked,
                    UnitPriceBrl = x.UnitPriceBrl,
                    EstimatedValueBrl = x.EstimatedValueBrl,
                    PriceSource = x.PriceSource
                }).ToList()
            };
            db.SpotPortfolioSnapshots.Add(snapshot);
            db.AuditEvents.Add(new AuditEvent
            {
                EventType = "SPOT_PORTFOLIO_OBSERVED",
                Description = "Carteira Binance Spot consultada manualmente em modo leitura; nenhuma ordem enviada."
            });
            await db.SaveChangesAsync(ct);
            return SnapshotView(snapshot);
        }
        finally { _captureGate.Release(); }
    }

    public async Task<object> HistoryAsync(Guid ownerId, CancellationToken ct)
    {
        await using var db = await database.CreateDbContextAsync(ct);
        var snapshots = await db.SpotPortfolioSnapshots.AsNoTracking()
            .Where(x => x.OwnerUserId == ownerId)
            .OrderByDescending(x => x.Id).Take(60).ToListAsync(ct);
        var recent = snapshots.FirstOrDefault();
        var holdings = recent is null
            ? []
            : await db.SpotPortfolioHoldings.AsNoTracking()
                .Where(x => x.SnapshotId == recent.Id)
                .OrderBy(x => x.Asset).ToListAsync(ct);
        var cashFlows = await db.SpotCashFlowNotes.AsNoTracking()
            .Where(x => x.OwnerUserId == ownerId)
            .OrderByDescending(x => x.Id).Take(60).ToListAsync(ct);
        var plan = await db.SpotInvestmentPlans.AsNoTracking()
            .SingleOrDefaultAsync(x => x.OwnerUserId == ownerId, ct);
        return new
        {
            source = "BINANCE_SPOT_REAL",
            realOrdersEnabled = false,
            statementsVerified = false,
            profitReconciled = false,
            latest = recent is null ? null : SnapshotView(recent),
            snapshots = snapshots.Select(SnapshotView).ToArray(),
            holdings = holdings.Select(x => new
            {
                x.Asset, x.Free, x.Locked, x.UnitPriceBrl,
                x.EstimatedValueBrl, x.PriceSource
            }).ToArray(),
            cashFlows = cashFlows.Select(x => new
            {
                x.Id, x.Kind, x.AmountBrl, x.OccurredOn,
                x.RecordedUtc, x.Description, verifiedByBinance = false
            }).ToArray(),
            plan = new
            {
                monthlyContributionBrl = plan?.MonthlyContributionBrl ?? 200m,
                reinvestRealizedProfitPercent = plan?.ReinvestRealizedProfitPercent ?? 100m,
                automaticExecutionEnabled = false,
                verifiedRealizedProfitBrl = (decimal?)null
            }
        };
    }

    public async Task<object> SavePlanAsync(
        Guid ownerId, SpotPlanRequest input, CancellationToken ct)
    {
        if (input.MonthlyContributionBrl is < 0m or > 1000000m ||
            input.ReinvestRealizedProfitPercent is < 0m or > 100m)
            throw new ArgumentException("Aporte mensal deve ser 0 a R$ 1 milhão e reinvestimento 0% a 100%.");
        await using var db = await database.CreateDbContextAsync(ct);
        var plan = await db.SpotInvestmentPlans.SingleOrDefaultAsync(
            x => x.OwnerUserId == ownerId, ct);
        if (plan is null)
        {
            plan = new SpotInvestmentPlan { OwnerUserId = ownerId };
            db.SpotInvestmentPlans.Add(plan);
        }
        plan.MonthlyContributionBrl = input.MonthlyContributionBrl;
        plan.ReinvestRealizedProfitPercent = input.ReinvestRealizedProfitPercent;
        plan.UpdatedUtc = DateTimeOffset.UtcNow;
        db.AuditEvents.Add(new AuditEvent
        {
            EventType = "SPOT_INVESTMENT_PLAN_UPDATED",
            Description = "Plano informativo de aportes e reinvestimento atualizado; nenhuma transferência ou negociação realizada."
        });
        await db.SaveChangesAsync(ct);
        return new
        {
            plan.MonthlyContributionBrl, plan.ReinvestRealizedProfitPercent,
            plan.UpdatedUtc, automaticExecutionEnabled = false
        };
    }

    public async Task<object> AddCashFlowAsync(
        Guid ownerId, SpotContributionRequest request, CancellationToken ct)
    {
        if (request.Kind is not ("CONTRIBUTION" or "WITHDRAWAL") ||
            request.AmountBrl is < 0.01m or > 1000000m ||
            request.Description?.Length > 240 ||
            request.OccurredOn < new DateOnly(2000, 1, 1) ||
            request.OccurredOn > DateOnly.FromDateTime(DateTime.UtcNow))
            throw new ArgumentException("Lançamento manual inválido: informe tipo, valor, data passada e descrição opcional.");

        await using var db = await database.CreateDbContextAsync(ct);
        var note = new SpotCashFlowNote
        {
            OwnerUserId = ownerId,
            Kind = request.Kind,
            AmountBrl = request.AmountBrl,
            OccurredOn = request.OccurredOn,
            Description = request.Description?.Trim() ?? string.Empty
        };
        db.SpotCashFlowNotes.Add(note);
        db.AuditEvents.Add(new AuditEvent
        {
            EventType = "SPOT_MANUAL_CASHFLOW_NOTED",
            Description = "Lançamento manual declarado para controle financeiro; não confirmado pela Binance nem executado pelo RAC."
        });
        await db.SaveChangesAsync(ct);
        return new
        {
            note.Id, note.Kind, note.AmountBrl, note.OccurredOn,
            verifiedByBinance = false, automaticExecutionEnabled = false
        };
    }

    private static object SnapshotView(SpotPortfolioSnapshot s) => new
    {
        s.Id, s.WalletObservedUtc, s.QuotesObservedUtc,
        s.UsdtBrlRate, s.ValuedSubsetBrl, s.TotalEstimatedBrl,
        s.CompleteValuation, s.PricedAssetCount, s.UnpricedAssetCount
    };
}
