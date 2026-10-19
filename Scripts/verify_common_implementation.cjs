// Read-only evidence binding and headless report layout verification. No OS input.
const fs=require('fs'),path=require('path'),crypto=require('crypto'),cp=require('child_process');
const {pathToFileURL,fileURLToPath}=require('url');
const {chromium}=require('C:/Users/hizen/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root=path.resolve(__dirname,'..'),base=path.join(root,'Docs/Design/CommonContractV1'),out=path.join(base,'review-local');
const sha=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');const read=n=>JSON.parse(fs.readFileSync(path.join(base,n),'utf8'));
const checks=[],assert=(v,m)=>{if(!v)throw new Error(m);checks.push(m)};
(async()=>{
const r=read('implementation-report.json'),gpu=read('godot-verification.json');
assert(r.g1.status==='COMPLETED'&&r.tests.total===412&&r.tests.passed===412&&r.tests.new_cases===95&&r.tests.legacy_cases===317,'G1 four items; 412 passed = 317 legacy + 95 new');
assert(r.tests.failed===0&&r.tests.skipped===0&&r.tests.new_case_results.every(t=>t.outcome==='Passed'),'no failed or skipped cases');
for(const [p,h]of Object.entries({...r.source_sha256,...r.test_binary_sha256,...gpu.code_sha256,...gpu.binary_sha256}))assert(sha(path.join(root,p))===h,'evidence code/binary unchanged: '+p);
for(const [p,h]of Object.entries(r.proof_sha256))assert(sha(path.join(base,p))===h,'proof unchanged: '+p);
for(const [p,h]of Object.entries(r.shared_source_sha256_normalized_lf))assert(crypto.createHash('sha256').update(fs.readFileSync(path.join(root,p),'utf8').replace(/\r\n/g,'\n')).digest('hex')===h,'shared compiled source unchanged: '+p);
assert(sha(path.join(base,'runtime-local/tests/core.trx'))===r.tests.trx_sha256,'TRX matches executed report');
for(const [p,h]of Object.entries(gpu.images))assert(sha(path.join(base,'Evidence',p))===h,'actual GPU image unchanged: '+p);
assert(r.foreign_process.semantic_digest_identical&&r.foreign_process.next_random_identical&&r.abrupt_exit.old_generation_still_current,'foreign process and interrupted save evidence passed');
assert(r.further_progress.every(g=>g.status!=='COMPLETED'),'later gates are scoped fixture progress, not whole-product completion');
const outputs=['implementation-report.json','implementation-report.html'],before=outputs.map(n=>sha(path.join(base,n)));
cp.execFileSync('C:/Users/hizen/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe',['-X','utf8',path.join(root,'Scripts/build_common_implementation_report.py')],{cwd:root});
assert(outputs.every((p,i)=>sha(path.join(base,p))===before[i]),'report rebuild is byte-identical');
const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',args:['--disable-gpu']});
try{
 const page=await browser.newPage({viewport:{width:1440,height:1100},deviceScaleFactor:1}),errors=[],network=[];
 page.on('pageerror',e=>errors.push(e.message));page.on('request',r=>{if(/^https?:/.test(r.url()))network.push(r.url())});
 const url=pathToFileURL(path.join(base,'implementation-report.html')).href;await page.goto(url);await page.evaluate(()=>document.fonts.ready);
 assert(await page.locator('.stat').count()===4,'four concise result cards');assert(await page.locator('.timeline li').count()===5,'five actual timeline stages');
 assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),'desktop no horizontal overflow');
 const links=await page.locator('a[href]').evaluateAll(a=>a.map(x=>x.getAttribute('href')));
 for(const link of links)assert(fs.existsSync(fileURLToPath(new URL(link,url))),'report link exists: '+link);
 await page.screenshot({path:path.join(out,'implementation-desktop.png')});
 await page.locator('#images').evaluate(e=>e.open=true);await page.locator('#images').scrollIntoViewIfNeeded();
 await page.waitForFunction(()=>[...document.querySelectorAll('img')].every(i=>i.complete&&i.naturalWidth>0));
 assert(await page.locator('img').count()===4,'four embedded GPU captures load offline');await page.screenshot({path:path.join(out,'implementation-images.png')});
 await page.setViewportSize({width:390,height:844});await page.evaluate(()=>{scrollTo(0,0);document.getElementById('images').open=false});
 assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),'mobile 390px no page overflow');await page.screenshot({path:path.join(out,'implementation-mobile.png')});
 await page.locator('.next').scrollIntoViewIfNeeded();assert(await page.locator('.next').innerText().then(t=>t.includes('完了条件')),'next task includes completion condition');
 await page.screenshot({path:path.join(out,'implementation-next.png')});assert(errors.length===0&&network.length===0,'no JS errors or external HTTP requests');
}finally{await browser.close()}
const report={schema:1,status:'QA_PASSED_VISUAL_REVIEW_PENDING',check_count:checks.length,checks,report_html_sha256:sha(path.join(base,'implementation-report.html')),report_data_sha256:sha(path.join(base,'implementation-report.json')),validator_sha256:sha(__filename)};
fs.writeFileSync(path.join(base,'implementation-review.json'),JSON.stringify(report,null,2)+'\n');process.stdout.write('PASS implementation evidence/report checks='+checks.length+'\n');
})().catch(e=>{process.stderr.write(e.stack+'\n');process.exitCode=1});
