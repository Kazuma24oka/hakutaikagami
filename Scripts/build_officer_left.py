"""Left-facing STATIC modular authoring trial. No side-walk acceptance implied."""
from pathlib import Path
import argparse,json,hashlib,itertools,base64
import numpy as np
from PIL import Image,ImageDraw
import officer_art_pipeline as gate
SLOTS=['torso','shoulders','arms','waist','legs','footwear','coat','sword']
ORDER=['sword','legs','footwear','torso','waist','coat','arms','shoulders','face','hair']
def read(p):return json.loads(p.read_text(encoding='utf-8'))
def save(p,v):p.write_bytes((json.dumps(v,ensure_ascii=False,indent=2)+'\n').encode())
def rgba(p):return np.array(Image.open(p).convert('RGBA'))
def mask(poly):
 im=Image.new('L',(64,96));ImageDraw.Draw(im).polygon(poly,fill=255);return np.array(im)>0
def native(p,colors):
 # One canvas-wide exact 1/16 scale. No individual part or frame transforms.
 im=Image.open(p).convert('RGBA');assert im.size==(1024,1536)
 a=np.array(im.resize((64,96),Image.Resampling.NEAREST));m=a[:,:,3]>=200
 q=np.array(Image.fromarray(a[:,:,:3]).quantize(colors=colors,method=Image.Quantize.MEDIANCUT,dither=Image.Dither.NONE).convert('RGB'))
 a[:,:,:3]=q;a[:,:,3]=m*255;a[~m]=0;return a
def put(p,a):Image.fromarray(a).save(p)
def compose(base,layers,hides,selected,foreground=None):
 # Far katana is behind the body as well as near arm/cuirass/coat.
 a=np.zeros_like(base)
 if 'sword' in selected:
  q=layers['sword'];m=q[:,:,3]>0;a[m]=q[m]
 m=base[:,:,3]>0;a[m]=base[m]
 for name in selected:
  if name in hides:a[hides[name]]=0
 for name in ORDER:
  if name=='sword':continue
  if name=='shoulders' and 'arms' not in selected and foreground is not None:
   m=foreground[:,:,3]>0;a[m]=foreground[m]
  if name in selected:
   q=layers[name];m=q[:,:,3]>0;a[m]=q[m]
 return a
def warp(a,amount):
 # Shared horizontal near-elbow probe. Continuous weights, torso/head/feet fixed.
 y,x=np.mgrid[:96,:64];wy=np.maximum(0,1-np.abs(y-53)/17);wx=np.maximum(0,1-np.abs(x-34)/10)
 sx=np.rint(x-amount*wy*wx).astype(int);valid=(sx>=0)&(sx<64);b=np.zeros_like(a);b[valid]=a[y[valid],sx[valid]];return b
def build(root):
 o=root/'Art/Officer/LeftViewV11';assets=root/'Game/Assets/Trials/OfficerLeftViewV11';assets.mkdir(parents=True,exist_ok=True)
 w=read(root/'Art/Officer/work-record.json');p=w['plans']['left-parts-v11-authoring'];binding=gate.require_plan(root,'front_authoring',p)
 spec=read(o/'body-definition.json');assert spec['canvas']==[64,96] and spec['near_character_side']=='right'
 assert set(spec['body_parts'])=={'head','neck','chest','abdomen','pelvis','upper_arm_right','forearm_right','hand_right','upper_arm_left','forearm_left','hand_left','thigh_right','calf_right','foot_right','thigh_left','calf_left','foot_left'}
 for item in p['inputs']+p['tools']+p['references']:gate.evidence(root,item)
 source=rgba(o/'source-left-candidate.png');put(assets/'source-left.png',source)
 # Existing provisional side geometry reflected; character side names exchanged
 # in the bound body definition. No new body generation or per-part fitting.
 base=rgba(root/'Game/Assets/Trials/OfficerRightViewV7/body-right.png')[:,::-1].copy()
 sil=base[:,:,3]>0
 put(assets/'body-left.png',base)
 arm_mask=np.zeros((96,64),bool)
 for n in ['upper_arm_right','forearm_right','hand_right']:arm_mask|=mask(spec['body_parts'][n]['polygon'])
 foreground=np.zeros_like(base);foreground[arm_mask]=base[arm_mask];put(assets/'bare-arm-front.png',foreground)
 render=lambda selected:compose(base,layers,hides,selected,foreground)
 # Explicit raster ownership polygons authored against this native source.
 d=read(o/'part-definition.json');owners=np.zeros((96,64),np.uint8);visible=source[:,:,3]>0
 assert d['canvas']==[64,96] and set(d['ids'])==set(ORDER) and d['near_character_side']=='right'
 for i,name in enumerate(d['ownership_priority'],1):
  m=np.zeros((96,64),bool)
  for poly in d['regions'][name]:m|=mask(poly)
  owners[m&visible]=d['ids'][name]
 assert not np.any(visible&(owners==0)), 'Source pixel without authored owner'
 Image.fromarray(owners).save(assets/'ownership.png')
 assert all(np.any(owners==d['ids'][n]) for n in ORDER),'Empty equipment slot'
 # Material/control masks are independent of source ownership and occlusion.
 material=np.zeros((96,64),np.uint8);r,g,b=source[:,:,:3].astype(int).transpose(2,0,1)
 lacquer_slots=np.isin(owners,[d['ids'][n] for n in ['torso','shoulders','arms','waist','legs']])
 material[visible&lacquer_slots&(r>g*1.7)&(r>65)]=2 # lacquer candidate, never hair/skin/cloth
 material[visible&(owners==d['ids']['coat'])&(r>g*1.7)&(r>65)]=3 # red coat textile, separate from lacquer
 material[(owners==d['ids']['face'])&(r>120)&(g>65)]=1
 Image.fromarray(material).save(assets/'material.png')
 put(o/'material-review.png',np.dstack([material==2,material==1,material==3,visible]).astype('uint8')*255)
 yy,xx=np.mgrid[:96,:64];weight=np.maximum(0,1-np.abs(yy-53)/17)*np.maximum(0,1-np.abs(xx-34)/10)
 Image.fromarray(np.rint(weight*255).astype('uint8')).save(assets/'elbow-control-weight.png')
 overlap=np.zeros((96,64),bool)
 for k in ['shoulder_right','elbow_right','wrist_right','knee_right','ankle_right']:
  x,y=spec['anchors'][k];overlap|=(abs(xx-x)<=3)&(abs(yy-y)<=3)
 Image.fromarray(overlap.astype('uint8')*255).save(assets/'joint-overlap.png')
 layers={};hides={};hidden_masks={}
 for name in ORDER:
  a=np.zeros_like(source);m=owners==d['ids'][name]
  # Bounded hidden completions only under another observed armor layer.
  hm=np.zeros((96,64),bool)
  for poly in d.get('hidden',{}).get(name,[]):hm|=mask(poly)
  hm &= ~m
  if name=='sword':hm &= (visible|sil)
  if name!='sword':hm &= visible&(owners!=d['ids']['hair'])&(owners!=d['ids']['face'])
  if name!='sword':hm &= np.isin(owners,[d['ids'][n] for n in ORDER[ORDER.index(name)+1:]])
  a[hm]=d['hidden_colors'].get(name,[68,46,40,255]);a[m]=source[m]
  if name=='torso':
   yy,xx=np.mgrid[:96,:64];a[hm&(yy%4==0)]=[47,32,30,255];a[hm&(yy%4==1)]=[157,70,44,255];a[hm&(xx%5==0)&(yy%4==2)]=[35,30,30,255]
  layers[name]=a;hidden_masks[name]=hm;put(assets/(name+'.png'),a);Image.fromarray(hm.astype('uint8')*255).save(assets/(name+'-hidden.png'))
 # Source silhouette discrepancies have PER ITEM occlusion, never a global
 # compositing crop. The off state always exposes complete immutable bare base.
 for name in ORDER:
  m=np.zeros((96,64),bool)
  for poly in d.get('body_occlusion',{}).get(name,[]):m|=mask(poly)
  hides[name]=m&sil&~visible;Image.fromarray(hides[name].astype('uint8')*255).save(assets/(name+'-occlusion.png'))
 allnames=SLOTS+['face','hair'];full=render(allnames)
 put(assets/'assembled-left.png',full)
 diff=np.any(full!=source,axis=2);put(o/'restoration-difference.png',np.dstack([diff*255,diff*0,diff*0,np.full_like(owners,255)]).astype('uint8'))
 assert not diff.any(), f'Full restoration differs at {diff.sum()} pixels'
 hashes={};basehash=hashlib.sha256(base.tobytes()).hexdigest()
 for bits in itertools.product([False,True],repeat=10):
  selected=[n for n,b in zip(allnames,bits) if b];a=render(selected)
  assert set(np.unique(a[:,:,3]))<={0,255};assert hashlib.sha256(base.tobytes()).hexdigest()==basehash
  hashes[''.join(str(int(b)) for b in bits)]=hashlib.sha256(a.tobytes()).hexdigest()
 assert np.array_equal(render([]),base)
 save(o/'combination-hashes.json',hashes)
 def board(name,cases):
  cols=4;rows=(len(cases)+cols-1)//cols;im=Image.new('RGBA',(cols*72,rows*112),(34,39,42,255));draw=ImageDraw.Draw(im)
  for i,(label,a) in enumerate(cases):
   x=(i%cols)*72+4;y=(i//cols)*112;draw.text((x,y),label,fill='white');im.alpha_composite(Image.fromarray(a),(x,y+14))
  im.resize((im.width*3,im.height*3),Image.Resampling.NEAREST).save(o/name)
 board('removal-review.png',[('all',full),('bare',base)]+[(n+' off',render([x for x in allnames if x!=n])) for n in allnames])
 board('isolated-review.png',[(n,layers[n]) for n in ORDER])
 board('joint-probes.png',[(f'{kind} {amt:+}',warp(a,amt)) for kind,a in [('bare',base),('all',full),('arms off',render([n for n in allnames if n!='arms'])),('shoulders off',render([n for n in allnames if n!='shoulders']))] for amt in [-1,0,1]])
 front=Image.open(root/'Game/Assets/Trials/OfficerContextV1/front-64x96.png');board('proportion-review.png',[('front',np.array(front)),('left',full),('bare',base),('ownership',np.dstack([owners*19,owners*47,owners*83,np.full_like(owners,255)]))])
 for n,a in [('body',base),('assembled',full)]:
  Image.fromarray(a).resize((512,768),Image.Resampling.NEAREST).save(o/(n+'-8x.png'))
 for bgname,bgcolor in [('white','white'),('black','black'),('gray','#bdbfb7')]:
  im=Image.new('RGBA',(64,96),bgcolor);im.alpha_composite(Image.fromarray(base));im.resize((256,384),Image.Resampling.NEAREST).save(o/('body-'+bgname+'.png'))
 board('four-directions.png',[(n,rgba(root/p)) for n,p in [('front','Game/Assets/Trials/OfficerContextV1/front-64x96.png'),('right','Game/Assets/Trials/OfficerRightViewV7/source-right.png'),('back','Game/Assets/Trials/OfficerBackViewV9/source-back.png')]]+[('left',full)])
 board('hidden-review.png',[(n,np.where(hidden_masks[n][:,:,None],layers[n],0).astype('uint8')) for n in ORDER])
 manifest={'schema':1,'status':'STATIC_AUTHORING_TRIAL_NOT_SIDE_WALK','canvas':[64,96],'display_scale':1,'binding':binding,'direction':'screen-left','near_character_side':'right','order':ORDER,'slots':allnames,'geometry':{'uniform_scale':1/16,'source_translate':[0,1],'body_translate':[0,0],'per_part_fit':False},'conversion':{'source_alpha_threshold':200,'source_palette_colors':48,'skin_palette_colors':12,'dither':False,'sampling':'nearest','raw_retained':True},'layers':{n:str((assets/(n+'.png')).relative_to(root)).replace('\\','/') for n in ORDER},'restoration_difference_pixels':int(diff.sum()),'combination_count':len(hashes),'limitations':['Static near/far ownership only; no side walking or far-limb swing accepted.','Hidden cloth/armor completion is bounded authored flat color, not recovered source detail.','Body follows provisional front construction and is not final approved anatomy.','Joint probes are horizontal +/-1px elbow surface displacement only; not independent shoulder rotation or gait.']}
 save(o/'manifest.json',manifest)
 data={'slots':allnames,'order':ORDER,'labels':['胴','肩','腕','腰','脚','履物','陣羽織','刀','顔・髭','髪'],'tex':{'body':base,'foreground':foreground,'front':np.array(front),**layers,**{'hide_'+n:np.dstack([np.zeros((96,64,3),dtype=np.uint8),m.astype('uint8')*255]) for n,m in hides.items()}}}
 data['tex']={n:base64.b64encode(a.tobytes()).decode() for n,a in data['tex'].items()}
 template=(root/'Scripts/officer_left_page.html').read_text(encoding='utf-8');(root/'Docs/Proposals/officer-left-review.html').write_bytes(template.replace('__DATA__',json.dumps(data,ensure_ascii=False,separators=(',',':'))).encode())
 return manifest
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--root',type=Path,default=Path(__file__).resolve().parents[1]);a=p.parse_args();print(json.dumps(build(a.root),ensure_ascii=False))
