using System.Net;
using System.Text;
using RAC.Application;
using RAC.Domain;
using RAC.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace RAC.Tests;

public sealed class TestnetExecutorTests
{
    private const string ExchangeInfo = """
    {"symbols":[{"symbol":"BTCUSDT","status":"TRADING",
    "orderTypes":["LIMIT","MARKET"],
    "filters":[
      {"filterType":"LOT_SIZE","minQty":"0.00001000","maxQty":"900.00000000","stepSize":"0.00001000"},
      {"filterType":"MARKET_LOT_SIZE","minQty":"0.00000000","maxQty":"100.00000000","stepSize":"0.00001000"},
      {"filterType":"MIN_NOTIONAL","minNotional":"5.00000000","applyToMarket":true}
    ]}]}
    """;

    [Fact]
    public void Testnet_filter_rounds_down_and_rejects_dust()
    {
        using var parsed=System.Text.Json.JsonDocument.Parse(ExchangeInfo);
        var filters=TestnetOrderValidation.Parse(parsed.RootElement,"BTCUSDT");
        Assert.Equal(0.00002m, TestnetOrderValidation.ValidateQuantity(0.000029m,300000m,filters));
        Assert.Throws<InvalidOperationException>(()=>
            TestnetOrderValidation.ValidateQuantity(0.00002m,10000m,filters));
        Assert.Throws<InvalidOperationException>(()=>
            TestnetOrderValidation.ValidateQuantity(0.000001m,100000m,filters));
    }
    [Fact]
    public void An_unapproved_or_disabled_market_is_rejected()
    {
        using var parsed=System.Text.Json.JsonDocument.Parse(ExchangeInfo);
        Assert.Throws<InvalidOperationException>(()=>
            TestnetOrderValidation.Parse(parsed.RootElement,"ETHUSDT"));
        var filters=TestnetOrderValidation.Parse(parsed.RootElement,"BTCUSDT");
        Assert.Throws<InvalidOperationException>(()=>
            TestnetOrderValidation.ValidateQuantity(1m,1m,filters with {Trading=false}));
    }

    [Fact]
    public async Task Testnet_client_refuses_mainnet_host_before_request()
    {
        if(!OperatingSystem.IsWindows())return;
        var store=new BinanceTestnetCredentialStore(
            Path.Combine(Path.GetTempPath(),"RAC-testnet-"+Guid.NewGuid().ToString("N")));
        var handler=new GuardHandler();
        using var http=new HttpClient(handler) {BaseAddress=new Uri("https://api.binance.com/")};
        var client=new BinanceTestnetOrderClient(http,store);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>
            client.FiltersAsync("BTCUSDT",CancellationToken.None));
        Assert.Equal(0,handler.Calls);
    }

    [Fact]
    public async Task Public_filters_use_only_testnet_host_and_get_requests()
    {
        if(!OperatingSystem.IsWindows())return;
        var folder=Path.Combine(Path.GetTempPath(),"RAC-testnet-"+Guid.NewGuid().ToString("N"));
        var store=new BinanceTestnetCredentialStore(folder);
        var handler=new GuardHandler();
        using var http=new HttpClient(handler) {BaseAddress=new Uri("https://testnet.binance.vision/")};
        var client=new BinanceTestnetOrderClient(http,store);
        var filters=await client.FiltersAsync("BTCUSDT",CancellationToken.None);
        Assert.Equal(5m,filters.MinNotional);
        Assert.Equal(1,handler.Calls);
    }

    [Fact]
    public void SQL_ledger_enforces_exactly_one_intent_per_paper_fill()
    {
        using var db=new RacDbContext(new DbContextOptionsBuilder<RacDbContext>()
            .UseSqlServer("Server=(local);Database=MetadataOnly;Integrated Security=True;TrustServerCertificate=True").Options);
        var entity=db.Model.FindEntityType(typeof(TestnetOrderRecord));
        Assert.NotNull(entity);
        Assert.Contains(entity.GetIndexes(),i=>i.IsUnique &&
            i.Properties.Single().Name==nameof(TestnetOrderRecord.PaperFillId));
        var state=db.Model.FindEntityType(typeof(TestnetExecutionState));
        Assert.NotNull(state);
        Assert.True(state.FindProperty(nameof(TestnetExecutionState.RowVersion))!.IsConcurrencyToken);
        Assert.False(new TestnetExecutionState().Enabled);
    }

    [Fact]
    public void Risk_policy_remains_unchanged_for_testnet()
    {
        Assert.Equal(0.5m,PaperRisk.MaxRiskPerTradePct);
        Assert.Equal(1m,PaperRisk.DailyLossLimitPct);
        Assert.Equal(10m,PaperRisk.MaxDrawdownPct);
        Assert.Equal(100m,PaperRisk.ReinvestmentPercent);
        Assert.Equal(12,RacMarketSymbols.Supported.Length);
        Assert.Equal(1,PaperRisk.MaxOpenPositions);
    }

    [Fact]
    public async Task Signed_test_order_uses_pinned_testnet_host_and_never_mainnet()
    {
        if(!OperatingSystem.IsWindows())return;
        var folder=Path.Combine(Path.GetTempPath(),"RAC-testnet-"+Guid.NewGuid().ToString("N"));
        try
        {
            var store=new BinanceTestnetCredentialStore(folder);
            store.Save(new string('K',64),new string('S',64));
            var handler=new SignedTestnetHandler();
            using var http=new HttpClient(handler)
                {BaseAddress=new Uri("https://testnet.binance.vision/")};
            var client=new BinanceTestnetOrderClient(http,store);
            var order=await client.MarketAsync("BTCUSDT","BUY",0.0001m,
                "RAC12000000000123",CancellationToken.None);
            Assert.Equal("FILLED",order.Status);
            Assert.Equal("BTCUSDT",order.Symbol);
            Assert.Equal(1,handler.Posts);
            Assert.Equal(1,handler.Times);
        }
        finally
        {
            if(Directory.Exists(folder))Directory.Delete(folder,true);
        }
    }

    private sealed class SignedTestnetHandler : HttpMessageHandler
    {
        public int Posts {get;private set;}
        public int Times {get;private set;}
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req,CancellationToken ct)
        {
            Assert.Equal("testnet.binance.vision",req.RequestUri!.Host);
            string payload;
            if(req.RequestUri.AbsolutePath=="/api/v3/time")
            {
                Assert.Equal(HttpMethod.Get,req.Method);
                Times++;
                payload="""{"serverTime":1795360000000}""";
            }
            else
            {
                Assert.Equal(HttpMethod.Post,req.Method);
                Assert.Equal("/api/v3/order",req.RequestUri.AbsolutePath);
                Assert.Contains("newClientOrderId=RAC12000000000123",req.RequestUri.Query);
                Assert.Contains("signature=",req.RequestUri.Query);
                Assert.True(req.Headers.Contains("X-MBX-APIKEY"));
                Posts++;
                payload="""{"clientOrderId":"RAC12000000000123","symbol":"BTCUSDT","status":"FILLED","orderId":5,"executedQty":"0.0001","cummulativeQuoteQty":"10.5"}""";
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {Content=new StringContent(payload,Encoding.UTF8,"application/json")});
        }
    }

    private sealed class GuardHandler : HttpMessageHandler
    {
        public int Calls {get;private set;}
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req,CancellationToken ct)
        {
            Calls++;
            Assert.Equal("testnet.binance.vision",req.RequestUri!.Host);
            Assert.Equal(HttpMethod.Get,req.Method);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {Content=new StringContent(ExchangeInfo,Encoding.UTF8,"application/json")});
        }
    }
}
