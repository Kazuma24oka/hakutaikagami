// Document QA only: headless Edge, no native input and no game state mutations.
const fs=require('fs'),path=require('path'),crypto=require('crypto'),cp=require('child_process');
const {pathToFileURL,fileURLToPath}=require('url');
const {chromium}=require('C:/Users/hizen/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root=path.resolve(__dirname,'..'),base=path.join(root,'Docs/Design/CommonContractV1'),out=path.join(base,'review-local');
const read=n=>JSON.parse(fs.readFileSync(path.join(base,n),'utf8'));
const hash=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const checks=[],assert=(v,s)=>{if(!v)throw new Error(s);checks.push(s)};
(async()=>{
 const data=read('common-contract-v1.json'),plan=read('acceptance-plan.json');
 assert(data.items.length===24&&new Set(data.items.map(i=>i.id)).size===24,'24 unique adopted contracts');
 for(const i of data.items){assert(['decision','fields','rules','current','change'].every(k=>typeof i[k]==='string'&&i[k].trim().length>0),i.id+' has decision, schema boundaries, reuse and changes');assert(i.acceptance.length===2&&i.acceptance.some(a=>a.kind==='failure'),i.id+' normal and failure acceptance examples');}
 assert(plan.count===48&&plan.cases.length===48&&plan.cases.every(c=>c.status==='PLANNED_NOT_EXECUTED'),'48 acceptance examples explicitly planned, not executed');
 assert(data.state_axes.length===8&&data.api.length===6,'8 state axes and 6 API contracts');
 for(const [p,h]of Object.entries(data.source_hashes_normalized_lf)){const text=fs.readFileSync(path.join(root,p),'utf8').replace(/\r\n/g,'\n');assert(crypto.createHash('sha256').update(text).digest('hex')===h,'source snapshot unchanged: '+p);}
 const generated=['common-contract-v1.html','COMMON_CONTRACT_V1.md','acceptance-plan.json'],before=generated.map(p=>hash(path.join(base,p)));
 cp.execFileSync('C:/Users/hizen/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe',['-X','utf8',path.join(root,'Scripts/build_common_contract.py')],{cwd:root});
 assert(generated.every((p,i)=>hash(path.join(base,p))===before[i]),'generator rebuild is byte-identical');
 fs.mkdirSync(out,{recursive:true});const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',args:['--disable-gpu']});
 try{
  const page=await browser.newPage({viewport:{width:1440,height:1100},deviceScaleFactor:1}),errors=[],remote=[];
  page.on('pageerror',e=>errors.push(e.message));page.on('request',r=>{if(/^https?:/.test(r.url()))remote.push(r.url())});
  const url=pathToFileURL(path.join(base,'common-contract-v1.html')).href;
  await page.goto(url,{waitUntil:'load'});await page.evaluate(()=>document.fonts.ready);await page.addStyleTag({content:'html{scroll-behavior:auto!important}'});
  assert(await page.locator('[data-contract]').count()===24,'HTML has every contract');
  const links=await page.evaluate(()=>({ids:[...document.querySelectorAll('[id]')].map(e=>e.id),hrefs:[...document.querySelectorAll('a[href]')].map(e=>e.getAttribute('href'))}));
  assert(new Set(links.ids).size===links.ids.length,'HTML IDs are unique');
  for(const href of links.hrefs){const u=new URL(href,url);if(u.protocol!=='file:')continue;const p=fileURLToPath(u);assert(fs.existsSync(p),'link exists: '+href);if(u.hash&&p.endsWith('.html')){const text=fs.readFileSync(p,'utf8'),id=decodeURIComponent(u.hash.slice(1));assert(new RegExp('id=["\x27]'+id+'["\x27]').test(text),'anchor exists: '+href);}}
  const overflow=()=>page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1);
  assert(await overflow(),'1440px layout has no page overflow');await page.screenshot({path:path.join(out,'desktop-overview.png')});
  await page.evaluate(()=>document.getElementById('save').scrollIntoView());await page.screenshot({path:path.join(out,'desktop-save.png')});
  await page.evaluate(()=>{const s=document.getElementById('search');s.value='CC-22';s.dispatchEvent(new Event('input'))});
  assert(await page.locator('[data-contract]:visible').count()===1,'search CC-22 isolates legacy migration contract');
  await page.evaluate(()=>document.getElementById('expand').click());assert(await page.locator('[data-contract]:visible[open]').count()===1,'expand opens filtered result');
  await page.evaluate(()=>document.getElementById('CC-22').scrollIntoView());await page.screenshot({path:path.join(out,'desktop-migration.png')});
  await page.evaluate(()=>window.dispatchEvent(new Event('beforeprint')));assert(await page.locator('[data-contract]:visible[open]').count()===24,'print prepares all 24 sections');
  await page.evaluate(()=>window.dispatchEvent(new Event('afterprint')));assert(await page.locator('[data-contract]:visible').count()===1,'print restores previous search');
  await page.evaluate(()=>{document.getElementById('search').value='';document.getElementById('search').dispatchEvent(new Event('input'));document.getElementById('collapse').click()});
  assert(await page.locator('[data-contract][open]').count()===0,'collapse resets open sections');
  await page.setViewportSize({width:390,height:844});await page.evaluate(()=>scrollTo(0,0));assert(await overflow(),'390px layout has no page overflow');await page.screenshot({path:path.join(out,'mobile-overview.png')});
  await page.evaluate(()=>document.getElementById('structure').scrollIntoView());await page.screenshot({path:path.join(out,'mobile-structure.png')});
  await page.evaluate(()=>{document.getElementById('CC-20').open=true;document.getElementById('CC-20').scrollIntoView()});assert(await overflow(),'expanded contract has no mobile page overflow');await page.screenshot({path:path.join(out,'mobile-save-contract.png')});
  assert(errors.length===0,'no browser JavaScript errors');assert(remote.length===0,'no HTTP requests; self-contained offline document');
 }finally{await browser.close();}
 const files=['common-contract-v1.json','common-contract-v1.html','COMMON_CONTRACT_V1.md','acceptance-plan.json'];const artifacts=['desktop-overview.png','desktop-save.png','desktop-migration.png','mobile-overview.png','mobile-structure.png','mobile-save-contract.png'];
 const report={schema:1,status:'DOCUMENT_QA_PASSED_VISUAL_REVIEW_PENDING',checks,check_count:checks.length,game_behavior_tests:'NOT_RUN_BY_THIS_DOCUMENT_CHECKER: design-time 48-case plan; see implementation-report.json for separate Core/Godot execution evidence',files:Object.fromEntries(files.map(f=>[f,hash(path.join(base,f))])),generator_sha256:hash(path.join(root,'Scripts/build_common_contract.py')),template_sha256:hash(path.join(root,'Scripts/common_contract_template.html')),validator_sha256:hash(__filename),artifact_sha256:Object.fromEntries(artifacts.map(f=>[f,hash(path.join(out,f))]))};
 fs.writeFileSync(path.join(base,'document-review.json'),JSON.stringify(report,null,2)+'\n');process.stdout.write('PASS document checks='+checks.length+'; planned acceptance=48 (not executed)\n');
})().catch(e=>{process.stderr.write(e.stack+'\n');process.exitCode=1});
