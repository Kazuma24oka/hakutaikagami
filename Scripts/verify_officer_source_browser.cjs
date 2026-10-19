const fs=require('fs'),path=require('path'),{execFileSync}=require('child_process');
const {chromium}=require('C:/Users/hizen/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root=process.argv[2];if(!root)throw Error('Explicit repository root required');
const python='C:/Users/hizen/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe';
const code="import json,sys;from pathlib import Path;r=Path(sys.argv[1]);sys.path.insert(0,str(r/'Scripts'));import officer_art_pipeline as g;w=json.loads((r/'Art/Officer/work-record.json').read_text(encoding='utf-8'));print(g.require_plan(r,'front_authoring',w['plans'][w['current_review_plan']]))";
execFileSync(python,['-c',code,root],{encoding:'utf8'});
(async()=>{
 const browser=await chromium.launch({headless:true,channel:'msedge'});const page=await browser.newPage({viewport:{width:1180,height:980},deviceScaleFactor:1});const errors=[];page.on('pageerror',e=>errors.push(e.message));page.on('console',m=>{if(m.type()==='error')errors.push(m.text())});
 try{
 await page.goto('file:///'+path.join(root,'Docs/Proposals/officer-body-source-review.html').replaceAll('\\','/'));await page.waitForFunction(()=>window.bodyReady===true);
 async function compare(id,rel){const data='data:image/png;base64,'+fs.readFileSync(path.join(root,rel)).toString('base64');return await page.evaluate(async({id,data})=>{const im=new Image;im.src=data;await im.decode();const expected=document.createElement('canvas');expected.width=64;expected.height=96;expected.getContext('2d').drawImage(im,0,0);const a=expected.getContext('2d').getImageData(0,0,64,96).data;const b=document.getElementById(id).getContext('2d').getImageData(0,0,64,96).data;let n=0;for(let i=0;i<a.length;i++)if(a[i]!==b[i])n++;return n},{id,data})}
 const dimensions=await page.evaluate(()=>Object.fromEntries(['native','body','overlay'].map(id=>{let e=document.getElementById(id),r=e.getBoundingClientRect();return[id,{pixels:[e.width,e.height],css:[r.width,r.height],filter:getComputedStyle(e).imageRendering}]})));
 const results={dimensions,page_errors:errors,modes:{}};
 const base='Game/Assets/Trials/OfficerBodyBaseV2/';
 for(const [mode,file] of Object.entries({body:'body-front.png',skin:'skin-mask.png',edit:'edit-mask.png',preserve:'preserve-mask.png'})){
  await page.evaluate(mode=>{let s=document.getElementById('mode');s.value=mode;s.dispatchEvent(new Event('change'))},mode);
  results.modes[mode]=await compare('body',base+file);
 }
 results.native_rgba_difference=await compare('native',base+'body-front.png');
 await page.evaluate(()=>{let s=document.getElementById('mode');s.value='body';s.dispatchEvent(new Event('change'));let e=document.getElementById('opacity');e.value=0;e.dispatchEvent(new Event('input'))});results.overlay_zero_difference=await compare('overlay',base+'body-front.png');
 await page.evaluate(()=>{let e=document.getElementById('opacity');e.value=100;e.dispatchEvent(new Event('input'))});results.overlay_full_reference_difference=await compare('overlay','Game/Assets/Trials/OfficerContextV1/front-64x96.png');
 await page.evaluate(()=>document.getElementById('contour').onclick());results.contour_difference=await compare('overlay','Art/Officer/BodyBaseV2/source-contour-overlay.png');
 for(const theme of ['white','black','checker'])await page.evaluate(theme=>document.querySelector('button[data-bg="'+theme+'"]').onclick(),theme);
 await page.evaluate(()=>{let e=document.getElementById('opacity');e.value=50;e.dispatchEvent(new Event('input'))});
 if(errors.length||Object.values(results.modes).some(v=>v!==0)||results.native_rgba_difference||results.overlay_zero_difference||results.overlay_full_reference_difference||results.contour_difference||JSON.stringify(dimensions.native.css)!=='[64,96]'||JSON.stringify(dimensions.body.css)!=='[256,384]')throw Error(JSON.stringify(results));
 await page.screenshot({path:path.join(root,'Docs/Proposals/Evidence/officer-body-source-browser.png'),fullPage:true});results.status='PASS';
 fs.writeFileSync(path.join(root,'Art/Officer/BodyBaseV2/browser-validation.json'),JSON.stringify(results,null,2)+'\n');console.log(JSON.stringify(results));
 }finally{await browser.close()}
})().catch(e=>{console.error(e);process.exit(1)});
