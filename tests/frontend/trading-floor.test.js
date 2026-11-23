'use strict';
const {test}=require('node:test');
const assert=require('node:assert/strict');
const vm=require('node:vm');
const fs=require('node:fs');
const path=require('node:path');
const code=fs.readFileSync(path.resolve(__dirname,'../../src/RAC.Web/wwwroot/trading-floor.js'),'utf8');
function setup(){
 const nodes=new Map();
 const el=id=>{if(!nodes.has(id)) nodes.set(id,{textContent:'',classList:{toggle(){}},setAttribute(k,v){this[k]=v}});return nodes.get(id);};
 const context={window:{},document:{getElementById:el,querySelector:()=>el('floor')},Intl,Number,String,Boolean,Array,Object};
 vm.runInNewContext(code,context);
 return {render:context.window.updateTradingFloorHud,el};
}
test('live status counters reflect paper data, not guessed values',()=>{
 const {render,el}=setup();
 render({running:true,riskLocked:false,dailyPnl:22.5,
 supervisors:[{},{},{}],agents:Array.from({length:12},(_,i)=>({status:i<2?'EM OPERAÇÃO':i<5?'AGUARDANDO':i<8?'PAUSADO':'ANALISANDO'}))});
 assert.equal(el('officeCountOperating').textContent,2);
 assert.equal(el('officeCountWaiting').textContent,3);
 assert.equal(el('officeCountPaused').textContent,3);
 assert.equal(el('officeCountAnalyzing').textContent,4);
 assert.equal(el('officeStageState').textContent,'PAPER EM EXECUÇÃO');
 assert.match(el('officeDayResult').textContent,/22,50/);
});
test('paused paper stays paused on screen',()=>{
 const {render,el}=setup();
 render({running:false,riskLocked:false,dailyPnl:0,agents:[],supervisors:[]});
 assert.equal(el('officeStageState').textContent,'PAPER PAUSADO');
 assert.equal(el('officeCountOperating').textContent,0);
});
test('locked paper is labeled blocked',()=>{
 const {render,el}=setup();
 render({running:true,riskLocked:true,dailyPnl:-5,agents:[{status:'BLOQUEADO'}],supervisors:[]});
 assert.equal(el('officeStageState').textContent,'RISCO BLOQUEADO');
 assert.equal(el('officeCountPaused').textContent,1);
 assert.equal(el('floor')['data-paper-running'],'false');
});
