from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import json,sys
R=Path(sys.argv[1]) if len(sys.argv)>1 else Path(r'C:/Workspace/Projects/Games/hakutaikagami');D=R/'Art/Officer3D/TrialV1/runtime-local'
sys.path.insert(0,str(R/'Scripts'));import officer3d_pipeline as gate
verified=gate.read(R/'Art/Officer3D/TrialV1/runtime-report.json');assert verified['runtime_binding']==gate.require(R,'runtime')[0]
for name,digest in verified['evidence_sha256'].items():assert gate.sha(D/name)==digest,'Capture changed: '+name
font=ImageFont.truetype('C:/Windows/Fonts/meiryo.ttc',15)
def sheet(names,path,cols=4,box=(360,300,590,530)):
 out=Image.new('RGB',(cols*250,((len(names)+cols-1)//cols)*265),'#18232c');draw=ImageDraw.Draw(out)
 for i,(name,label) in enumerate(names):
  im=Image.open(D/(name+'.png')).crop(box).convert('RGB');x=(i%cols)*250;y=(i//cols)*265;out.paste(im,(x+10,y+30));draw.text((x+10,y+5),label,font=font,fill='white')
 out.save(D/path)
sheet([(f'{kind}-{a}',f'{kind} {a}° / 実寸') for kind in ['full','bare'] for a in [0,90,180,270]],'views-contact.png')
slots=['mask','weapon','underclothes','cloak','helmet','torso','neck_guard','shoulders','arms','feet','waist','thighs','shins']
for pose in ['idle','walk']:sheet([(f'off-{s}-{pose}',s+' OFF / '+pose) for s in slots],f'off-{pose}-contact.png',cols=5)
frames=[Image.open(D/f'walk-frame-{i:02}.png').crop((300,260,650,550)).convert('RGB') for i in range(24)]
frames[0].save(D/'walk-preview.gif',save_all=True,append_images=frames[1:],duration=57,loop=0)
result=json.loads((D/'results.json').read_text())
rows=''.join(f"<tr><td>{r['count']}体</td><td>{r['frame_mean_ms']:.2f} ms</td><td>{r['estimated_fps']:.1f} fps</td></tr>" for r in result['performance'])
html='''<!doctype html><html lang="ja"><meta charset="utf-8"><title>百代鏡 3D武将V1 実機確認</title><style>body{background:#18232c;color:#eee;font:16px system-ui;max-width:1280px;margin:30px auto;padding:20px}img{max-width:100%;image-rendering:pixelated}section{padding:24px 0;border-bottom:1px solid #53626a}table{border-collapse:collapse}td,th{padding:12px;border:1px solid #53626a}a{color:#93d7ff}</style><h1>百代鏡 / 3D武将V1</h1><p>Godot 4.5.2 .NETで描画した実画像。ゲーム内は3D、立ち絵は2Dイラスト。外見のユーザー承認は未了です。</p><section><h2>操作画面</h2><img src="review-default.png"><p>再表示：リポジトリの Scripts/Show-Officer3DTrial.ps1。待機・歩行・斬撃、全周表示、肌と装備色、13区分の着脱、外見保存・復旧を確認できます。</p></section><section><h2>歩行</h2><img src="walk-preview.gif"><p>実Godotから採取した24コマ。制作ツールで描き直した画像ではありません。</p></section><section><h2>同じモデルの4方向 / 装備なし</h2><img src="views-contact.png"><p>画像は元の画素数で切り出し。肌色変更の検査では肌以外のRGB差分0。装備色変更も装備以外のRGB差分0。</p></section><section><h2>部位ごとの着脱</h2><img src="off-idle-contact.png"><img src="off-walk-contact.png"></section><section><h2>人数別の基準値</h2><p>RTX 4060 Ti / 1280×800 / OpenGL Compatibility / VSyncなし / 自動LODなし / 影あり / 60フレーム平均。製品の性能保証ではありません。</p><table><tr><th>体数</th><th>平均フレーム時間</th><th>換算FPS</th></tr>'''+rows+'''</table><p>約101万三角形の元モデルのため、50体では軽量化が必要です。近・中・遠LODは次段階です。</p></section></html>'''
(D/'review.html').write_text(html,encoding='utf-8')
print(D/'review.html')
