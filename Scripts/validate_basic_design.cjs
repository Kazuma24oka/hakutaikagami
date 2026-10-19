// Headless browser and document QA only. No OS input, cursor settings, user browser or game saves.
const fs=require('fs'),path=require('path'),crypto=require('crypto'),{pathToFileURL}=require('url');
const {chromium}=require('C:/Users/hizen/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root=path.resolve(process.argv[2]||path.join(__dirname,'..')),base=path.join(root,'Docs/Design'),out=path.join(base,'review-local');
const read=n=>JSON.parse(fs.readFileSync(path.join(base,n),'utf8'));
const sha=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const assert=(ok,msg)=>{if(!ok)throw new Error(msg);checks.push(msg)};const checks=[];
(async()=>{
fs.mkdirSync(out,{recursive:true});const data=read('basic-design-v1.json'),rules=read('basic-design-rules.json'),plans=read('basic-design-acceptance.json'),provenance=read('basic-design-provenance.json');
const decisions=data.groups.flatMap(g=>g.items);assert(decisions.length===60&&new Set(decisions.map(d=>d.id)).size===60,'60 unique design decisions');
assert(data.groups.length===15&&data.groups.every(g=>g.items.length===4),'15 groups, four decisions each');
for(const d of decisions)assert(['decision','boundary','normal','failure','acceptance'].every(k=>typeof d[k]==='string'&&d[k].length>10),d.id+' has decision boundary examples and acceptance');
assert(plans.count===170&&plans.cases.length===170&&plans.cases.every(c=>c.status==='PLANNED_NOT_EXECUTED'),'170 acceptance plans explicitly not executed');
for(const d of decisions)assert(plans.cases.filter(c=>c.id.startsWith(d.id+'-')).length===2,d.id+' traced to normal and failure plans');
assert(rules.rules.length===10&&rules.role_matrix.length===20,'10 boundary rules and 20 role/category pairs');
for(const [name,proof] of Object.entries(provenance.runtime_input_snapshots)){
assert(sha(path.join(root,'Art/Officer3D',name,'runtime-plan.json'))===proof.plan_sha256,name+' existing plan unchanged');
for(const dep of proof.dependencies)if(sha(path.join(root,dep.path))!==dep.sha256)throw new Error('Runtime dependency changed: '+dep.path);
}
for(const input of provenance.inputs){
const p=input.path.startsWith('local-reference:')?path.join(root,'..',input.path.slice(16)):path.join(root,input.path);
const canonical=fs.readFileSync(p,'utf8').replace(/\r\n/g,'\n');const h=crypto.createHash('sha256').update(canonical).digest('hex');
assert(h===input.sha256_normalized_lf,'source unchanged: '+input.path);
}
const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',args:['--disable-gpu']});
const page=await browser.newPage({viewport:{width:1440,height:1000},deviceScaleFactor:1});const errors=[],remote=[];
page.on('pageerror',e=>errors.push(e.message));page.on('request',r=>{if(/^https?:/.test(r.url()))remote.push(r.url())});
await page.goto(pathToFileURL(path.join(base,'basic-design-v1.html')).href,{waitUntil:'load'});await page.addStyleTag({content:'html{scroll-behavior:auto!important}'});await page.evaluate(()=>document.fonts.ready);
assert(await page.locator('[data-decision]').count()===60,'HTML contains all 60 decisions');
const linkAudit=await page.evaluate(()=>{const ids=[...document.querySelectorAll('[id]')].map(x=>x.id);return {duplicates:ids.filter((x,i)=>ids.indexOf(x)!==i),broken:[...document.querySelectorAll('a[href^="#"]')].map(a=>a.getAttribute('href').slice(1)).filter(id=>!document.getElementById(id)),local:[...document.querySelectorAll('a[href]')].map(a=>a.getAttribute('href')).filter(h=>!h.startsWith('#')&&!/^https?:/.test(h))}});
assert(linkAudit.duplicates.length===0&&linkAudit.broken.length===0,'all HTML anchors unique and resolvable');
for(const link of linkAudit.local)assert(fs.existsSync(path.join(base,link)),'local HTML link '+link);
assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),'desktop has no page horizontal overflow');
await page.screenshot({path:path.join(out,'desktop-overview.png')});
await page.locator('#images').evaluate(x=>x.scrollIntoView());await page.screenshot({path:path.join(out,'desktop-diagrams.png')});
await page.evaluate(()=>{const q=document.getElementById('design-search');q.value='BD-36';q.dispatchEvent(new Event('input'))});
assert(await page.locator('[data-decision]:visible').count()===1,'search isolates BD-36');
await page.locator('#BD-36').evaluate(x=>x.scrollIntoView());await page.screenshot({path:path.join(out,'decision-resource.png')});
await page.evaluate(()=>{const s=document.getElementById('sim-time');s.value='5';s.dispatchEvent(new Event('input'))});
const known=await page.locator('#sim-lanes .known').allTextContents(),truth=await page.locator('#sim-lanes .truth').allTextContents();
assert(known[2].includes('未確認')&&truth[2].includes('移動中'),'diagram separates recipient receipt from issuer knowledge');
await page.evaluate(()=>{const q=document.getElementById('design-search');q.value='';q.dispatchEvent(new Event('input'));document.getElementById('expand').click()});
assert(await page.locator('[data-decision]:visible').count()===60,'expand opens all decisions');
await page.evaluate(()=>document.getElementById('collapse').click());assert(await page.locator('[data-decision]:visible').count()===0,'collapse closes all groups');
await page.evaluate(()=>window.dispatchEvent(new Event('beforeprint')));assert(await page.locator('[data-decision]:visible').count()===60,'print preparation includes all decisions');
await page.evaluate(()=>window.dispatchEvent(new Event('afterprint')));assert(await page.locator('[data-decision]:visible').count()===0,'print restores reading state');
await page.setViewportSize({width:390,height:844});await page.evaluate(()=>window.scrollTo(0,0));
assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),'390px mobile has no page horizontal overflow');
await page.screenshot({path:path.join(out,'mobile-overview.png')});await page.locator('#images').evaluate(x=>x.scrollIntoView());await page.screenshot({path:path.join(out,'mobile-diagrams.png')});
await page.setViewportSize({width:1440,height:1000});await page.evaluate(()=>{document.getElementById('expand').click();document.getElementById('G12').scrollIntoView()});await page.screenshot({path:path.join(out,'succession-section.png')});
await page.pdf({path:path.join(out,'print-check.pdf'),format:'A4',printBackground:true,margin:{top:'12mm',bottom:'12mm',left:'12mm',right:'12mm'}});
assert(errors.length===0,'no browser JavaScript errors');assert(remote.length===0,'offline HTML makes no network requests');await browser.close();
const artifacts={};for(const n of ['desktop-overview.png','desktop-diagrams.png','decision-resource.png','mobile-overview.png','mobile-diagrams.png','succession-section.png','print-check.pdf'])artifacts[n]=sha(path.join(out,n));
const report={schema:1,status:'AUTOMATED_DOCUMENT_QA_PASSED_VISUAL_REVIEW_PENDING',checks,design_count:60,group_count:15,boundary_count:10,role_pairs:20,acceptance_plans:170,game_acceptance_execution:'NOT_EXECUTED_FOR_NEW_DESIGN',source_sha256:sha(path.join(base,'basic-design-v1.json')),rules_sha256:sha(path.join(base,'basic-design-rules.json')),html_sha256:sha(path.join(base,'basic-design-v1.html')),artifacts_sha256:artifacts};
fs.writeFileSync(path.join(base,'basic-design-review.json'),JSON.stringify(report,null,2)+'\n');console.log('BASIC_DESIGN_DOCUMENT_QA_PASSED',checks.length,'checks');
})().catch(e=>{console.error(e.stack);process.exit(1)});
