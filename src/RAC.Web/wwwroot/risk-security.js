'use strict';
// Read-only endpoint: no forms, mutations, trading controls, or credentials.
const el = id => document.getElementById(id);
function clear() {
  for (const name of ['status','paper','risk','testnet','pending','incidents'])
    el(name).textContent = '—';
  el('alerts').replaceChildren();
}
async function refresh() {
  el('refresh').disabled = true;
  clear();
  try {
    const response = await fetch('/api/risk/safety-monitoring',
      {credentials:'same-origin',cache:'no-store'});
    if (response.status === 401) throw new Error('Sessão necessária. Faça login no RAC.');
    if (response.status === 403) throw new Error('Perfil sem permissão de revisão de risco.');
    if (!response.ok) throw new Error('Diagnóstico indisponível. Consulte os logs do serviço.');
    const report = await response.json();
    if (report.mode !== 'READ_ONLY_SAFETY_DIAGNOSTICS' ||
        report.realOrdersEnabled !== false ||
        !['OK','ATTENTION','CRITICAL'].includes(report.status) ||
        !Array.isArray(report.incidents))
      throw new Error('Resposta incompatível: informações de risco não confiáveis.');
    el('status').textContent = report.status;
    el('status').className = report.status === 'CRITICAL' ? 'critical' :
      report.status === 'ATTENTION' ? 'warning' : 'ok';
    el('paper').textContent = report.paperRunning ? 'Executando' : 'Pausado';
    el('risk').textContent = report.paperRiskLocked ? 'Ativo' : 'Inativo';
    el('testnet').textContent = report.testnetEnabled ? 'HABILITADA' : 'Desabilitada';
    el('pending').textContent = String(report.pendingReviews);
    el('incidents').textContent = String(report.incidents.length);
    for (const incident of report.incidents) {
      const tr = document.createElement('tr');
      for (const value of [incident.code, incident.severity, incident.message]) {
        const td = document.createElement('td');
        td.textContent = String(value ?? '—');
        tr.appendChild(td);
      }
      el('alerts').appendChild(tr);
    }
    if (report.incidents.length === 0) {
      const tr = document.createElement('tr');
      const td = document.createElement('td');
      td.colSpan = 3; td.textContent = 'Sem incidentes detectados na consulta.';
      tr.appendChild(td); el('alerts').appendChild(tr);
    }
    el('notice').textContent = 'Diagnóstico consultado em ' +
      new Date(report.observedUtc).toLocaleString('pt-BR') +
      '. Consulta somente leitura. Operações reais continuam bloqueadas.';
  } catch (error) {
    clear();
    el('notice').textContent = error.message;
  } finally { el('refresh').disabled = false; }
}
el('refresh').addEventListener('click', refresh);
refresh();
