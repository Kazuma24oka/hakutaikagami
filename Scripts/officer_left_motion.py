"""Native side-view surface rig; character-right near, left far. No I/O writes."""
from pathlib import Path
import json
import numpy as np
from PIL import Image,ImageDraw
KEYS=['face','hair','torso','shoulders','arms','waist','legs','footwear','coat','sword']
def rgba(p):
 a=np.array(Image.open(p).convert('RGBA'));a[a[:,:,3]==0]=0;return a
def polygon(poly):
 im=Image.new('L',(64,96));ImageDraw.Draw(im).polygon(poly,fill=255);return np.array(im)>0
def smooth(t):
 t=np.clip(t,0,1);return t*t*(3-2*t)
def load(root):
 root=Path(root);a=root/'Game/Assets/Trials/OfficerLeftViewV11';spec=json.loads((root/'Art/Officer/LeftViewV11/body-definition.json').read_text(encoding='utf-8'));rig=json.loads((root/'Art/Officer/LeftMotionV12/rig.json').read_text(encoding='utf-8'));body=rgba(a/'body-left.png');parts=spec['body_parts'];t={};operations=[]
 def add(name,image,kind,when=None,unless=None):
  t[name]=(image,kind);op={'sheet':name}
  if when:op['when']=when
  if unless:op['unless']=unless
  operations.append(op)
 def skin(names):
  m=np.zeros((96,64),bool)
  for n in names:m|=polygon(parts[n]['polygon'])
  out=np.zeros_like(body);out[m]=body[m];return out
 add('sword',rgba(a/'sword.png'),'sword',when='sword')
 far_leg=skin(['thigh_left','calf_left']);near_leg=skin(['thigh_right','calf_right'])
 far_foot=skin(['foot_left']);near_foot=skin(['foot_right'])
 far_arm=skin(['upper_arm_left','forearm_left','hand_left']);near_arm=skin(['upper_arm_right','forearm_right','hand_right'])
 core=skin(['head','neck','chest','abdomen','pelvis'])
 # Boundary correction masks remain per item and belong to body coordinates.
 for name,image,kind,unless in [('far_leg_skin',far_leg,'far_leg','legs'),('far_foot_skin',far_foot,'far_leg','footwear'),('far_arm_skin',far_arm,'far_arm','arms')]:add(name,image,kind,unless=unless)
 for name,slot,kind in [('far_legs','legs','far_leg'),('far_boot','footwear','far_leg'),('far_arm','arms','far_arm'),('far_shoulder','shoulders','far_arm')]:
  donor=rgba(a/(slot+'.png'));donor[:,:,:3]=np.floor(donor[:,:,:3].astype(float)*rig['far_material_brightness']+.5).astype('uint8');add(name,donor,kind,when=slot)

 add('core',core,'core')
 # Per-slot static boundary correction, warped with the core rather than limbs.
 for slot in ['torso','waist','coat','arms','legs','footwear','shoulders','face','hair']:
  mask=np.zeros_like(body);mask[:,:,3]=np.array(Image.open(a/(slot+'-occlusion.png')).convert('L'));name='core_hide_'+slot;t[name]=(mask,'core');operations.append({'sheet':name,'when':slot,'erase':True})
 add('near_leg_skin',near_leg,'near_leg',unless='legs');add('near_foot_skin',near_foot,'near_leg',unless='footwear')
 add('near_legs',rgba(a/'legs.png'),'near_leg',when='legs');add('near_boot',rgba(a/'footwear.png'),'near_leg',when='footwear')
 for name,slot,kind in [('torso','torso','core'),('waist','waist','core'),('coat','coat','coat')]:add(name,rgba(a/(slot+'.png')),kind,when=slot)
 add('near_arm_skin',near_arm,'near_arm',unless='arms');add('near_arm',rgba(a/'arms.png'),'near_arm',when='arms');add('near_shoulder',rgba(a/'shoulders.png'),'near_arm',when='shoulders')
 add('face',rgba(a/'face.png'),'head',when='face');add('hair',rgba(a/'hair.png'),'head',when='hair')
 return t,operations
def field(x,y,f,rig,kind):
 bob=f['bob'];s=f['swing'];lift=f['lift'];dx=np.zeros_like(x,dtype=float);dy=np.zeros_like(y,dtype=float)+bob
 if kind in ['head','sword']:return dx,dy
 if kind.endswith('arm'):
  phase=-s if kind=='near_arm' else s
  t=smooth((y-rig['shoulder_y'])/(rig['wrist_y']-rig['shoulder_y']))
  dx-=rig['arm_swing']*phase*t
  # Arc retracts the hand a little at both extremes, independent of side.
  dy-=rig['arm_rise']*abs(phase)*t
 elif kind.endswith('leg'):
  phase=s if kind=='near_leg' else -s;up=max(0,lift if kind=='near_leg' else -lift)
  t=smooth((y-rig['hip_y'])/(rig['ankle_y']-rig['hip_y']))
  dx-=rig['stride']*phase*t+rig['knee_bend']*up*np.sin(np.pi*np.clip((y-rig['hip_y'])/(rig['ankle_y']-rig['hip_y']),0,1))
  dy=bob*(1-t)-rig['foot_lift']*up*t
 elif kind=='coat':
  tail=smooth((y-rig['hip_y'])/18);dx-=rig['coat_sway']*s*tail
 return dx,dy
def mapping(f,rig,kind):
 yy,xx=np.mgrid[:96,:64].astype(float);x=xx.copy();y=yy.copy()
 for _ in range(50):
  dx,dy=field(x,y,f,rig,kind);x=xx-dx;y=yy-dy
 ix=np.floor(x+.5).astype(int);iy=np.floor(y+.5).astype(int);valid=(ix>=0)&(ix<64)&(iy>=0)&(iy<96)
 return np.clip(ix,0,63),np.clip(iy,0,95),valid
def animate(t,f,rig):
 maps={kind:mapping(f,rig,kind) for kind in set(kind for _,kind in t.values())};out={}
 for n,(a,kind) in t.items():
  x,y,v=maps[kind];b=a[y,x].copy();b[~v]=0;out[n]=b
 return out
def compose(textures,operations,selection):
 a=np.zeros((96,64,4),np.uint8)
 for op in operations:
  if op.get('when') and op['when'] not in selection:continue
  if op.get('unless') and op['unless'] in selection:continue
  b=textures[op['sheet']];m=b[:,:,3]>0
  if op.get('erase'):a[m]=0
  else:a[m]=b[m]
 return a
def frames(rig):return [(name,i,f) for name,clip in rig['clips'].items() for i,f in enumerate(clip['frames'])]
