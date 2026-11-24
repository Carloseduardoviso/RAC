using System.Text.Json;
using RAC.Application;
namespace RAC.Api.Services;

// Optional local feed adapter. No external source is assumed or fabricated.
public sealed class RebecaFeedWorker(RebecaNewsCalendar calendar,IConfiguration config,ILogger<RebecaFeedWorker> logger):BackgroundService
{
    public sealed record Feed(DateTimeOffset ObservedUtc,RebecaNewsCalendar.Item[] Items);
    public static Feed Parse(string json,DateTimeOffset now)
    {
        var feed=JsonSerializer.Deserialize<Feed>(json,new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidDataException("Missing feed.");
        if(feed.Items is null || feed.ObservedUtc>now || now-feed.ObservedUtc>TimeSpan.FromMinutes(5))
            throw new InvalidDataException("Invalid feed freshness.");
        return feed;
    }
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var path=config["Rac:News:LocalFeedPath"];
        if(string.IsNullOrWhiteSpace(path))return;
        DateTimeOffset? last=null;
        using var timer=new PeriodicTimer(TimeSpan.FromSeconds(30));
        do
        {
            try
            {
                await using var stream=File.OpenRead(path);
                if(stream.Length>262144)throw new InvalidDataException("Feed too large.");
                using var reader=new StreamReader(stream);
                var buffer=new char[262145];
                var count=await reader.ReadBlockAsync(buffer.AsMemory(),ct);
                if(count>262144)throw new InvalidDataException("Feed too large.");
                var feed=Parse(new string(buffer,0,count),DateTimeOffset.UtcNow);
                if(last is null || feed.ObservedUtc>last)
                {
                    calendar.Publish(feed.ObservedUtc,feed.Items);
                    last=feed.ObservedUtc;
                }
            }
            catch(OperationCanceledException) when(ct.IsCancellationRequested){return;}
            catch(Exception e) when(e is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException)
            {logger.LogWarning("Rebeca feed unavailable: {Category}",e.GetType().Name);}
        }while(await timer.WaitForNextTickAsync(ct));
    }
}
