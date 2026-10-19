"""Gate every native side-motion output before writing it."""
import argparse,json,hashlib,base64
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import numpy as np
import officer_art_pipeline as g
import officer_left_motion as m
def write(p,v):p.write_bytes((json.dumps(v,ensure_ascii=False,indent=2)+'\n').encode())
def board(rows,p,labels):
 font=ImageFont.truetype('C:/Windows/Fonts/meiryo.ttc',13);im=Image.new('RGB',(8*144+140,len(rows)*216+10),'#273239');d=ImageDraw.Draw(im)
 for j,row in enumerate(rows):
  d.text((8,j*216+16),labels[j],font=font,fill='#e1c58d')
  for i,a in enumerate(row):
   bg=Image.new('RGBA',(64,96),'#bdc0b6');bg.alpha_composite(Image.fromarray(a));im.paste(bg.resize((128,192),Image.Resampling.NEAREST),(140+i*144,j*216+18));d.text((140+i*144,j*216),str(i),font=font,fill='white')
 im.save(p)
def build(root,probe=False):
 o=root/'Art/Officer/LeftMotionV12';w=json.loads((root/'Art/Officer/work-record.json').read_text(encoding='utf-8'));id='left-motion-v12-probe' if probe else 'left-motion-v12';p=w['plans'][id];binding=g.require_plan(root,'parts' if probe else 'motion',p)
 rig=json.loads((o/'rig.json').read_text());assert rig['canvas']==[64,96] and rig['direction']=='screen-left' and rig['near_character_side']=='right'
 t,ops=m.load(root);anim=[m.animate(t,f,rig) for _,_,f in m.frames(rig)];allkeys=set(m.KEYS);cases=[('全装備',allkeys),('裸素体',set()),*[(n+'なし',allkeys-{n}) for n in m.KEYS[2:]]]
 board([[m.compose(a,ops,s) for a in anim[4:]] for _,s in cases],o/'walk-probes.png',[n for n,_ in cases]);board([[a[n] for a in anim[4:]] for n in ['near_arm','far_arm','near_legs','far_legs','near_boot','far_boot']],o/'isolated-probes.png',['近腕','遠腕','近脚','遠脚','近履物','遠履物'])
 for clip,offset,count in [('idle',0,4),('walk',4,8)]:
  pics=[]
  for a in anim[offset:offset+count]:
   im=Image.new('RGB',(736,410),'#273239');d=ImageDraw.Draw(im);font=ImageFont.truetype('C:/Windows/Fonts/meiryo.ttc',14)
   for i,(label,s) in enumerate([cases[0],cases[1],('腕・肩なし',allkeys-{'arms','shoulders'}),('陣羽織なし',allkeys-{'coat'})]):
    b=Image.new('RGBA',(64,96),'#bdc0b6');b.alpha_composite(Image.fromarray(m.compose(a,ops,s)));im.paste(b,(i*184+10,28));im.paste(b.resize((128,192),Image.Resampling.NEAREST),(i*184+10,166));d.text((i*184+10,5),label,font=font,fill='white')
   pics.append(im)
  pics[0].save(o/(clip+'.gif'),save_all=True,append_images=pics[1:],duration=rig['clips'][clip]['duration_ms'],loop=0,disposal=2,optimize=False)
 if probe:
  write(o/'probe-binding.json',{'binding':binding,'plan':id,'scope':'Left-only small in-place displacement probes, not accepted walking.'});print('LEFT_PROBES_READY');return
 out=root/'Game/Assets/Trials/OfficerLeftMotionV12';out.mkdir(parents=True,exist_ok=True);sheets={}
 for n in t:
  im=Image.new('RGBA',(768,96));hashes=[]
  for j,a in enumerate(anim):im.paste(Image.fromarray(a[n]),(64*j,0));hashes.append(hashlib.sha256(a[n].tobytes()).hexdigest())
  path=out/(n+'.png');im.save(path);sheets[n]={'path':'res://Assets/Trials/OfficerLeftMotionV12/'+n+'.png','sha256':g.sha(path),'frame_hashes':hashes}
 hashes=[]
 for flags in range(1024):
  selected={n for i,n in enumerate(m.KEYS) if flags&(1<<i)};row=[]
  for a in anim:
   b=m.compose(a,ops,selected);assert set(np.unique(b[:,:,3]))<={0,255};assert not(b[0,:,3].any() or b[-1,:,3].any() or b[:,0,3].any() or b[:,-1,3].any()),'Clipped side motion'
   row.append(hashlib.sha256(b.tobytes()).hexdigest())
  hashes.append(row)
 data={'canvas':[64,96],'display_scale':1,'keys':m.KEYS,'labels':['顔・髭','髪','胴','肩','腕','腰','脚','履物','陣羽織','刀'],'operations':ops,'sheets':sheets,'hashes':hashes,'binding':binding,'idle_duration_ms':rig['clips']['idle']['duration_ms'],'walk_duration_ms':rig['clips']['walk']['duration_ms'],'scope':'Left-only authored in-place surface rig; no world simulation or hidden-anatomy recovery'}
 write(root/'Game/Data/officer_left_motion_v12.json',data);write(o/'manifest.json',{'binding':binding,'sheets':sheets,'frames':12,'combinations':1024,'native_cases':12288,'direction':'screen-left','near_character_side':'right','new_generator_invocations':0})
 # Page keeps the same conditional compositor as Godot, at true native size.
 data['images']={n:'data:image/png;base64,'+base64.b64encode((out/(n+'.png')).read_bytes()).decode() for n in sheets}
 template=(root/'Scripts/officer_left_motion_page.html').read_text(encoding='utf-8');(root/'Docs/Proposals/officer-left-motion-review.html').write_bytes(template.replace('__DATA__',json.dumps(data,ensure_ascii=False,separators=(',',':'))).encode());print('LEFT_MOTION_12288_CASES_EXPORTED')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--root',type=Path,default=Path(__file__).resolve().parents[1]);p.add_argument('--probe',action='store_true');a=p.parse_args();build(a.root,a.probe)
