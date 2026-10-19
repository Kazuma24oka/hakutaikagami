"""Review derived only from verified normal-Main military GPU captures; no automatic visual approval."""
from pathlib import Path
import sys,json,html,math
from PIL import Image,ImageDraw
from validate_military_grid import require,read,sha

root=Path(sys.argv[1]).resolve();base=root/'Art/Officer3D/MilitaryGridV1';local=base/'runtime-local';report=read(base/'runtime-report.json');assert report['runtime_plan_sha256']==require(root)
for n,h in report['evidence_sha256'].items():assert sha(local/n)==h,n
names=list(report['evidence_sha256']);w,h=640,425;sheet=Image.new('RGB',(w*3,h*math.ceil(len(names)/3)),'#18272a');draw=ImageDraw.Draw(sheet)
for i,n in enumerate(names):
 im=Image.open(local/n).convert('RGB');im.thumbnail((w,h-25));x=i%3*w;y=i//3*h;sheet.paste(im,(x,y));draw.text((x+4,y+h-21),n,fill='white')
sheet.save(local/'contact-sheet.jpg',quality=95)
left=Image.open(local/'contact-and-fight-2.png').convert('RGB');right=Image.open(local/'report-received-stale.png').convert('RGB');pair=Image.new('RGB',(left.width+right.width,max(left.height,right.height)),'#18272a');pair.paste(left,(0,0));pair.paste(right,(left.width,0));pair.save(local/'perspective-comparison.jpg',quality=96)
sections=[('initial-red-unseen.png','未発見：相手の姿・位置・兵数は表示しない'),('perspective-comparison.jpg','同じ第9刻：左は直接目撃、右は第6刻の報告だけを知る武将'),('exit-not-globally-known.png','相手が退場しても、受領済みの古い報告は勝手に消さない'),('absence-report-received.png','不在の報告が届いて初めて記憶表示を更新'),('bridge-crossing.png','川は橋を通って移動'),('withdraw-over-bridge.png','所属軍の退路へ、橋を使って撤退'),('cancel-effective.png','取消の到達と指定効力時点を分離'),('deadline-expired.png','到達前の着手期限切れ'),('paused-moving-restored.png','移動途中の位相・外見・配送中の報告を通常保存から復旧'),('restart.png','別プロセスで同じ状態を復旧'),('contact-sheet.jpg','原本25枚の比較表')]
body=''.join(f'<section><h2>{html.escape(title)}</h2><a href="{name}"><img loading="lazy" src="{name}"></a></section>' for name,title in sections)
page='''<!doctype html><html lang="ja"><meta charset="utf-8"><title>百代鏡 部隊・軍令・認識・地図</title><style>body{background:#18272a;color:#eee5d2;font:18px Meiryo,sans-serif;max-width:1600px;margin:24px auto;padding:0 20px}img{max-width:100%;height:auto}section{margin:44px 0}a{color:#dbc28a}p{line-height:1.8}</style><h1>部隊・軍令・認識・地形の統合検証</h1><p>通常Mainの実Godot画面。部隊と指揮権を分け、既存の共通配送で軍令・報告を運び、武将が知った情報だけを表示します。原本25枚と派生を区別しています。</p><p>11×7の演習地図。街道・平地・林に移動コストがあり、川は橋で渡り、林・岩が視線を遮ります。赤青は明示した演習の対抗区分で、政治的所属や権限を自動変更しません。損害式・視界・歩行時間は検証用です。</p><p>軍令紛失→失敗報告→再送と原本遅着→一度だけ履行、取消・着手期限、目撃と遅延報告、途中保存・別プロセス復旧を検査。地形は簡易表示で、正式な軍事AI・全国地図・兵站・攻城戦は未実装です。</p>'''+body+'<h2>原本</h2>'+''.join(f'<a href="{html.escape(n)}">{html.escape(n)}</a><br>' for n in names)+'</html>'
(local/'review.html').write_text(page,encoding='utf-8');derived={n:sha(local/n) for n in ['contact-sheet.jpg','perspective-comparison.jpg','review.html']};(local/'derived-evidence.json').write_bytes((json.dumps(derived,indent=2)+'\n').encode());print('MILITARY_REVIEW_DERIVED',len(names))
