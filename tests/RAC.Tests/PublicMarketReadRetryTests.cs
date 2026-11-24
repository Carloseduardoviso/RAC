using System.Net;
using RAC.Infrastructure;
namespace RAC.Tests;
public class PublicMarketReadRetryTests
{
    [Fact]
    public async Task RecoversTransientRead()
    {
        int calls=0;
        var result=await PublicMarketReadRetry.RunAsync<int>(_=>++calls==1 ?
            Task.FromException<int>(new HttpRequestException()) : Task.FromResult(42),default);
        Assert.Equal(42,result);Assert.Equal(2,calls);
    }
    [Fact]
    public async Task ExhaustionFailsClosed()
    {
        int calls=0;
        await Assert.ThrowsAsync<HttpRequestException>(()=>PublicMarketReadRetry.RunAsync<int>(_=>{
            calls++;return Task.FromException<int>(new HttpRequestException());},default));
        Assert.Equal(3,calls);
    }
    [Fact]
    public async Task RateLimitNotRetried()
    {
        int calls=0;
        await Assert.ThrowsAsync<HttpRequestException>(()=>PublicMarketReadRetry.RunAsync<int>(_=>{
            calls++;return Task.FromException<int>(new HttpRequestException("limit",null,HttpStatusCode.TooManyRequests));},default));
        Assert.Equal(1,calls);
    }
}
