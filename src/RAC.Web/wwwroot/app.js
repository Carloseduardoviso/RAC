const $ = id => document.getElementById(id);
const usd = n => new Intl.NumberFormat('pt-BR', {style:'currency',currency:'BRL'}).format(n);
const pct = n => `${Number(n).toFixed(2)}%`;
const decimal = n => Number(n).toLocaleString('pt-BR',{maximumFractionDigits:5});
let first = true;
let authenticated = false;
let canControl = false;
let canReview = false;

let canViewWallet = false;
const walletNumber = n => Number(n).toLocaleString('pt-BR', {maximumFractionDigits:12});

function clearWalletDisplay() {
  $('walletAvailable').textContent = '—';
  $('walletLocked').textContent = '—';
  $('walletAssets').innerHTML = '<tr><td colspan="4">Saldo ainda não consultado.</td></tr>';
}

async function checkWalletStatus() {
  if (!authenticated || !canViewWallet) return;
  try {
    const status = await api('binance/spot/status');
    $('walletStatus').textContent = status.configured ? 'Pronto para consultar' : 'Chave não configurada';
    $('walletInfo').textContent = status.configured
      ? 'Binance Spot real • somente leitura • clique para consultar o saldo atual.'
      : 'Configure as credenciais HMAC somente leitura com tools/RAC.BinanceSetup no PowerShell.';
    $('walletRefresh').disabled = !status.configured;
  } catch {
    $('walletStatus').textContent = 'Serviço indisponível';
    $('walletRefresh').disabled = true;
  }
}

$('walletRefresh').onclick = async () => {
  if (!authenticated || !canViewWallet) return;
  $('walletRefresh').disabled = true;
  $('walletStatus').textContent = 'Consultando Binance...';
  clearWalletDisplay();
  try {
    const data = await api('binance/spot/balances');
    if (data.source !== 'BINANCE_SPOT_REAL' || data.realOrdersEnabled) {
      throw new Error('Resposta de carteira incompatível com modo somente leitura.');
    }
    $('walletAvailable').textContent = walletNumber(data.availableUsdt) + ' USDT';
    $('walletLocked').textContent = walletNumber(data.lockedUsdt) + ' USDT';
    $('walletAssets').innerHTML = data.assets.length
      ? data.assets.map(a => '<tr><td>'+safe(a.asset)+'</td><td>'+walletNumber(a.free)+
         '</td><td>'+walletNumber(a.locked)+'</td><td>'+walletNumber(Number(a.free)+Number(a.locked))+'</td></tr>').join('')
      : '<tr><td colspan="4">Sem ativos com saldo positivo na carteira Spot.</td></tr>';
    $('walletStatus').textContent = 'Saldo real consultado';
    $('walletInfo').textContent = 'Binance Spot real • atualização: ' +
      formatDate(data.observedUtc) + ' • nenhuma negociação realizada pelo RAC.';
  } catch (error) {
    $('walletStatus').textContent = 'Falha na consulta';
    $('walletInfo').textContent = error.message;
  } finally {
    $('walletRefresh').disabled = !authenticated || !canViewWallet;
  }
};

const safe = value => String(value).replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
async function api(path, options={}) {
  const headers = {...(options.headers || {})};
  if (options.method === 'POST') headers['X-RAC-CSRF'] = '1';
  const response = await fetch(`/api/${path}`, {...options, credentials:'same-origin', headers});
  if (!response.ok) {
    const details = await response.json().catch(() => ({}));
    throw new Error(details.error || `HTTP ${response.status}`);
  }
  return response.json();
}
function draw(history) {
  const values = history.length ? history : [100];
  const lo = Math.min(...values), hi=Math.max(...values), pad=Math.max((hi-lo)*.2,1);
  const min=lo-pad,max=hi+pad;
  const pts=values.map((v,i)=>`${(i/Math.max(1,values.length-1)*700).toFixed(1)},${(190-(v-min)/(max-min)*180).toFixed(1)}`);
  $('line').setAttribute('points',pts.join(' '));
  $('area').setAttribute('d',`M ${pts[0]} L ${pts.slice(1).join(' L ')} L 700,200 L 0,200 Z`);
}
function render(state){
  $('error').hidden=true;
  $('equity').textContent=usd(state.equity);
  $('balance').textContent=`Saldo realizado: ${usd(state.balance)}`;
  $('pnl').textContent=usd(state.totalPnl);
  $('pnl').className=state.totalPnl >= 0 ? 'positive' : 'negative';
  $('daily').textContent=`Resultado do dia: ${usd(state.dailyPnl)}`;
  $('open').textContent=state.positions.length;
  $('limit').textContent=`Limite: ${state.settings.maxOpenPositions}`;
  $('risk').textContent=state.riskLocked ? 'BLOQUEADO' : (state.running ? 'OPERANDO' : 'PAUSADO');
  $('risk').className=state.riskLocked ? 'negative' : 'positive';
  $('drawdown').textContent=`Queda acumulada: ${pct(state.peakDrawdownPct)} / ${pct(state.settings.maxCumulativeDrawdownPct)} · Dia: ${pct(state.dayDrawdownPct)}`;
  $('contributions').textContent=`Aportes virtuais acumulados: ${usd(state.totalContributionsBrl)} (${state.contributedMonths} meses)`;
  $('riskMessage').textContent=state.riskMessage;
  $('start').disabled=!canControl || state.riskLocked;
  $('pause').disabled=!canControl;
  $('reset').disabled=!canControl || state.riskLocked;
  $('step').disabled=!canControl || state.riskLocked;
  $('riskReviewPanel').hidden=!(canReview && state.riskLocked);
  $('tick').textContent=`Tick ${state.tick}`;
  $('supervisors').innerHTML=state.supervisors.map(a=>`<article class="agent"><div class="agent-top"><b>${safe(a.name)}</b><span>${safe(a.status)}</span></div><p>${safe(a.function)}</p><p>${safe(a.detail)}</p></article>`).join('');
  $('agents').innerHTML=state.agents.map(a=>`<article class="agent ${a.status==='EM OPERAÇÃO'?'active':''}"><div class="agent-top"><b>${safe(a.symbol)}</b><span>● ${safe(a.status)}</span></div><div class="price">${decimal(a.price)}</div><div class="foot"><span class="${a.changePct>=0?'positive':'negative'}">${a.changePct>0?'+':''}${pct(a.changePct)}</span><span class="signal ${a.signal.toLowerCase()}">${safe(a.signal)}</span></div><div class="foot" style="margin-top:10px"><span>${a.wins}W / ${a.losses}L</span><span>${usd(a.realizedPnl)}</span></div></article>`).join('');
  $('positions').innerHTML=state.positions.length ? state.positions.map(p=>`<tr><td>${safe(p.symbol)}</td><td>${safe(p.side)}</td><td>${decimal(p.entry)}</td><td>${decimal(p.currentPrice)}</td><td class="${p.unrealizedPnl>=0?'positive':'negative'}">${usd(p.unrealizedPnl)}</td></tr>`).join('') : '<tr><td colspan="5">Nenhuma posição aberta</td></tr>';
  $('trades').innerHTML=state.trades.length ? state.trades.slice(0,15).map(t=>`<tr><td>${safe(t.symbol)}</td><td class="${t.pnl>=0?'positive':'negative'}">${usd(t.pnl)}</td><td>${safe(t.exitReason)}</td><td>${t.closedTick}</td></tr>`).join('') : '<tr><td colspan="4">Sem operações concluídas</td></tr>';
  draw(state.equityHistory);
  if(first){ $('startingBalance').value=state.settings.referenceCapitalBrl; $('riskPct').value=state.settings.maxRiskPerTradePct; $('dailyLimit').value=state.settings.maxDailyLossPct; $('maxOpen').value=state.settings.maxOpenPositions; $('cumulativeLimit').value=state.settings.maxCumulativeDrawdownPct; $('monthlyContribution').value=state.settings.monthlyContributionBrl; first=false; }
}
async function refresh(){try{render(await api('state'));}catch(e){$('error').textContent=`Falha ao carregar o simulador: ${e.message}`;$('error').hidden=false;}}
async function command(path,body){try{render(await api(path,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(body ?? {})}));}catch(e){$('error').textContent=e.message;$('error').hidden=false;}}
$('start').onclick=()=>command('control/start');
$('pause').onclick=()=>command('control/pause');
$('step').onclick=()=>command('control/step');
$('reset').onclick=()=>command('control/reset');
$('settings').onsubmit=e=>e.preventDefault();
$('ping').onclick=async()=>{try{const data=await api('binance/testnet/ping');$('binanceStatus').textContent=data.connected?'Binance Spot Testnet: API pública acessível (sem envio de ordens)':'Binance Spot Testnet: indisponível';}catch(e){$('binanceStatus').textContent='Binance Spot Testnet: indisponível ('+e.message+')';}};
setInterval(() => { if(authenticated) refresh(); },2000);

const formatDate = value => new Date(value).toLocaleString('pt-BR', {dateStyle:'short',timeStyle:'short'});
async function refreshHistory() {
  try {
    const history = await api('history');
    $('historyStatus').textContent = 'SQL Server conectado';
    $('persistedOrders').innerHTML = history.orders.length
      ? history.orders.slice(0,10).map(o => '<tr><td>'+safe(formatDate(o.createdUtc))+'</td><td>'+safe(o.symbol)+'</td><td>'+safe(o.side)+'</td><td>'+decimal(o.quantity)+'</td><td>'+decimal(o.price)+'</td></tr>').join('')
      : '<tr><td colspan="5">Nenhuma ordem simulada registrada</td></tr>';
    $('persistedEquity').innerHTML = history.equity.length
      ? history.equity.slice(0,10).map(e => '<tr><td>'+safe(formatDate(e.recordedUtc))+'</td><td>'+usd(e.equityBrl)+'</td><td>'+usd(e.totalContributionsBrl)+'</td><td>'+pct(e.drawdownPercent)+'</td></tr>').join('')
      : '<tr><td colspan="4">Nenhum registro patrimonial</td></tr>';
    const active = history.riskLocks.filter(r => r.active);
    $('persistedRisk').textContent = 'Aportes registrados: '+history.contributions.length+' | Bloqueios ativos: '+active.length;
  } catch (error) {
    $('historyStatus').textContent = 'Histórico indisponível';
    $('persistedRisk').textContent = 'Não foi possível consultar o SQL Server: '+error.message;
  }
}
setInterval(() => { if(authenticated) refreshHistory(); },5000);

function showSession(session) {
  authenticated = !!session;
  canControl = !!session?.canControl;
  canReview = !!session?.canReview;
  canViewWallet = !!session?.canViewWallet;
  $('walletPanel').hidden = !authenticated || !canViewWallet;
  $('portfolioPanel').hidden = !authenticated || !canViewWallet;
  $('activityPanel').hidden = !authenticated || !canViewWallet;
  $('realStrategiesPanel').hidden = !authenticated;
  $('paperPanel').hidden = !authenticated;
  $('paperMonitoringPanel').hidden = !authenticated;
  $('testnetExecutionPanel').hidden = !authenticated || !canControl;
  if (!authenticated) { clearRealStrategiesDisplay(); clearPaperDisplay(); clearMonitoringDisplay(); }
  if (!authenticated || !canViewWallet) {
    clearWalletDisplay();
    clearPortfolioDisplay();
    clearActivityDisplay();
    $('walletStatus').textContent = 'Não consultado';
    $('walletInfo').textContent = 'Configure uma chave exclusivamente de leitura no Windows.';
  }
  document.body.classList.toggle('authenticated', authenticated);
  $('authPanel').hidden = authenticated;
  $('sessionPanel').hidden = !authenticated;
  $('sessionUser').textContent = authenticated ? session.userName + ' · ' + session.role : '';
  $('riskReviewPanel').hidden = true;
}

async function loadSession() {
  try {
    const session = await api('auth/me');
    showSession(session);
    await refresh();
    await refreshHistory();
    await refreshMarketData();
    await refreshMarketCandles();
    await refreshRealStrategies();
    await refreshPaper();
    await refreshMonitoring();
    if (canControl) await refreshTestnetExecution();
    await checkWalletStatus();
    if (canViewWallet) { await refreshPortfolioHistory(); await refreshActivityHistory(); }
  } catch {
    showSession(null);
  }
}

$('loginForm').onsubmit = async event => {
  event.preventDefault();
  const userName = $('loginUser').value.trim();
  const password = $('loginPassword').value;
  $('loginPassword').value = '';
  $('loginMessage').textContent = 'Verificando credenciais...';
  try {
    await api('auth/login', {
      method: 'POST', headers: {'Content-Type':'application/json'},
      body: JSON.stringify({userName,password})
    });
    $('loginMessage').textContent = '';
    await loadSession();
  } catch (error) {
    $('loginMessage').textContent = 'Não foi possível entrar: ' + error.message;
  }
};
$('logout').onclick = async () => {
  try { await api('auth/logout', {method:'POST', headers:{'Content-Type':'application/json'},body:'{}'}); }
  catch { /* Clear local UI even when the backend is inaccessible. */ }
  showSession(null);
};
$('riskReviewForm').onsubmit = async event => {
  event.preventDefault();
  const justification = $('reviewReason').value.trim();
  if (!canReview || justification.length < 20) return;
  $('reviewMessage').textContent = 'Registrando revisão...';
  try {
    const state = await api('risk/review', {
      method:'POST', headers:{'Content-Type':'application/json'},
      body:JSON.stringify({justification})
    });
    $('reviewReason').value = '';
    $('reviewMessage').textContent = 'Revisão registrada. Novo cenário criado e pausado.';
    render(state);
    await refreshHistory();
  } catch (error) {
    $('reviewMessage').textContent = 'Revisão recusada: ' + error.message;
  }
};
loadSession();

async function refreshMarketData() {
  if (!authenticated) return;
  try {
    const market = await api('market/overview');
    const feed = market.feed;
    const label = {
      LIVE: 'Cotações atuais', DEGRADED: 'Atualização parcial',
      STALE: 'Cotações desatualizadas', WAITING: 'Aguardando coleta'
    };
    $('marketFeedStatus').textContent = label[feed.status] || 'Indisponível';
    const last = feed.lastSuccessUtc ? formatDate(feed.lastSuccessUtc) : 'nenhuma consulta concluída';
    $('marketUpdated').textContent = 'Binance Spot pública • USDT • Última coleta: ' + last +
      (feed.error ? ' • ' + feed.error : '') +
      ' • Sem vínculo com as operações sintéticas';
    $('marketQuotes').innerHTML = market.quotes.length
      ? market.quotes.map(q => '<article class="agent"><div class="agent-top"><b>'+
          safe(q.symbol)+'</b><span>'+(q.stale?'DESATUALIZADO':'SPOT PÚBLICO')+
          '</span></div><div class="price">'+decimal(q.priceUsdt)+
          '</div><div class="foot"><span>USDT</span><span>Binance</span></div></article>').join('')
      : '<p>Nenhuma cotação persistida. A coleta automática pode estar iniciando ou indisponível.</p>';
  } catch (error) {
    $('marketFeedStatus').textContent = 'Consulta indisponível';
    $('marketUpdated').textContent = 'Não foi possível consultar as cotações: ' + error.message;
  }
}
async function refreshMarketCandles() {
  if (!authenticated) return;
  try {
    const candles = await api('market/candles/'+encodeURIComponent($('marketPair').value));
    $('marketCandles').innerHTML = candles.candles.length
      ? candles.candles.slice(-12).reverse().map(c =>
        '<tr><td>'+safe(formatDate(c.openUtc))+'</td><td>'+decimal(c.open)+'</td><td>'+
        decimal(c.high)+'</td><td>'+decimal(c.low)+'</td><td>'+decimal(c.close)+
        '</td><td>'+decimal(c.volume)+'</td></tr>').join('')
      : '<tr><td colspan="6">Aguardando candles fechados da Binance</td></tr>';
  } catch (error) {
    $('marketCandles').innerHTML = '<tr><td colspan="6">Histórico temporariamente indisponível</td></tr>';
  }
}
$('marketPair').onchange = refreshMarketCandles;
setInterval(() => { if(authenticated) {refreshMarketData();refreshMarketCandles();} }, 20000);

const brlEstimate = value => Number(value).toLocaleString('pt-BR', {
  style:'currency', currency:'BRL', minimumFractionDigits:2, maximumFractionDigits:8
});

function clearPortfolioDisplay() {
  for (const id of ['portfolioTotal','portfolioPriced','portfolioRate','portfolioUnpriced'])
    $(id).textContent = '—';
  $('portfolioInfo').textContent = 'Nenhum dado privado carregado.';
  $('portfolioStatus').textContent = 'Sem consulta';
  $('portfolioHoldings').innerHTML = '<tr><td colspan="5">Nenhuma consulta registrada.</td></tr>';
  $('portfolioSnapshots').innerHTML = '<tr><td colspan="4">Sem registros.</td></tr>';
  $('portfolioCashflows').innerHTML = '<tr><td colspan="5">Nenhuma anotação.</td></tr>';
  $('plannedMonthly').value = '200';
  $('plannedReinvest').value = '100';
  $('cashflowAmount').value = '';
  $('cashflowDescription').value = '';
  $('portfolioPlanMessage').textContent = '';
  $('portfolioCashflowMessage').textContent = '';
}

function renderPortfolioHistory(data) {
  if (data.source !== 'BINANCE_SPOT_REAL' || data.realOrdersEnabled)
    throw new Error('Origem de patrimônio incompatível com modo de leitura.');
  const latest = data.latest;
  $('portfolioStatus').textContent = latest ? 'Histórico SQL Server' : 'Aguardando 1ª consulta';
  $('portfolioTotal').textContent = latest
    ? (latest.completeValuation && latest.totalEstimatedBrl !== null
      ? brlEstimate(latest.totalEstimatedBrl) : 'Parcial — sem total')
    : '—';
  $('portfolioPriced').textContent = latest ? brlEstimate(latest.valuedSubsetBrl) : '—';
  $('portfolioRate').textContent = latest?.usdtBrlRate
    ? brlEstimate(latest.usdtBrlRate) : 'Sem cotação';
  $('portfolioUnpriced').textContent = latest ? String(latest.unpricedAssetCount) : '—';
  $('portfolioTotalNote').textContent = !latest ? 'Sem histórico'
    : latest.completeValuation ? 'Estimativa Spot sem taxas e spreads'
    : 'Total indisponível: ativos sem cotação';
  $('portfolioInfo').textContent = latest
    ? 'Última leitura privada: '+formatDate(latest.walletObservedUtc)+
      ' • preços públicos: '+formatDate(latest.quotesObservedUtc)+
      ' • variação entre datas não representa lucro.'
    : 'Clique em Consultar Binance e registrar patrimônio para criar o primeiro instantâneo.';
  $('portfolioHoldings').innerHTML = data.holdings?.length
    ? data.holdings.map(h =>
        '<tr><td>'+safe(h.asset)+'</td><td>'+walletNumber(h.free)+
        '</td><td>'+walletNumber(h.locked)+'</td><td>'+
        (h.estimatedValueBrl === null ? 'Sem cotação' : brlEstimate(h.estimatedValueBrl))+
        '</td><td>'+safe(h.priceSource)+'</td></tr>').join('')
    : '<tr><td colspan="5">Nenhum ativo registrado no instantâneo.</td></tr>';
  $('portfolioSnapshots').innerHTML = data.snapshots?.length
    ? data.snapshots.slice(0,12).map(s =>
        '<tr><td>'+safe(formatDate(s.walletObservedUtc))+'</td><td>'+
        (s.totalEstimatedBrl === null ? 'Incompleto' : brlEstimate(s.totalEstimatedBrl))+
        '</td><td>'+brlEstimate(s.valuedSubsetBrl)+
        '</td><td>'+s.pricedAssetCount+' avaliado(s) / '+s.unpricedAssetCount+
        ' sem cotação</td></tr>').join('')
    : '<tr><td colspan="4">Nenhum registro ainda.</td></tr>';
  $('portfolioCashflows').innerHTML = data.cashFlows?.length
    ? data.cashFlows.slice(0,15).map(c =>
        '<tr><td>'+safe(c.occurredOn.split('-').reverse().join('/'))+
        '</td><td>'+(c.kind === 'CONTRIBUTION' ? 'Aporte' : 'Retirada')+
        '</td><td>'+brlEstimate(c.amountBrl)+
        '</td><td>'+safe(c.description)+
        '</td><td>Não verificado (manual)</td></tr>').join('')
    : '<tr><td colspan="5">Nenhuma anotação manual.</td></tr>';
  $('plannedMonthly').value = data.plan.monthlyContributionBrl;
  $('plannedReinvest').value = data.plan.reinvestRealizedProfitPercent;
}

async function refreshPortfolioHistory() {
  if (!authenticated || !canViewWallet) return;
  try {
    renderPortfolioHistory(await api('portfolio/history'));
  } catch (error) {
    $('portfolioStatus').textContent = 'Histórico indisponível';
    $('portfolioInfo').textContent = 'Falha ao consultar patrimônio no SQL Server: '+error.message;
  }
}

$('portfolioCapture').onclick = async () => {
  if (!authenticated || !canViewWallet) return;
  $('portfolioCapture').disabled = true;
  $('portfolioStatus').textContent = 'Consultando Binance...';
  try {
    await api('portfolio/snapshot', {
      method:'POST', headers:{'Content-Type':'application/json'}, body:'{}'
    });
    await refreshPortfolioHistory();
  } catch (error) {
    $('portfolioStatus').textContent = 'Falha ao registrar';
    $('portfolioInfo').textContent = error.message;
  } finally { $('portfolioCapture').disabled = false; }
};

$('portfolioPlanForm').onsubmit = async event => {
  event.preventDefault();
  if (!authenticated || !canViewWallet) return;
  const monthlyContributionBrl = Number($('plannedMonthly').value);
  const reinvestRealizedProfitPercent = Number($('plannedReinvest').value);
  $('portfolioPlanMessage').textContent = 'Salvando plano...';
  try {
    await api('portfolio/plan',{
      method:'POST', headers:{'Content-Type':'application/json'},
      body:JSON.stringify({monthlyContributionBrl,reinvestRealizedProfitPercent})
    });
    $('portfolioPlanMessage').textContent = 'Plano salvo no SQL Server. Nenhuma operação real realizada.';
    await refreshPortfolioHistory();
  } catch(error) {
    $('portfolioPlanMessage').textContent = error.message;
  }
};

const nowLocal = new Date();
$('cashflowDate').value = [
  nowLocal.getFullYear(),
  String(nowLocal.getMonth()+1).padStart(2,'0'),
  String(nowLocal.getDate()).padStart(2,'0')
].join('-');

$('portfolioCashFlowForm').onsubmit = async event => {
  event.preventDefault();
  if (!authenticated || !canViewWallet) return;
  $('portfolioCashflowMessage').textContent = 'Registrando anotação...';
  try {
    await api('portfolio/cashflows',{
      method:'POST', headers:{'Content-Type':'application/json'},
      body:JSON.stringify({
        kind:$('cashflowKind').value,
        amountBrl:Number($('cashflowAmount').value),
        occurredOn:$('cashflowDate').value,
        description:$('cashflowDescription').value
      })
    });
    $('portfolioCashflowMessage').textContent = 'Anotação salva. Não é uma transferência e não comprova aporte na Binance.';
    $('cashflowAmount').value = '';
    $('cashflowDescription').value = '';
    await refreshPortfolioHistory();
  } catch(error) {
    $('portfolioCashflowMessage').textContent = error.message;
  }
};



const brl = usd;
async function refreshTestnetExecution() {
  if (!authenticated || !canControl) return;
  try {
    const s = await api('testnet/execution/status');
    if (s.mode !== 'BINANCE_SPOT_TESTNET_ONLY' || s.realOrdersEnabled ||
        s.endpoint !== 'https://testnet.binance.vision')
      throw new Error('Endpoint da Testnet inesperado.');
    $('testnetExecutionStatus').textContent = s.emergencyStopped ? 'EMERGÊNCIA' :
      s.circuitLocked ? 'BLOQUEADO' : s.enabled ? 'TESTNET ATIVO' : 'TESTNET PAUSADO';
    $('testnetExecutionHomologation').textContent = s.submissionAuthorized ?
      'Envio Testnet explicitamente autorizado por configuração. Verifique a revisão operacional antes de operar.' :
      'MVP 1.2.1: somente testes simulados. Envio Testnet bloqueado até autorização separada.';
    const counts = s.summary || {};
    $('testnetCountAll').textContent = counts.total ?? 0;
    $('testnetCountFilled').textContent = counts.filled ?? 0;
    $('testnetCountPending').textContent = counts.pending ?? 0;
    $('testnetCountBlocked').textContent = counts.blocked ?? 0;
    $('testnetCountCancelled').textContent = counts.cancelled ?? 0;
    $('testnetExecutionMessage').textContent =
      'Credenciais TESTNET: '+(s.credentialsConfigured ? 'configuradas' : 'ausentes')+
      ' • PAPER: '+(s.paperRunning ? 'ativo' : 'pausado')+
      ' • última verificação: '+(s.lastCycleUtc ? formatDate(s.lastCycleUtc) : 'nenhuma')+
      (s.lockReason ? ' • '+s.lockReason : '')+
      '. Nenhuma ordem real habilitada.';
    $('testnetExecutionStart').disabled = !s.submissionAuthorized || !s.credentialsConfigured ||
      !s.paperRunning || s.paperRiskLocked || s.enabled ||
      s.circuitLocked || s.emergencyStopped;
    $('testnetExecutionPause').disabled = !s.enabled;
    $('testnetExecutionStop').disabled = s.emergencyStopped;
    $('testnetExecutionOrders').innerHTML = s.orders.length ? s.orders.map(x =>
      '<tr><td>'+safe(formatDate(x.createdUtc))+'</td><td>'+safe(x.symbol)+
      '</td><td>'+safe(x.side)+'</td><td>'+walletNumber(x.quantity)+
      '</td><td>'+safe(x.status)+'</td><td>'+safe(x.clientOrderId)+
      '</td><td>'+safe(x.note || '—')+'</td></tr>').join('')
      : '<tr><td colspan="7">Nenhuma ordem de teste.</td></tr>';
  } catch(error) {
    $('testnetExecutionStatus').textContent = 'INDISPONÍVEL';
    $('testnetExecutionMessage').textContent = error.message;
  }
}
async function testnetCommand(action) {
  if(action === 'emergency' &&
      !confirm('Parar permanentemente o executor de ordens TESTNET? Exige revisão manual.')) return;
  try {
    await api('testnet/execution/'+action, {method:'POST',
      headers:{'Content-Type':'application/json'},body:'{}'});
  } catch(error) {
    $('testnetExecutionMessage').textContent = 'Erro no controle Testnet: '+error.message;
  }
  await refreshTestnetExecution();
}
$('testnetExecutionStart').onclick = () => testnetCommand('start');
$('testnetExecutionPause').onclick = () => testnetCommand('pause');
$('testnetExecutionStop').onclick = () => testnetCommand('emergency');
$('testnetExecutionRefresh').onclick = refreshTestnetExecution;
setInterval(() => {if(authenticated && canControl) refreshTestnetExecution();},30000);

function clearMonitoringDisplay() {
  $('paperMonitorStatus').textContent = 'Sem dados';
  $('paperMonitorPnl').textContent = '—';
  $('paperMonitorRealized').textContent = '—';
  $('paperMonitorFees').textContent = '—';
  $('paperMonitorDrawdown').textContent = '—';
  $('paperMonitorHealth').textContent = 'Aguardando dados de monitoramento.';
  $('paperMonitorAlerts').innerHTML = '<tr><td colspan="5">Nenhum alerta.</td></tr>';
  $('paperMonitorDecisions').innerHTML = '<tr><td colspan="5">Aguardando decisões.</td></tr>';
  $('paperMonitorHistory').innerHTML = '<tr><td colspan="5">Aguardando amostras.</td></tr>';
}
async function refreshMonitoring() {
  if (!authenticated) return;
  try {
    const m = await api('paper/monitoring');
    if (m.mode !== 'PAPER_ONLY' || m.realOrdersEnabled || m.testnetOrdersEnabled)
      throw new Error('Relatório fora do modo PAPER ONLY.');
    const p = m.performance;
    const active = m.alerts.filter(a => a.active);
    const ageMs = p.latestMonitorUtc ? Date.now() - Date.parse(p.latestMonitorUtc) : Infinity;
    const fresh = Number.isFinite(ageMs) && ageMs >= 0 && ageMs < 180000;
    $('paperMonitorStatus').textContent = !fresh ? 'MONITOR SEM ATUALIZAÇÃO' :
      active.length ? active.length + ' alerta(s) ativo(s)' :
      m.running ? 'MONITORANDO PAPEL' : 'PAUSADO';
    $('paperMonitorPnl').textContent = brl(p.pnlExcludingContributionsBrl);
    $('paperMonitorRealized').textContent = brl(p.realizedPnlBrl);
    $('paperMonitorFees').textContent = brl(p.feesBrl);
    $('paperMonitorDrawdown').textContent = Number(p.drawdownPct).toFixed(3) + '%';
    $('paperMonitorHealth').textContent =
      'Agentes prontos: '+m.agentStates.filter(a=>a.status==='READY').length+
      '/12 • último ciclo: '+(p.paperCycleUtc ? formatDate(p.paperCycleUtc) : 'nenhum')+
      ' • monitor: '+(p.latestMonitorUtc ? formatDate(p.latestMonitorUtc) : 'ainda não coletado')+
      ' • posições abertas: '+p.openPositions+' • fills virtuais: '+p.virtualFills+
      ' • operações fechadas: '+p.closedPositions+'.';
    $('paperMonitorAlerts').innerHTML = m.alerts.length ? m.alerts.map(a =>
      '<tr><td>'+safe(formatDate(a.openedUtc))+'</td><td>'+safe(a.code)+
      '</td><td>'+safe(a.severity)+'</td><td>'+(a.active ? 'ATIVO' : 'RESOLVIDO')+
      '</td><td>'+safe(a.message)+'</td></tr>').join('')
      : '<tr><td colspan="5">Nenhum alerta registrado.</td></tr>';
    $('paperMonitorDecisions').innerHTML = m.decisions.length ? m.decisions.map(x =>
      '<tr><td>'+safe(formatDate(x.recordedUtc))+'</td><td>'+safe(x.symbol)+
      '</td><td>'+safe(x.signal)+'</td><td>'+safe(x.outcome)+
      '</td><td>'+safe(x.actor)+'</td></tr>').join('')
      : '<tr><td colspan="5">Nenhuma decisão de candle registrada.</td></tr>';
    $('paperMonitorHistory').innerHTML = m.history.length ? m.history.slice(-30).reverse().map(x =>
      '<tr><td>'+safe(formatDate(x.recordedUtc))+'</td><td>'+brl(x.equityBrl)+
      '</td><td>'+brl(x.exposureBrl)+'</td><td>'+x.readyAgents+
      '/12</td><td>'+(x.riskLocked ? 'BLOQUEADO' : x.running ? 'EXECUTANDO' : 'PAUSADO')+
      '</td></tr>').join('')
      : '<tr><td colspan="5">Aguardando amostras.</td></tr>';
  } catch(error) {
    $('paperMonitorStatus').textContent = 'MONITOR INDISPONÍVEL';
    $('paperMonitorHealth').textContent = error.message;
  }
}
$('paperMonitorRefresh').onclick = refreshMonitoring;
setInterval(() => { if(authenticated) refreshMonitoring(); }, 30000);

function clearPaperDisplay() {
  $('paperStatus').textContent = 'Pausado';
  $('paperEquity').textContent = '—';
  $('paperCash').textContent = '—';
  $('paperContributions').textContent = '—';
  $('paperEntries').textContent = '—';
  $('paperRiskMessage').textContent = 'Carteira virtual não carregada.';
  $('paperPositions').innerHTML = '<tr><td colspan="5">Nenhuma posição.</td></tr>';
  $('paperFills').innerHTML = '<tr><td colspan="7">Nenhuma operação.</td></tr>';
}
async function refreshPaper() {
  if (!authenticated) return;
  try {
    const s = await api('paper/status');
    if (s.mode !== 'PAPER_ONLY' || s.realOrdersEnabled || s.testnetOrdersEnabled)
      throw new Error('Motor de papel indisponível ou não autorizado.');
    $('paperStatus').textContent = s.riskLocked ? 'BLOQUEADO — Carlos' :
      s.running ? 'AUTOMÁTICO (PAPEL)' : 'PAUSADO';
    $('paperEquity').textContent = brl(s.lastEquityBrl);
    $('paperCash').textContent = brl(s.cashBrl);
    $('paperContributions').textContent = brl(s.totalContributionsBrl);
    $('paperEntries').textContent = String(s.entriesToday);
    $('paperRiskMessage').textContent = s.riskLocked
      ? s.riskReason : 'R$ 100 iniciais; aportes virtuais de R$ 200/30 dias; 0,5% risco/operação; 1% perda diária; 10% drawdown; 100% reinvestimento simulado. ' +
      (s.lastCycleUtc ? 'Último ciclo: '+formatDate(s.lastCycleUtc) : 'Nenhum ciclo executado.');
    $('paperStart').disabled = s.running || s.riskLocked || !canControl;
    $('paperPause').disabled = !s.running || !canControl;
    $('paperPositions').innerHTML = s.positions.length ? s.positions.map(p =>
      '<tr><td>'+safe(p.symbol)+'</td><td>'+walletNumber(p.quantity)+'</td><td>'+
      brl(p.entryPriceBrl)+'</td><td>'+brl(p.lastPriceBrl)+'</td><td>'+
      brl(p.stopPriceBrl)+'</td></tr>').join('') :
      '<tr><td colspan="5">Sem posições virtuais.</td></tr>';
    $('paperFills').innerHTML = s.fills.length ? s.fills.map(f =>
      '<tr><td>'+safe(formatDate(f.executedUtc))+'</td><td>'+safe(f.symbol)+
      '</td><td>'+safe(f.side)+'</td><td>'+walletNumber(f.quantity)+
      '</td><td>'+brl(f.priceBrl)+'</td><td>'+brl(f.feeBrl)+
      '</td><td>'+(f.realizedPnlBrl === null ? '—' : brl(f.realizedPnlBrl))+
      '</td></tr>').join('') : '<tr><td colspan="7">Sem operações.</td></tr>';
  } catch (error) {
    $('paperStatus').textContent = 'Indisponível';
    $('paperRiskMessage').textContent = error.message;
  }
}
async function paperCommand(path) {
  try {
    await api(path, {method:'POST', headers:{'Content-Type':'application/json'},
      body:'{}'});
    await refreshPaper();
  } catch(error) {
    $('paperRiskMessage').textContent = 'Falha no controle: '+error.message;
  }
}
$('paperStart').onclick = () => paperCommand('paper/start');
$('paperPause').onclick = () => paperCommand('paper/pause');
$('paperRefresh').onclick = refreshPaper;
setInterval(() => { if(authenticated) refreshPaper(); }, 15000);

function clearRealStrategiesDisplay() {
  $('realStrategiesStatus').textContent = 'Sem consulta';
  $('realStrategiesRows').innerHTML = '<tr><td colspan="7">Sem dados carregados.</td></tr>';
  $('strategyBacktestResult').textContent = 'Sem backtest executado.';
}

async function refreshRealStrategies() {
  if (!authenticated) return;
  try {
    const data = await api('strategy/overview');
    if (data.source !== 'BINANCE_SPOT_PUBLIC' || data.orderPlacementEnabled ||
        data.simulationLinked || data.agents.length !== 12)
      throw new Error('Modo do motor de estratégias não autorizado.');
    $('realStrategiesStatus').textContent = data.agents.filter(x => x.status === 'READY').length +
      '/12 pares com histórico contínuo';
    $('realStrategiesRows').innerHTML = data.agents.map(x =>
      '<tr><td>'+safe(x.symbol)+'</td><td>'+(x.priceUsdt === null ? '—' : decimal(x.priceUsdt))+
      '</td><td>'+(x.sma9 === null ? '—' : decimal(x.sma9))+
      '</td><td>'+(x.sma21 === null ? '—' : decimal(x.sma21))+
      '</td><td>'+(x.rsi14 === null ? '—' : Number(x.rsi14).toFixed(2))+
      '</td><td>'+(x.atr14 === null ? '—' : decimal(x.atr14))+
      '</td><td>'+safe(x.status === 'READY' ? x.action : 'BLOQUEADO: '+(x.note || 'dados ausentes'))+
      '</td></tr>').join('');
    $('realStrategiesNote').textContent = 'Estratégia '+data.strategyVersion+
      '. Sinais derivados de candles Binance Spot fechados; sem ordens e sem influência no patrimônio sintético.';
  } catch(error) {
    $('realStrategiesStatus').textContent = 'Indisponível';
    $('realStrategiesNote').textContent = error.message;
  }
}

$('refreshRealStrategies').onclick = refreshRealStrategies;
$('strategyBacktestForm').onsubmit = async event => {
  event.preventDefault();
  if (!authenticated) return;
  $('runStrategyBacktest').disabled = true;
  $('strategyBacktestResult').textContent = 'Calculando apenas com candles fechados...';
  try {
    const result = await api('strategy/backtest/'+encodeURIComponent($('strategyBacktestSymbol').value));
    if (result.orderPlacementEnabled || result.source !== 'BINANCE_SPOT_PUBLIC')
      throw new Error('Backtest incompatível com o modo somente leitura.');
    $('strategyBacktestResult').textContent = result.status !== 'COMPLETE'
      ? 'Backtest bloqueado: '+result.note
      : result.symbol+' • '+result.candleCount+' candles • '+result.trades+
        ' posições fechadas • inicial: '+result.initialUsdt+
        ' USDT fictícios • final: '+result.finalUsdt+
        ' USDT fictícios • variação histórica: '+result.returnPct+
        '% • drawdown máximo: '+result.maxDrawdownPct+
        '% • taxa assumida por lado: '+result.feePctPerSide+
        '% • slippage por lado: '+result.slippagePctPerSide+
        '%. Não é promessa de rentabilidade.';
  } catch(error) {
    $('strategyBacktestResult').textContent = 'Backtest indisponível: '+error.message;
  } finally { $('runStrategyBacktest').disabled = false; }
};
setInterval(() => { if(authenticated) refreshRealStrategies(); }, 60000);

function clearActivityDisplay() {
  $('activityStatus').textContent = 'Histórico não consultado';
  $('activityProfit').textContent = 'Não conciliado';
  $('activityWarnings').textContent = 'Nenhuma importação nesta sessão.';
  $('activityCounts').textContent = 'Ainda não há lançamentos reais importados.';
  $('activityFees').textContent = 'Sem comissões importadas.';
  $('activityRows').innerHTML = '<tr><td colspan="8">Nenhum evento importado.</td></tr>';
  $('activityExplanation').textContent = 'Custo médio, movimentações e histórico completo precisam ser conciliados.';
}

async function refreshActivityHistory() {
  if (!authenticated || !canViewWallet) return;
  try {
    const h = await api('portfolio/activity/history');
    if (h.source !== 'BINANCE_SPOT_REAL' || h.realOrdersEnabled ||
        h.profitReconciled || h.completeHistory)
      throw new Error('Histórico da Binance incompatível com modo de segurança.');
    $('activityProfit').textContent = 'Não conciliado';
    $('activityStatus').textContent = h.latestImportUtc
      ? 'SQL Server • ' + formatDate(h.latestImportUtc) : 'Sem importações';
    $('activityExplanation').textContent = h.reason;
    $('activityCounts').textContent = h.counts.length
      ? h.counts.map(x => x.kind + ': ' + x.count).join(' • ')
      : 'Nenhum registro importado.';
    $('activityFees').textContent = h.feesByAsset.length
      ? h.feesByAsset.map(x => walletNumber(x.amount) + ' ' + x.asset).join(' • ')
      : 'Sem comissões importadas.';
    $('activityRows').innerHTML = h.entries.length ? h.entries.map(x =>
      '<tr><td>'+safe(formatDate(x.occurredUtc))+'</td><td>'+safe(x.kind)+
      '</td><td>'+safe(x.symbol || x.asset)+'</td><td>'+safe(x.side || '—')+
      '</td><td>'+walletNumber(x.quantity)+'</td><td>'+walletNumber(x.price)+
      '</td><td>'+walletNumber(x.fee)+' '+safe(x.feeAsset)+
      '</td><td>'+safe(x.status)+'</td></tr>').join('')
      : '<tr><td colspan="8">Nenhuma atividade importada.</td></tr>';
  } catch (error) {
    $('activityStatus').textContent = 'Histórico indisponível';
    $('activityWarnings').textContent = error.message;
  }
}

$('activityImportForm').onsubmit = async event => {
  event.preventDefault();
  if (!authenticated || !canViewWallet) return;
  $('activityImport').disabled = true;
  $('activityStatus').textContent = 'Consultando Binance (somente GET)...';
  try {
    const result = await api('portfolio/activity/import', {
      method:'POST', headers:{'Content-Type':'application/json'},
      body:JSON.stringify({
        symbol:$('activitySymbol').value,
        includeTransfers:$('activityTransfers').value === 'true'
      })
    });
    if (result.realOrdersEnabled || result.completeHistory || result.profitReconciled)
      throw new Error('Resultado incompatível com importação de histórico parcial.');
    $('activityWarnings').textContent =
      'Novos eventos: '+result.imported+' • situações atualizadas: '+result.updated+
      ' • '+result.warnings.join(' ');
    await refreshActivityHistory();
  } catch(error) {
    $('activityWarnings').textContent = error.message;
    $('activityStatus').textContent = 'Importação não concluída';
  } finally { $('activityImport').disabled = false; }
};
