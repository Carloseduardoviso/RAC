using RAC.Application;
namespace RAC.Infrastructure;

public sealed class GabrielDynamicHistoricalSource(GabrielPublicArchiveClient archives, ResearchQuantitativeExecutionStore? memory=null, ResearchExecutionManifestStore? manifests=null)
{


    public Task<string> ReadAsync(IReadOnlyList<string> symbols,CancellationToken ct)
        => ReadBoundedAsync(symbols,TimeSpan.FromMinutes(30),ct);

    public async Task<string> ReadBoundedAsync(IReadOnlyList<string> symbols,TimeSpan timeout,CancellationToken ct)
    {
        if(timeout<=TimeSpan.Zero || timeout>TimeSpan.FromMinutes(30))
            throw new ArgumentOutOfRangeException(nameof(timeout));
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(timeout);
        var evidence=new List<ResearchExecutionManifest.Evidence>();
        var result=await GabrielHistoricalMatrixBuilder.BuildAsync(symbols,ReadArchiveAsync,deadline.Token,
            memory is null?null:async (row,hash,month,replay,token)=>
            {
                await RecordAsync(row,hash,month,replay,token);
                evidence.Add(new(row,month,hash));
            });
        if(manifests is not null && memory is null) throw new InvalidOperationException("Manifest persistence requires quantitative memory.");
        if(manifests is not null)
            await manifests.SaveAsync(symbols,ResearchEngineCatalog.Current.Id,evidence,DateTimeOffset.UtcNow,deadline.Token);
        else if(memory is not null)
            ResearchExecutionManifest.Create(symbols,ResearchEngineCatalog.Current.Id,evidence);
        deadline.Token.ThrowIfCancellationRequested();
        return result;
    }

    private async Task RecordAsync(ResearchExperimentPlan.Row row,string hash,DateOnly month,ResearchExperimentReplay.Result result,CancellationToken ct)
        => await memory!.SaveAsync(row,hash,month,ResearchEngineCatalog.Current.Id,result,DateTimeOffset.UtcNow,ct);

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
