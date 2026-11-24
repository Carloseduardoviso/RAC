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

const officeStationScene = `
  <svg class="office-station-scene" viewBox="0 0 200 112" aria-hidden="true" focusable="false">
    <path class="office-floor" d="M63 22h74l48 62-85 20-85-20z"/>
    <path class="office-floor-lines" d="m40 53 120 0M28 69h144M51 92l32-70m17 82V22m49 70-32-70"/>
    <g class="office-monitor">
      <path d="M77 44v11m46-11v11M69 55h16m30 0h16"/>
      <rect x="57" y="24" width="40" height="22" rx="3"/>
      <rect x="103" y="21" width="40" height="25" rx="3"/>
      <path class="office-monitor-display" d="m63 39 7-4 6 3 6-7 9 1m18 2h16m-16 5h24m-24-10h11"/>
    </g>
    <g class="office-desk">
      <path class="office-desk-legs" d="M55 66v19m90-19v19"/>
      <path class="office-desk-top" d="M52 53h96l13 13H39z"/>
      <path class="office-desk-edge" d="M39 66h122v5H39z"/>
      <path class="office-keyboard" d="M87 57h26l5 5H82z"/>
    </g>
    <g class="office-chair">
      <path class="office-chair-base" d="M100 86v11m-10 3 10-3 10 3"/>
      <rect x="84" y="69" width="32" height="21" rx="8"/>
    </g>
    <g class="office-person">
      <path class="office-walker-leg" d="M95 80l-2 15h-4"/>
      <path class="office-walker-leg" d="M105 80l2 15h4"/>
      <path class="office-person-shirt" d="M87 80v-8c0-5 5-8 13-8s13 3 13 8v8z"/>
      <circle class="office-person-face" cx="100" cy="59" r="8"/>
      <path class="office-person-hair" d="M92 59a8 8 0 1 1 16 0l-3-4-5 2-5-1z"/>
      <path class="office-person-eyes" d="M97 60h.1m5.8 0h.1"/>
    </g>
  </svg>`;

const officeLeaderNames = ['Carlos', 'Ana', 'Rebeca'];
const officeVisitDuration = { stand: 520, walk: 850, meeting: 2200 };
const officeRooms = document.querySelector('.office-rooms');
const officeVisitControls = document.createElement('div');
officeVisitControls.className = 'office-visit-controls';
officeVisitControls.innerHTML = `
  <label>Chefe para visita
    <select>${officeLeaderNames.map(name => `<option>${name}</option>`).join('')}</select>
  </label>
  <p role="status" aria-live="polite" aria-atomic="true">Liderança disponível para visitas locais.</p>`;
officeRooms.before(officeVisitControls);

const officeLeaderSelect = officeVisitControls.querySelector('select');
const officeVisitStatus = officeVisitControls.querySelector('[role="status"]');
let officeVisit = null;

function renderOfficeStations(container, records, key, create, update) {
  const stations = new Map(Array.from(container.children, station => [station.dataset.officeKey, station]));
  records.forEach((record, index) => {
    const name = record[key];
    let station = stations.get(name);
    if (!station) {
      station = document.createElement('article');
      station.dataset.officeKey = name;
      station.innerHTML = create(name);
    }
    stations.delete(name);
    update(station, record);
    if (container.children[index] !== station) {
      container.insertBefore(station, container.children[index] || null);
    }
  });
  for (const station of stations.values()) station.remove();
}

function renderOffice(state) {
  renderOfficeStations($('supervisors'), state.supervisors, 'name', name => `
    <div class="agent-top">
      <h3>${safe(name)}</h3>
      <span></span>
    </div>
    ${officeStationScene}
    <div class="office-station-details"></div>
    ${officeLeaderNames.includes(name) ? '<p class="office-leader-state">Na estação</p>' : ''}`,
  (station, supervisor) => {
    station.classList.add('agent', 'office-station');
    if (officeLeaderNames.includes(supervisor.name) && !station.dataset.officeState) {
      station.dataset.officeState = 'seated';
    }
    station.querySelector('.agent-top > span').textContent = supervisor.status;
    station.querySelector('.office-station-details').innerHTML = `
      <p><strong>${safe(supervisor.function)}</strong></p>
      <p>${safe(supervisor.detail)}</p>`;
  });

  renderOfficeStations($('agents'), state.agents, 'symbol', symbol => `
    <div class="agent-top">
      <h3>${safe(symbol)}</h3>
      <span><span aria-hidden="true">● </span><span class="office-agent-status"></span></span>
    </div>
    ${officeStationScene}
    <div class="office-station-details"></div>
    <button type="button" class="office-visit-request" aria-label="Solicitar visita a ${safe(symbol)}">
      Solicitar visita
    </button>`,
  (station, agent) => {
    const signal = String(agent.signal ?? '—');
    const signalClass = signal.toLowerCase();
    const signalColor = signalClass === 'compra' || signalClass === 'venda' ? ' ' + signalClass : '';
    const changeColor = agent.changePct >= 0 ? 'positive' : 'negative';
    station.classList.add('agent', 'office-station');
    station.classList.toggle('active', agent.status === 'EM OPERAÇÃO');
    station.querySelector('.office-agent-status').textContent = agent.status;
    station.querySelector('.office-station-details').innerHTML = `
      <div class="price">${safe(decimal(agent.price))}</div>
      <div class="foot">
        <span class="${changeColor}">${agent.changePct > 0 ? '+' : ''}${safe(pct(agent.changePct))}</span>
        <span class="signal${signalColor}">${safe(signal)}</span>
      </div>
      <dl class="office-results">
        <div><dt>Vitórias</dt><dd>${safe(agent.wins)}</dd></div>
        <div><dt>Perdas</dt><dd>${safe(agent.losses)}</dd></div>
        <div><dt>P/L</dt><dd>${safe(usd(agent.realizedPnl))}</dd></div>
      </dl>`;
  });

  if (officeVisit && (!officeVisit.leader.isConnected || !officeVisit.agent.isConnected)) {
    finishOfficeVisit('Visita encerrada. Uma estação ficou indisponível.');
  }
  updateOfficeVisitControls();
}

function updateOfficeVisitControls() {
  const leaders = Array.from($('supervisors').children, station => station.dataset.officeKey);
  for (const option of officeLeaderSelect.options) {
    option.disabled = !leaders.includes(option.value);
  }
  if (officeLeaderSelect.selectedIndex < 0 || officeLeaderSelect.selectedOptions[0].disabled) {
    officeLeaderSelect.selectedIndex = Array.from(officeLeaderSelect.options).findIndex(option => !option.disabled);
  }
  const unavailable = String(Boolean(officeVisit) || officeLeaderSelect.selectedIndex < 0);
  for (const button of $('agents').querySelectorAll('.office-visit-request')) {
    if (button.getAttribute('aria-disabled') !== unavailable) button.setAttribute('aria-disabled', unavailable);
  }
}

function positionOfficeWalker(visit) {
  const room = officeRooms.getBoundingClientRect();
  const corridor = parseFloat(getComputedStyle(officeRooms).getPropertyValue('--office-corridor-width'));
  let path;

  if (corridor) {
    const home = visit.leader.getBoundingClientRect();
    const destination = visit.agent.getBoundingClientRect();
    const spine = corridor / 2;
    const homeY = home.top - room.top - spine;
    const destinationY = destination.top - room.top - spine;
    path = `M ${home.left + home.width / 2 - room.left} ${homeY} L ${spine} ${homeY} ` +
      `L ${spine} ${destinationY} L ${destination.left + destination.width / 2 - room.left} ${destinationY}`;
  } else {
    const home = visit.leader.querySelector('.office-chair rect').getBoundingClientRect();
    const destination = visit.agent.querySelector('.office-chair rect').getBoundingClientRect();
    const homeX = home.left + home.width / 2 - room.left;
    const homeY = home.top + home.height / 2 - room.top - 8;
    const destinationX = destination.left + destination.width / 2 - room.left - 28;
    const destinationY = destination.top + destination.height / 2 - room.top - 8;
    path = `M ${homeX} ${homeY} L ${destinationX} ${destinationY}`;
  }

  visit.route.firstElementChild.setAttribute('d', path);
  visit.walker.style.offsetPath = `path("${path}")`;
  visit.walker.style.offsetDistance = visit.atAgent ? '100%' : '0%';
}

function setOfficeVisitState(visit, state, label) {
  visit.leader.dataset.officeState = state;
  visit.leader.querySelector('.office-leader-state').textContent = label;
  visit.walker.dataset.officeState = state;
  officeVisitStatus.textContent = `${visit.leader.dataset.officeKey}: ${label}.`;
}

function finishOfficeVisit(message) {
  const visit = officeVisit;
  clearTimeout(visit.timer);
  visit.layout.disconnect();
  setOfficeVisitState(visit, 'seated', 'Na estação');
  visit.agent.classList.remove('office-meeting');
  visit.route.remove();
  visit.walker.remove();
  officeVisit = null;
  if (message) officeVisitStatus.textContent = message;
  updateOfficeVisitControls();
}

$('agents').addEventListener('click', event => {
  const button = event.target.closest('.office-visit-request');
  if (!button || officeVisit || button.getAttribute('aria-disabled') === 'true') return;

  const agent = button.closest('.office-station');
  const leader = Array.from($('supervisors').children)
    .find(station => station.dataset.officeKey === officeLeaderSelect.value);
  if (!leader) return;

  const scene = leader.querySelector('.office-station-scene');
  const walker = scene.cloneNode(false);
  walker.setAttribute('class', 'office-walker');
  walker.setAttribute('viewBox', '80 42 40 56');
  walker.style.setProperty('--office-walk-duration', `${officeVisitDuration.walk}ms`);
  walker.append(scene.querySelector('.office-person').cloneNode(true));

  const route = scene.cloneNode(false);
  route.setAttribute('class', 'office-route');
  route.removeAttribute('viewBox');
  route.innerHTML = '<path/>';

  const colors = getComputedStyle(scene);
  for (const property of ['--office-shirt', '--office-skin', '--office-hair']) {
    walker.style.setProperty(property, colors.getPropertyValue(property));
  }

  const visit = { leader, agent, route, walker, atAgent: false, timer: null };
  visit.layout = new ResizeObserver(() => positionOfficeWalker(visit));
  visit.layout.observe(officeRooms);
  officeRooms.append(route, walker);
  positionOfficeWalker(visit);
  officeVisit = visit;
  updateOfficeVisitControls();

  const symbol = agent.dataset.officeKey;
  setOfficeVisitState(visit, 'standing', `Levantando para visitar ${symbol}`);
  visit.timer = setTimeout(() => {
    setOfficeVisitState(visit, 'walking', `A caminho de ${symbol}`);
    visit.atAgent = true;
    positionOfficeWalker(visit);
    visit.timer = setTimeout(() => {
      agent.classList.add('office-meeting');
      setOfficeVisitState(visit, 'meeting', `Em reunião com ${symbol}`);
      visit.timer = setTimeout(() => {
        agent.classList.remove('office-meeting');
        setOfficeVisitState(visit, 'walking', `Retornando de ${symbol}`);
        visit.atAgent = false;
        positionOfficeWalker(visit);
        visit.timer = setTimeout(() => finishOfficeVisit(), officeVisitDuration.walk);
      }, officeVisitDuration.meeting);
    }, officeVisitDuration.walk);
  }, officeVisitDuration.stand);
});

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
  renderOffice(state);
  window.updateTradingFloorHud?.(state);
  window.RacAgentMotion?.update(state);
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
  $('researchShadowPanel').hidden = !authenticated;
  $('researchThreeMonthPanel').hidden = !authenticated;
  $('overfitScreenPanel').hidden = !authenticated;
  $('sixMonthValidationPanel').hidden = !authenticated;
  $('frozenDecisionsPanel').hidden = !authenticated;
  $('robustnessPanel').hidden = !authenticated;
  $('productionPolicyPanel').hidden = !authenticated || !canReview;
  $('policyProposalForm').hidden = !authenticated || !canReview;
  $('policyReviewForm').hidden = !authenticated || !canViewWallet;
  if (!authenticated || !canReview) clearProductionPolicies();
  if (!authenticated) clearRobustness();
  if (!authenticated) clearFrozenDecisions();
  if (!authenticated) clearSixMonthValidation();
  if (!authenticated) clearOverfitScreen();
  if (!authenticated) clearThreeMonthResearch();
  if (!authenticated) clearShadowResearch();
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

// Research-only comparison: a bar is an event count, not profit.
let validatedShadowRows = [];
const shadowMetricKeys = new Set(['unresolvedExits', 'blockedEntryAttempts',
  'partialEntryAttempts', 'hypotheticalClosedTrades', 'noTradeDays']);
function renderShadowBars() {
  const metric = $('researchShadowChartMetric').value;
  if (!shadowMetricKeys.has(metric) || validatedShadowRows.length !== 12) {
    $('researchShadowBars').textContent = 'Nenhuma comparação SHADOW validada.';
    return;
  }
  const rows = validatedShadowRows;
  if (rows.some(x => !Number.isSafeInteger(x[metric]) || x[metric] < 0)) {
    $('researchShadowBars').textContent = 'Contagem inválida para comparação.';
    return;
  }
  const max = Math.max(1, ...rows.map(x => x[metric]));
  $('researchShadowBars').innerHTML = rows.map(x => {
    const n = x[metric];
    const width = Math.min(100, n / max * 100);
    const risk = metric === 'unresolvedExits' && n > 0;
    return `<div class="research-bar-row${risk ? ' has-risk' : ''}">` +
      `<b>${safe(x.symbol)}</b><div class="research-bar-track">` +
      `<div class="research-bar-fill" style="width:${width.toFixed(2)}%"></div>` +
      `</div><span class="research-bar-count">${n}</span></div>`;
  }).join('');
}
$('researchShadowChartMetric').onchange = renderShadowBars;

let validThreeMonthTimeline = [];
const threeMonthMetricNames = new Set(['unresolvedExits', 'hypotheticalTrades', 'noTradeDays']);
function renderThreeMonthBars() {
  const key = $('researchThreeMonthMetric').value;
  if (!threeMonthMetricNames.has(key) || validThreeMonthTimeline.length !== 3 ||
      validThreeMonthTimeline.some(m => !Number.isSafeInteger(m[key]) || m[key] < 0)) {
    $('researchThreeMonthBars').textContent = 'Comparação indisponível ou inválida.';
    return;
  }
  const max = Math.max(1, ...validThreeMonthTimeline.map(m => m[key]));
  $('researchThreeMonthBars').innerHTML = validThreeMonthTimeline.map(m => {
    const value = m[key];
    const width = Math.min(100, value / max * 100);
    const atRisk = key === 'unresolvedExits' && value > 0;
    return `<div class="research-bar-row${atRisk ? ' has-risk' : ''}">` +
      `<b>${safe(m.monthKey)}</b><div class="research-bar-track">` +
      `<div class="research-bar-fill" style="width:${width.toFixed(2)}%"></div>` +
      `</div><span class="research-bar-count">${value}</span></div>`;
  }).join('');
}
$('researchThreeMonthMetric').onchange = renderThreeMonthBars;
function clearThreeMonthResearch() {
  validThreeMonthTimeline = [];
  $('researchThreeMonthBars').textContent = 'Aguardando três relatórios validados.';
  $('researchThreeMonthStatus').textContent = 'Aguardando relatórios';
  $('researchThreeMonthNotice').textContent = 'Mês ausente não equivale a retorno zero.';
  for (const id of ['researchThreeMonthDays', 'researchThreeMonthNoTrade',
                    'researchThreeMonthUnresolved', 'researchThreeMonthEligible'])
    $(id).textContent = '—';
  $('researchThreeMonthMonths').innerHTML = '<tr><td colspan="7">Sem histórico comparável.</td></tr>';
  $('researchThreeMonthAgents').innerHTML = '<tr><td colspan="7">Sem comparação validada.</td></tr>';
}
async function refreshThreeMonthResearch() {
  if (!authenticated) return;
  $('researchThreeMonthRefresh').disabled = true;
  try {
    const report = await api('research/three-month-dashboard');
    if (!report.available) {
      clearThreeMonthResearch();
      const missing = Array.isArray(report.missingMonths) ? report.missingMonths.join(', ') : '';
      $('researchThreeMonthStatus').textContent = 'Histórico incompleto';
      $('researchThreeMonthNotice').textContent = (report.notice || 'Relatórios não disponíveis.') +
        (missing ? ' Meses pendentes: ' + missing : '');
      return;
    }
    if (report.mode !== 'THREE_MONTH_EXIT_CAPACITY_SHADOW_ONLY' ||
        report.paperModified || report.orderPlacementEnabled || report.promotionAllowed ||
        report.agentCount !== 12 || !Array.isArray(report.timeline) ||
        report.timeline.length !== 3 || !Array.isArray(report.rows) || report.rows.length !== 12 ||
        report.timeline.map(x => x.monthKey).join(',') !== '2026-08,2026-09,2026-10')
      throw new Error('Relatório de três meses não satisfaz requisitos de segurança.');
    $('researchThreeMonthStatus').textContent = '3 meses validados · somente leitura';
    $('researchThreeMonthNotice').textContent = report.notice;
    $('researchThreeMonthDays').textContent = report.totalAgentDays;
    $('researchThreeMonthNoTrade').textContent = report.noTradeDays;
    $('researchThreeMonthUnresolved').textContent = report.unresolvedExits;
    $('researchThreeMonthEligible').textContent = report.agentsEligibleForResearchReview + ' / 12';
    validThreeMonthTimeline = report.timeline;
    renderThreeMonthBars();
    $('researchThreeMonthMonths').innerHTML = report.timeline.map(m =>
      `<tr><td>${safe(m.monthKey)}</td><td>${m.agentDays}</td>` +
      `<td>${m.noTradeDays}</td><td>${m.hypotheticalTrades}</td>` +
      `<td>${m.unresolvedExits}</td><td>${m.rejectedAgents}</td>` +
      `<td>${pct(m.meanHypotheticalReturnPct)}</td></tr>`).join('');
    $('researchThreeMonthAgents').innerHTML = report.rows.map(a => {
      const months = a.months;
      if (!Array.isArray(months) || months.length !== 3 ||
          months.map(x => x.monthKey).join(',') !== '2026-08,2026-09,2026-10')
        throw new Error('Histórico de agente incompleto.');
      return `<tr><td>${safe(a.symbol)}</td>` +
        months.map(x => `<td>${pct(x.hypotheticalMeanReturnPct)}</td>`).join('') +
        `<td>${a.hypotheticalTrades}</td><td>${a.unresolvedExits}</td>` +
        `<td>${a.allMonthsResearchReviewEligible ? 'REVISÃO CIENTÍFICA' : 'REJEITADO'}</td></tr>`;
    }).join('');
  } catch (error) {
    clearThreeMonthResearch();
    $('researchThreeMonthStatus').textContent = 'Falha de validação';
    $('researchThreeMonthNotice').textContent = error.message;
  } finally {
    $('researchThreeMonthRefresh').disabled = !authenticated;
  }
}
$('researchThreeMonthRefresh').onclick = refreshThreeMonthResearch;

function clearFrozenDecisions() {
  $('frozenDecisionsStatus').textContent = 'Aguardando pesquisa';
  $('frozenDecisionsNotice').textContent = 'Sem evidência temporal validada nesta sessão.';
  for (const id of ['frozenTrainingWindows', 'frozenHoldoutWindows',
                    'frozenNoTrade', 'frozenReplayRequired']) $(id).textContent = '—';
  $('frozenDecisionsRows').innerHTML = '<tr><td colspan="7">Sem dados validados.</td></tr>';
}
async function refreshFrozenDecisions() {
  if (!authenticated) return;
  $('frozenDecisionsRefresh').disabled = true;
  try {
    const report = await api('research/frozen-decisions');
    if (!report.available) {
      clearFrozenDecisions();
      $('frozenDecisionsStatus').textContent = 'Histórico incompleto';
      $('frozenDecisionsNotice').textContent = report.notice || 'Pesquisa indisponível.';
      return;
    }
    if (report.mode !== 'PRE_HOLDOUT_FROZEN_DECISION_SHADOW_ONLY' ||
        report.paperModified || report.orderPlacementEnabled || report.promotionAllowed ||
        report.agents !== 12 || !Array.isArray(report.rows) || report.rows.length !== 12 ||
        report.trainingMonths.join(',') !== '2026-05,2026-06,2026-07' ||
        report.holdoutMonths.join(',') !== '2026-08,2026-09,2026-10')
      throw new Error('Auditoria congelada incompatível com as salvaguardas.');
    if (report.rows.some(r => !Number.isSafeInteger(r.trainingWindows) ||
        r.trainingWindows !== 92 || !Number.isSafeInteger(r.votesForFrozen) ||
        r.votesForFrozen < 0 || r.votesForFrozen > 92 ||
        (r.frozenEntryGate === 'NO_TRADE' &&
         (r.outOfSampleSimulatedTrades !== 0 || Number(r.outOfSampleReturnPct) !== 0))))
      throw new Error('Contagens inválidas para decisões congeladas.');
    $('frozenDecisionsStatus').textContent = 'Treino anterior ao teste · SHADOW';
    $('frozenDecisionsNotice').textContent = report.notice;
    $('frozenTrainingWindows').textContent = report.trainingWindows;
    $('frozenHoldoutWindows').textContent = report.holdoutWindows;
    $('frozenNoTrade').textContent = report.frozenNoTradeAgents + ' / 12';
    $('frozenReplayRequired').textContent = report.strategiesNeedingReplay;
    $('frozenDecisionsRows').innerHTML = report.rows.map(r =>
      `<tr><td>${safe(r.symbol)}</td><td>${safe(r.frozenEntryGate)}</td>` +
      `<td>${r.votesForFrozen} / ${r.trainingWindows}</td>` +
      `<td>${r.outOfSampleWindows}</td>` +
      `<td>${r.outOfSampleSimulatedTrades === null ? 'não avaliado' : r.outOfSampleSimulatedTrades}</td>` +
      `<td>${r.outOfSampleReturnPct === null ? 'não avaliado' : pct(r.outOfSampleReturnPct)}</td>` +
      `<td>${safe(r.evaluationStatus)}</td></tr>`).join('');
  } catch (error) {
    clearFrozenDecisions();
    $('frozenDecisionsStatus').textContent = 'Falha de validação';
    $('frozenDecisionsNotice').textContent = error.message;
  } finally {
    $('frozenDecisionsRefresh').disabled = !authenticated;
  }
}
$('frozenDecisionsRefresh').onclick = refreshFrozenDecisions;

function clearProductionPolicies() {
  $('policyStatus').textContent = 'Aguardando consulta';
  $('policyNotice').textContent = 'Políticas documentais não habilitam ordens.';
  for (const id of ['policyPending', 'policyReviewed', 'policyRejected',
                    'policyIntegrity']) $(id).textContent = '—';
  $('policyRows').innerHTML = '<tr><td colspan="7">Sem dados disponíveis.</td></tr>';
  $('policyAuditRows').innerHTML = '<tr><td colspan="6">Sem dados disponíveis.</td></tr>';
}
async function refreshProductionPolicies() {
  if (!authenticated || !canReview) return;
  $('policyRefresh').disabled = true;
  try {
    const report = await api('risk/policies');
    if (report.mode !== 'RISK_POLICY_REVIEW_ONLY' ||
        report.tradingEnabled || report.paperModified ||
        !Array.isArray(report.policies) || !Array.isArray(report.audits) ||
        !report.security || report.security.orderPlacementEnabled ||
        report.security.integrityAlerts !== 0)
      throw new Error('Resposta de auditoria incompatível com modo seguro.');
    $('policyStatus').textContent = 'Auditoria consultada — ordens desabilitadas';
    $('policyPending').textContent = report.security.pendingReview;
    $('policyReviewed').textContent = report.security.reviewedDocumentOnly;
    $('policyRejected').textContent = report.security.rejected;
    $('policyIntegrity').textContent = report.security.integrityAlerts;
    $('policyRows').innerHTML = report.policies.length
      ? report.policies.map(p =>
        '<tr><td>' + Number(p.id) + '</td><td>' + Number(p.version) +
        '</td><td>' + safe(p.status) + '</td><td>' + safe(p.limitsJson) +
        '</td><td>' + safe(p.proposedBy) +
        '</td><td>' + safe(p.reviewedBy || 'pendente') +
        '</td><td>' + safe(formatDate(p.proposedUtc)) + '</td></tr>').join('')
      : '<tr><td colspan="7">Nenhuma proposta registrada.</td></tr>';
    $('policyAuditRows').innerHTML = report.audits.length
      ? report.audits.map(a =>
        '<tr><td>' + Number(a.id) + '</td><td>' + Number(a.policyId) +
        '</td><td>' + safe(a.action) + '</td><td>' + safe(a.actor) +
        '</td><td>' + safe(formatDate(a.createdUtc)) +
        '</td><td>' + safe(a.reason) + '</td></tr>').join('')
      : '<tr><td colspan="6">Nenhum evento registrado.</td></tr>';
  } catch (error) {
    clearProductionPolicies();
    $('policyStatus').textContent = 'Falha na consulta';
    $('policyNotice').textContent = error.message;
  } finally {
    $('policyRefresh').disabled = !authenticated || !canReview;
  }
}
$('policyRefresh').onclick = refreshProductionPolicies;
$('policyProposalForm').onsubmit = async event => {
  event.preventDefault();
  if (!authenticated || !canReview) return;
  const form = event.currentTarget;
  const data = new FormData(form);
  const number = key => Number(data.get(key));
  const request = {
    maximumCapitalUsdt: number('maximumCapitalUsdt'),
    maximumOrderUsdt: number('maximumOrderUsdt'),
    maximumDailyLossPercent: number('maximumDailyLossPercent'),
    maximumDrawdownPercent: number('maximumDrawdownPercent'),
    maximumOpenPositions: number('maximumOpenPositions'),
    maximumOrdersPerHour: number('maximumOrdersPerHour'),
    maximumOrdersPerDay: number('maximumOrdersPerDay'),
    allowedSymbols: String(data.get('allowedSymbols')).split(',').map(x => x.trim())
  };
  if (request.allowedSymbols.some(s => !/^[A-Z0-9]{6,24}$/.test(s))) {
    $('policyNotice').textContent = 'Use pares Spot válidos separados por vírgula.';
    return;
  }
  try {
    const result = await api('risk/policies', {
      method: 'POST', headers: {'Content-Type':'application/json'},
      body: JSON.stringify(request)
    });
    $('policyNotice').textContent = 'Proposta #' + result.id +
      ' registrada para revisão independente. Negociação continua bloqueada.';
    form.reset();
    await refreshProductionPolicies();
  } catch (error) {
    $('policyNotice').textContent = 'Proposta recusada: ' + error.message;
  }
};
$('policyReviewForm').onsubmit = async event => {
  event.preventDefault();
  if (!authenticated || !canViewWallet) return;
  const id = Number($('policyReviewId').value);
  const reason = $('policyReviewReason').value.trim();
  if (!Number.isSafeInteger(id) || id <= 0 || reason.length < 20) return;
  try {
    const result = await api('risk/policies/' + id + '/review', {
      method:'POST', headers:{'Content-Type':'application/json'},
      body: JSON.stringify({
        reason, approve: $('policyReviewApprove').value === 'true'
      })
    });
    $('policyNotice').textContent = 'Revisão documental #' + result.id +
      ': ' + result.status + '. Nenhuma ordem autorizada.';
    $('policyReviewReason').value = '';
    await refreshProductionPolicies();
  } catch (error) {
    $('policyNotice').textContent = 'Revisão recusada: ' + error.message;
  }
};

function clearRobustness() {
  $('robustnessStatus').textContent = 'Aguardando relatório';
  $('robustnessNotice').textContent = 'Sem análise de robustez validada nesta sessão.';
  for (const id of ['robustnessAgents', 'robustnessAbstaining',
                    'robustnessInsufficient', 'robustnessUnstable']) {
    $(id).textContent = '—';
  }
  $('robustnessRows').innerHTML =
    '<tr><td colspan="9">Sem pesquisa validada.</td></tr>';
}

async function refreshRobustness() {
  if (!authenticated) return;
  $('robustnessRefresh').disabled = true;
  try {
    const report = await api('research/robustness');
    if (!report.available) {
      clearRobustness();
      $('robustnessStatus').textContent = 'Indisponível';
      $('robustnessNotice').textContent = report.notice || 'Relatório indisponível.';
      return;
    }
    if (report.mode !== 'FIXED_REPLAY_ROBUSTNESS_SHADOW_ONLY' ||
        report.agents !== 12 || report.holdoutMonths !== 3 ||
        report.paperModified || report.orderPlacementEnabled ||
        report.promotionAllowed || !Array.isArray(report.rows) ||
        report.rows.length !== 12 ||
        report.rows.some(r => r.tradingAllowed ||
          r.evaluatedMonths !== 3 ||
          (r.frozenGate === 'NO_TRADE' &&
           (r.normalMeanPct !== null || r.doubleCostMeanPct !== null)))) {
      throw new Error('Dados SHADOW de robustez incompatíveis com as salvaguardas.');
    }
    $('robustnessStatus').textContent = 'Pesquisa SHADOW — sem autorização de ordens';
    $('robustnessNotice').textContent = report.notice;
    $('robustnessAgents').textContent = report.agents;
    $('robustnessAbstaining').textContent = report.abstainingAgents;
    $('robustnessInsufficient').textContent = report.insufficientEvidenceAgents;
    $('robustnessUnstable').textContent = report.unstableAgents;
    const percentage = n => n === null || n === undefined ? 'não avaliado' : pct(n);
    $('robustnessRows').innerHTML = report.rows.map(r =>
      '<tr><td>' + safe(r.symbol) + '</td><td>' + safe(r.frozenGate) + '</td>' +
      '<td>' + (r.frozenGate === 'NO_TRADE' ? 'sem estratégia ativa' : r.positiveMonths + ' / 3') + '</td>' +
      '<td>' + r.closedTrades + '</td><td>' + percentage(r.normalMeanPct) +
      '</td><td>' + percentage(r.doubleCostMeanPct) +
      '</td><td>' + percentage(r.costPenaltyPct) +
      '</td><td>' + (r.frozenGate === 'NO_TRADE' ? 'não aplicável' : percentage(r.worstDrawdownPct)) +
      '</td><td>' + safe(r.status) + '</td></tr>').join('');
  } catch (error) {
    clearRobustness();
    $('robustnessStatus').textContent = 'Falha de validação';
    $('robustnessNotice').textContent = error.message;
  } finally {
    $('robustnessRefresh').disabled = !authenticated;
  }
}
$('robustnessRefresh').onclick = refreshRobustness;

function clearSixMonthValidation() {
  $('sixMonthValidationStatus').textContent = 'Aguardando pesquisa';
  $('sixMonthValidationNotice').textContent = 'Sem comparação de seis meses validada nesta sessão.';
  for (const id of ['sixMonthAgentDays', 'sixMonthHypotheticalTrades',
                    'sixMonthUnresolvedExits', 'sixMonthResearchEligible'])
    $(id).textContent = '—';
  $('sixMonthTimeline').innerHTML = '<tr><td colspan="6">Sem dados completos validados.</td></tr>';
  $('sixMonthAgents').innerHTML = '<tr><td colspan="8">Sem comparação concluída.</td></tr>';
}
async function refreshSixMonthValidation() {
  if (!authenticated) return;
  $('sixMonthValidationRefresh').disabled = true;
  try {
    const report = await api('research/six-month-validation');
    if (!report.available) {
      clearSixMonthValidation();
      $('sixMonthValidationStatus').textContent = 'Histórico incompleto';
      const missing = Array.isArray(report.missingMonths) ? report.missingMonths.join(', ') : '';
      $('sixMonthValidationNotice').textContent = (report.notice || 'Histórico não validado.') +
        (missing ? ' Meses pendentes: ' + missing : '');
      return;
    }
    if (report.mode !== 'SIX_MONTH_WALK_FORWARD_RISK_SHADOW_ONLY' ||
        report.paperModified || report.orderPlacementEnabled || report.promotionAllowed ||
        report.agents !== 12 || !Array.isArray(report.timeline) || report.timeline.length !== 6 ||
        !Array.isArray(report.rows) || report.rows.length !== 12 ||
        report.timeline.map(m => m.monthKey).join(',') !==
          '2026-05,2026-06,2026-07,2026-08,2026-09,2026-10')
      throw new Error('Pesquisa longitudinal incompatível com as salvaguardas.');
    if (report.rows.some(a => !Number.isSafeInteger(a.hypotheticalTrades) ||
        a.hypotheticalTrades < 0 || !Number.isSafeInteger(a.unresolvedExits) ||
        a.unresolvedExits < 0 || !Number.isFinite(Number(a.adjustedP)) ||
        Number(a.adjustedP) < 0 || Number(a.adjustedP) > 1))
      throw new Error('Contagens ou diagnóstico estatístico inválidos.');
    $('sixMonthValidationStatus').textContent = '6 meses · SHADOW · somente leitura';
    $('sixMonthValidationNotice').textContent = report.notice;
    $('sixMonthAgentDays').textContent = report.agentDays;
    $('sixMonthHypotheticalTrades').textContent = report.hypotheticalTrades;
    $('sixMonthUnresolvedExits').textContent = report.unresolvedExits;
    $('sixMonthResearchEligible').textContent = report.agentsEligibleForResearchReview + ' / 12';
    $('sixMonthTimeline').innerHTML = report.timeline.map(m =>
      `<tr><td>${safe(m.monthKey)}</td><td>${m.agentDays}</td>` +
      `<td>${m.noTradeDays}</td><td>${m.hypotheticalTrades}</td>` +
      `<td>${m.unresolvedExits}</td><td>${pct(m.meanHypotheticalReturnPct)}</td></tr>`
    ).join('');
    $('sixMonthAgents').innerHTML = report.rows.map(a =>
      `<tr><td>${safe(a.symbol)}</td><td>${a.positiveMonths}</td>` +
      `<td>${a.negativeMonths}</td><td>${a.flatMonths}</td>` +
      `<td>${a.hypotheticalTrades}</td><td>${a.unresolvedExits}</td>` +
      `<td>${Number(a.adjustedP).toFixed(4)}</td>` +
      `<td>${safe(a.evidenceStatus)}</td></tr>`
    ).join('');
  } catch (error) {
    clearSixMonthValidation();
    $('sixMonthValidationStatus').textContent = 'Falha de validação';
    $('sixMonthValidationNotice').textContent = error.message;
  } finally {
    $('sixMonthValidationRefresh').disabled = !authenticated;
  }
}
$('sixMonthValidationRefresh').onclick = refreshSixMonthValidation;

function clearOverfitScreen() {
  $('overfitScreenStatus').textContent = 'Aguardando validação';
  $('overfitScreenNotice').textContent = 'Sem evidência estatística validada nesta sessão.';
  for (const id of ['overfitMonths', 'overfitAllPositive',
                    'overfitExitRisk', 'overfitSupported'])
    $(id).textContent = '—';
  $('overfitScreenRows').innerHTML = '<tr><td colspan="8">Sem diagnóstico validado.</td></tr>';
}
async function refreshOverfitScreen() {
  if (!authenticated) return;
  $('overfitScreenRefresh').disabled = true;
  try {
    const report = await api('research/overfit-screen');
    if (!report.available) {
      clearOverfitScreen();
      $('overfitScreenStatus').textContent = 'Sem evidência suficiente';
      $('overfitScreenNotice').textContent = report.notice || 'Histórico incompleto ou inválido.';
      return;
    }
    if (report.mode !== 'THREE_MONTH_OVERFIT_SCREEN_SHADOW_ONLY' ||
        report.paperModified || report.orderPlacementEnabled || report.promotionAllowed ||
        report.agents !== 12 || report.monthsObserved !== 3 ||
        report.multipleComparisons !== 12 || report.minimumNonzeroMonths < 6 ||
        !Array.isArray(report.rows) || report.rows.length !== 12)
      throw new Error('Diagnóstico estatístico SHADOW incompatível com as salvaguardas.');
    if (report.rows.some(a => !Number.isInteger(a.positiveMonths) ||
        !Number.isInteger(a.negativeMonths) || !Number.isInteger(a.flatMonths) ||
        a.positiveMonths + a.negativeMonths + a.flatMonths !== 3 ||
        !Number.isFinite(Number(a.oneSidedSignP)) || !Number.isFinite(Number(a.bonferroniAdjustedP)) ||
        Number(a.oneSidedSignP) < 0 || Number(a.oneSidedSignP) > 1 ||
        Number(a.bonferroniAdjustedP) < 0 || Number(a.bonferroniAdjustedP) > 1))
      throw new Error('Contagens ou p-valores inválidos na pesquisa.');
    $('overfitScreenStatus').textContent = 'SHADOW · 3 meses · pesquisa';
    $('overfitScreenNotice').textContent = report.notice;
    $('overfitMonths').textContent = report.monthsObserved;
    $('overfitAllPositive').textContent = report.agentsWithPositiveEveryMonth + ' / 12';
    $('overfitExitRisk').textContent = report.agentsWithUnresolvedExits;
    $('overfitSupported').textContent = report.agentsStatisticallySupported + ' / 12';
    $('overfitScreenRows').innerHTML = report.rows.map(a =>
      `<tr><td>${safe(a.symbol)}</td>` +
      `<td>${a.positiveMonths} / ${a.negativeMonths} / ${a.flatMonths}</td>` +
      `<td>${a.activeMonths}</td><td>${pct(a.worstMonthlyMeanPct)}</td>` +
      `<td>${a.unresolvedExits}</td><td>${Number(a.oneSidedSignP).toFixed(4)}</td>` +
      `<td>${Number(a.bonferroniAdjustedP).toFixed(4)}</td>` +
      `<td>${safe(a.evidenceStatus)}</td></tr>`).join('');
  } catch (error) {
    clearOverfitScreen();
    $('overfitScreenStatus').textContent = 'Falha de validação';
    $('overfitScreenNotice').textContent = error.message;
  } finally {
    $('overfitScreenRefresh').disabled = !authenticated;
  }
}
$('overfitScreenRefresh').onclick = refreshOverfitScreen;

function clearShadowResearch() {
  validatedShadowRows = [];
  $('researchShadowBars').textContent = 'Nenhuma comparação SHADOW validada.';
  $('researchShadowStatus').textContent = 'Aguardando relatório';
  $('researchShadowNotice').textContent = 'Sem consulta de pesquisa nesta sessão.';
  for (const id of ['researchShadowDays', 'researchShadowNoTrade',
                    'researchShadowUnresolved', 'researchShadowRejected'])
    $(id).textContent = '—';
  $('researchShadowRows').innerHTML = '<tr><td colspan="8">Sem dados de pesquisa carregados.</td></tr>';
}

async function refreshShadowResearch() {
  if (!authenticated) return;
  $('researchShadowRefresh').disabled = true;
  try {
    const report = await api('research/shadow-dashboard');
    if (!report.available) {
      clearShadowResearch();
      $('researchShadowStatus').textContent = 'Indisponível';
      $('researchShadowNotice').textContent = report.notice || 'Relatório não validado neste computador.';
      return;
    }
    if (report.mode !== 'EXIT_CAPACITY_GATED_SHADOW_ONLY' ||
        report.paperModified || report.orderPlacementEnabled ||
        report.promotionAllowed || report.agents !== 12 ||
        !Array.isArray(report.rows) || report.rows.length !== 12)
      throw new Error('Evidência SHADOW incompatível com os bloqueios de segurança.');
    $('researchShadowStatus').textContent = 'SHADOW ' + report.month + ' · somente leitura';
    $('researchShadowNotice').textContent = report.notice;
    $('researchShadowDays').textContent = report.totalAgentDays;
    $('researchShadowNoTrade').textContent = report.noTradeDays;
    $('researchShadowUnresolved').textContent = report.unresolvedExits;
    $('researchShadowRejected').textContent = report.rejectedAgents + ' / 12';
    validatedShadowRows = report.rows;
    renderShadowBars();
    $('researchShadowRows').innerHTML = report.rows.map(a =>
      `<tr><td>${safe(a.symbol)}</td><td>${safe(a.gate)}</td>` +
      `<td>${a.noTradeDays} / ${a.totalDays}</td>` +
      `<td>${a.hypotheticalClosedTrades}</td><td>${a.unresolvedExits}</td>` +
      `<td>${a.blockedEntryAttempts}</td><td>${a.partialEntryAttempts}</td>` +
      `<td>${pct(a.hypotheticalMeanNetPct)}</td></tr>`
    ).join('');
  } catch (error) {
    clearShadowResearch();
    $('researchShadowStatus').textContent = 'Falha na validação';
    $('researchShadowNotice').textContent = error.message;
  } finally {
    $('researchShadowRefresh').disabled = !authenticated;
  }
}
$('researchShadowRefresh').onclick = refreshShadowResearch;

async function loadSession() {
  try {
    const session = await api('auth/me');
    showSession(session);
    await refresh();
    await refreshHistory();
    await refreshMarketData();
    await refreshMarketCandles();
    await refreshRealStrategies();
    await refreshShadowResearch();
    await refreshThreeMonthResearch();
    await refreshOverfitScreen();
    await refreshSixMonthValidation();
    await refreshFrozenDecisions();
    await refreshRobustness();
    if (canReview) await refreshProductionPolicies();
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
    const configured = value => value === true ? 'habilitada na configuracao' : value === false ? 'desabilitada na configuracao' : 'configuracao indisponivel';
    $('testnetExecutionHomologation').textContent += ' | Ordens reais: '+configured(s.realOrdersConfigured)+
      ' | Ordens Testnet: '+configured(s.testnetOrdersConfigured)+
      ' | Envio efetivo: '+(s.submissionAuthorized ? 'autorizado' : 'bloqueado');
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

function renderPaperEquityTrend(history) {
  const target = $('paperEquityTrend');
  const values = (Array.isArray(history) ? history : []).map(x => Number(x.equityBrl))
    .filter(x => Number.isFinite(x) && x >= 0);
  if (!values.length) { target.textContent = 'Sem amostras de patrimonio.'; return; }
  const min = Math.min(...values), max = Math.max(...values);
  const span = Math.max(max - min, 0.01), width = 720, height = 128, pad = 12;
  const points = values.map((v, i) => {
    const x = pad + i * (width - 2 * pad) / Math.max(values.length - 1, 1);
    const y = height - pad - (v - min) / span * (height - 2 * pad);
    return x.toFixed(2) + ',' + y.toFixed(2);
  }).join(' ');
  target.textContent = '';
  const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
  svg.setAttribute('viewBox', '0 0 720 128');
  svg.setAttribute('width', '100%');
  svg.setAttribute('height', '128');
  svg.setAttribute('aria-label', 'Patrimonio virtual: minimo '+brl(min)+', maximo '+brl(max));
  const line = document.createElementNS('http://www.w3.org/2000/svg', 'polyline');
  line.setAttribute('points', points);
  line.setAttribute('fill', 'none');
  line.setAttribute('stroke', 'currentColor');
  line.setAttribute('stroke-width', '2.5');
  svg.appendChild(line);
  target.appendChild(svg);
  const caption = document.createElement('p');
  caption.textContent = values.length+' amostras • min '+brl(min)+' • max '+brl(max)+
    ' • atual '+brl(values[values.length - 1]);
  target.appendChild(caption);
}

function clearMonitoringDisplay() {
  $('paperMonitorStatus').textContent = 'Sem dados';
  $('paperMonitorPnl').textContent = '—';
  $('paperMonitorRealized').textContent = '—';
  $('paperMonitorFees').textContent = '—';
  $('paperMonitorDrawdown').textContent = '—';
  $('paperMonitorExposure').textContent = '—';
  $('paperMonitorWinRate').textContent = '—';
  $('paperEquityTrend').textContent = 'Aguardando amostras.';
  $('paperMonitorHealth').textContent = 'Aguardando dados de monitoramento.';
  $('paperAgentPerformance').innerHTML = '<tr><td colspan="13">Aguardando relatório.</td></tr>';
  $('paperHistoricalComparison').innerHTML = '<tr><td colspan="7">Aguardando dados historicos.</td></tr>';
  $('paperComparisonPeriod').textContent = 'Aguardando janelas comparativas.';
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
    $('paperMonitorExposure').textContent = brl(p.exposureBrl) + ' / ' + pct(p.exposurePct);
    $('paperMonitorWinRate').textContent = Number(p.closedPositions) > 0
      ? pct(p.winRatePct) + ' (' + Number(p.closedPositions) + ' vendas)'
      : 'Sem vendas fechadas';
    const healthyAgents = m.agentStates.filter(a => a.healthy === true);
    const unhealthySymbols = m.agentStates.filter(a => !a.healthy).map(a => a.symbol);
    $('paperMonitorHealth').textContent =
      'Motor: '+(m.cycleHealth?.status ?? 'INDEFINIDO')+
      ' • idade do último ciclo: '+(Number.isFinite(m.cycleHealth?.lastCycleAgeSeconds)
        ? Math.round(m.cycleHealth.lastCycleAgeSeconds)+'s' : 'sem ciclo')+
      ' • Agentes com candle recente: '+healthyAgents.length+
      '/12'+(unhealthySymbols.length ? ' • verificar: '+unhealthySymbols.join(', ') : '')+
      ' • último ciclo: '+(p.paperCycleUtc ? formatDate(p.paperCycleUtc) : 'nenhum')+
      ' • monitor: '+(p.latestMonitorUtc ? formatDate(p.latestMonitorUtc) : 'ainda não coletado')+
      ' • posições abertas: '+p.openPositions+' • fills virtuais: '+p.virtualFills+
      ' • operações fechadas: '+p.closedPositions+'.';
    $('paperAgentPerformance').innerHTML = Array.isArray(m.agentPerformance) && m.agentPerformance.length === 12
      ? m.agentPerformance.map(a => '<tr><td>'+safe(a.symbol)+'</td><td>'+safe(a.status)+' / '+safe(a.lastSignal)+
        '</td><td title="'+safe(a.diagnosticReason)+'">'+safe(a.diagnosticStatus)+
        ' / '+(Number.isFinite(a.candleAgeSeconds) ? Math.round(a.candleAgeSeconds)+'s' : 'sem candle')+
        '</td><td>'+(a.lastDecisionUtc ? safe(formatDate(a.lastDecisionUtc)) : 'sem decisao')+
        '</td><td>'+Number(a.decisions)+'</td><td>'+Number(a.buys)+'</td><td>'+Number(a.sells)+
        '</td><td>'+Number(a.wins)+' / '+Number(a.losses)+' / '+Number(a.breakevenSells)+
        '</td><td>'+(Number(a.sells) ? pct(a.winRatePct) : '—')+'</td><td>'+brl(a.realizedPnlBrl)+
        '</td><td>'+brl(a.feesBrl)+'</td><td>'+brl(a.openPositionBrl)+
        '</td><td>'+pct(a.exposurePct)+'</td></tr>').join('')
      : '<tr><td colspan="13">Relatório de agentes indisponível.</td></tr>';
    const cw = m.comparisonWindow;
    $('paperComparisonPeriod').textContent = cw
      ? 'Anterior: '+formatDate(cw.previousStartUtc)+' ate '+formatDate(cw.currentStartUtc)+
        ' | Atual: '+formatDate(cw.currentStartUtc)+' ate '+formatDate(cw.endUtc)+' (UTC, periodos moveis de 7 dias)'
      : 'Janelas comparativas indisponiveis.';
    $('paperHistoricalComparison').innerHTML = Array.isArray(m.historicalComparison) && m.historicalComparison.length === 12
      ? m.historicalComparison.map(x => '<tr><td>'+safe(x.symbol)+'</td><td>'+Number(x.currentBuys)+' / '+Number(x.previousBuys)+
        '</td><td>'+Number(x.currentSells)+' / '+Number(x.previousSells)+
        '</td><td>'+(Number(x.currentSells) ? pct(x.currentWinRatePct) : '—')+' / '+
        (Number(x.previousSells) ? pct(x.previousWinRatePct) : '—')+
        '</td><td>'+brl(x.currentRealizedPnlBrl)+' / '+brl(x.previousRealizedPnlBrl)+
        '</td><td>'+brl(x.realizedPnlChangeBrl)+
        '</td><td>'+brl(x.currentFeesBrl)+' / '+brl(x.previousFeesBrl)+'</td></tr>').join('')
      : '<tr><td colspan="7">Comparacao historica indisponivel.</td></tr>';
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
    renderPaperEquityTrend(m.history);
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
$('runExtendedHistory').onclick = async () => {
  if (!authenticated) return;
  $('runExtendedHistory').disabled = true;
  $('extendedHistoryStatus').textContent = 'Consultando ate 48h por par e avaliando janelas SHADOW...';
  $('extendedHistoryRows').innerHTML = '<tr><td colspan="9">Calculando...</td></tr>';
  try {
    const r = await api('strategy/extended-history');
    if (r.mode !== 'EXTENDED_HISTORY_48H_SHADOW_ONLY' || r.count !== 12 ||
        r.historyPersisted || r.paperModified || r.riskLimitsModified ||
        r.orderPlacementEnabled || r.promotionAllowed ||
        !Array.isArray(r.agents) || r.agents.length !== 12)
      throw new Error('Historico ampliado fora do modo SHADOW seguro.');
    const completed = r.agents.filter(x => x.status === 'COMPLETE');
    const reviewed = completed.filter(x => x.researchGate === 'RESEARCH_REVIEW_ONLY');
    $('extendedHistoryStatus').textContent = completed.length+'/12 pares com 48h completas; '+
      reviewed.length+' marcados apenas para revisao de pesquisa. Nenhum par esta autorizado a operar.';
    $('extendedHistoryRows').innerHTML = r.agents.map(x =>
      '<tr><td>'+safe(x.symbol)+'</td><td>'+Number(x.coveredUtcDays)+
      '</td><td>'+Number(x.foldCount)+'</td><td>'+Number(x.selectedTrades)+
      '</td><td>'+Number(x.positiveHoldouts)+
      '</td><td>'+(x.status === 'COMPLETE' ? pct(x.averageBaselineReturnPct) : '-')+
      '</td><td>'+(x.status === 'COMPLETE' ? pct(x.averageSelectedReturnPct) : '-')+
      '</td><td>'+(x.status === 'COMPLETE' ? pct(x.worstSelectedDrawdownPct) : '-')+
      '</td><td>'+safe(x.researchGate)+'</td></tr>').join('');
  } catch(error) {
    $('extendedHistoryStatus').textContent = 'Pesquisa ampliada indisponivel: '+error.message;
    $('extendedHistoryRows').innerHTML = '<tr><td colspan="9">Dados indisponiveis.</td></tr>';
  } finally { $('runExtendedHistory').disabled = false; }
};

$('runWalkForward').onclick = async () => {
  if (!authenticated) return;
  $('runWalkForward').disabled = true;
  $('walkForwardStatus').textContent = 'Consultando candles publicos e analisando janelas cronologicas...';
  $('walkForwardRows').innerHTML = '<tr><td colspan="8">Calculando...</td></tr>';
  $('profitabilityStatus').textContent = 'Calculando custos hipoteticos, entradas e stops...';
  $('profitabilityRows').innerHTML = '<tr><td colspan="9">Calculando...</td></tr>';
  $('entryFilterStatus').textContent = 'Comparando filtros somente no modo SHADOW...';
  $('entryFilterRows').innerHTML = '<tr><td colspan="8">Calculando...</td></tr>';
  $('tradeHorizonStatus').textContent = 'Simulando operacoes completas em modo SHADOW...';
  $('tradeHorizonRows').innerHTML = '<tr><td colspan="10">Calculando...</td></tr>';
  try {
    const r = await api('strategy/walk-forward');
    if (r.mode !== 'WALK_FORWARD_RESEARCH_ONLY' || r.orderPlacementEnabled ||
        r.paperModified || r.promotionAllowed || r.riskLimitsModified ||
        r.historyPersisted || r.count !== 12 || !Array.isArray(r.agents) ||
        r.agents.length !== 12)
      throw new Error('Relatorio walk-forward fora do modo seguro.');
    const done = r.agents.filter(x => x.status === 'COMPLETE');
    const diagnostic = r.profitabilityDiagnostics;
    if (!diagnostic || diagnostic.mode !== 'READ_ONLY_SIGNAL_COST_DIAGNOSTICS' ||
        diagnostic.paperModified || diagnostic.orderPlacementEnabled ||
        diagnostic.promotionAllowed || diagnostic.count !== 12 ||
        !Array.isArray(diagnostic.agents) || diagnostic.agents.length !== 12)
      throw new Error('Diagnostico de rentabilidade fora do modo somente leitura.');
    const filters = r.entryFilterResearch;
    if (!filters || filters.mode !== 'ENTRY_FILTER_WALK_FORWARD_SHADOW_ONLY' ||
        filters.paperModified || filters.historyPersisted ||
        filters.orderPlacementEnabled || filters.promotionAllowed ||
        filters.riskLimitsModified || filters.count !== 12 ||
        !Array.isArray(filters.agents) || filters.agents.length !== 12)
      throw new Error('Filtros de entrada fora do modo exclusivamente experimental.');
    const trades = r.tradeHorizonResearch;
    if (!trades || trades.mode !== 'FULL_TRADE_HORIZON_SHADOW_ONLY' ||
        trades.paperModified || trades.historyPersisted ||
        trades.orderPlacementEnabled || trades.promotionAllowed ||
        trades.riskLimitsModified || trades.count !== 12 ||
        !Array.isArray(trades.agents) || trades.agents.length !== 12)
      throw new Error('Operacoes completas fora do modo SHADOW seguro.');
    $('tradeHorizonStatus').textContent = 'Comparacao SHADOW de operacoes completas: horizontes '+
      (trades.horizonsMinutes || []).map(Number).join(', ')+' minutos. Sem promocao automatica.';
    $('tradeHorizonRows').innerHTML = trades.agents.map(x =>
      '<tr><td>'+safe(x.symbol)+'</td><td>'+Number(x.foldCount)+
      '</td><td>'+Number(x.selectedNonBaselineFolds)+
      '</td><td>'+Number(x.positiveSelectedHoldouts)+
      '</td><td>'+Number(x.completedSelectedTrades)+
      '</td><td>'+Number(x.selectedStops)+
      '</td><td>'+(x.status === 'COMPLETE' ? pct(x.meanBaselineHoldoutReturnPct) : '-')+
      '</td><td>'+(x.status === 'COMPLETE' ? pct(x.meanSelectedHoldoutReturnPct) : '-')+
      '</td><td>'+(x.status === 'COMPLETE' ? pct(x.maxSelectedHoldoutDrawdownPct) : '-')+
      '</td><td>'+safe(x.status)+'</td></tr>').join('');
    $('entryFilterStatus').textContent = 'Comparativo de filtros em 12 agentes, treino sem acesso ao teste. Movimento de apenas um candle; NAO sao lucros de operacoes.';
    $('entryFilterRows').innerHTML = filters.agents.map(x =>
      '<tr><td>'+safe(x.symbol)+'</td><td>'+Number(x.foldCount)+
      '</td><td>'+Number(x.filteredSelectionFolds)+
      '</td><td>'+Number(x.holdoutsWithSelectedSignals)+
      '</td><td>'+Number(x.totalBaselineHoldoutSignals)+' / '+Number(x.totalSelectedHoldoutSignals)+
      '</td><td>'+(x.weightedBaselineHoldoutMeanNetPct === null ? '-' : pct(x.weightedBaselineHoldoutMeanNetPct))+
      '</td><td>'+(x.weightedSelectedHoldoutMeanNetPct === null ? '-' : pct(x.weightedSelectedHoldoutMeanNetPct))+
      '</td><td>'+safe(x.status)+'</td></tr>').join('');
    $('profitabilityStatus').textContent = 'Custo hipotetico de compra e venda: '+
      pct(diagnostic.roundTripAssumedCostPct)+
      ' • sinais por candle nao sao trades nem lucro realizado.';
    $('profitabilityRows').innerHTML = diagnostic.agents.map(x =>
      '<tr><td>'+safe(x.symbol)+'</td><td>'+Number(x.buySignalCandles)+
      '</td><td>'+Number(x.positiveNextCandleMoves)+'/'+Number(x.buyWithNextCandle)+
      '</td><td>'+Number(x.nextCandleMovesAboveRoundTripCost)+
      '</td><td>'+(x.meanNextCandleGrossMovePct === null ? '-' : pct(x.meanNextCandleGrossMovePct))+
      '</td><td>'+(x.meanNextCandleNetIfRoundTripPct === null ? '-' : pct(x.meanNextCandleNetIfRoundTripPct))+
      '</td><td>'+Number(x.lowAtrBuySignals)+' / '+Number(x.highAtrBuySignals)+
      '</td><td>'+Number(x.historicalHoldoutStopsBaseline)+' / '+Number(x.historicalHoldoutStopsSelected)+
      '</td><td>'+safe(x.status)+'</td></tr>').join('');
    $('walkForwardStatus').textContent = done.length+'/12 agentes com janelas completas; treino '+
      Number(r.trainingCandles)+' candles, teste '+Number(r.testCandles)+
      ' candles; historico externo sem persistencia. Nao aprova nenhuma estrategia.';
    $('walkForwardRows').innerHTML = r.agents.map(x =>
      '<tr><td>'+safe(x.symbol)+'</td><td>'+Number(x.candleCount)+
      '</td><td>'+Number(x.foldCount)+'</td><td>'+Number(x.selectedCooldownFolds)+
      '</td><td>'+Number(x.positiveSelectedHoldouts)+
      '</td><td>'+(x.status === 'COMPLETE' ? pct(x.averageHoldoutBaselinePct) : '-')+
      '</td><td>'+(x.status === 'COMPLETE' ? pct(x.averageHoldoutSelectedPct) : '-')+
      '</td><td>'+safe(x.status)+'</td></tr>').join('');
  } catch(error) {
    $('walkForwardStatus').textContent = 'Walk-forward indisponivel: '+error.message;
    $('walkForwardRows').innerHTML = '<tr><td colspan="8">Dados indisponiveis.</td></tr>';
    $('profitabilityStatus').textContent = 'Diagnostico indisponivel: '+error.message;
    $('profitabilityRows').innerHTML = '<tr><td colspan="9">Dados indisponiveis.</td></tr>';
    $('entryFilterStatus').textContent = 'Filtros indisponiveis: '+error.message;
    $('entryFilterRows').innerHTML = '<tr><td colspan="8">Dados indisponiveis.</td></tr>';
    $('tradeHorizonStatus').textContent = 'Horizontes indisponiveis: '+error.message;
    $('tradeHorizonRows').innerHTML = '<tr><td colspan="10">Dados indisponiveis.</td></tr>';
  } finally { $('runWalkForward').disabled = false; }
};

$('runShadowComparison').onclick = async () => {
  if (!authenticated) return;
  $('runShadowComparison').disabled = true;
  $('shadowComparisonStatus').textContent = 'Comparando 12 agentes em duas janelas historicas...';
  $('shadowComparisonRows').innerHTML = '<tr><td colspan="6">Calculando...</td></tr>';
  try {
    const r = await api('strategy/shadow-comparison');
    if (r.mode !== 'SHADOW_COMPARISON_ONLY' || r.orderPlacementEnabled ||
        r.paperModified || r.promotionAllowed || r.count !== 12 ||
        !Array.isArray(r.agents) || r.agents.length !== 12)
      throw new Error('Comparacao fora do modo exclusivamente observacional.');
    $('shadowComparisonStatus').textContent = r.agents.filter(x => x.status === 'COMPLETE').length+
      '/12 agentes com historico suficiente. Sao cenarios ilustrativos, sem alteracao do PAPER.';
    $('shadowComparisonRows').innerHTML = r.agents.map(x =>
      '<tr><td>'+safe(x.symbol)+'</td><td>'+safe(x.status)+'</td><td>'+
      (x.status === 'COMPLETE' ? pct(x.earlierBaseline.returnPct)+' / '+pct(x.earlierCooldown.returnPct) : '-')+
      '</td><td>'+
      (x.status === 'COMPLETE' ? pct(x.laterBaseline.returnPct)+' / '+pct(x.laterCooldown.returnPct) : '-')+
      '</td><td>'+Number(x.laterBaseline.atrStops)+' / '+Number(x.laterCooldown.atrStops)+
      '</td><td>'+Number(x.laterCooldown.skippedReentries)+'</td></tr>').join('');
  } catch(error) {
    $('shadowComparisonStatus').textContent = 'Comparacao indisponivel: '+error.message;
    $('shadowComparisonRows').innerHTML = '<tr><td colspan="6">Dados indisponiveis.</td></tr>';
  } finally { $('runShadowComparison').disabled = false; }
};

$('runStrategyValidation').onclick = async () => {
  if (!authenticated) return;
  $('runStrategyValidation').disabled = true;
  $('strategyValidationStatus').textContent = 'Validando os 12 agentes com candles publicos persistidos...';
  $('strategyValidationRows').innerHTML = '<tr><td colspan="11">Calculando...</td></tr>';
  $('strategyRiskReviewStatus').textContent = 'Consultando bloqueio e causas de perdas...';
  $('strategyRiskExitRows').innerHTML = '<tr><td colspan="5">Consultando...</td></tr>';
  $('strategyRiskPositionRows').innerHTML = '<tr><td colspan="6">Consultando...</td></tr>';
  try {
    const r = await api('strategy/validation');
    if (r.mode !== 'READ_ONLY_VALIDATION' || r.source !== 'BINANCE_SPOT_PUBLIC' ||
        r.orderPlacementEnabled || r.simulationModified || r.count !== 12 ||
        !Array.isArray(r.agents) || r.agents.length !== 12)
      throw new Error('Relatorio fora do modo seguro de validacao.');
    $('strategyValidationStatus').textContent = r.agents.filter(x => x.backtest.status === 'COMPLETE').length+
      '/12 com backtest concluido • '+safe(r.strategyVersion)+' • custo assumido: taxa '+
      pct(r.assumptions.feePctPerSide)+', slippage '+pct(r.assumptions.slippagePctPerSide)+
      ' e meio spread '+pct(r.assumptions.halfSpreadPctPerSide)+
      ' por lado. Apenas in-sample, sem promessa de retorno.';
    const risk = r.riskReview;
    if (!risk || risk.financialLimitsChanged || !Array.isArray(risk.exitReasons) ||
        !Array.isArray(risk.openPositions)) throw new Error('Diagnostico financeiro incompleto.');
    $('strategyRiskReviewStatus').textContent =
      'PAPER: '+(risk.running ? 'EM EXECUCAO' : 'PAUSADO')+
      ' • Bloqueio de risco: '+(risk.riskLocked ? 'ATIVO' : 'INATIVO')+
      ' • Perda diaria: '+(risk.dailyLossPct === null ? 'sem referencia valida' : pct(risk.dailyLossPct))+
      ' / limite '+pct(risk.dailyLossLimitPct)+
      ' • Vendas com prejuizo: '+Number(risk.totalLosingSells)+'/'+Number(risk.totalClosedSells)+
      ' • Motivo: '+safe(risk.riskReason || 'nenhum')+' • Revisao somente leitura.';
    $('strategyRiskExitRows').innerHTML = risk.exitReasons.length
      ? risk.exitReasons.map(x => '<tr><td>'+safe(x.reason)+'</td><td>'+Number(x.sells)+
        '</td><td>'+Number(x.wins)+'</td><td>'+Number(x.losses)+
        '</td><td>'+brl(x.realizedPnlBrl)+'</td></tr>').join('')
      : '<tr><td colspan="5">Nenhuma venda fechada.</td></tr>';
    $('strategyRiskPositionRows').innerHTML = risk.openPositions.length
      ? risk.openPositions.map(x => '<tr><td>'+safe(x.symbol)+'</td><td>'+decimal(x.quantity)+
        '</td><td>'+brl(x.entryPriceBrl)+'</td><td>'+brl(x.lastPriceBrl)+
        '</td><td>'+brl(x.stopPriceBrl)+'</td><td>'+brl(x.unrealizedPnlBeforeExitFeesBrl)+
        '</td></tr>').join('')
      : '<tr><td colspan="6">Nenhuma posicao aberta.</td></tr>';
    $('strategyValidationRows').innerHTML = r.agents.map(x =>
      '<tr><td>'+safe(x.symbol)+'</td><td>'+Number(x.backtest.candleCount)+
      '</td><td>'+Number(x.backtest.trades)+'</td><td>'+
      (x.backtest.status === 'COMPLETE' ? pct(x.backtest.returnPct) : 'indisponivel')+
      '</td><td>'+(x.backtest.status === 'COMPLETE' ? pct(x.backtest.maxDrawdownPct) : 'indisponivel')+
      '</td><td>'+Number(x.backtest.winningTrades)+' / '+Number(x.backtest.losingTrades)+
      '</td><td>'+Number(x.backtest.maxConsecutiveLosses)+
      '</td><td>'+pct(x.backtest.feePctPerSide)+' / '+pct(x.backtest.slippagePctPerSide)+' / '+pct(x.backtest.halfSpreadPctPerSide)+
      '</td><td>'+Number(x.paper.sells)+'</td><td>'+brl(x.paper.realizedPnlBrl)+
      '</td><td title="'+safe(x.backtest.note || '')+'">'+safe(x.verdict)+'</td></tr>').join('');
  } catch(error) {
    $('strategyValidationStatus').textContent = 'Validacao indisponivel: '+error.message;
    $('strategyValidationRows').innerHTML = '<tr><td colspan="11">Dados indisponiveis.</td></tr>';
    $('strategyRiskReviewStatus').textContent = 'Revisao indisponivel: '+error.message;
    $('strategyRiskExitRows').innerHTML = '<tr><td colspan="5">Dados indisponiveis.</td></tr>';
    $('strategyRiskPositionRows').innerHTML = '<tr><td colspan="6">Dados indisponiveis.</td></tr>';
  } finally { $('runStrategyValidation').disabled = false; }
};

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
        '% • meio spread por lado: '+result.halfSpreadPctPerSide+
        '% • vitorias/derrotas: '+result.winningTrades+'/'+result.losingTrades+
        ' • maximo de perdas seguidas: '+result.maxConsecutiveLosses+
        '. Não é promessa de rentabilidade.';
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
