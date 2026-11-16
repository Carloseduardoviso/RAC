const $ = id => document.getElementById(id);
const usd = n => new Intl.NumberFormat('pt-BR', {style:'currency',currency:'BRL'}).format(n);
const pct = n => `${Number(n).toFixed(2)}%`;
const decimal = n => Number(n).toLocaleString('pt-BR',{maximumFractionDigits:5});
let first = true;
const safe = value => String(value).replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
async function api(path, options={}) {
  const response = await fetch(`/api/${path}`, options);
  if (!response.ok) throw new Error((await response.text()).slice(0,300));
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
  $('start').disabled=state.riskLocked;
  $('reset').disabled=state.riskLocked;
  $('step').disabled=state.riskLocked;
  $('tick').textContent=`Tick ${state.tick}`;
  $('supervisors').innerHTML=state.supervisors.map(a=>`<article class="agent"><div class="agent-top"><b>${safe(a.name)}</b><span>${safe(a.status)}</span></div><p>${safe(a.function)}</p><p>${safe(a.detail)}</p></article>`).join('');
  $('agents').innerHTML=state.agents.map(a=>`<article class="agent ${a.status==='EM OPERAÇÃO'?'active':''}"><div class="agent-top"><b>${safe(a.symbol)}</b><span>● ${safe(a.status)}</span></div><div class="price">${decimal(a.price)}</div><div class="foot"><span class="${a.changePct>=0?'positive':'negative'}">${a.changePct>0?'+':''}${pct(a.changePct)}</span><span class="signal ${a.signal.toLowerCase()}">${safe(a.signal)}</span></div><div class="foot" style="margin-top:10px"><span>${a.wins}W / ${a.losses}L</span><span>${usd(a.realizedPnl)}</span></div></article>`).join('');
  $('positions').innerHTML=state.positions.length ? state.positions.map(p=>`<tr><td>${safe(p.symbol)}</td><td>${safe(p.side)}</td><td>${decimal(p.entry)}</td><td>${decimal(p.currentPrice)}</td><td class="${p.unrealizedPnl>=0?'positive':'negative'}">${usd(p.unrealizedPnl)}</td></tr>`).join('') : '<tr><td colspan="5">Nenhuma posição aberta</td></tr>';
  $('trades').innerHTML=state.trades.length ? state.trades.slice(0,15).map(t=>`<tr><td>${safe(t.symbol)}</td><td class="${t.pnl>=0?'positive':'negative'}">${usd(t.pnl)}</td><td>${safe(t.exitReason)}</td><td>${t.closedTick}</td></tr>`).join('') : '<tr><td colspan="4">Sem operações concluídas</td></tr>';
  draw(state.equityHistory);
  if(first){ $('startingBalance').value=state.settings.referenceCapitalBrl; $('riskPct').value=state.settings.maxRiskPerTradePct; $('dailyLimit').value=state.settings.maxDailyLossPct; $('maxOpen').value=state.settings.maxOpenPositions; $('cumulativeLimit').value=state.settings.maxCumulativeDrawdownPct; $('monthlyContribution').value=state.settings.monthlyContributionBrl; first=false; }
}
async function refresh(){try{render(await api('state'));}catch(e){$('error').textContent=`Falha ao carregar o simulador: ${e.message}`;$('error').hidden=false;}}
async function command(path,body){try{render(await api(path,{method:'POST',headers:body?{'Content-Type':'application/json'}:{},body:body?JSON.stringify(body):undefined}));}catch(e){$('error').textContent=e.message;$('error').hidden=false;}}
$('start').onclick=()=>command('control/start');
$('pause').onclick=()=>command('control/pause');
$('step').onclick=()=>command('control/step');
$('reset').onclick=()=>command('control/reset');
$('settings').onsubmit=e=>e.preventDefault();
$('ping').onclick=async()=>{try{const data=await api('binance/testnet/ping');$('binanceStatus').textContent=data.connected?'Binance Spot Testnet: API pública acessível (sem envio de ordens)':'Binance Spot Testnet: indisponível';}catch(e){$('binanceStatus').textContent='Binance Spot Testnet: indisponível ('+e.message+')';}};
refresh(); setInterval(refresh,2000);

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
    $('historyStatus').textContent = 'HistÃ³rico indisponÃ­vel';
    $('persistedRisk').textContent = 'NÃ£o foi possÃ­vel consultar o SQL Server: '+error.message;
  }
}
refreshHistory();
setInterval(refreshHistory,5000);