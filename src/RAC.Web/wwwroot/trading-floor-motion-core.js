'use strict';
/* Pure UI navigation rules. Does not control trading, risk, or API state. */
(function (root) {
  function normalized(value) {
    return String(value ?? '').normalize('NFD').replace(/[\u0300-\u036f]/g, '').toUpperCase();
  }
  const deskXs = [37, 45.5, 58, 66.2, 78.2, 87.3];
  function desk(index) {
    const n = Math.max(0, Math.min(11, Math.trunc(index)));
    return {x: deskXs[n % 6], y: n < 6 ? 41 : 60};
  }
  function lounge(index) {
    const n = Math.max(0, Math.min(11, Math.trunc(index)));
    return {x: 8 + (n % 4) * 6.4, y: 54 + Math.floor(n / 4) * 10};
  }
  const leaderCoords = Object.freeze({
    Ana: {x: 40, y: 85}, Rebeca: {x: 61, y: 85}, Carlos: {x: 84, y: 85}
  });
  function zoneFor(agent, state) {
    const s = normalized(agent?.status);
    if (state?.riskLocked || state?.running === false) return 'lounge';
    if (/AGUARD|ESPER|DESCANS|PAUS|INATIV|BLOQUE|SEM_SINAL/.test(s)) return 'lounge';
    return 'desk';
  }
  function route(from, to, destZone) {
    // Keep walkers in the corridor instead of crossing through desk monitors.
    const corridor = destZone === 'lounge' ? 72 : 69;
    return [{x: from.x, y: from.y},
      {x: from.x, y: Math.max(from.y, corridor)},
      {x: to.x, y: Math.max(to.y, corridor)},
      {x: to.x, y: to.y}];
  }
  function requestedLeader(agent) {
    const raw = agent?.supervisorRequest || agent?.requestedSupervisor;
    if (!raw && agent?.needsSupervisor !== true) return null;
    const preferred = typeof raw === 'string' ? raw : raw?.leader;
    if (Object.hasOwn(leaderCoords, preferred)) return preferred;
    const status = normalized(agent?.status);
    return /RISCO|BLOQUE/.test(status) ? 'Carlos' : 'Ana';
  }
  const api = Object.freeze({normalized, desk, lounge, leaderCoords, zoneFor,
    route, requestedLeader});
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
  if (root) root.RacMotionCore = api;
})(typeof window === 'undefined' ? null : window);
