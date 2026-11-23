using System.Globalization;
using System.IO.Compression;
using RAC.Domain;

namespace RAC.Application;

/// <summary>
/// SHADOW-only fixed decision evaluation. Selection must happen elsewhere using
/// pre-holdout inputs. This module never chooses or changes a gate/horizon.
/// Each test window is the afternoon of one UTC day, disjoint from daily
/// training. No accounts, database or exchange dependencies.
/// </summary>
public static class RacFixedHoldoutReplay
{
    public sealed record Result(string Symbol, string Month, string FrozenGate,
        int FrozenHorizonMinutes, int TestedDays, int ClosedTrades,
        int UnresolvedExitIntents, decimal? MeanNetReturnPct,
        string EvaluationStatus, bool ReviewEligible, bool PaperModified,
        bool OrderPlacementEnabled, bool PromotionAllowed);

    public static Result Evaluate(string symbol, int year, int month,
        Stream archive, string frozenGate, int frozenHorizonMinutes)
    {
        if (!RacMarketSymbols.IsSupported(symbol))
            throw new ArgumentException("Unsupported symbol.", nameof(symbol));
        // Consistent decisions cannot be changed by holdout observations.
        RacFrozenDecisionAudit.FreezeByVotes([(frozenGate, frozenHorizonMinutes)]);
        var validated = MonthlyArchiveResearch.Analyze(symbol, year, month, archive);
        var period = $"{year:D4}-{month:D2}";
        if (frozenGate == "NO_TRADE")
            return new(symbol, period, frozenGate, frozenHorizonMinutes,
                validated.CalendarDays, 0, 0, 0m,
                "NO_TRADE_BASELINE_NOT_ACTIVE_REPLAY", false, false, false, false);

        archive.Position = 0;
        using var zip = new ZipArchive(archive, ZipArchiveMode.Read, leaveOpen: true);
        using var reader = new StreamReader(zip.Entries.Single().Open());
        var culture = CultureInfo.InvariantCulture;
        var candles = new List<MarketCandlePoint>(1440);
        var results = new List<(decimal Return, int Trades, int Unresolved)>();
        DateOnly? date = null;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var parts = line.Split(',');
            var raw = long.Parse(parts[0], culture);
            var open = DateTimeOffset.FromUnixTimeMilliseconds(
                raw > 100_000_000_000_000L ? raw / 1000 : raw);
            var today = DateOnly.FromDateTime(open.UtcDateTime);
            if (date.HasValue && today != date)
            {
                EvaluateDay();
                candles.Clear();
            }
            date = today;
            decimal Number(int index) => decimal.Parse(parts[index],
                NumberStyles.AllowDecimalPoint, culture);
            candles.Add(new MarketCandlePoint(symbol, open,
                open.AddMinutes(1).AddMilliseconds(-1), Number(1), Number(2),
                Number(3), Number(4), Number(5), Number(7),
                long.Parse(parts[8], culture)));
        }
        if (date.HasValue) EvaluateDay();
        if (results.Count != validated.CalendarDays)
            throw new InvalidDataException("Missing fixed holdout days.");
        var unresolved = results.Sum(x => x.Unresolved);
        return new(symbol, period, frozenGate, frozenHorizonMinutes,
            results.Count, results.Sum(x => x.Trades), unresolved,
            unresolved == 0 ? Math.Round(results.Average(x => x.Return), 4) : null,
            unresolved == 0 ? "FIXED_SHADOW_REPLAY_RESEARCH_ONLY" :
                "EXIT_UNRESOLVED_RETURN_WITHHELD",
            false, false, false, false);

        void EvaluateDay()
        {
            if (candles.Count != 1440)
                throw new InvalidDataException("Incomplete fixed replay day.");
            // Only UTC 12:00–23:59 candles are evaluated. Daily morning
            // windows and all later holdout returns are never used for selection.
            var test = candles.Skip(MonthlyWalkForwardResearch.TrainMinutes).ToArray();
            var slice = PaperTradeHorizonResearch.SimulateSlice(symbol, test,
                frozenHorizonMinutes, 1m, frozenGate,
                liquidityStress: true, captureExitIntents: true);
            if (slice.Status != "COMPLETE")
                throw new InvalidDataException("Fixed replay simulator blocked.");
            var exits = PaperExitCapacityGate.Assess(slice, test);
            results.Add((slice.ReturnPct, slice.CompletedTrades, exits.UnresolvedExits));
        }
    }
}
