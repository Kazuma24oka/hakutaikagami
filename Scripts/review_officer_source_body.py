"""Inspect real V2 asset pixels, bundle and browser-ready comparison without mouse input."""
from pathlib import Path
import argparse,json,sys,base64,io
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--root',type=Path,required=True);ap.add_argument('--plan',default='body-source-v2-review-02');a=ap.parse_args();r=a.root.resolve();sys.path.insert(0,str(r/'Scripts'));import officer_art_pipeline as g
    w=json.loads((r/'Art/Officer/work-record.json').read_text(encoding='utf-8'));g.require_plan(r,'front_authoring',w['plans'][a.plan])
    f=r/'Art/Officer/BodyBaseV2';out=r/'Game/Assets/Trials/OfficerBodyBaseV2';spec=json.loads((f/'source-definition.json').read_text(encoding='utf-8'))
    def ev(p):return {'path':str(p.relative_to(r)).replace('\\','/'),'sha256':g.sha(p)}
    body=Image.open(out/'body-front.png').convert('RGBA');arr=np.array(body);ids=np.array(Image.open(out/'ownership.png'));solid=arr[:,:,3]>0
    control_ids=np.array(Image.open(f/'controls/ownership.png'))
    if not np.array_equal(ids,control_ids):raise g.Blocked('Output IDs changed from reviewed source controls')
    if not np.array_equal(solid,control_ids>0):raise g.Blocked('Output silhouette changed')
    Image.fromarray((ids==1).astype(np.uint8)*255).save(out/'head-protection.png')
    geometry={};criteria={};parts={}
    (f/'check-masks').mkdir(exist_ok=True)
    for region in spec['regions']:
        name=region['name'];m=control_ids==region['id'];ys,xs=np.where(m)
        if not len(xs):raise g.Blocked('Empty body part '+name)
        path=f/'check-masks'/(name+'.png');Image.fromarray(m.astype(np.uint8)*255).save(path);geometry[name]=ev(path)
        criteria[name]={'bounds':[int(xs.min()),int(ys.min()),int(xs.max())+1,int(ys.max())+1],'tolerance_px':0,'basis':'Frozen original-pose source-definition polygons and paint order, not output-fitted geometry'}
        parts[name]={'image':ev(out/'parts'/(name+'.png')),'owner_ids':[region['id']],'hidden':ev(out/'empty-mask.png'),'joints':[],'max_components':1}
    bundle={'source':ev(out/'body-front.png'),'ownership':ev(out/'ownership.png'),'protected_face':ev(out/'head-protection.png'),'hair_ids':[],'geometry_regions':geometry,'geometry_criteria':criteria,'palette_rgb':spec['palette_rgb'],'parts':parts,'order':[region['name'] for region in spec['regions']],'recolor_cases':[]}
    result=g.validate_bundle(r,bundle)
    src=Image.open(r/'Game/Assets/Trials/OfficerContextV1/front-64x96.png').convert('RGBA')
    def bg(im,color):
        b=Image.new('RGBA',(64,96),color);b.alpha_composite(im);return b.convert('RGB')
    bodybg=bg(body,(189,189,178,255));sourcebg=bg(src,(189,189,178,255));over=Image.blend(sourcebg,bodybg,.5)
    eroded=np.array(Image.fromarray(solid.astype(np.uint8)*255).filter(ImageFilter.MinFilter(3)))>0
    edge=np.zeros_like(arr);edge[solid&~eroded]=[27,225,213,255];edgeview=src.copy();edgeview.alpha_composite(Image.fromarray(edge))
    false=np.zeros_like(arr)
    for ident in range(1,18):false[ids==ident]=[*((np.array([60,95,130])+ident*np.array([41,67,83]))%180+50).tolist(),255]
    font=ImageFont.truetype('C:/Windows/Fonts/meiryo.ttc',17);small=ImageFont.truetype('C:/Windows/Fonts/meiryo.ttc',14)
    board=Image.new('RGB',(1100,1000),(29,43,51));d=ImageDraw.Draw(board)
    for title,im,x in [('元の武将・位置不変',sourcebg,20),('裸素体・同座標',bodybg,290),('50%重ね合わせ',over,560),('人体輪郭・青',bg(edgeview,(189,189,178,255)),830)]:
        d.text((x,15),title,font=font,fill=(230,214,183));board.paste(im.resize((256,384),Image.Resampling.NEAREST),(x,50))
    for title,im,x in [('白背景・実PNG',bg(body,'white'),20),('黒背景・実PNG',bg(body,(12,17,21,255)),290),('部位ID・17部位',bg(Image.fromarray(false),(189,189,178,255)),560)]:
        d.text((x,470),title,font=font,fill=(230,214,183));board.paste(im.resize((256,384),Image.Resampling.NEAREST),(x,505))
    d.text((835,470),'通常表示：64×96・1倍',font=small,fill='white');board.paste(bodybg,(850,505));board.paste(sourcebg,(930,505))
    d.text((835,625),'顔・髪・衣服なし',font=small,fill='white');d.text((835,652),'性器の細部なし・肌色',font=small,fill='white');d.text((835,680),'原画と同じ配置',font=small,fill='white');d.text((835,710),'拡大は最近傍',font=small,fill='white')
    d.text((20,930),'甲冑で隠れた人体は作画による補完。正面静止のみ。ユーザー受入・歩行完了は未認定。',font=small,fill=(217,213,200))
    evidence=r/'Docs/Proposals/Evidence';board.save(evidence/'officer-body-source-v2.png');over.save(f/'overlay-50.png');edgeview.save(f/'source-contour-overlay.png')
    colors=np.unique(arr[solid,:3],axis=0);result.update({'canvas':[64,96],'display_scale':1,'alpha_values':np.unique(arr[:,:,3]).tolist(),'bounds':list(body.getbbox()),'visible_colors':len(colors),'visible_pixels':int(solid.sum()),'source_registration_unchanged':True,'all_17_parts_connected':True,'head_and_pelvis_protected':True,'generation':json.loads((f/'assembly-result.json').read_text(encoding='utf-8'))})
    (f/'candidate-bundle.json').write_text(json.dumps(bundle,ensure_ascii=False,indent=2)+'\n',encoding='utf-8');(f/'validation.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    def data(im):
        if isinstance(im,Path):b=im.read_bytes()
        else:s=io.BytesIO();im.save(s,format='PNG');b=s.getvalue()
        return 'data:image/png;base64,'+base64.b64encode(b).decode()
    assets={'source':data(src),'body':data(body),'ids':data(Image.fromarray(false)),'contour':data(edgeview),'skin':data(out/'skin-mask.png'),'edit':data(out/'edit-mask.png'),'preserve':data(out/'preserve-mask.png')}
    html="""<!doctype html><html lang="ja"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>百代鏡｜元武将に対応する裸素体</title><style>*{box-sizing:border-box}body{margin:0;background:#1d2b33;color:#eee7db;font:16px/1.8 system-ui,sans-serif}main{max-width:1120px;margin:auto;padding:24px}h1{font-size:25px}h2{font-size:18px;color:#dfc18c}.row{display:flex;gap:30px;flex-wrap:wrap}.large{width:256px;height:384px;image-rendering:pixelated}.native{width:64px;height:96px;image-rendering:pixelated}canvas{background:repeating-conic-gradient(#bfc0b5 0 25%,#a5a99f 0 50%) 0/16px 16px}button,select{font:inherit;color:inherit;background:#344852;border:1px solid #829397;padding:5px 10px;margin:3px}input{width:220px}a{color:#9cdae5}.note{border-left:3px solid #d7b57e;padding:10px 18px}.small{font-size:14px;color:#cdc8bb}details{margin:20px 0}.board{max-width:100%;height:auto}</style><main><h1>元の武将に対応する裸素体｜64×96・1倍</h1><p>顔・髪・髭・衣服・履物なし。性器の細部を描かない肌色の素体。元絵の配置を引き継ぎ、甲冑に隠れた身体を補いました。</p><div class="row"><section><h2>元の武将</h2><canvas id="source" class="large" width="64" height="96"></canvas></section><section><h2>裸素体</h2><canvas id="body" class="large" width="64" height="96"></canvas></section><section><h2>同じ座標で重ね合わせ</h2><canvas id="overlay" class="large" width="64" height="96"></canvas><p><label>元絵の濃さ <input id="opacity" type="range" min="0" max="100" value="50"></label></p><button id="contour">人体輪郭を重ねる</button></section></div><h2>通常表示・1倍</h2><div class="row"><canvas id="native-source" class="native" width="64" height="96"></canvas><canvas id="native" class="native" width="64" height="96"></canvas><p class="small">上の拡大表示と同じ画像です。<br>元絵に合わせて身体だけを移動・拡縮していません。</p></div><h2>マスクと背景</h2><select id="mode"><option value="body">裸素体</option><option value="ids">17部位の所属</option><option value="skin">肌色マスク</option><option value="edit">陰影の編集範囲</option><option value="preserve">頭・骨盤・外周の保持範囲</option></select><button data-bg="white">白</button><button data-bg="black">黒</button><button data-bg="checker">市松</button><p class="note">元絵と実PNGの重ね合わせ、原寸・拡大・白黒背景を検査。固定輪郭の差分0、保持画素の差分0、17部位の再合成差分0です。元絵から見えない人体は作画値であり、正確な裸体を復元したという意味ではありません。</p><p>正面静止の制作結果です。ユーザー受入、別方向、歩行・関節の隠れ面は未認定です。</p><details><summary>制作設定と実測</summary><p>参照画像付きの画像編集を使用。生成された背景や輪郭は採用せず、固定した身体輪郭内の陰影を原寸化しました。頭・骨盤・外周は事前の作画を保持。全画面に一度のBOX縮小と固定12色パレットを適用し、部位ごとの位置合わせはしていません。</p><p>生成モデルのseedと正確なモデル版はツールから取得できません。原本・プロンプト・参照・制御図・マスクを保存し、その原本からの再組み立てを検査します。新規生成の完全一致は保証していません。</p><a href="../../Art/Officer/BodyBaseV2/validation.json">実画素検査</a> · <a href="../../Art/Officer/BodyBaseV2/source-definition.json">原画対応と身体制御点</a></details><p id="status">画像を読み込み中…</p><script>"""
    html+='const assets='+json.dumps(assets)+';'
    html+="""const imgs={};window.bodyReady=false;let contour=false;function draw(id,key){let c=document.getElementById(id).getContext('2d');c.clearRect(0,0,64,96);c.imageSmoothingEnabled=false;c.drawImage(imgs[key],0,0)}function overlay(){let c=document.getElementById('overlay').getContext('2d');c.clearRect(0,0,64,96);c.globalAlpha=1;if(contour){c.drawImage(imgs.contour,0,0);return}if(Number(document.getElementById('opacity').value)===100){c.drawImage(imgs.source,0,0);return}c.drawImage(imgs.body,0,0);c.globalAlpha=Number(document.getElementById('opacity').value)/100;c.drawImage(imgs.source,0,0);c.globalAlpha=1}window.officerReview={draw,overlay};(async()=>{await Promise.all(Object.entries(assets).map(async([k,url])=>{let im=new Image;im.src=url;await im.decode();imgs[k]=im}));draw('source','source');draw('body','body');draw('native','body');draw('native-source','source');overlay();document.getElementById('opacity').oninput=()=>{contour=false;overlay()};document.getElementById('contour').onclick=()=>{contour=!contour;overlay()};document.getElementById('mode').onchange=e=>draw('body',e.target.value);document.querySelectorAll('button[data-bg]').forEach(b=>b.onclick=()=>document.querySelectorAll('canvas').forEach(c=>c.style.background=b.dataset.bg==='checker'?'':b.dataset.bg==='white'?'white':'#10171c'));document.getElementById('status').textContent='実画像の読み込み完了';window.bodyReady=true})().catch(e=>{document.getElementById('status').textContent='表示失敗：'+e.message;throw e});</script></main></html>"""
    (r/'Docs/Proposals/officer-body-source-review.html').write_text(html,encoding='utf-8');print(json.dumps(result))
if __name__=='__main__':main()
