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
