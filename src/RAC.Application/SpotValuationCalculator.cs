namespace RAC.Application;

public sealed record SpotAssetAmount(string Asset, decimal Free, decimal Locked);

public sealed record SpotValuedAsset(
    string Asset, decimal Free, decimal Locked,
    decimal? UnitPriceBrl, decimal? EstimatedValueBrl, string PriceSource);

public sealed record SpotValuation(
    DateTimeOffset QuotesObservedUtc, decimal? UsdtBrlRate,
    decimal ValuedSubsetBrl, decimal? TotalEstimatedBrl,
    bool CompleteValuation, IReadOnlyList<SpotValuedAsset> Holdings)
{
    public int PricedAssetCount => Holdings.Count(x => x.EstimatedValueBrl.HasValue);
    public int UnpricedAssetCount => Holdings.Count - PricedAssetCount;
}

/// <summary>
/// Display-only mark-to-market estimate. A positive change between estimates is
/// NOT trading profit: deposits, withdrawals, fees and costs are not reconciled.
/// </summary>
public static class SpotValuationCalculator
{
    public static SpotValuation Calculate(
        IReadOnlyCollection<SpotAssetAmount> assets,
        IReadOnlyDictionary<string, decimal> marketPrices,
        DateTimeOffset quotesObservedUtc)
    {
        marketPrices.TryGetValue("USDTBRL", out var usdtBrl);
        var valued = new List<SpotValuedAsset>();

        foreach (var amount in assets.OrderBy(x => x.Asset, StringComparer.Ordinal))
        {
            if (amount.Free < 0m || amount.Locked < 0m)
                throw new ArgumentException("Saldo Spot negativo.");
            if (string.IsNullOrEmpty(amount.Asset) || amount.Asset.Length > 20 ||
                !amount.Asset.All(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9'))
                throw new ArgumentException("Código de ativo Spot inválido.");
            if (amount.Free == 0m && amount.Locked == 0m) continue;
            decimal? unitPrice = null;
            var source = "SEM_COTACAO";

            if (amount.Asset == "BRL")
            {
                unitPrice = 1m;
                source = "BRL_DIRETO";
            }
            else if (marketPrices.TryGetValue(amount.Asset + "BRL", out var brlPrice) && brlPrice > 0m)
            {
                unitPrice = brlPrice;
                source = "BINANCE_PAR_BRL";
            }
            else if (amount.Asset == "USDT" && usdtBrl > 0m)
            {
                unitPrice = usdtBrl;
                source = "BINANCE_USDTBRL";
            }
            else if (usdtBrl > 0m &&
                marketPrices.TryGetValue(amount.Asset + "USDT", out var usdtPrice) &&
                usdtPrice > 0m)
            {
                unitPrice = usdtPrice * usdtBrl;
                source = "BINANCE_VIA_USDTBRL";
            }

            var value = unitPrice * (amount.Free + amount.Locked);
            valued.Add(new SpotValuedAsset(amount.Asset, amount.Free, amount.Locked,
                unitPrice, value, source));
        }

        var priced = valued.Where(x => x.EstimatedValueBrl.HasValue)
            .Sum(x => x.EstimatedValueBrl!.Value);
        var complete = valued.All(x => x.EstimatedValueBrl.HasValue);
        return new SpotValuation(quotesObservedUtc,
            usdtBrl > 0m ? usdtBrl : null,
            priced, complete ? priced : null, complete, valued);
    }
}
