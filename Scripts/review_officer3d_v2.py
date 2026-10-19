"""Build a local review from verified real GPU captures, preserving source screenshots."""
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import sys,json,html
import officer3d_v2_pipeline as gate
R=Path(sys.argv[1]);E=R/'Art/Officer3D/TrialV2';O=E/'runtime-local';report=gate.read(E/'runtime-report.json');assert report['runtime_binding']==gate.require(R,'runtime')
for n,h in report['evidence_sha256'].items():assert gate.sha(O/n)==h,n
font=ImageFont.truetype('C:/Windows/Fonts/meiryo.ttc',17)
def sheet(name,items,columns=4):
 cellw,cellh=270,290;canvas=Image.new('RGB',(columns*cellw,((len(items)+columns-1)//columns)*cellh),'#1b282b');draw=ImageDraw.Draw(canvas)
 for i,(label,file) in enumerate(items):
  image=Image.open(O/file).convert('RGB');image=image.crop((335,175,625,465));image.thumbnail((250,250));x=i%columns*cellw+10;y=i//columns*cellh+35;canvas.paste(image,(x+(250-image.width)//2,y));draw.text((x,y-28),label,font=font,fill='#e8ddc4')
 canvas.save(O/name)
sheet('lod-contact.png',[(level+' / '+clip,'lod-'+level+'-'+clip+'.png') for level in ['near','mid','far'] for clip in ['idle','walk','attack']],3)
sheet('veteran-contact.png',[(level+' / '+str(a)+'°','veteran-'+level+'-'+str(a)+'-idle.png') for level in ['near','mid','far'] for a in [0,90,180,270]])
sheet('armor-contact.png',[(level+' / '+slot,'off-'+level+'-'+slot+'.png') for level in ['near','mid','far'] for slot in ['mask','weapon','underclothes','cloak','helmet','torso','neck_guard','shoulders','arms','feet','waist','thighs','shins']],5)
sheet('color-contact.png',[('基準','color-0-cloth-before.png'),('布だけ変更','color-0-cloth-after.png'),('漆だけ変更','color-0-lacquer-after.png'),('金属だけ変更','color-0-metal-after.png')])
frames=[Image.open(O/f'delivery-frame-{i:02}.png').convert('RGB') for i in range(24)]
frames[0].save(O/'delivery-preview.gif',save_all=True,append_images=frames[1:],duration=65,loop=0)
perf=report['performance'];table=''.join('<tr><td>'+label+'</td>'+''.join(f'<td>{perf[f"{kind}-{n}"]["fps"]:.1f} fps</td>' for n in [1,10,50])+'</tr>' for kind,label in [('source','元モデル'),('near','近距離'),('mid','中距離'),('far','遠距離')])
page='<!doctype html><html lang="ja"><meta charset="utf-8"><title>百代鏡 / 3D武将 第2試作</title><style>body{background:#142124;color:#eee1c8;font:18px Meiryo,sans-serif;max-width:1440px;margin:32px auto;padding:0 24px}img{max-width:100%;height:auto;border:1px solid #586665}table{border-collapse:collapse}td,th{padding:12px 22px;border:1px solid #67716a}p{line-height:1.8}a{color:#d4bc81}h2{margin-top:44px}</style><h1>3D武将 / 軽量化・色分け・交換・ゲーム接続</h1><p>実Godot描画。完成した正式ゲーム画面ではなく、既存の命令配送・世界時間につないだ管理用の試作です。元モデルは保持し、近・中・遠のモデルを別に用意しました。</p><h2>共通時間で使者が移動</h2><img src="delivery-preview.gif"><p>命令を発送→使者が移動→Coreで本人が受領。表示だけでは時刻・乱数・徴収を進めません。一般の部隊移動・合戦・内応工作は今回追加していません。</p><h2>同じ条件の性能比較</h2><table><tr><th>モデル</th><th>1体</th><th>10体</th><th>50体</th></tr>TABLE</table><p>RTX4060Ti / 1280×800 / GL Compatibility / 15フレーム準備＋60フレーム測定。全員歩行、影あり、50体は重複しない配置。平均値であり最低fpsや全国マップの保証値ではありません。</p><h2>近・中・遠 / 待機・歩行・攻撃</h2><img src="lod-contact.png"><p>比較のため同じ大きさで掲載。遠距離モデルは通常65px未満で使用し、近づいたときは自動で細かいモデルへ戻します。</p><h2>布・漆・金属の色を独立変更</h2><img src="color-contact.png"><p>材質IDとUVから作る共通マスクを全方向で共有。4方向の実GPU比較で範囲外の変化は0px。色は乗算で、黒を自由な明色へ置換する機能ではありません。</p><h2>交換武将 / 同じ体・骨格・動作</h2><img src="veteran-contact.png"><p>顔型・結い髪・短髭・装備・色をIDで交換して保存。髪と髭は再利用の確認用に作った簡易形状で、完成したキャラクターデザインではありません。</p><h2>13部位の着脱 / 各LOD</h2><img src="armor-contact.png"><p>各部位を1つずつ外した歩行姿勢。全8192通りの組合せを検査したという意味ではありません。</p><h2>操作画面</h2><img src="game-world.png"><p>ゲームの「3D武将・配送」タブ、または Scripts/Show-Officer3DV2.ps1 から実行。内応工作は「左手前に自分、右奥に相手」の対面構図のみ記録し、制作は保留です。</p></html>'.replace('TABLE',table)
if (O/'main-game.png').exists():page=page.replace('src="game-world.png"','src="main-game.png"')
(O/'review.html').write_text(page,encoding='utf-8')
print('V2_REVIEW_CREATED',O/'review.html')
