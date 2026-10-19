"""Derive review sheets/GIFs only from SHA-verified Godot captures, not generated art."""
from pathlib import Path
import hashlib,json,sys,html
from PIL import Image,ImageDraw
root=Path(sys.argv[1]).resolve();base=root/'Art/Officer3D/FormationWorldV1';local=base/'runtime-local'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
report=json.loads((base/'runtime-report.json').read_text(encoding='utf-8'));names=list(report['evidence_sha256'])
for name,h in report['evidence_sha256'].items():assert sha(local/name)==h,name
sheet=Image.new('RGB',(1600,((len(names)+3)//4)*275),'#172329');d=ImageDraw.Draw(sheet)
for i,n in enumerate(names):
 im=Image.open(local/n).convert('RGB');im.thumbnail((400,250));x=(i%4)*400;y=(i//4)*275;sheet.paste(im,(x,y));d.text((x+5,y+253),n,fill='#e0d2b6')
sheet.save(local/'contact-sheet.jpg',quality=90)
for title,selected,duration in [('continuous-zoom.gif',['deployed-nation.png']+[f'zoom-{i:02}.png' for i in range(12)]+['deployed-near.png'],120),('deployment.gif',['sent-not-arrived.png','arrival-1.png','arrival-3.png','arrival-5.png','bridge-queue.png','deployment-3.png','deployment-8.png','deployed-near.png'],850)]:
 frames=[]
 for n in selected:
  im=Image.open(local/n).convert('RGB');im=im.resize((1120,700));draw=ImageDraw.Draw(im);draw.rectangle((0,679,1120,700),fill='#172329');draw.text((10,682),n,fill='#ffffff');frames.append(im)
 frames[0].save(local/title,save_all=True,append_images=frames[1:],duration=duration,loop=0,optimize=False)
buttons=''.join('<button data-image="'+html.escape(n)+'">'+html.escape(n)+'</button>' for n in ['nation-before.png','region-before.png','near-before.png','line-draft.png','sent-not-arrived.png','arrival-1.png','arrival-3.png','arrival-5.png','bridge-queue.png','deployed-near.png','deployed-region.png','deployed-nation.png','restart.png'])
page='''<!doctype html><html lang="ja"><meta charset="utf-8"><meta name="viewport" content="width=device-width"><title>百代鏡・作戦線と連続地図</title><style>body{background:#172329;color:#e6dfcf;font:17px system-ui,sans-serif;margin:24px}main{max-width:1500px;margin:auto}button{font:inherit;background:#31474c;color:inherit;padding:8px;margin:3px;border:1px solid #7c8b85;border-radius:4px}img{width:100%;height:auto}nav{display:flex;flex-wrap:wrap}p{line-height:1.6}h1{font-size:25px}h2{font-size:21px}</style><main><h1>作戦線を引く → 命令が届く → 3部隊が展開</h1><p>同じGodotシーンを全国概略・地域・近景へ拡大。架空地形の試作で、全国の軍事移動は未実装です。演習地域は従来の川・橋と同じ座標。伝令到達は1・3・5刻、全隊の配置完了は第20刻、以下の完成画面は第22刻です。</p><h2>連続ズーム（実描画の連続フレーム）</h2><img src="continuous-zoom.gif" alt="同じ地形・部隊へカメラが近づく"><h2>展開の経過（時刻を抜粋）</h2><img src="deployment.gif" alt="配送の時間差に応じて部隊が移動して並ぶ"><h2>実画面を選ぶ</h2><nav>'''+buttons+'''</nav><p id="selected">deployed-near.png</p><img id="capture" src="deployed-near.png" alt="Godot実画面"><p>Core317件・原本28枚・独立プロセスでの保存復旧を検証。自己画像確認はユーザー採用とは別です。一つの線の演習のみ。再指定は初期化。軍議、独断行動の評価、歴史地理、全国経路探索、複数部隊の交戦は未実装。既存の髪・LOD・多数表示性能の制約は保持。</p></main><script>document.querySelectorAll('[data-image]').forEach(b=>b.addEventListener('click',()=>{document.getElementById('capture').src=b.dataset.image;document.getElementById('selected').textContent=b.dataset.image}));</script></html>'''
(local/'review.html').write_text(page,encoding='utf-8')
derived={n:sha(local/n) for n in ['contact-sheet.jpg','continuous-zoom.gif','deployment.gif','review.html']}
(local/'derived-evidence.json').write_text(json.dumps(derived,indent=2)+'\n',encoding='utf-8');print('FORMATION_REVIEW_DERIVED',len(names))
