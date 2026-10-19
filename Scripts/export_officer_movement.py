"""Verify real movement captures against existing native parts, then publish replay."""
from pathlib import Path
import json,hashlib
import numpy as np
from PIL import Image
import officer_art_pipeline as g
R=Path(__file__).resolve().parents[1];O=R/'Art/Officer/MovementV13';w=json.loads((R/'Art/Officer/work-record.json').read_text(encoding='utf-8'));p=w['plans'][w['movement_plan']];binding=g.require_plan(R,'motion',p)
report=json.loads((O/'runtime-validation.json').read_text());assert report['status']=='PASS' and report['binding']==binding
names={'front':'officer_motion_trial_v6.json','right':'officer_right_motion_v8.json','back':'officer_back_motion_v10.json','left':'officer_left_motion_v12.json'}
packages={n:json.loads((R/'Game/Data'/f).read_text(encoding='utf-8')) for n,f in names.items()}
def compose(direction,flags,index):
 d=packages[direction];t={}
 for n,v in d['sheets'].items():
  a=np.array(Image.open(R/'Game'/v['path'].removeprefix('res://')).convert('RGBA'))[:,index*64:(index+1)*64].copy();a[a[:,:,3]==0]=0;t[n]=a
 on=lambda n:bool(flags&(1<<d['keys'].index(n)))
 a=np.zeros((96,64,4),np.uint8)
 def over(n):
  b=t[n];live=b[:,:,3]>0;a[live]=b[live]
 if 'operations' in d:
  for op in d['operations']:
   if op.get('when') and not on(op['when']) or op.get('unless') and on(op['unless']):continue
   if op.get('erase'):a[t[op['sheet']][:,:,3]>0]=0
   else:over(op['sheet'])
 else:
  a=t['body'].copy()
  for n in d['order']:
   if on(n):a[t['hide_'+n][:,:,3]>0]=0
  if on('face'):over('face')
  for n in d['order']:
   if on(n):over(n)
  if not on('arms'):over('hands')
  if on('hair'):over('hair')
 assert hashlib.sha256(a.tobytes()).hexdigest()==d['hashes'][flags][index]
 return a
pics=[];checks=[]
for row in report['route']:
 im=Image.open(O/row['file']).convert('RGB');assert im.size==(1280,800)
 a=compose(row['direction'],row['flags'],row['index']);actual=np.array(im.crop((row['x'],row['y'],row['x']+64,row['y']+96)));live=a[:,:,3]>0;assert np.array_equal(actual[live],a[live,:3])
 large=np.array(im.crop((964,400,1220,784)));expected=np.full((96,64,3),[32,45,50],np.uint8);expected[live]=a[live,:3];assert np.array_equal(large,np.repeat(np.repeat(expected,4,axis=0),4,axis=1))
 checks.append({'file':row['file'],'sha256':g.sha(O/row['file']),'native_opaque_pixel_difference':0,'large_all_pixel_difference':0});pics.append(im)
pics[0].save(O/'godot-route.gif',save_all=True,append_images=pics[1:],duration=180,loop=0,disposal=2,optimize=False)
(O/'capture-validation.json').write_bytes((json.dumps({'binding':binding,'frames':checks,'source':'actual Godot viewport PNGs; native opaque pixels, whole4x preview compared to Python; GIF has palette conversion'},indent=2)+'\n').encode())
page='''<!doctype html><html lang="ja"><meta charset="utf-8"><title>百代鏡・4方向移動</title><style>body{margin:24px;background:#202d32;color:#eee;font:16px system-ui}button{padding:10px;margin:6px}.scroll{overflow:auto}canvas{image-rendering:pixelated;width:1280px;height:800px}p{line-height:1.8}</style><h1>4方向の移動確認・64×96／1倍</h1><p>Godot実画面20コマの再生です。実際の操作は起動済みのGodot画面で、矢印またはWASD。キーを離すと待機。装備は4方向共通です。自動巡回中もキーで手動操作へ移れます。</p><button id="play">停止／再開</button><button id="step">1コマ送る</button><span id="status"></span><div class="scroll"><canvas id="replay" width="1280" height="800"></canvas></div><p>画像全体を縮小せず横スクロールで表示。草地・街道・河岸は表示用の仮背景で、地形衝突・合戦・世界時刻への接続は未実施。体型・顎の方向差・隠れ面も暫定です。</p><script>const images=Array.from({length:20},(_,i)=>{const im=new Image();im.src='../../Art/Officer/MovementV13/route-'+String(i).padStart(2,'0')+'.png';return im});let at=0,playing=true;const canvas=document.getElementById('replay'),ctx=canvas.getContext('2d');function draw(){if(images[at].complete&&images[at].naturalWidth){ctx.drawImage(images[at],0,0);document.getElementById('status').textContent=(at+1)+'/20'}}images[0].onload=draw;document.getElementById('play').onclick=()=>playing=!playing;document.getElementById('step').onclick=()=>{playing=false;at=(at+1)%20;draw()};setInterval(()=>{if(playing){at=(at+1)%20;draw()}},180);window.review={images,canvas,setFrame:n=>{playing=false;at=n;draw()},ready:()=>images.every(i=>i.complete&&i.naturalWidth===1280)};</script></html>'''
count=len(pics);page=page.replace('20コマ',str(count)+'コマ').replace('length:20','length:'+str(count)).replace('(at+1)%20','(at+1)%'+str(count)).replace("+'/20'",f"+'/{count}'")
(R/'Docs/Proposals/officer-movement-review.html').write_bytes(page.encode());print('MOVEMENT_NATIVE_AND_4X_CAPTURES_VERIFIED',count)
