"""Static layer/recolor diagnostics on provisionally accepted body. No new anatomy or generation."""
from pathlib import Path
import argparse,json,sys,base64,io
import numpy as np
from PIL import Image,ImageDraw,ImageFont

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--root',type=Path,required=True);a=ap.parse_args();r=a.root.resolve();sys.path.insert(0,str(r/'Scripts'));import officer_art_pipeline as g
    w=json.loads((r/'Art/Officer/work-record.json').read_text(encoding='utf-8'));plan=w['plans']['body-v2-layer-diagnostics'];g.require_plan(r,'front_authoring',plan)
    f=r/'Art/Officer/BodyBaseV2';bundle=json.loads((f/'candidate-bundle.json').read_text(encoding='utf-8'));g.validate_bundle(r,bundle)
    source=Image.open(g.evidence(r,bundle['source'])).convert('RGBA');arr=np.array(source);ids=np.array(Image.open(g.evidence(r,bundle['ownership'])))
    parts={k:Image.open(g.evidence(r,v['image'])).convert('RGBA') for k,v in bundle['parts'].items()};order=bundle['order']
    palette=np.array(bundle['palette_rgb'],np.int16)
    tones={'original':palette,'light':np.minimum(255,palette+np.array([10,12,13])), 'tan':np.maximum(0,palette-np.array([16,23,28]))}
    translated={'head':'頭','neck':'首','chest':'胸','abdomen':'腹','pelvis':'骨盤','upper_arm_right':'右上腕','forearm_right':'右前腕','hand_right':'右手','upper_arm_left':'左上腕','forearm_left':'左前腕','hand_left':'左手','thigh_right':'右太腿','calf_right':'右すね','foot_right':'右足','thigh_left':'左太腿','calf_left':'左すね','foot_left':'左足'}
    target=f/'LayerDiagnostics';target.mkdir(exist_ok=True);assets={};checks={}
    def recolor(im,tone):
        old=np.array(im);new=old.copy()
        for p,q in zip(palette,tones[tone]):new[(old[:,:,3]>0)&np.all(old[:,:,:3]==p,axis=2),:3]=q
        g.validate_recolor(old,new,old[:,:,3]>0);return Image.fromarray(new)
    def data(im):
        s=io.BytesIO();im.save(s,format='PNG');return 'data:image/png;base64,'+base64.b64encode(s.getvalue()).decode()
    for tone in tones:
        assets[tone]={name:data(recolor(im,tone)) for name,im in parts.items()}
        composed=g.compose_parts({name:recolor(im,tone) for name,im in parts.items()},order,set(order))
        expected=recolor(source,tone);diff=int(np.count_nonzero(np.array(composed)!=np.array(expected)))
        if diff:raise g.Blocked('Layer reconstruction/recolor mismatch')
        composed.save(target/(tone+'.png'));checks[tone]={'reconstruction_rgba_difference':diff,'alpha_difference':int(np.count_nonzero(np.array(composed)[:,:,3]!=arr[:,:,3]))}
    removals={}
    for name in order:
        removed=np.array(g.compose_parts(parts,order,set(order)-{name}));expected=arr.copy();expected[ids==bundle['parts'][name]['owner_ids'][0]]=0
        if not np.array_equal(removed,expected):raise g.Blocked('Removal changed other owners: '+name)
        removals[name]=0
    arm={'upper_arm_right','forearm_right','hand_right'};removed=g.compose_parts(parts,order,set(order)-arm);removed.save(target/'right-arm-removed.png')
    empty=g.compose_parts(parts,order,set())
    if np.any(np.array(empty)):raise g.Blocked('Empty selection not transparent')
    report={'status':'STATIC_DIAGNOSTICS_PASS_NOT_PARTS_STAGE_ACCEPTANCE','source':bundle['source'],'source_unchanged':g.sha(r/bundle['source']['path'])==bundle['source']['sha256'],'tones':checks,'single_part_removals_rgba_difference':removals,'empty_selection_transparent':True,'user_scope':'Proceed provisionally despite too skinny/unpleasant appearance; no final body/style acceptance','not_implemented':['face/hair/armor replacements','portrait counterpart resolution','joint hidden surfaces or animation'],'generation_executed':False}
    (target/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    board=Image.new('RGB',(1120,550),(29,43,51));d=ImageDraw.Draw(board);font=ImageFont.truetype('C:/Windows/Fonts/meiryo.ttc',16)
    variants=[('仮基準・再合成',source),('肌色変更・明',recolor(source,'light')),('肌色変更・濃',recolor(source,'tan')),('右腕だけを除去',removed)]
    for x,(title,im) in zip([20,290,560,830],variants):
        d.text((x,14),title,font=font,fill=(230,214,183));tile=Image.new('RGBA',(256,384),(187,189,180,255));tile.alpha_composite(im.resize((256,384),Image.Resampling.NEAREST));board.paste(tile.convert('RGB'),(x,50))
    d.text((20,460),'体型の外見採用ではなく、部位・配色の仕組みを先に確認する仮試験です。',font=font,fill='white');d.text((20,495),'顔・髪・装備・動作の完成は含みません。元の素体PNGは変更していません。',font=font,fill='white')
    board.save(r/'Docs/Proposals/Evidence/officer-body-layer-diagnostics.png')
    groups={'head':['head','neck'],'torso':['chest','abdomen','pelvis'],'rightArm':['upper_arm_right','forearm_right','hand_right'],'leftArm':['upper_arm_left','forearm_left','hand_left'],'rightLeg':['thigh_right','calf_right','foot_right'],'leftLeg':['thigh_left','calf_left','foot_left']}
    html="""<!doctype html><html lang="ja"><meta charset="utf-8"><title>百代鏡｜仮素体の部位確認</title><style>body{margin:0;background:#1d2b33;color:#eee7db;font:16px/1.8 system-ui,sans-serif}main{max-width:1050px;margin:auto;padding:24px}h1{font-size:25px}h2{font-size:18px;color:#dfc18c}.row{display:flex;gap:40px;flex-wrap:wrap}canvas{image-rendering:pixelated;background:repeating-conic-gradient(#bfc0b5 0 25%,#a5a99f 0 50%) 0/16px 16px}#large{width:256px;height:384px}#native{width:64px;height:96px}button,select{font:inherit;background:#344852;color:inherit;padding:6px 12px;border:1px solid #829397;margin:3px}label{display:inline-block;margin:5px 12px 5px 0}.note{border-left:3px solid #d7b57e;padding:8px 16px}a{color:#9ed4e0}.parts{max-width:600px}</style><main><h1>仮素体の部位・肌色確認</h1><p class="note">「細すぎて不自然」という指摘は保留課題です。今回はこの素体を仮に使い、部位の取り外し・再合成・配色が独立して動くかを確認します。体型や画風の正式採用ではありません。</p><div class="row"><section><h2>4倍表示</h2><canvas id="large" width="64" height="96"></canvas></section><section><h2>通常表示・1倍</h2><canvas id="native" width="64" height="96"></canvas><h2>肌色</h2><select id="tone"><option value="original">元の肌色</option><option value="light">明るめ</option><option value="tan">濃いめ</option></select><h2>部位を表示</h2><div id="groups"></div><button id="restore">すべて戻す</button><button id="hide">すべて外す</button><details><summary>17部位を個別に確認</summary><div id="parts" class="parts"></div></details></section></div><p id="status">読み込み中…</p><p>すべて戻した画像は元PNGと画素一致。各部位を外しても他の部位の画素は変わりません。取り外した関節の隠れ面は未補完で、動作・別方向の合格ではありません。</p><p><a href="officer-body-source-review.html">元絵との重ね合わせ</a> · <a href="../../Art/Officer/BodyBaseV2/LayerDiagnostics/validation.json">実画像の検査記録</a></p><script>"""
    html+='const assets='+json.dumps(assets)+';const order='+json.dumps(order)+';const labels='+json.dumps(translated,ensure_ascii=False)+';const groups='+json.dumps(groups)+';'
    html+="""const images={},enabled=new Set(order);let tone='original';window.layersReady=false;function render(){for(let id of ['native','large']){let c=document.getElementById(id).getContext('2d');c.clearRect(0,0,64,96);c.imageSmoothingEnabled=false;for(let key of order)if(enabled.has(key))c.drawImage(images[tone][key],0,0)}for(let e of document.querySelectorAll('[data-part]'))e.checked=enabled.has(e.dataset.part);for(let e of document.querySelectorAll('[data-group]')){let a=groups[e.dataset.group];e.checked=a.every(k=>enabled.has(k));e.indeterminate=!e.checked&&a.some(k=>enabled.has(k))}document.getElementById('status').textContent='表示 '+enabled.size+' / 17 部位'}function choose(keys){enabled.clear();for(let k of keys)if(order.includes(k))enabled.add(k);render()}window.layerReview={choose,setTone(k){if(!assets[k])throw Error('Unknown tone');tone=k;document.getElementById('tone').value=k;render()},order};(async()=>{for(let [k,parts] of Object.entries(assets)){images[k]={};await Promise.all(Object.entries(parts).map(async([name,url])=>{let im=new Image;im.src=url;await im.decode();images[k][name]=im}))}const groupLabels={head:'頭・首',torso:'胴',rightArm:'本人の右腕',leftArm:'本人の左腕',rightLeg:'本人の右脚',leftLeg:'本人の左脚'};for(let [key,parts] of Object.entries(groups)){let l=document.createElement('label'),e=document.createElement('input');e.type='checkbox';e.dataset.group=key;e.onchange=()=>{for(let part of parts)e.checked?enabled.add(part):enabled.delete(part);render()};l.append(e,groupLabels[key]);document.getElementById('groups').append(l)}for(let key of order){let l=document.createElement('label'),e=document.createElement('input');e.type='checkbox';e.dataset.part=key;e.onchange=()=>{e.checked?enabled.add(key):enabled.delete(key);render()};l.append(e,labels[key]);document.getElementById('parts').append(l)}document.getElementById('tone').onchange=e=>window.layerReview.setTone(e.target.value);document.getElementById('restore').onclick=()=>choose(order);document.getElementById('hide').onclick=()=>choose([]);render();window.layersReady=true})().catch(e=>{document.getElementById('status').textContent=e.message;throw e});</script></main></html>"""
    (r/'Docs/Proposals/officer-body-layer-review.html').write_text(html,encoding='utf-8');print(json.dumps(report,ensure_ascii=False))
if __name__=='__main__':main()
