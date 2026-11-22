using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RAC.Domain;
using RAC.Infrastructure;

namespace RAC.Tests;

/// <summary>
/// MVP 1.2.1 contract/recovery suite: all responses are fabricated by
/// HttpMessageHandler. No real Binance request can escape a test.
/// </summary>
public sealed class TestnetMvp121Tests
{
    private static readonly string[] Symbols=RacMarketSymbols.Supported;

    [Theory]
    [InlineData("BTCUSDT")][InlineData("ETHUSDT")][InlineData("SOLUSDT")]
    [InlineData("BNBUSDT")][InlineData("XRPUSDT")][InlineData("ADAUSDT")]
    [InlineData("DOGEUSDT")][InlineData("LINKUSDT")][InlineData("AVAXUSDT")]
    [InlineData("LTCUSDT")][InlineData("DOTUSDT")][InlineData("TRXUSDT")]
    public void All_twelve_agents_are_approved_and_have_stable_test_order_ids(string symbol)
    {
        Assert.True(RacMarketSymbols.IsSupported(symbol));
        Assert.Equal(12,Symbols.Distinct().Count());
        Assert.Equal("RAC12"+42.ToString("D14"),TestnetExecutionPolicy.ClientOrderId(42));
        Assert.Throws<ArgumentOutOfRangeException>(()=>TestnetExecutionPolicy.ClientOrderId(0));
    }

    [Fact]
    public void Risk_and_restart_policies_block_unsafe_execution()
    {
        var now=DateTimeOffset.UtcNow;
        var state=new TestnetExecutionState {Enabled=true};
        var paper=new PaperAccount {Running=true,LastCycleUtc=now};
        // This suite never opts in to actual Testnet sends.
        Assert.NotNull(TestnetExecutionPolicy.BlockReason(state,paper,now,false));
        Assert.False(new TestnetExecutionState().Enabled);
        Assert.True(TestnetExecutionPolicy.IsUnsettled("SUBMITTING"));
        Assert.True(TestnetExecutionPolicy.IsUnsettled("UNKNOWN"));
        Assert.True(TestnetExecutionPolicy.IsUnsettled("PARTIALLY_FILLED"));
        Assert.True(TestnetExecutionPolicy.IsTerminal("FILLED"));
        Assert.True(TestnetExecutionPolicy.IsTerminal("CANCELED"));
        Assert.False(TestnetExecutionPolicy.IsTerminal("UNKNOWN"));
        Assert.Equal(0.5m,RAC.Application.PaperRisk.MaxRiskPerTradePct);
        Assert.Equal(1m,RAC.Application.PaperRisk.DailyLossLimitPct);
        Assert.Equal(10m,RAC.Application.PaperRisk.MaxDrawdownPct);
    }

    [Fact]
    public void Paper_guard_blocks_stale_data_loss_and_drawdown_without_altering_limits()
    {
        var now=DateTimeOffset.UtcNow;
        var paper=new PaperAccount {Running=true,LastCycleUtc=now,
            LastEquityBrl=100m,DailyStartEquityBrl=100m,PeakEquityBrl=100m};
        Assert.Null(TestnetExecutionPolicy.PaperSafetyReason(paper,now));
        paper.LastCycleUtc=now.AddMinutes(-10);
        Assert.Contains("desatualizado",TestnetExecutionPolicy.PaperSafetyReason(paper,now)!);
        paper.LastCycleUtc=now;
        paper.LastEquityBrl=98.99m;
        Assert.NotNull(TestnetExecutionPolicy.PaperSafetyReason(paper,now));
        paper.LastEquityBrl=90m;
        Assert.NotNull(TestnetExecutionPolicy.PaperSafetyReason(paper,now));
        paper.LastEquityBrl=100m;
        paper.RiskLocked=true;
        Assert.NotNull(TestnetExecutionPolicy.PaperSafetyReason(paper,now));
        paper.RiskLocked=false;
        paper.Running=false;
        Assert.NotNull(TestnetExecutionPolicy.PaperSafetyReason(paper,now));
    }

    [Fact]
    public void Reconciliation_preserves_idempotency_and_never_regresses_executions()
    {
        var intent=NewIntent("BUY",0.5m);
        var now=DateTimeOffset.UtcNow;
        TestnetExecutionPolicy.Reconcile(intent,NewOrder("PARTIALLY_FILLED",0.2m),now);
        Assert.Equal(0.2m,intent.ExecutedQuantity);
        Assert.True(TestnetExecutionPolicy.IsUnsettled(intent.Status));
        Assert.Throws<InvalidOperationException>(()=>
            TestnetExecutionPolicy.Reconcile(intent,NewOrder("NEW",0.1m),now));
        TestnetExecutionPolicy.Reconcile(intent,NewOrder("FILLED",0.5m),now);
        Assert.Equal("FILLED",intent.Status);
        Assert.Throws<InvalidOperationException>(()=>
            TestnetExecutionPolicy.Reconcile(intent,NewOrder("NEW",0.5m),now));
        Assert.Throws<InvalidOperationException>(()=>
            TestnetExecutionPolicy.Reconcile(intent,NewOrder("FILLED",0.6m),now));
        Assert.Throws<InvalidOperationException>(()=>
            TestnetExecutionPolicy.Reconcile(intent,NewOrder("UNKNOWN",0.5m),now));
    }

    [Fact]
    public void Reconciliation_rejects_another_symbol_or_client_identifier()
    {
        var intent=NewIntent("SELL",1m);
        Assert.Throws<InvalidOperationException>(()=>TestnetExecutionPolicy.Reconcile(intent,
            NewOrder("FILLED",1m) with {Symbol="ETHUSDT"},DateTimeOffset.UtcNow));
        Assert.Throws<InvalidOperationException>(()=>TestnetExecutionPolicy.Reconcile(intent,
            NewOrder("FILLED",1m) with {ClientOrderId="OTHER"},DateTimeOffset.UtcNow));
        Assert.Equal("PREPARED",intent.Status);
    }

    [Fact]
    public void Sells_require_confirmed_net_testnet_holdings()
    {
        var history=new[] {
            NewIntent("BUY",2m,"FILLED",2m),
            NewIntent("SELL",0.75m,"FILLED",0.75m),
            NewIntent("BUY",5m,"UNKNOWN",5m)
        };
        Assert.True(TestnetExecutionPolicy.HasConfirmedSellQuantity(history,"BTCUSDT",1m));
        Assert.False(TestnetExecutionPolicy.HasConfirmedSellQuantity(history,"BTCUSDT",1.5m));
        Assert.False(TestnetExecutionPolicy.HasConfirmedSellQuantity(history,"ETHUSDT",0.1m));
    }

    [Theory]
    [InlineData("BUY","FILLED")]
    [InlineData("SELL","FILLED")]
    [InlineData("BUY","NEW")]
    public async Task Mocked_market_orders_use_only_the_testnet_and_a_unique_id(
        string side,string status)
    {
        if(!OperatingSystem.IsWindows())return;
        await WithClientAsync(new MockHandler {ResultStatus=status},async (client,mock)=>
        {
            var order=await client.MarketAsync("BTCUSDT",side,0.5m,
                "RAC12000000000042",CancellationToken.None);
            Assert.Equal(status,order.Status);
            Assert.Equal(1,mock.Posts);
            Assert.Equal(0,mock.Deletes);
            Assert.Equal(1,mock.Times);
            Assert.Contains(mock.Log,x=>x.Contains("newClientOrderId=RAC12000000000042"));
            Assert.Contains(mock.Log,x=>x.Contains("side="+side));
            Assert.All(mock.Log,x=>Assert.StartsWith("https://testnet.binance.vision/",x));
        });
    }

    [Fact]
    public async Task Mocked_cancel_uses_delete_by_same_client_id()
    {
        if(!OperatingSystem.IsWindows())return;
        await WithClientAsync(new MockHandler {ResultStatus="CANCELED"},async (client,mock)=>
        {
            var result=await client.CancelAsync("BTCUSDT","RAC12000000000042",CancellationToken.None);
            Assert.Equal("CANCELED",result.Status);
            Assert.Equal(1,mock.Deletes);
            Assert.Equal(0,mock.Posts);
            Assert.Contains(mock.Log,x=>x.Contains("origClientOrderId=RAC12000000000042"));
        });
    }

    [Fact]
    public async Task Recovery_queries_existing_order_without_posting_replacement()
    {
        if(!OperatingSystem.IsWindows())return;
        await WithClientAsync(new MockHandler {ResultStatus="FILLED"},async (client,mock)=>
        {
            var recovered=await client.QueryAsync("BTCUSDT",
                "RAC12000000000042",CancellationToken.None);
            Assert.NotNull(recovered);
            var record=NewIntent("BUY",0.5m,"SUBMITTING");
            TestnetExecutionPolicy.Reconcile(record,recovered!,DateTimeOffset.UtcNow);
            Assert.Equal("FILLED",record.Status);
            Assert.Equal(0,mock.Posts);
            Assert.Equal(0,mock.Deletes);
        });
    }

    [Fact]
    public async Task Unknown_order_returns_null_and_does_not_trigger_repost()
    {
        if(!OperatingSystem.IsWindows())return;
        await WithClientAsync(new MockHandler {OrderNotFound=true},async (client,mock)=>
        {
            var result=await client.QueryAsync("BTCUSDT","RAC12000000000042",
                CancellationToken.None);
            Assert.Null(result);
            Assert.Equal(0,mock.Posts);
            Assert.Equal(0,mock.Deletes);
        });
    }

    [Fact]
    public async Task Network_failure_is_not_mistaken_for_success()
    {
        if(!OperatingSystem.IsWindows())return;
        await WithClientAsync(new MockHandler {FailOrders=true},async (client,mock)=>
        {
            await Assert.ThrowsAsync<HttpRequestException>(()=>
                client.MarketAsync("BTCUSDT","BUY",0.5m,
                    "RAC12000000000042",CancellationToken.None));
            Assert.Equal(1,mock.Posts);
            Assert.Equal(0,mock.Deletes);
        });
    }

    [Fact]
    public void Sql_metadata_guarantees_single_paper_fill_and_client_id_per_order()
    {
        using var db=new RacDbContext(new DbContextOptionsBuilder<RacDbContext>()
            .UseSqlServer("Server=(local);Database=MetadataOnly;Integrated Security=True;TrustServerCertificate=True").Options);
        var entity=db.Model.FindEntityType(typeof(TestnetOrderRecord))!;
        var keys=entity.GetIndexes().Where(i=>i.IsUnique).Select(i=>i.Properties.Single().Name).ToArray();
        Assert.Contains(nameof(TestnetOrderRecord.PaperFillId),keys);
        Assert.Contains(nameof(TestnetOrderRecord.ClientOrderId),keys);
        Assert.True(entity.FindProperty(nameof(TestnetOrderRecord.RowVersion))!.IsConcurrencyToken);
    }

    private static TestnetOrderRecord NewIntent(string side,decimal quantity,
        string status="PREPARED",decimal executed=0) => new()
    {
        PaperFillId=42,Symbol="BTCUSDT",Side=side,ClientOrderId="RAC12000000000042",
        Quantity=quantity,Status=status,ExecutedQuantity=executed,
        CreatedUtc=DateTimeOffset.UtcNow
    };
    private static TestnetExchangeOrder NewOrder(string status,decimal executed) =>
        new("RAC12000000000042","BTCUSDT",status,"9001",executed,executed*100m);

    private static async Task WithClientAsync(MockHandler handler,
        Func<BinanceTestnetOrderClient,MockHandler,Task> action)
    {
        var directory=Path.Combine(Path.GetTempPath(),"rac-testnet-mock-"+Guid.NewGuid().ToString("N"));
        try
        {
            var store=new BinanceTestnetCredentialStore(directory);
            store.Save(new string('K',64),new string('S',64));
            using var http=new HttpClient(handler) {
                BaseAddress=new Uri("https://testnet.binance.vision/")
            };
            await action(new BinanceTestnetOrderClient(http,store),handler);
        }
        finally {if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }

    private sealed class MockHandler:HttpMessageHandler
    {
        public string ResultStatus {get;set;}="FILLED";
        public bool OrderNotFound {get;set;}
        public bool FailOrders {get;set;}
        public int Posts {get;private set;}
        public int Deletes {get;private set;}
        public int Times {get;private set;}
        public List<string> Log {get;}=[];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            var uri=request.RequestUri!;
            Assert.Equal("https",uri.Scheme);
            Assert.Equal("testnet.binance.vision",uri.Host);
            Log.Add(uri.AbsoluteUri);
            if(uri.AbsolutePath=="/api/v3/time")
            {
                Times++;
                Assert.Equal(HttpMethod.Get,request.Method);
                return Ok("""{"serverTime":1795360000000}""");
            }
            Assert.Equal("/api/v3/order",uri.AbsolutePath);
            Assert.Contains("signature=",uri.Query);
            Assert.True(request.Headers.Contains("X-MBX-APIKEY"));
            if(request.Method==HttpMethod.Post)Posts++;
            else if(request.Method==HttpMethod.Delete)Deletes++;
            else Assert.Equal(HttpMethod.Get,request.Method);
            if(FailOrders)throw new HttpRequestException("Simulated network outage.");
            if(OrderNotFound && request.Method==HttpMethod.Get)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest) {
                    Content=new StringContent("""{"code":-2013,"msg":"Order does not exist"}""")
                });
            return Ok($$"""{"clientOrderId":"RAC12000000000042","symbol":"BTCUSDT","status":"{{ResultStatus}}","orderId":9001,"executedQty":"0.5","cummulativeQuoteQty":"50"}""");
        }
        private static Task<HttpResponseMessage> Ok(string body) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
                Content=new StringContent(body,Encoding.UTF8,"application/json")
            });
    }
}
