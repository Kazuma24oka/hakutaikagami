from pathlib import Path
import sys,json,html,math
from PIL import Image,ImageDraw
from validate_officer_world import sha,read,require

root=Path(sys.argv[1]).resolve();base=root/'Art/Officer3D/WorldIntegrationV1';local=base/'runtime-local';report=read(base/'runtime-report.json');assert report['runtime_plan_sha256']==require(root)
for n,h in report['evidence_sha256'].items():assert sha(local/n)==h,n
names=list(report['evidence_sha256']);w,h=480,344;sheet=Image.new('RGB',(w*3,h*math.ceil(len(names)/3)),'#182127');draw=ImageDraw.Draw(sheet)
for i,n in enumerate(names):
 im=Image.open(local/n).convert('RGB');im.thumbnail((w,h-24));x=i%3*w;y=i//3*h;sheet.paste(im,(x,y));draw.text((x+4,y+h-20),n,fill='white')
sheet.save(local/'contact-sheet.jpg',quality=95)
frames=[Image.open(local/f'walk-progress-{i}.png').convert('RGB') for i in range(6)]
frames[0].save(local/'walk-progress.gif',save_all=True,append_images=frames[1:],duration=125,loop=0)
body=''.join(f'<section><h2>{html.escape(n)}</h2><a href="{n}"><img src="{n}"></a></section>' for n in ['officer-bindings.png','walk-progress.gif','battle-04-Contact.png','paused-combat-restored.png','retreat-04-Routing.png','ordered-withdrawal.png','restart.png','contact-sheet.jpg'])
page='''<!doctype html><meta charset="utf-8"><title>百代鏡 武将と世界の統合</title><style>body{background:#182127;color:#eee6d6;font:18px sans-serif;max-width:1440px;margin:24px auto}img{max-width:100%;height:auto}a{color:#dac28b}section{margin:40px 0}</style><h1>武将・外見・世界時間・小規模合戦</h1><p>実際の通常Main画面で、外見割当、通常保存、1/2/4倍、停止、接敵、交戦、敗走と撤退、別プロセスでの復旧を検査しました。</p><p>赤100人対青60人の固定検証。接敵と開戦を分離し、明示した交戦指示の到達後に損害が発生します。兵数・士気の式は検証専用で、正式仕様ではありません。敗走した部隊は移動して退場し、武将や生存兵のデータは残ります。</p><p>歩行GIFは実Godotの同じ1刻内の6時点。原本30枚と派生を区別しています。戦場の情報制限、軍事AI、地形、兵站、軍令の紛失・再送は未実装です。</p>'''+body+'<h2>原本</h2>'+''.join(f'<a href="{html.escape(n)}">{html.escape(n)}</a><br>' for n in names)
(local/'review.html').write_text(page,encoding='utf-8')
derived={n:sha(local/n) for n in ['contact-sheet.jpg','walk-progress.gif','review.html']}
(local/'derived-evidence.json').write_text(json.dumps(derived,indent=2)+'\n',encoding='utf-8');print('OFFICER_WORLD_REVIEW_DERIVED',len(names))
