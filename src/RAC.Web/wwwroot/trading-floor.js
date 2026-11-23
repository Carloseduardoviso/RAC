'use strict';
/* Trading Floor presentation only. All values are returned by /api/state.
 * No timers are used to fabricate P/L, activities or trading signals. */
window.updateTradingFloorHud = function updateTradingFloorHud(state) {
  const byId = id => document.getElementById(id);
  const agents = Array.isArray(state.agents) ? state.agents : [];
  const active = Boolean(state.running) && !state.riskLocked;
  const counts = {analyzing:0, operating:0, waiting:0, paused:0};
  for (const agent of agents) {
    const status = String(agent.status ?? '').normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '').toUpperCase();
    const isBlocked = status.includes('BLOQUE') || status.includes('PAUSA') ||
      status.includes('DESCANS') || status.includes('INATIV');
    const isOperating = status.includes('OPERAC') || status.includes('EXECUT');
    const isWaiting = status.includes('AGUARD') || status.includes('ARMAD') ||
      status.includes('ESPERA');
    const bucket = isBlocked ? 'paused' : isOperating ? 'operating' :
      isWaiting ? 'waiting' : 'analyzing';
    counts[bucket] += 1;
  }
  const summaryIds = {
    analyzing:'officeCountAnalyzing', operating:'officeCountOperating',
    waiting:'officeCountWaiting', paused:'officeCountPaused'
  };
  for (const [key,id] of Object.entries(summaryIds)) byId(id).textContent = counts[key];
  const support = Array.isArray(state.supervisors) ? state.supervisors.length : 0;
  byId('officeCountInfo').textContent = `${agents.length} agentes monitorados · ${support} apoios`;
  byId('officeLoungeCount').textContent = counts.paused
    ? `${counts.paused} agente(s) em pausa ou bloqueio.`
    : 'Nenhum agente sinalizou pausa ou bloqueio.';
  byId('officeStageState').textContent = state.riskLocked ? 'RISCO BLOQUEADO' :
    active ? 'PAPER EM EXECUÇÃO' : 'PAPER PAUSADO';
  const day = Number(state.dailyPnl);
  byId('officeDayResult').textContent = Number.isFinite(day)
    ? new Intl.NumberFormat('pt-BR',{style:'currency',currency:'BRL'}).format(day) : '—';
  byId('officeDayResult').classList.toggle('positive', Number.isFinite(day) && day > 0);
  byId('officeDayResult').classList.toggle('negative', Number.isFinite(day) && day < 0);
  byId('officeDayCaption').textContent = 'Resultado virtual do dia · não é saldo Binance';
  document.querySelector('.office-map')?.setAttribute('data-paper-running',String(active));
};
