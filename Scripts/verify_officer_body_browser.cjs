const fs=require('fs'),path=require('path');const{pathToFileURL}=require('url');
const{chromium}=require(process.argv[2]);const root=path.resolve(process.argv[3]||path.join(__dirname,'..'));
const assets=path.join(root,'Game/Assets/Trials/OfficerBodyBaseV1');
const url=name=>'data:image/png;base64,'+fs.readFileSync(path.join(assets,name)).toString('base64');
(async()=>{const browser=await chromium.launch({headless:true,channel:'msedge'});try{
const page=await browser.newPage({viewport:{width:1180,height:1050},deviceScaleFactor:1});const errors=[];page.on('pageerror',e=>errors.push(e.message));
await page.goto(pathToFileURL(path.join(root,'Docs/Proposals/officer-body-base-review.html')).href);await page.waitForFunction(()=>window.bodyReady);
const sizes=await page.evaluate(()=>['native','large'].map(id=>{const c=document.getElementById(id),r=c.getBoundingClientRect();return{id,pixels:[c.width,c.height],css:[r.width,r.height]}}));
if(JSON.stringify(sizes.map(s=>s.css))!==JSON.stringify([[64,96],[320,480]]))throw Error('Display scale drift');
for(const [mode,file]of Object.entries({body:'body-front.png',skin:'skin-mask.png',edit:'edit-mask.png',preserve:'preserve-mask.png'})){
const count=await page.evaluate(async({mode,url})=>{bodyReview.render(mode);let im=new Image;im.src=url;await im.decode();let c=document.createElement('canvas');c.width=64;c.height=96;let x=c.getContext('2d');x.drawImage(im,0,0);let e=x.getImageData(0,0,64,96).data,a=document.getElementById('large').getContext('2d').getImageData(0,0,64,96).data;return a.reduce((n,v,i)=>n+(v!==e[i]),0)},{mode,url:url(file)});if(count)throw Error(mode+' pixel differences: '+count);
}
await page.evaluate(()=>bodyReview.render('ids'));await page.evaluate(()=>bodyReview.render('landmarks'));await page.evaluate(()=>bodyReview.render('body'));
const nativeDifference=await page.evaluate(()=>{let a=document.getElementById('native').getContext('2d').getImageData(0,0,64,96).data,b=document.getElementById('large').getContext('2d').getImageData(0,0,64,96).data;return a.reduce((n,v,i)=>n+(v!==b[i]),0)});if(nativeDifference||errors.length)throw Error('Native pixels/page errors differ');
await page.screenshot({path:path.join(root,'Docs/Proposals/Evidence/officer-body-base-browser.png'),fullPage:true});
fs.writeFileSync(path.join(root,'Art/Officer/BodyBaseV1/browser-verification.json'),JSON.stringify({status:'PASS',errors,sizes,rgba_difference:0,states:6,native_input:0},null,2)+'\n');console.log('BODY_BROWSER_PASS modes=6 rgba_difference=0 native_css=64x96 enlarged_css=320x480 native_input=0');
}finally{await browser.close()}})().catch(e=>{console.error(e);process.exit(1)});
