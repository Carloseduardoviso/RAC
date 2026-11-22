using Microsoft.EntityFrameworkCore;
using RAC.Domain;
using RAC.Infrastructure;

namespace RAC.Api.Services;

public sealed record SpotActivityImportRequest(string Symbol, bool IncludeTransfers = true);

/// <summary>
/// Stores only observed Binance history. Never places orders or schedules
/// private calls. Do not interpret snapshots or imported trades as net profit.
/// </summary>
public sealed class RacSpotActivityService(
    IDbContextFactory<RacDbContext> factory, BinanceSpotActivityClient binance)
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<object> ImportAsync(Guid ownerId, SpotActivityImportRequest request,
        CancellationToken ct)
    {
        if (request is null || !BinanceSpotActivityClient.AllowedSymbols.Contains(
                request.Symbol, StringComparer.Ordinal))
            throw new ArgumentException("Escolha um par Spot monitorado pelo RAC.");

        await _gate.WaitAsync(ct);
        try
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            var lastImport = await db.SpotActivities.AsNoTracking()
                .Where(x => x.OwnerUserId == ownerId)
                .OrderByDescending(x => x.ImportedUtc)
                .Select(x => (DateTimeOffset?)x.ImportedUtc)
                .FirstOrDefaultAsync(ct);
            // Avoid an unintended loop of signed Binance requests.
            if (lastImport.HasValue && DateTimeOffset.UtcNow - lastImport.Value <
                TimeSpan.FromSeconds(30))
                throw new InvalidOperationException(
                    "Aguarde 30 segundos entre importações de histórico real.");

            var batch = await binance.FetchAsync(request.Symbol, request.IncludeTransfers, ct);
            var references = batch.Entries.Select(x => x.ReferenceId).Distinct().ToArray();
            var previous = await db.SpotActivities
                .Where(x => x.OwnerUserId == ownerId && references.Contains(x.ReferenceId))
                .ToListAsync(ct);
            var byKey = previous.ToDictionary(x => Key(x.Kind, x.Symbol, x.ReferenceId));
            var added = 0;
            var changed = 0;
            foreach (var row in batch.Entries)
            {
                var key = Key(row.Kind, row.Symbol, row.ReferenceId);
                if (byKey.TryGetValue(key, out var stored))
                {
                    // Binance deposit/withdrawal statuses can change. The original
                    // trade fills must remain immutable.
                    if (row.Kind != "TRADE" && stored.Status != row.Status)
                    {
                        stored.Status = row.Status;
                        changed++;
                    }
                    continue;
                }
                row.OwnerUserId = ownerId;
                row.ImportedUtc = batch.ObservedUtc;
                db.SpotActivities.Add(row);
                byKey.Add(key, row);
                added++;
            }
            db.AuditEvents.Add(new AuditEvent
            {
                EventType = "BINANCE_ACTIVITY_IMPORTED_READONLY",
                Description = $"Importação solicitada por usuário autorizado: par {request.Symbol}, " +
                    $"novos {added}, status atualizados {changed}, avisos {batch.Warnings.Count}; nenhuma ordem enviada."
            });
            await db.SaveChangesAsync(ct);
            return new
            {
                source = batch.Source, mode = "READ_ONLY", realOrdersEnabled = false,
                symbol = request.Symbol, batch.ObservedUtc, imported = added,
                updated = changed, returned = batch.Entries.Count,
                completeHistory = false, profitReconciled = false,
                warnings = batch.Warnings
            };
        }
        finally { _gate.Release(); }
    }

    public async Task<object> HistoryAsync(Guid ownerId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var entries = await db.SpotActivities.AsNoTracking()
            .Where(x => x.OwnerUserId == ownerId)
            .OrderByDescending(x => x.OccurredUtc).ThenByDescending(x => x.Id)
            .Take(200).ToListAsync(ct);
        var kinds = await db.SpotActivities.AsNoTracking()
            .Where(x => x.OwnerUserId == ownerId)
            .GroupBy(x => x.Kind)
            .Select(g => new { Kind = g.Key, Count = g.Count() }).ToListAsync(ct);
        var fees = await db.SpotActivities.AsNoTracking()
            .Where(x => x.OwnerUserId == ownerId && x.Fee > 0)
            .GroupBy(x => x.FeeAsset)
            .Select(g => new { Asset = g.Key, Amount = g.Sum(x => x.Fee) })
            .OrderBy(x => x.Asset).ToListAsync(ct);
        var latest = await db.SpotActivities.AsNoTracking()
            .Where(x => x.OwnerUserId == ownerId)
            .OrderByDescending(x => x.ImportedUtc)
            .Select(x => (DateTimeOffset?)x.ImportedUtc)
            .FirstOrDefaultAsync(ct);
        return new
        {
            source = "BINANCE_SPOT_REAL", mode = "READ_ONLY",
            realOrdersEnabled = false, completeHistory = false,
            profitReconciled = false, realizedProfitBrl = (decimal?)null,
            reason = "Amostra limitada por par; faltam custo médio histórico, fluxos, pares não importados e conversão das taxas.",
            latestImportUtc = latest,
            counts = kinds, feesByAsset = fees,
            entries = entries.Select(x => new
            {
                x.Kind, x.ReferenceId, x.Symbol, x.Asset, x.Side, x.Status,
                x.Quantity, x.Price, x.QuoteQuantity, x.Fee, x.FeeAsset,
                x.OccurredUtc, x.ImportedUtc
            })
        };
    }

    private static string Key(string kind, string symbol, string id) =>
        kind + "|" + symbol + "|" + id;
}
