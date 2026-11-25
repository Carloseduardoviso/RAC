using RAC.Infrastructure;
namespace RAC.Tests;
public sealed class GabrielResearchLeaseTests
{
    [Fact]
    public async Task LostLeaseCancelsResearch()
    {
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var cancelled=false;
        await Assert.ThrowsAsync<InvalidOperationException>(()=>GabrielResearchLease.RunAsync<int>(
            async ct=>{
                try { await Task.Delay(Timeout.InfiniteTimeSpan,ct); return 1; }
                finally { cancelled=ct.IsCancellationRequested; }
            },_=>Task.FromResult(false),TimeSpan.FromMilliseconds(10),deadline.Token));
        Assert.True(cancelled);
    }

    [Fact]
    public async Task ValidRenewalAllowsResearchToFinish()
    {
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var renewed=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var result=await GabrielResearchLease.RunAsync(
            async ct=>{await renewed.Task.WaitAsync(ct); return 42;},
            _=>{renewed.TrySetResult(true); return Task.FromResult(true);},
            TimeSpan.FromMilliseconds(10),deadline.Token);
        Assert.Equal(42,result);
    }
}
