'use strict';
const {test} = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const core = require('../../src/RAC.Web/wwwroot/trading-floor-motion-core.js');
const assets = path.resolve(__dirname, '../../src/RAC.Web/wwwroot');
test('awaiting or paused goes to leisure and observing returns to desk', () => {
  const context = {running: true, riskLocked: false};
  assert.equal(core.zoneFor({status:'AGUARDANDO'},context),'lounge');
  assert.equal(core.zoneFor({status:'ESPERANDO SINAL'},context),'lounge');
  assert.equal(core.zoneFor({status:'PAUSADO'},context),'lounge');
  assert.equal(core.zoneFor({status:'DESCANSANDO'},context),'lounge');
  assert.equal(core.zoneFor({status:'OBSERVANDO'},context),'desk');
  assert.equal(core.zoneFor({status:'EM OPERAÇÃO'},context),'desk');
  assert.equal(core.zoneFor({status:'ANALISANDO'},context),'desk');
});
test('risk-lock and paper-pause always keep avatars away from simulated execution', () => {
  assert.equal(core.zoneFor({status:'OBSERVANDO'}, {running:true,riskLocked:true}),'lounge');
  assert.equal(core.zoneFor({status:'OBSERVANDO'}, {running:false,riskLocked:false}),'lounge');
});
test('12 desks and waiting positions stay in distinct scene coordinates', () => {
  const pairs = Array.from({length:12},(_,i)=>[core.desk(i),core.lounge(i)]);
  assert.equal(new Set(pairs.map(pair=>JSON.stringify(pair[0]))).size,12);
  assert.equal(new Set(pairs.map(pair=>JSON.stringify(pair[1]))).size,12);
  for(const [desk,lounge] of pairs) {
    for(const p of [desk,lounge]) {assert.ok(p.x>=0&&p.x<=100);assert.ok(p.y>=0&&p.y<=100);}
    const path = core.route(desk,lounge,'lounge');
    assert.ok(path.length >= 2);
    assert.deepEqual(path[0],desk);
    assert.deepEqual(path.at(-1),lounge);
    for(let i=1;i<path.length;i++) assert.ok(path[i].x===path[i-1].x || path[i].y===path[i-1].y);
  }
});
test('only an explicit request or backend assistance flag calls a leader',()=>{
  assert.equal(core.requestedLeader({status:'OBSERVANDO'}),null);
  assert.equal(core.requestedLeader({needsSupervisor:true,status:'BLOQUEIO DE RISCO'}),'Carlos');
  assert.equal(core.requestedLeader({needsSupervisor:true,status:'OBSERVANDO'}),'Ana');
  assert.equal(core.requestedLeader({supervisorRequest:'Rebeca'}),'Rebeca');
});
test('3D human pose and animated typing assets are valid optimized WebP',()=>{
  for(const name of ['rac-people-poses.webp','rac-typing-male.webp','rac-typing-female.webp']) {
    const buffer=fs.readFileSync(path.join(assets,name));
    assert.ok(buffer.length>4000 && buffer.length<50000, name);
    assert.equal(buffer.toString('ascii',0,4),'RIFF');
    assert.equal(buffer.toString('ascii',8,12),'WEBP');
  }
});
test('UI code never sends trading orders or changes risk gate',()=>{
  const text=fs.readFileSync(path.join(assets,'trading-floor-motion.js'),'utf8');
  assert.doesNotMatch(text,/fetch\s*\(|XMLHttpRequest|\/api\/.*(order|trade|start|reset|unlock)/i);
  assert.match(text,/RacAgentMotion/);
  assert.match(text,/speechSynthesis/);
});

test('preview button is optional and clearly labelled as UI-only',()=>{
  const text=fs.readFileSync(path.join(assets,'trading-floor-motion.js'),'utf8');
  assert.match(text,/Testar movimentos \(somente visual\)/);
  assert.match(text,/DEMONSTRAÇÃO VISUAL/);
  assert.match(text,/demoActive/);
});

test('motion layer exposes stable pose classes for typing, standing, walking and speaking',()=>{
  const js=fs.readFileSync(path.join(assets,'trading-floor-motion.js'),'utf8');
  const css=fs.readFileSync(path.join(assets,'trading-floor-motion.css'),'utf8');
  for(const pose of ['typing','standing','walking','speaking']) {
    assert.match(js,new RegExp("data-pose.*"+pose+"|pose\\(actor, ['\\\"]"+pose));
    assert.match(css,new RegExp('data-pose=["\\\']'+pose));
  }
});

test('motion layer includes subtle idle micro-movement and reduced-motion fallback',()=>{
  const css=fs.readFileSync(path.join(assets,'trading-floor-motion.css'),'utf8');
  assert.match(css,/rac-type|rac-idle|rac-breathe/);
  assert.match(css,/prefers-reduced-motion:reduce/);
  assert.match(css,/motion-disabled/);
});

test('pose atlas uses two complete horizontal cells rather than eight fragments',()=>{
  const css=fs.readFileSync(path.join(assets,'trading-floor-motion.css'),'utf8');
  assert.match(css,/background-size:200% 100%/);
  assert.doesNotMatch(css,/background-size:400% 200%|background-position-x:66\.666%|background-position-x:33\.333%/);
});
test('avatars retain a stable identity and visible name across poses',()=>{
 const js=fs.readFileSync(path.join(assets,'trading-floor-motion.js'),'utf8');
 const css=fs.readFileSync(path.join(assets,'trading-floor-motion.css'),'utf8');
 assert.match(js,/Array.from\(key\)/);
 assert.match(js,/name.textContent = label/);
 assert.match(css,/rac-avatar-name/);
 assert.match(css,/--avatar-hue/);
});
test('leader returns home before another visit and agent acknowledges simulated dialogue',()=>{
 const js=fs.readFileSync(path.join(assets,'trading-floor-motion.js'),'utf8');
 assert.match(js,/core.leaderCoords\[leader\], 'leader', \(\) => \{ busyVisit = false;/);
 assert.match(js,/Entendido/);
 assert.match(js,/if \(!active \|\| busyVisit\) return/);
});