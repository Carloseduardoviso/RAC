'use strict';
const field=id=>document.getElementById(id);
const formatNumber=value=>typeof value==='number' && Number.isFinite(value)
  ? value.toLocaleString('pt-BR',{maximumFractionDigits:8}) : 'Não disponível';
const dateValue=value=>value?new Date(value).toLocaleString('pt-BR'):'Não disponível';
function text(id,value){field(id).textContent=String(value);}
function fillRows(id,rows,columns){
  const target=field(id);target.replaceChildren();
  for(const row of rows){
    const tr=document.createElement('tr');
    for(const value of columns(row)){
      const td=document.createElement('td');
      td.textContent=String(value??'—');
      tr.appendChild(td);
    }
    target.appendChild(tr);
  }
  if(!rows.length){
    const tr=document.createElement('tr'), td=document.createElement('td');
    td.colSpan=id==='positions'?6:3;td.textContent='Nenhum registro.';
    tr.appendChild(td);target.appendChild(tr);
  }
}
function clear(){
 for(const id of ['reviewState','paperState','lockState','approval','cycle',
 'valuation','cash','equity','daily','drawdown','fills','testnet','reason'])
   text(id,'—');
 text('required','Revisão humana obrigatória.');
 field('positions').replaceChildren();field('events').replaceChildren();
}
async function refresh(){
 field('refresh').disabled=true;clear();
 try{
  const response=await fetch('/api/paper/risk-review',
    {credentials:'same-origin',cache:'no-store'});
  if(response.status===401) throw new Error('Entre no RAC antes de consultar.');
  if(response.status===403) throw new Error('Necessário perfil Administrator ou RiskReviewer.');
  if(!response.ok) throw new Error('Relatório indisponível; não autorize retomada.');
  const data=await response.json();
  if(data.mode!=='READ_ONLY_PAPER_RISK_REVIEW' ||
    data.reviewApproved!==false || data.unlockPermitted!==false ||
    data.realOrdersEnabled!==false || data.testnetOrdersEnabled!==false ||
    !Array.isArray(data.positions) || !Array.isArray(data.incidents))
    throw new Error('Resposta de risco inválida; liberação proibida.');
  text('reviewState',data.status);
  text('paperState',data.paperRunning?'Em execução':'Pausado');
  text('lockState',data.paperRiskLocked?'ATIVA':'Inativa');
  text('approval','NÃO AUTORIZADA');
  text('cycle',dateValue(data.lastPaperCycleUtc));
  text('valuation',data.valuationStale?'Obsoleta':'Último ciclo recente');
  text('cash','R$ '+formatNumber(data.cashBrl));
  text('equity','R$ '+formatNumber(data.lastRecordedEquityBrl));
  text('daily',data.dailyLossPercent==null?'Não calculável':formatNumber(data.dailyLossPercent)+'%');
  text('drawdown',data.drawdownPercent==null?'Não calculável':formatNumber(data.drawdownPercent)+'%');
  text('fills',formatNumber(data.fills));
  text('testnet',formatNumber(data.testnetOrders)+(data.testnetEnabled?' — HABILITADA':''));
  text('reason',data.riskReason||'Não registrado');
  text('required',data.requiredNextStep);
  fillRows('positions',data.positions,p=>[p.symbol,formatNumber(p.quantity),
    formatNumber(p.entryPriceBrl),formatNumber(p.lastPriceBrl),
    formatNumber(p.stopPriceBrl),formatNumber(p.lastRecordedValueBrl)]);
  fillRows('events',data.incidents,e=>[dateValue(e.createdUtc),e.type,e.description]);
  text('notice','Consultado em '+dateValue(data.observedUtc)+'. Apenas leitura; nenhuma aprovação registrada.');
 }catch(error){
  clear();text('notice',error.message);
 }finally{field('refresh').disabled=false;}
}
field('refresh').addEventListener('click',refresh);
refresh();
