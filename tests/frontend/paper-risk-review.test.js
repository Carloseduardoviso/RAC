'use strict';
const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const path=require('node:path');
const root=path.resolve(__dirname,'../../src/RAC.Web/wwwroot');
const page=fs.readFileSync(path.join(root,'paper-risk-review.html'),'utf8');
const script=fs.readFileSync(path.join(root,'paper-risk-review.js'),'utf8');
const api=fs.readFileSync(path.resolve(root,'../../RAC.Api/Program.cs'),'utf8');
const web=fs.readFileSync(path.resolve(root,'../Program.cs'),'utf8');

test('PAPER risk dossier is read-only and separated from legacy simulator review',()=>{
 assert.match(api,/app\.MapGet\("\/api\/paper\/risk-review"/);
 assert.match(api,/RequireAuthorization\("RacRiskReviewer"\)/);
 assert.doesNotMatch(api,/app\.MapPost\("\/api\/paper\/risk-review"/);
 assert.match(web,/"\/api\/paper\/risk-review"/);
 assert.doesNotMatch(web,/postRoutes[^]*"\/api\/paper\/risk-review"/);
});

test('dossier avoids unsafe approval controls and shows persisted evidence',()=>{
 assert.match(page,/Dossiê de risco PAPER/);
 assert.match(page,/id="positions"/);
 assert.match(page,/id="events"/);
 assert.match(page,/não autoriza desbloqueio/i);
 assert.doesNotMatch(page,/<form|type="submit"/i);
 assert.match(script,/data\.unlockPermitted!==false/);
 assert.match(script,/textContent/);
 assert.doesNotMatch(script,/innerHTML|fetch\([^]*method:\s*['"]POST/i);
});

test('recovery preview is read-only and rejects stale quotes',()=>{
 assert.match(page,/id="previewMetrics" hidden/);
 assert.match(script,/renderPreview\(data\.recoveryPreview\)/);
 assert.match(script,/age>30000/);
 assert.match(script,/renderPreview\(null\)/);
});
