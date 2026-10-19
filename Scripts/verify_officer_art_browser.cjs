const fs=require('fs'),path=require('path');
const {pathToFileURL}=require('url');
const {chromium}=require(process.argv[2]||'playwright');
const root=path.resolve(process.argv[3]||path.join(__dirname,'..'));
(async()=>{
  const browser=await chromium.launch({headless:true,channel:'msedge'});
  try{
    const page=await browser.newPage();const errors=[];
    page.on('pageerror',e=>errors.push(e.message));
    await page.goto(pathToFileURL(path.join(root,'Docs/Proposals/officer-parts-review.html')).href);
    const status=await page.locator('#production-status').textContent();
    const oldImages=await page.locator('img,canvas').count();
    if(errors.length||!status.includes('不採用')||!status.includes('未制作')||oldImages!==0)
      throw Error('Rejected assets presented as current candidate');
    const result={status:'PASS',scope:'quarantine-page-only',errors,rejectedImagesDisplayed:oldImages,native_input:0};
    fs.writeFileSync(path.join(root,'Art/Officer/browser-verification.json'),JSON.stringify(result,null,2)+'\n');
    console.log('OFFICER_ART_BROWSER_PASS rejected_images=0 native_input=0');
  }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exit(1)});
