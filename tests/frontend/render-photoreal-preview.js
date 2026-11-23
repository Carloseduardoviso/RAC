'use strict';
const fs=require('fs');
const path=require('path');
const root=path.resolve(__dirname,'../../src/RAC.Web/wwwroot');
const original=fs.readFileSync(path.join(root,'index.html'),'utf8');
const start=original.indexOf('<section id="officePhotoreal"');
const end=original.indexOf('<section class="metrics">',start);
if(start<0 || end<0) throw Error('Trading floor section missing.');
const symbols=['BTCUSDT','ETHUSDT','BNBUSDT','SOLUSDT','XRPUSDT','ADAUSDT',
  'DOGEUSDT','LINKUSDT','AVAXUSDT','LTCUSDT','DOTUSDT','TRXUSDT'];
const station=(name,leader=false)=>'<article class="agent office-station" data-office-key="'+name+
  '"><div class="agent-top"><h3>'+name+'</h3><span>OBSERVANDO</span></div>'+
  '<div class="office-station-details"><div class="price">—</div>'+
  '<div class="foot"><span>0,00%</span><span class="signal">AGUARDAR</span></div>'+
  '<dl class="office-results"><div><dt>Vitórias</dt><dd>0</dd></div>'+
  '<div><dt>Perdas</dt><dd>0</dd></div><div><dt>P/L</dt><dd>—</dd></div></dl></div>'+
  (leader?'<p class="office-leader-state">Na estação</p>':
    '<button class="office-visit-request" type="button">Solicitar visita</button>')+'</article>';
let section=original.slice(start,end);
section=section.replace('<div class="agent-grid office-stations" id="agents"></div>',
 '<div class="agent-grid office-stations" id="agents">'+symbols.map(x=>station(x)).join('')+'</div>');
section=section.replace('<div class="agent-grid office-stations" id="supervisors"></div>',
 '<div class="agent-grid office-stations" id="supervisors">'+
 ['Ana','Rebeca','Carlos'].map(x=>station(x,true)).join('')+'</div>');
const html='<!doctype html><html lang="pt-BR"><head><meta charset="utf-8">'+
 '<meta name="viewport" content="width=device-width,initial-scale=1">'+
 '<link rel="stylesheet" href="styles.css"><link rel="stylesheet" href="trading-floor-photo.css">'+
 '<link rel="stylesheet" href="trading-floor-responsive.css"></head>'+
 '<body class="authenticated"><div class="shell"><main>'+section+
 '</main></div></body></html>';
fs.writeFileSync(path.join(root,'qa-photoreal-preview.html'),html,'utf8');
console.log('Isolated CSS preview generated; no API, authentication or order actions.');
