using RAC.Application;
namespace RAC.Infrastructure;

public sealed class GabrielDynamicHistoricalSource(GabrielPublicArchiveClient archives, ResearchQuantitativeExecutionStore? memory=null)
{


    public Task<string> ReadAsync(IReadOnlyList<string> symbols,CancellationToken ct)
        => ReadBoundedAsync(symbols,TimeSpan.FromMinutes(30),ct);

    public async Task<string> ReadBoundedAsync(IReadOnlyList<string> symbols,TimeSpan timeout,CancellationToken ct)
    {
        if(timeout<=TimeSpan.Zero || timeout>TimeSpan.FromMinutes(30))
            throw new ArgumentOutOfRangeException(nameof(timeout));
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(timeout);
        var result=await GabrielHistoricalMatrixBuilder.BuildAsync(symbols,ReadArchiveAsync,deadline.Token,memory is null?null:RecordAsync);
        deadline.Token.ThrowIfCancellationRequested();
        return result;
    }

    private async Task RecordAsync(ResearchExperimentPlan.Row row,string hash,DateOnly month,ResearchExperimentReplay.Result result,CancellationToken ct)
        => await memory!.SaveAsync(row,hash,month,"REPLAY_V1",result,DateTimeOffset.UtcNow,ct);

    private async Task<GabrielHistoricalMatrixBuilder.Archive> ReadArchiveAsync(string symbol,int year,int month,CancellationToken ct)
    {
        for(var attempt=1;;attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try { return await archives.ReadAsync(symbol,year,month,ct); }
            catch(HttpRequestException ex) when(attempt<3 &&
                (ex.StatusCode is null || (int)ex.StatusCode.Value>=500 || (int)ex.StatusCode.Value==408))
            {
                await Task.Delay(TimeSpan.FromSeconds(attempt),ct);
            }
        }
    }
}
