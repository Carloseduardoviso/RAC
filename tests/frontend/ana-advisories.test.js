const test = require("node:test");
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const source = fs.readFileSync(path.join(__dirname,"../../src/RAC.Web/wwwroot/ana-advisories.js"),"utf8");
function node(tag) {
    return {tag,children:[],textContent:"",disabled:false,appendChild(n){this.children.push(n);},replaceChildren(){this.children=[];},addEventListener(name,fn){this[name]=fn;}};
}
async function run(response) {
    const elements={notice:node("p"),history:node("section"),refresh:node("button")};
    const calls=[];
    const context={document:{getElementById:id=>elements[id],createElement:node},fetch:async (...args)=>{calls.push(args);return response;}};
    await vm.runInNewContext(source,context);
    return {elements,calls};
}
test("risk evidence is rendered as text and never authorizes trading",async()=>{
    const {elements,calls}=await run({ok:true,status:200,json:async()=>[{status:"RISK_REVIEW_REQUIRED",createdUtc:"2026-11-25T12:00:00Z",reasons:["<script>bad</script>"],evidenceIds:["risk-id"],tradingAuthorized:true}]});
    const card=elements.history.children[0];
    assert.equal(card.children[0].className,"ana-critical");
    assert.equal(card.children[3].children[0].textContent,"<script>bad</script>");
    assert.equal(card.children[5].children[0].textContent,"risk-id");
    assert.equal(card.children[6].textContent,"Negociacao nao autorizada");
    assert.equal(calls[0][0],"/api/ana/advisories");
    assert.equal(calls[0][1].credentials,"same-origin");
    assert.equal(elements.refresh.disabled,false);
});
for(const status of [401,403,500]) test("HTTP "+status+" clears history",async()=>{
    const {elements}=await run({ok:false,status});
    assert.equal(elements.history.children.length,0);
    assert.match(elements.notice.textContent,status===500?/indisponivel/:/restrito/);
    assert.equal(elements.refresh.disabled,false);
});
test("empty history is not an error",async()=>{
    const {elements}=await run({ok:true,status:200,json:async()=>[]});
    assert.equal(elements.notice.textContent,"Nenhum parecer registrado.");
});
