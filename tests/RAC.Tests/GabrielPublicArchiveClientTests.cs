using RAC.Infrastructure;
using System.Net;
using System.Security.Cryptography;
namespace RAC.Tests;
public sealed class GabrielPublicArchiveClientTests
{
    private sealed class Handler(bool corrupt):HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Calls++;
            Assert.Equal(HttpMethod.Get,request.Method);
            Assert.Equal("data.binance.vision",request.RequestUri!.Host);
            Assert.Null(request.Headers.Authorization);
            byte[] bytes=[1,2,3];
            var hash=Convert.ToHexString(SHA256.HashData(bytes));
            var content=request.RequestUri.AbsolutePath.EndsWith(".CHECKSUM")?
                (HttpContent)new StringContent(hash+"  ABCUSDT-1m-2026-05.zip\n"):
                new ByteArrayContent(corrupt?new byte[]{4}:bytes);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=content});
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ValidatesPublicChecksum(bool corrupt)
    {
        using var handler=new Handler(corrupt);
        using var http=new HttpClient(handler);
        var client=new GabrielPublicArchiveClient(http);
        if(corrupt) await Assert.ThrowsAsync<InvalidDataException>(()=>client.ReadAsync("ABCUSDT",2026,5,default));
        else Assert.Equal(new byte[]{1,2,3},(await client.ReadAsync("ABCUSDT",2026,5,default)).Bytes);
        Assert.Equal(2,handler.Calls);
    }
}
