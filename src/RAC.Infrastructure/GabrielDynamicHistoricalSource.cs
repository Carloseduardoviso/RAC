using RAC.Application;
namespace RAC.Infrastructure;

public sealed class GabrielDynamicHistoricalSource(GabrielPublicArchiveClient archives)
{
    public Task<string> ReadAsync(IReadOnlyList<string> symbols,CancellationToken ct)
        => GabrielHistoricalMatrixBuilder.BuildAsync(symbols,ReadArchiveAsync,ct);

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
