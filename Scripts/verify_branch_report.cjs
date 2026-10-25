const {chromium}=require('C:/Users/hizen/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const fs=require('fs'),path=require('path'),{pathToFileURL}=require('url');
(async()=>{
 const root=path.resolve(__dirname,'..'),base=path.join(root,'Docs/Design/BranchWorldV1');
 const browser=await chromium.launch({executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true});
 const results=[];
 try{
  for(const width of [1440,390]){
   const page=await browser.newPage({viewport:{width,height:1000}}),errors=[];
   page.on('pageerror',e=>errors.push(e.message));
   await page.goto(pathToFileURL(path.join(base,'report.html')).href);
   await page.waitForFunction(()=>[...document.images].every(i=>i.complete&&i.naturalWidth>0));
   if(errors.length)throw Error(errors.join('\n'));
   if(!await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth))throw Error('Horizontal overflow');
   const text=await page.locator('body').innerText();
   for(const expected of ['457件','24件','0画素','211秒','未実施','次の作業'])if(!text.includes(expected))throw Error('Missing '+expected);
   await page.screenshot({path:path.join(base,`runtime-local/review/report-${width}.png`),fullPage:true});
   results.push({width,images:await page.locator('img').count(),overflow:false,errors});await page.close();
  }
  fs.writeFileSync(path.join(base,'html-verification.json'),JSON.stringify({status:'PASSED',results},null,2)+'\n');
  console.log('PASS: report desktop/mobile, local images, no overflow or script errors');
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
