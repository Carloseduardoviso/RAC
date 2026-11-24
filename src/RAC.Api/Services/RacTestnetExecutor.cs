using Microsoft.EntityFrameworkCore;
using RAC.Application;
using RAC.Domain;
using RAC.Infrastructure;

namespace RAC.Api.Services;

/// <summary>
/// Test funds only. Mirrors approved PAPER fills into Binance Spot Testnet.
/// Single process, explicit arm, durable intent before network calls, fail closed.
/// </summary>
public sealed class RacTestnetExecutor(
    IDbContextFactory<RacDbContext> factory,
    BinanceTestnetOrderClient client,
    ILogger<RacTestnetExecutor> logger, IConfiguration? configuration = null)
{
    private readonly SemaphoreSlim _gate = new(1,1);
    private bool _initialized;

    private async Task InitializeAsync(CancellationToken ct)
    {
        if(_initialized)return;
        await using var db=await factory.CreateDbContextAsync(ct);
        var state=await db.TestnetExecutionStates.SingleOrDefaultAsync(x=>x.Id==1,ct);
        if(state is null)
            db.TestnetExecutionStates.Add(new TestnetExecutionState {Id=1,Enabled=false});
        else if(state.Enabled)
        {
            state.Enabled=false; // never resume after restart
            db.AuditEvents.Add(new AuditEvent {EventType="TESTNET_RESTORED_PAUSED",
                Description="Executor TESTNET pausado após reinício. Nenhuma ordem enviada."});
        }
        await db.SaveChangesAsync(ct);
        _initialized=true;
    }

    public async Task<object> StatusAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await InitializeAsync(ct);
            await using var db=await factory.CreateDbContextAsync(ct);
            var s=await db.TestnetExecutionStates.AsNoTracking().SingleAsync(x=>x.Id==1,ct);
            var orders=await db.TestnetOrders.AsNoTracking()
                .OrderByDescending(x=>x.Id).Take(50).ToArrayAsync(ct);
            var paper=await db.PaperAccounts.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==1,ct);
            return new {
                mode="BINANCE_SPOT_TESTNET_ONLY", realOrdersEnabled=false,
                endpoint="https://testnet.binance.vision",
                credentialsConfigured=client.CredentialsConfigured,
                submissionAuthorized=TestnetExecutionPolicy.SubmissionExplicitlyAllowed,
                testnetOrdersConfigured=configuration?.GetValue<bool>("TestnetOrdersEnabled") ?? false,
                realOrdersConfigured=configuration?.GetValue<bool>("RealOrdersEnabled") ?? false,
                homologationMode="MVP_1_2_1_MOCK_ONLY",
                enabled=s.Enabled,emergencyStopped=s.EmergencyStopped,
                circuitLocked=s.CircuitLocked,s.LockReason,s.LastCycleUtc,
                paperRunning=paper?.Running??false,
                paperRiskLocked=paper?.RiskLocked??false,
                parameters=new {
                    riskPerTradePct=PaperRisk.MaxRiskPerTradePct,
                    dailyLossPct=PaperRisk.DailyLossLimitPct,
                    drawdownPct=PaperRisk.MaxDrawdownPct,
                    reinvestmentPct=PaperRisk.ReinvestmentPercent,
                    maxPositions=PaperRisk.MaxOpenPositions
                },
                summary=new {
                    total=await db.TestnetOrders.CountAsync(ct),
                    filled=await db.TestnetOrders.CountAsync(x=>x.Status=="FILLED",ct),
                    pending=await db.TestnetOrders.CountAsync(x=>x.Status=="SUBMITTING" ||
                        x.Status=="UNKNOWN" || x.Status=="NEW" || x.Status=="PARTIALLY_FILLED",ct),
                    blocked=await db.TestnetOrders.CountAsync(x=>x.Status=="FILTER_BLOCKED",ct),
                    cancelled=await db.TestnetOrders.CountAsync(x=>x.Status=="CANCELED",ct),
                    lastOrderUtc=await db.TestnetOrders.MaxAsync(x=>(DateTimeOffset?)x.UpdatedUtc,ct)
                },
                orders=orders.Select(x=>new {
                    x.PaperFillId,x.Symbol,x.Side,x.ClientOrderId,x.Status,
                    x.Quantity,x.ExecutedQuantity,x.CumulativeQuoteQuantity,
                    x.Note,x.CreatedUtc,x.UpdatedUtc
                })
            };
        }
        finally {_gate.Release();}
    }

    public async Task<object> SetEnabledAsync(bool enable,CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await InitializeAsync(ct);
            await using var db=await factory.CreateDbContextAsync(ct);
            var s=await db.TestnetExecutionStates.SingleAsync(x=>x.Id==1,ct);
            if(enable)
            {
                if(!TestnetExecutionPolicy.SubmissionExplicitlyAllowed)
                    throw new InvalidOperationException("MVP 1.2.1 somente simulado; envio Testnet exige autorização futura e configuração explícita.");
                if(!client.CredentialsConfigured)
                    throw new InvalidOperationException("Credenciais exclusivas da Testnet não configuradas.");
                if(s.EmergencyStopped||s.CircuitLocked)
                    throw new InvalidOperationException("Executor Testnet bloqueado. Revisão manual obrigatória.");
                var paper=await db.PaperAccounts.AsNoTracking().SingleAsync(x=>x.Id==1,ct);
                if(!paper.Running || paper.RiskLocked || PaperRisk.LockReason(
                    paper.LastEquityBrl,paper.DailyStartEquityBrl,paper.PeakEquityBrl)!=null)
                    throw new InvalidOperationException("Carteira PAPER não está saudável e ativa.");
                if(await db.TestnetOrders.AnyAsync(x=>x.Status=="SUBMITTING"||
                    x.Status=="UNKNOWN"||x.Status=="NEW"||x.Status=="PARTIALLY_FILLED",ct))
                    throw new InvalidOperationException("Há ordem de teste com resultado incerto.");
            }
            s.Enabled=enable;s.UpdatedUtc=DateTimeOffset.UtcNow;
            if(enable)s.EnabledUtc=s.UpdatedUtc;
            db.AuditEvents.Add(new AuditEvent {
                EventType=enable?"TESTNET_ARMED":"TESTNET_PAUSED",
                Description="Executor BINANCE_SPOT_TESTNET_ONLY; endpoint fixo testnet.binance.vision; ordens reais desativadas."
            });
            await db.SaveChangesAsync(ct);
            return new {mode="BINANCE_SPOT_TESTNET_ONLY",s.Enabled,s.CircuitLocked,
                s.EmergencyStopped,realOrdersEnabled=false};
        }
        finally {_gate.Release();}
    }

    public async Task<object> EmergencyStopAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await InitializeAsync(ct);
            await using var db=await factory.CreateDbContextAsync(ct);
            var s=await db.TestnetExecutionStates.SingleAsync(x=>x.Id==1,ct);
            s.Enabled=false;s.EmergencyStopped=true;s.CircuitLocked=true;
            s.LockReason="Parada de emergência solicitada. Revisão obrigatória.";
            s.UpdatedUtc=DateTimeOffset.UtcNow;
            db.AuditEvents.Add(new AuditEvent {
                EventType="TESTNET_EMERGENCY_STOP",
                Description="Novas ordens TESTNET desativadas imediatamente; conciliação/cancelamento pendente quando necessário."
            });
            await db.SaveChangesAsync(ct);
            // Attempt cancel only already-known open test orders. Never touch live Binance.
            var pending=await db.TestnetOrders
                .Where(x=>x.Status=="NEW"||x.Status=="PARTIALLY_FILLED")
                .ToArrayAsync(ct);
            var failed=0;
            foreach(var record in pending)
            {
                if(!TestnetExecutionPolicy.SubmissionExplicitlyAllowed)
                {
                    record.Note="Cancelamento Testnet desativado no MVP 1.2.1; revisão manual.";
                    failed++;
                    continue;
                }
                try
                {
                    var result=await client.CancelAsync(record.Symbol,record.ClientOrderId,ct);
                    TestnetExecutionPolicy.Reconcile(record,result,DateTimeOffset.UtcNow);
                }
                catch(Exception ex) when(ex is HttpRequestException or TaskCanceledException
                    or InvalidOperationException or System.Text.Json.JsonException)
                {
                    record.Note="Cancelamento Testnet sem confirmação; verifique manualmente.";
                    failed++;
                }
            }
            await db.SaveChangesAsync(ct);
            return new {mode="BINANCE_SPOT_TESTNET_ONLY",emergencyStopped=true,
                realOrdersEnabled=false,cancelUnconfirmed=failed};
        }
        finally {_gate.Release();}
    }

    public async Task CycleAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await InitializeAsync(ct);
            await using var db=await factory.CreateDbContextAsync(ct);
            var state=await db.TestnetExecutionStates.SingleAsync(x=>x.Id==1,ct);
            if(!state.Enabled || state.CircuitLocked || state.EmergencyStopped)return;
            if(!TestnetExecutionPolicy.SubmissionExplicitlyAllowed)
            {
                // Fail closed without touching the PAPER account.
                state.Enabled=false;
                state.UpdatedUtc=DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);
                return;
            }
            var paper=await db.PaperAccounts.AsNoTracking().SingleAsync(x=>x.Id==1,ct);
            if(!paper.Running||paper.RiskLocked||
                PaperRisk.LockReason(paper.LastEquityBrl,paper.DailyStartEquityBrl,paper.PeakEquityBrl)!=null)
            {
                await LockAsync(db,state,"Carlos: carteira PAPER parada ou limite de risco atingido.",ct);
                return;
            }
            if(paper.LastCycleUtc is null ||
                DateTimeOffset.UtcNow-paper.LastCycleUtc>TimeSpan.FromMinutes(4))
            {
                await LockAsync(db,state,"Carlos: ciclo PAPER desatualizado.",ct);
                return;
            }
            // Reconcile every known unsettled order BEFORE any new submission.
            var unsettled=await db.TestnetOrders
                .Where(x=>x.Status=="SUBMITTING"||x.Status=="NEW"||
                    x.Status=="PARTIALLY_FILLED"||x.Status=="UNKNOWN")
                .ToListAsync(ct);
            foreach(var record in unsettled)
            {
                var found=await client.QueryAsync(record.Symbol,record.ClientOrderId,ct);
                if(found is null)
                {
                    // A previous POST may have reached the exchange even if we
                    // never received ACK. Do not retry: durable circuit breaker.
                    await LockAsync(db,state,"Ordem Testnet incerta: "+record.ClientOrderId,ct);
                    return;
                }
                TestnetExecutionPolicy.Reconcile(record,found,DateTimeOffset.UtcNow);
            }
            await db.SaveChangesAsync(ct);
            if(await db.TestnetOrders.AnyAsync(x=>x.Status=="UNKNOWN"||x.Status=="SUBMITTING"
                 ||x.Status=="NEW"||x.Status=="PARTIALLY_FILLED",ct))
            {
                state.LastCycleUtc=DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);
                return; // never stack test orders while an old one is open.
            }

            // Skip historical fills before arming: no replay of stale trades.
            var fills=await db.PaperFills.AsNoTracking()
                .Where(x=>state.EnabledUtc!=null && x.ExecutedUtc>=state.EnabledUtc)
                .OrderBy(x=>x.Id).Take(30).ToListAsync(ct);
            var processed=await db.TestnetOrders.Select(x=>x.PaperFillId).ToArrayAsync(ct);
            foreach(var fill in fills)
            {
                if(processed.Contains(fill.Id))continue;
                if(!RacMarketSymbols.IsSupported(fill.Symbol))continue;
                var id=TestnetExecutionPolicy.ClientOrderId(fill.Id);
                var rec=new TestnetOrderRecord {
                    PaperFillId=fill.Id,Symbol=fill.Symbol,Side=fill.Side,
                    ClientOrderId=id,Quantity=fill.Quantity,
                    CreatedUtc=DateTimeOffset.UtcNow,UpdatedUtc=DateTimeOffset.UtcNow
                };
                db.TestnetOrders.Add(rec);
                // Validate filters from THE TESTNET, not mainnet. Testnet balance
                // can differ from BRL paper balance. No quantity upsizing.
                try
                {
                    var filters=await client.FiltersAsync(fill.Symbol,ct);
                    var ticker=await client.TickerAsync(fill.Symbol,ct);
                    var lastCandle=await db.MarketCandles.AsNoTracking()
                        .Where(x=>x.Symbol==fill.Symbol && x.Interval=="1m")
                        .OrderByDescending(x=>x.CloseUtc)
                        .Select(x=>new {x.Close,x.CloseUtc})
                        .FirstOrDefaultAsync(ct);
                    if(lastCandle is null || DateTimeOffset.UtcNow-lastCandle.CloseUtc>TimeSpan.FromMinutes(4) ||
                        lastCandle.Close<=0 || ticker/lastCandle.Close is < 0.97m or > 1.03m)
                        throw new InvalidOperationException("Cotação Testnet divergente dos candles públicos.");
                    rec.Quantity=TestnetOrderValidation.ValidateQuantity(fill.Quantity,ticker,filters);
                    if(fill.Side=="SELL")
                    {
                        var confirmed=await db.TestnetOrders.AsNoTracking()
                            .Where(x=>x.Symbol==fill.Symbol && x.Status=="FILLED")
                            .ToArrayAsync(ct);
                        if(!TestnetExecutionPolicy.HasConfirmedSellQuantity(
                            confirmed,fill.Symbol,rec.Quantity))
                            throw new InvalidOperationException("Venda sem posição TESTNET confirmada; execução bloqueada.");
                    }
                    // Testnet funds are not the user's real Binance holdings.
                    // Refuse to send if a simulated account has insufficient free balance.
                    var asset=fill.Side=="BUY" ? "USDT" : fill.Symbol[..^4];
                    var required=fill.Side=="BUY" ? rec.Quantity*ticker*1.01m : rec.Quantity;
                    var available=await client.AvailableAsync(asset,ct);
                    if(available<required)
                        throw new InvalidOperationException("Saldo fictício Testnet insuficiente para a ordem.");
                }
                catch(InvalidOperationException ex)
                {
                    rec.Status="FILTER_BLOCKED";
                    rec.Note=ex.Message.Length>500?ex.Message[..500]:ex.Message;
                    await db.SaveChangesAsync(ct);
                    continue;
                }
                // Save intent first. If POST/ACK outcome is uncertain, no retry.
                rec.Status="SUBMITTING";
                await db.SaveChangesAsync(ct);
                try
                {
                    // Explicit POST only against hard-pinned testnet origin.
                    var result=await client.MarketAsync(fill.Symbol,fill.Side,rec.Quantity,id,ct);
                    TestnetExecutionPolicy.Reconcile(rec,result,DateTimeOffset.UtcNow);
                    await db.SaveChangesAsync(ct);
                }
                catch(Exception ex) when(ex is HttpRequestException or TaskCanceledException
                    or System.Text.Json.JsonException)
                {
                    rec.Status="UNKNOWN";
                    rec.Note="Resultado remoto incerto; conciliar antes de qualquer envio.";
                    await LockAsync(db,state,"Falha ambígua no envio à Testnet; execução bloqueada.",ct);
                    logger.LogError(ex,"Testnet ACK incerto; circuito bloqueado");
                    return;
                }
                catch(Exception ex) when(ex is InvalidOperationException)
                {
                    rec.Status="UNKNOWN";
                    rec.Note="Ordem recusada/ambígua. Verifique na Testnet.";
                    await LockAsync(db,state,"Erro no envio à Testnet; revisão necessária.",ct);
                    return;
                }
                break; // at most one new testnet order per cycle.
            }
            state.LastCycleUtc=DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        finally {_gate.Release();}
    }

    public async Task CircuitBreakAsync(string reason,CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await InitializeAsync(ct);
            await using var db=await factory.CreateDbContextAsync(ct);
            var s=await db.TestnetExecutionStates.SingleAsync(x=>x.Id==1,ct);
            await LockAsync(db,s,reason,ct);
        }
        finally {_gate.Release();}
    }
    private static async Task LockAsync(RacDbContext db,TestnetExecutionState state,
        string reason,CancellationToken ct)
    {
        state.Enabled=false;
        state.CircuitLocked=true;
        state.LockReason=reason.Length>500?reason[..500]:reason;
        state.UpdatedUtc=DateTimeOffset.UtcNow;
        db.AuditEvents.Add(new AuditEvent {EventType="TESTNET_CIRCUIT_LOCK",
            Description="Testnet-only: "+state.LockReason});
        await db.SaveChangesAsync(ct);
    }
    private static void UpdateRecord(TestnetOrderRecord rec,TestnetExchangeOrder order)
    {
        if(order.ClientOrderId!=rec.ClientOrderId||order.Symbol!=rec.Symbol)
            throw new InvalidOperationException("Resposta Testnet não corresponde à intenção persistida.");
        rec.ExchangeOrderId=order.OrderId;
        rec.Status=order.Status;
        rec.ExecutedQuantity=order.ExecutedQuantity;
        rec.CumulativeQuoteQuantity=order.QuoteQuantity;
        rec.UpdatedUtc=DateTimeOffset.UtcNow;
        rec.Note="";
    }
}

public sealed class RacTestnetWorker(
    RacTestnetExecutor executor,ILogger<RacTestnetWorker> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var clock=new PeriodicTimer(TimeSpan.FromSeconds(60));
        try
        {
            // Initialization always pauses previous active state.
            await executor.StatusAsync(ct);
            while(await clock.WaitForNextTickAsync(ct))
            {
                try {await executor.CycleAsync(ct);}
                catch(OperationCanceledException) when(ct.IsCancellationRequested){break;}
                catch(Exception ex)
                {
                    logger.LogCritical(ex,"Testnet worker fault: circuit breaker engaged.");
                    try{await executor.CircuitBreakAsync(
                        "Falha inesperada no executor Testnet.",ct);}
                    catch(Exception failure){logger.LogCritical(failure,
                        "Falha ao persistir bloqueio de segurança Testnet.");}
                    break;
                }
            }
        }
        catch(OperationCanceledException) when(ct.IsCancellationRequested){}
    }
}
