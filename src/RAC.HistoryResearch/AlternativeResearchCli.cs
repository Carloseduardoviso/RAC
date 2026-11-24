using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using RAC.Application;
using RAC.Domain;

internal static class AlternativeResearchCli
{
    // Exploratory training only; no operational dependencies or promotion.
    internal static async Task<int> RunAsync(bool holdout = false)
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RAC", "research", "monthly");
        var rows = new List<object>();
        foreach (var month in (holdout ? new[]{"2026-08","2026-09","2026-10"} : new[]{"2026-05","2026-06","2026-07"}))
        {
            var folder = Path.Combine(root,month);
            using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(folder,"manifest.json")));
            var m = doc.RootElement;
            if(m.GetProperty("month").GetString()!=month || m.GetProperty("mode").GetString()!="ISOLATED_MONTHLY_ARCHIVE_RESEARCH_ONLY" || m.GetProperty("paperModified").GetBoolean() || m.GetProperty("orderPlacementEnabled").GetBoolean() || m.GetProperty("promotionAllowed").GetBoolean()) throw new InvalidDataException("Unsafe manifest");
            foreach(var symbol in RacMarketSymbols.Supported)
            {
                if(holdout && symbol is not ("XRPUSDT" or "ADAUSDT")) continue;
                var name = $"{symbol}-1m-{month}.zip";
                var item = m.GetProperty("reports").EnumerateArray().Single(x=>x.GetProperty("symbol").GetString()==symbol);
                using var stream = File.OpenRead(Path.Combine(folder,name));
                if(stream.Length<=0 || stream.Length>MonthlyArchiveResearch.MaximumCompressedBytes || item.GetProperty("archiveFile").GetString()!=name || !string.Equals(Convert.ToHexString(SHA256.HashData(stream)),item.GetProperty("sha256").GetString(),StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Archive mismatch");
                stream.Position=0;
                MonthlyArchiveResearch.Analyze(symbol,2026,int.Parse(month[5..]),stream);
                stream.Position=0;
                using var zip = new ZipArchive(stream,ZipArchiveMode.Read,true);
                using var reader = new StreamReader(zip.Entries.Single().Open());
                var candles = new List<(decimal Open,decimal Close,decimal QuoteVolume)>();
                while(await reader.ReadLineAsync() is {} line)
                {
                    var p=line.Split(',');
                    candles.Add((decimal.Parse(p[1],CultureInfo.InvariantCulture),decimal.Parse(p[4],CultureInfo.InvariantCulture),decimal.Parse(p[7],CultureInfo.InvariantCulture)));
                }
                foreach(var interval in new[]{5,15,30,60,120,240})
                foreach(var strategy in new[]{"TREND","REVERSAL"})
                foreach(var volumeGate in new[]{false,true})
                foreach(var volatilityGate in new[]{false,true})
                foreach(var cost in new[]{0.0015m,0.003m})
                {
                    if(holdout && !(symbol=="XRPUSDT" && interval==240 && strategy=="REVERSAL" && volumeGate && volatilityGate || symbol=="ADAUSDT" && strategy=="TREND" && (interval==30 && volumeGate || interval==120 && !volumeGate && volatilityGate))) continue;
                    decimal cash=1000,qty=0,peak=1000,dd=0,entry=0,high=0; int trades=0,blocked=0,exitBlocked=0; bool pendingExit=false;
                    for(int i=interval*12;i<candles.Count;i++)
                    {
                        var price=candles[i].Open;
                        bool enter=false,exit=false;
                        if(i%interval==0)
                        {
                            var fast=Enumerable.Range(1,3).Average(k=>candles[i-(k-1)*interval-1].Close);
                            var slow=Enumerable.Range(1,12).Average(k=>candles[i-(k-1)*interval-1].Close);
                            enter=strategy=="TREND"?fast>slow*1.002m:fast<slow*.995m;
                            exit=strategy=="TREND"?fast<slow:fast>=slow;
                            if(volatilityGate)
                            {
                                var range=candles.GetRange(i-interval,interval);
                                enter=enter && range.Max(x=>x.Close)/range.Min(x=>x.Close)<=1.03m;
                            }
                            if(volumeGate)
                            {
                                var recent=candles.GetRange(i-interval,interval).Sum(x=>x.QuoteVolume);
                                var prior=candles.GetRange(i-12*interval,11*interval).Sum(x=>x.QuoteVolume)/11m;
                                enter=enter && prior>0 && recent>=prior*1.5m;
                            }
                        }
                        if(qty>0)
                        {
                            high=Math.Max(high,candles[i-1].Close);
                            exit=exit || price<=entry*.98m || price>=entry*1.04m || (high>=entry*1.02m && price<=high*.985m);
                            pendingExit=pendingExit || exit;
                            if(pendingExit)
                            {
                                if(qty*price<=candles[i-1].QuoteVolume*.001m){cash+=qty*price*(1-cost);qty=0;trades++;pendingExit=false;}
                                else exitBlocked++;
                            }
                        }
                        else if(enter)
                        {
                            var budget=cash*.10m;
                            // Previous minute volume is only a conservative entry proxy, not a fill guarantee.
                            if(budget<=candles[i-1].QuoteVolume*.001m)
                            {qty=budget*(1-cost)/price;cash-=budget;entry=price;high=price;}
                            else blocked++;
                        }
                        var equity=cash+qty*price*(1-cost);
                        peak=Math.Max(peak,equity);dd=Math.Max(dd,1-equity/peak);
                    }
                    var unresolved=qty>0;
                    var finalEquity=cash+qty*candles[^1].Close*(1-cost);
                    dd=Math.Max(dd,1-finalEquity/peak);
                    rows.Add(new {month,symbol,interval,strategy,volumeGate,volatilityGate,cost,trades,blocked,exitBlocked,unresolved,netPct=(finalEquity/1000-1)*100,drawdownPct=dd*100,buyHoldPct=(candles[^1].Close/candles[0].Open*(1-cost)*(1-cost)-1)*100});
                }
            }
        }
        var output=Path.Combine(root,holdout ? "alternative-holdout-mvp1624.json" : "alternative-training-mvp162-liquidity.json");
        await File.WriteAllTextAsync(output+".partial",JsonSerializer.Serialize(new {mode=holdout ? "FIXED_REPLAY_PREVIOUSLY_RESEARCHED_MONTHS" : "TRAINING_ONLY_EXPLORATORY",paperModified=false,orderPlacementEnabled=false,promotionAllowed=false,rows},new JsonSerializerOptions{WriteIndented=true}));
        File.Move(output+".partial",output,true);
        Console.WriteLine($"ALTERNATIVE_TRAINING rows={rows.Count}");
        return 0;
    }
}
