using System.Globalization;
using System.IO.Compression;
using RAC.Domain;

namespace RAC.Application;

/// <summary>Strictly offline/public data quality and descriptive daily regimes.
/// Does not infer profitable strategies, read the operational database or trade.</summary>
public static class MonthlyArchiveResearch
{
    public const int MaximumCompressedBytes = 20 * 1024 * 1024;
    public const long MaximumCsvBytes = 28L * 1024 * 1024;
    public sealed record Day(DateOnly DateUtc, int Candles, decimal FirstOpen,
        decimal LastClose, decimal ReturnPct, string DescriptiveRegime);
    public sealed record Report(string Symbol, int Year, int Month,
        int ExpectedCandles, int ValidatedCandles, int CalendarDays,
        int UpDays, int DownDays, int SidewaysDays,
        DateTimeOffset FirstOpenUtc, DateTimeOffset LastOpenUtc,
        bool PaperModified, bool OrderPlacementEnabled, bool PromotionAllowed,
        IReadOnlyList<Day> Days, string Interpretation);

    public static Report Analyze(string symbol, int year, int month, Stream zipStream)
    {
        if (!RacMarketSymbols.IsSupported(symbol))
            throw new ArgumentException("Unsupported symbol.", nameof(symbol));
        if (year is < 2017 or > 2100 || month is < 1 or > 12)
            throw new ArgumentOutOfRangeException(nameof(month));
        if (!zipStream.CanRead || !zipStream.CanSeek ||
            zipStream.Length <= 0 || zipStream.Length > MaximumCompressedBytes)
            throw new InvalidDataException("Compressed monthly archive exceeds bounds.");
        using var zip = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);
        if (zip.Entries.Count != 1) throw new InvalidDataException("Expected one CSV entry only.");
        var entry = zip.Entries[0];
        var expectedName = $"{symbol}-1m-{year:D4}-{month:D2}.csv";
        if (entry.FullName != expectedName || entry.Length <= 0 ||
            entry.Length > MaximumCsvBytes)
            throw new InvalidDataException("CSV archive name or expanded size invalid.");
        var first = new DateTimeOffset(year, month, 1, 0, 0, 0, TimeSpan.Zero);
        var end = first.AddMonths(1);
        var expectedCount = (int)(end - first).TotalMinutes;
        var count = 0;
        var days = new List<Day>();
        DateOnly? currentDay = null;
        decimal firstOpen = 0, lastClose = 0;
        var dayCount = 0;
        var number = CultureInfo.InvariantCulture;
        using var csv = new StreamReader(entry.Open());
        string? line;
        while ((line = csv.ReadLine()) != null)
        {
            if (line.Length > 1024 || count >= expectedCount)
                throw new InvalidDataException("CSV line size or count exceeded.");
            var fields = line.Split(',');
            if (fields.Length != 12)
                throw new InvalidDataException("Expected exactly 12 official kline columns.");
            if (!long.TryParse(fields[0], NumberStyles.None, number, out var timestamp))
                throw new InvalidDataException("Invalid time.");
            // Binance public data after Jan 2025 uses microseconds, earlier milliseconds.
            var micros = timestamp > 100_000_000_000_000L;
            var open = micros
                ? DateTimeOffset.FromUnixTimeMilliseconds(timestamp / 1000L)
                : DateTimeOffset.FromUnixTimeMilliseconds(timestamp);
            if (open != first.AddMinutes(count))
                throw new InvalidDataException("Missing/duplicated/out-of-order minutes.");
            if (!long.TryParse(fields[6], NumberStyles.None, number, out var closing) ||
                closing != timestamp + (micros ? 59_999_999L : 59_999L))
                throw new InvalidDataException("Malformed candle closing timestamp.");
            var o = Decimal(fields[1]); var h = Decimal(fields[2]);
            var l = Decimal(fields[3]); var c = Decimal(fields[4]);
            var volume = Decimal(fields[5]);
            var quoteVolume = Decimal(fields[7]);
            if (o <= 0 || c <= 0 || l <= 0 || h < Math.Max(o,c) ||
                l > Math.Min(o,c) || volume < 0 || quoteVolume < 0 ||
                !long.TryParse(fields[8], NumberStyles.None, number, out var trades) ||
                trades < 0) throw new InvalidDataException("Invalid OHLCV row.");
            var date = DateOnly.FromDateTime(open.UtcDateTime);
            if (currentDay.HasValue && date != currentDay.Value)
            {
                CommitDay();
                dayCount = 0;
            }
            if (dayCount == 0) { currentDay = date; firstOpen = o; }
            lastClose = c;
            dayCount++; count++;
        }
        if (dayCount > 0) CommitDay();
        if (count != expectedCount || days.Count != DateTime.DaysInMonth(year, month) ||
            days.Any(x => x.Candles != 1440))
            throw new InvalidDataException("Monthly candle coverage is incomplete.");
        return new(symbol, year, month, expectedCount, count, days.Count,
            days.Count(x => x.DescriptiveRegime == "UP"),
            days.Count(x => x.DescriptiveRegime == "DOWN"),
            days.Count(x => x.DescriptiveRegime == "SIDEWAYS"),
            first, end.AddMinutes(-1), false, false, false, days,
            "Historical daily price movement only. UP >1%, DOWN <-1%, SIDEWAYS otherwise; descriptive labels, not trades or predictions. No strategy approval, PAPER modification or exchange orders.");

        decimal Decimal(string s) => decimal.Parse(s, NumberStyles.AllowDecimalPoint,
            number);
        void CommitDay()
        {
            var ret = (lastClose / firstOpen - 1m) * 100m;
            days.Add(new Day(currentDay!.Value, dayCount, firstOpen, lastClose,
                Math.Round(ret, 4), ret > 1m ? "UP" : ret < -1m ? "DOWN" : "SIDEWAYS"));
        }
    }
}
