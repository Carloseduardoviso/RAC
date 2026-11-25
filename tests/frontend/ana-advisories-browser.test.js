const test=require("node:test");
const assert=require("node:assert/strict");
const http=require("node:http");
const fs=require("node:fs");
const os=require("node:os");
const path=require("node:path");
const {spawn}=require("node:child_process");
const delay=ms=>new Promise(resolve=>setTimeout(resolve,ms));
const edge=process.env.RAC_TEST_BROWSER;
test("Ana history renders in isolated Chromium browser",{skip:!edge,timeout:60000},async()=>{
    const root=path.join(__dirname,"../../src/RAC.Web/wwwroot");
    const profile=fs.mkdtempSync(path.join(os.tmpdir(),"rac-ana-browser-"));
    const server=http.createServer((req,res)=>{
        if(req.url==="/api/ana/advisories"){
            res.setHeader("Content-Type","application/json");
            return res.end(JSON.stringify([{status:"RISK_REVIEW_REQUIRED",createdUtc:"2026-11-25T12:00:00Z",reasons:["<script>unsafe</script>"],evidenceIds:["isolated-risk-id"],tradingAuthorized:false}]));
        }
        const files={"/":"ana-advisories.html","/ana-advisories.js":"ana-advisories.js","/styles.css":"styles.css"};
        const file=files[req.url];
        if(!file){res.statusCode=404;return res.end();}
        res.setHeader("Content-Type",file.endsWith(".js")?"application/javascript":file.endsWith(".css")?"text/css":"text/html; charset=utf-8");
        res.end(fs.readFileSync(path.join(root,file)));
    });
    let browser;
    try {
        await new Promise(resolve=>server.listen(0,"127.0.0.1",resolve));
        const url="http://127.0.0.1:"+server.address().port+"/";
        browser=spawn(edge,["--headless=new","--disable-gpu","--no-first-run","--no-default-browser-check","--user-data-dir="+profile,"--remote-debugging-port=0",url],{stdio:"ignore"});
        const portFile=path.join(profile,"DevToolsActivePort");
        for(let i=0;i<100&&!fs.existsSync(portFile);i++) await delay(200);
        assert.ok(fs.existsSync(portFile),"Browser did not expose DevTools endpoint");
        const port=fs.readFileSync(portFile,"utf8").split(/\r?\n/)[0];
        const targets=await (await fetch("http://127.0.0.1:"+port+"/json/list")).json();
        const target=targets.find(t=>t.type==="page"&&t.url===url);
        assert.ok(target,"Isolated test page missing");
        const ws=new WebSocket(target.webSocketDebuggerUrl);
        await new Promise((resolve,reject)=>{ws.addEventListener("open",resolve,{once:true});ws.addEventListener("error",reject,{once:true});});
        let stdout="";
        try {
            for(let i=1;i<=30;i++){
                const result=await new Promise(resolve=>{
                    const handler=e=>{const m=JSON.parse(e.data);if(m.id===i){ws.removeEventListener("message",handler);resolve(m);}};
                    ws.addEventListener("message",handler);
                    ws.send(JSON.stringify({id:i,method:"Runtime.evaluate",params:{expression:"document.documentElement.outerHTML",returnByValue:true}}));
                });
                stdout=result.result?.result?.value||"";
                if(stdout.includes("isolated-risk-id")) break;
                await delay(200);
            }
        } finally {ws.close();}
        assert.match(stdout,/class="ana-critical"/);
        assert.match(stdout,/isolated-risk-id/);
        assert.match(stdout,/&lt;script&gt;unsafe&lt;\/script&gt;/);
        assert.match(stdout,/Negociacao nao autorizada/);
    } finally {
        if(browser) browser.kill();
        server.closeAllConnections(); await new Promise(resolve=>server.close(resolve));
        try {fs.rmSync(profile,{recursive:true,force:true,maxRetries:5,retryDelay:200});} catch {}
    }
});
