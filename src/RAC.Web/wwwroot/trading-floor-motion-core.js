'use strict';
/* Pure UI navigation rules. Does not control trading, risk, or API state. */
(function (root) {
  function normalized(value) {
    return String(value ?? '').normalize('NFD').replace(/[\u0300-\u036f]/g, '').toUpperCase();
  }
  const deskXs = [37.5, 46, 58, 66.5, 79, 89];
  function desk(index) {
    const n = Math.max(0, Math.min(11, Math.trunc(index)));
    return {x: deskXs[n % 6], y: n < 6 ? 43 : 63};
  }
  const loungeSeats = [{x:7,y:68},{x:10,y:64},{x:14,y:60},{x:18,y:59},{x:26,y:67},{x:5,y:79},{x:12,y:85},{x:18,y:85},{x:24,y:83},{x:29,y:79},{x:30,y:73},{x:30,y:62}];
  function lounge(index) {
    const n = Math.max(0, Math.min(11, Math.trunc(index)));
    return loungeSeats[n];
  }
  const leaderCoords = Object.freeze({
    Ana: {x: 41, y: 84}, Rebeca: {x: 63, y: 84}, Carlos: {x: 85, y: 84}
  });
  function zoneFor(agent, state) {
    const signal = normalized(agent?.signal);
    if (signal === 'COMPRA' || signal === 'VENDA') return 'desk';
    if (signal === 'AGUARDAR') return 'lounge';
    const s = normalized(agent?.status);
    if (state?.riskLocked || state?.running === false) return 'lounge';
    if (/AGUARD|ARMAD|ESPER|DESCANS|PAUS|INATIV|BLOQUE|SEM_SINAL/.test(s)) return 'lounge';
    return 'desk';
  }
  function route(from, to, destZone) {
    const exit = p => p.x < 32 ? {x:32,y:p.y} :
      {x:p.x,y:p.y < 46 ? 44 : p.y < 66 ? 63 : 73};
    const a = exit(from), b = exit(to);
    const points = [from,a,{x:32,y:a.y},{x:32,y:b.y},b,to];
    return points.filter((p,i) => !i || p.x !== points[i-1].x || p.y !== points[i-1].y);
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
