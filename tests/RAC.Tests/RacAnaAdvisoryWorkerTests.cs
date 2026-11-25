using Microsoft.Extensions.Logging.Abstractions;
using RAC.Api;
using RAC.Application;
using Xunit;

namespace RAC.Tests;
public sealed class RacAnaAdvisoryWorkerTests
{
    [Fact]
    public async Task RunsReadOnlyCycleAndStops()
    {
        var observed = new TaskCompletionSource<TimeSpan>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var worker = new RacAnaAdvisoryWorker((now,age,ct) =>
        {
            observed.TrySetResult(age);
            return Task.FromResult(RacAnaAdvisory.Summarize([]));
        },NullLogger<RacAnaAdvisoryWorker>.Instance);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await worker.StartAsync(timeout.Token);
        try { Assert.Equal(TimeSpan.FromMinutes(15),await observed.Task.WaitAsync(timeout.Token)); }
        finally { await worker.StopAsync(timeout.Token); }
    }
}
