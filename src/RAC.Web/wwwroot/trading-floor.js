'use strict';

/* All figures come from the authenticated simulation API.
   This module only updates DOM labels; it cannot place orders. */
const FLOOR_STATUS_KEYWORDS = Object.freeze({
  paused: ['BLOQUE', 'PAUSA', 'DESCANS', 'INATIV'],
  operating: ['OPERAC', 'EXECUT'],
  waiting: ['AGUARD', 'ARMAD', 'ESPERA']
});
const FLOOR_COUNTER_IDS = Object.freeze({
  analyzing: 'officeCountAnalyzing',
  operating: 'officeCountOperating',
  waiting: 'officeCountWaiting',
  paused: 'officeCountPaused'
});

function floorBucketFor(status) {
  const normalized = String(status ?? '').normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '').toUpperCase();
  for (const [bucket, words] of Object.entries(FLOOR_STATUS_KEYWORDS)) {
    if (words.some(word => normalized.includes(word))) return bucket;
  }
  return 'analyzing';
}

function floorCountAgents(agents) {
  const counts = { analyzing: 0, operating: 0, waiting: 0, paused: 0 };
  for (const agent of agents) counts[floorBucketFor(agent.status)] += 1;
  return counts;
}

function floorUpdateCounts(agents, supervisors, state) {
  const byId = id => document.getElementById(id);
  const counts = state.riskLocked || state.running === false ? {analyzing:0,operating:0,waiting:0,paused:agents.length} : floorCountAgents(agents);
  for (const [bucket, id] of Object.entries(FLOOR_COUNTER_IDS)) {
    byId(id).textContent = counts[bucket];
  }
  byId('officeCountInfo').textContent =
    `${agents.length} agentes monitorados · ${supervisors.length} apoios`;
  const lounge = agents.filter(a => window.RacMotionCore ? window.RacMotionCore.zoneFor(a,state)==='lounge' : state.riskLocked || state.running===false || ['paused','waiting'].includes(floorBucketFor(a.status))).length;
  byId('officeLoungeCount').textContent = lounge + ' agente(s) na area de descanso (estado operacional).';
}

function floorUpdatePerformance(state) {
  const label = document.getElementById('officeDayResult');
  const value = typeof state.dailyPnl === 'number' ? state.dailyPnl : NaN;
  const hasValue = Number.isFinite(value);
  label.textContent = hasValue
    ? new Intl.NumberFormat('pt-BR', {
      style: 'currency', currency: 'BRL'
    }).format(value)
    : '—';
  label.classList.toggle('positive', hasValue && value > 0);
  label.classList.toggle('negative', hasValue && value < 0);
  document.getElementById('officeDayCaption').textContent =
    'Resultado virtual do dia · não é saldo Binance';
}

function floorUpdateRisk(state) {
  const active = Boolean(state.running) && !state.riskLocked;
  document.getElementById('officeStageState').textContent =
    state.riskLocked ? 'RISCO BLOQUEADO' :
    active ? 'PAPER EM EXECUÇÃO' : 'PAPER PAUSADO';
  document.querySelector('.office-map')
    ?.setAttribute('data-paper-running', String(active));
}

window.updateTradingFloorHud = function updateTradingFloorHud(state) {
  if (!state || typeof state !== 'object') return;
  const agents = Array.isArray(state.agents) ? state.agents : [];
  const supervisors = Array.isArray(state.supervisors) ? state.supervisors : [];
  floorUpdateCounts(agents, supervisors, state);
  floorUpdatePerformance(state);
  floorUpdateRisk(state);
  window.RacAgentMotion?.update(state);
};
