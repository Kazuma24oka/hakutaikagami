"""Derived review sheets from SHA-verified Godot captures. Does not grant visual approval."""
from pathlib import Path
import sys,json,math,html
from PIL import Image,ImageDraw
import officer3d_v3_pipeline as gate

root=Path(sys.argv[1]);base=root/'Art/Officer3D/TrialV3';local=base/'runtime-local'
report=gate.read(base/'runtime-report.json')
assert report['runtime_binding']==gate.require(root,'runtime')
names=list(report['evidence_sha256'])
for name,h in report['evidence_sha256'].items():assert gate.sha(local/name)==h,name
groups={p:[n for n in names if n.startswith('preset-'+p+'-')] for p in ['commander','veteran','red_guard']}
groups.update({'equipment':[n for n in names if n.startswith(('off-','swap-'))],
 'joints':[n for n in names if n.startswith('joint-')],
 'base':[n for n in names if n.startswith('base-')],
 'skin':[n for n in names if n.startswith('skin-')],
 'mixed':[n for n in names if n.startswith('mixed-')]})
derived=[]
for label,items in groups.items():
 cols=4;w,h=384,312;sheet=Image.new('RGB',(cols*w,math.ceil(len(items)/cols)*h),'#172027');draw=ImageDraw.Draw(sheet)
 for i,n in enumerate(items):
  im=Image.open(local/n).convert('RGB');im.thumbnail((w,h-25));x=i%cols*w;y=i//cols*h;sheet.paste(im,(x+(w-im.width)//2,y));draw.text((x+4,y+h-21),n,fill='white')
 name='sheet-'+label+'.jpg';sheet.save(local/name,quality=94);derived.append(name)
for clip,duration in [('walk',117),('attack',108)]:
 frames=[Image.open(local/f'joint-{clip}-{i:02}.png').convert('RGB') for i in range(12)]
 name='motion-'+clip+'.gif';frames[0].save(local/name,save_all=True,append_images=frames[1:],duration=duration,loop=0);derived.append(name)
sections=''.join(f'<section><h2>{html.escape(n)}</h2><a href="{n}"><img loading="lazy" src="{n}"></a></section>' for n in ['creator-ui.png','motion-walk.gif','motion-attack.gif']+derived[:8]+['mixed-near-50.png'])
page='''<!doctype html><meta charset="utf-8"><title>百代鏡 第3試作</title><style>body{background:#172027;color:#ede8dc;font:18px sans-serif;max-width:1400px;margin:24px auto}img{max-width:100%;height:auto}section{margin:40px 0}a{color:#dbc392}</style><h1>武将作成・別甲冑 第3試作</h1><p>実Godotの原本283枚から作成。赤漆の具足は資料に着想を得た簡略試作で、史実品の精密復元ではありません。画像を選ぶと原寸を表示します。</p><p>待機・歩行・攻撃、着脱、色変更、保存復旧、1/10/50体を検査。50体は約42〜44fps、60fps未達。見た目の最終採用は未承認です。</p>'''+sections+'<h2>原本一覧</h2>'+''.join(f'<a href="{html.escape(n)}">{html.escape(n)}</a><br>' for n in names)
fps=[p['fps'] for key,p in report['performance'].items() if key.endswith('-50')]
page=page.replace('約42〜44fps',f'約{min(fps):.0f}〜{max(fps):.0f}fps')
(local/'review.html').write_text(page,encoding='utf-8');derived.append('review.html')
(local/'derived-evidence.json').write_text(json.dumps({n:gate.sha(local/n) for n in derived},indent=2)+'\n',encoding='utf-8')
print('V3_REVIEW_DERIVED',len(derived))
