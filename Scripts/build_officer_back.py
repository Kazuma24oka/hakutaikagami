"""Gated rear body/material/8-slot construction. Existing front/right untouched."""
from pathlib import Path
import json,hashlib,itertools,argparse
import numpy as np
from PIL import Image,ImageDraw,ImageFont
import officer_art_pipeline as g
KEYS=['face','hair','torso','shoulders','arms','waist','legs','footwear','coat','sword']
ORDER=['legs','footwear','torso','waist','coat','sword','arms','shoulders','face','hair']
def read(p):return json.loads(p.read_text(encoding='utf-8'))
def write(p,v):p.write_bytes((json.dumps(v,ensure_ascii=False,indent=2)+'\n').encode())
def rgba(p):
 a=np.array(Image.open(p).convert('RGBA'));a[a[:,:,3]==0]=0;return a
def poly(points):
 im=Image.new('L',(64,96));ImageDraw.Draw(im).polygon(points,fill=255);return np.array(im)>0
def compose(t,selected):
 a=t['body'].copy()
 for n in KEYS[2:]:
  if n in selected:a[t['hide_'+n][:,:,3]>0]=0
 for n in ORDER:
  if n=='shoulders' and 'arms' not in selected:
   b=t['hands'];m=b[:,:,3]>0;a[m]=b[m]
  if n in selected:
   b=t[n];m=b[:,:,3]>0;a[m]=b[m]
 return a
def load(r):
 a=Path(r)/'Game/Assets/Trials/OfficerBackViewV9';return {n:rgba(a/(n+'.png')) for n in ['body','hands',*KEYS,*['hide_'+n for n in KEYS[2:]]]}
def board(rows,path,labels):
 columns=4;im=Image.new('RGB',(columns*210,((len(rows)+columns-1)//columns)*328),'#26343b');d=ImageDraw.Draw(im);font=ImageFont.truetype('C:/Windows/Fonts/meiryo.ttc',14)
 for i,(name,a) in enumerate(zip(labels,rows)):
  x=i%columns*210;y=i//columns*328;bg=Image.new('RGBA',(64,96),'#bfc1b7');bg.alpha_composite(Image.fromarray(a));im.paste(bg.resize((192,288),Image.Resampling.NEAREST),(x+8,y+28));d.text((x+8,y+5),name,font=font,fill='white')
 im.save(path)
def build(r):
 o=r/'Art/Officer/BackViewV9';w=read(r/'Art/Officer/work-record.json');p=w['plans']['back-parts-v9'];binding=g.require_plan(r,'front_authoring',p);spec=read(o/'definition.json');d=read(o/'parts.json');a=r/'Game/Assets/Trials/OfficerBackViewV9';a.mkdir(parents=True,exist_ok=True)
 source=rgba(o/'source-back-candidate.png');visible=source[:,:,3]>0;y,x=np.mgrid[:96,:64]
 body=rgba(r/'Game/Assets/Trials/OfficerBodyBaseV2/body-front.png')[:,::-1].copy();sil=body[:,:,3]>0;owner=np.array(Image.open(r/'Game/Assets/Trials/OfficerBodyBaseV2/ownership.png'))[:,::-1].copy()
 core=np.isin(owner,[2,3,4,5]);body[core]=[198,148,104,255]
 # Rear-only material, not new body geometry. Remove anterior pectoral shading.
 blade=(((x>=26)&(x<=28)&(y>=40)&(y<=47))|((x>=38)&(x<=40)&(y>=40)&(y<=47)))&core;body[blade]=[177,125,86,255]
 spine=(x==33)&(y>=43)&(y<=56)&core;body[spine]=[185,133,92,255]
 head=owner==1;body[head]=[202,151,107,255];body[head&(x<=28)]=[171,119,80,255];body[owner==5]=[198,148,104,255]
 # Observed rear ear/nape pixels are body-owned, never baked into hair.
 observed=np.zeros((96,64),bool)
 for xy in d['observed_skin_pixels']:observed[xy[1],xy[0]]=True
 for xx,yy in d['ear_bridges']:body[yy,xx]=[184,129,88,255];owner[yy,xx]=1;sil[yy,xx]=True
 body[observed]=source[observed];owner[observed]=1;sil|=observed
 Image.fromarray(body).save(a/'body.png');Image.fromarray(owner).save(a/'body-ownership.png');Image.fromarray(sil.astype('uint8')*255).save(a/'body-mask.png')
 owners=np.zeros((96,64),np.uint8)
 for n in d['priority']:
  m=np.zeros((96,64),bool)
  for points in d['regions'][n]:m|=poly(points)
  owners[m&visible&~observed]=d['ids'][n]
 assert not np.any(visible&~observed&(owners==0))
 Image.fromarray(owners).save(a/'ownership.png');t={'body':body}
 for n in KEYS:
  layer=np.zeros_like(body);m=owners==d['ids'][n] if n!='face' else np.zeros((96,64),bool)
  hm=np.zeros((96,64),bool)
  for points in d['hidden'].get(n,[]):hm|=poly(points)
  later=[d['ids'][q] for q in ORDER[ORDER.index(n)+1:]];hm &= visible&~m&np.isin(owners,later)&~observed
  layer[hm]=d['hidden_colors'].get(n,[45,35,32,255])
  if n=='torso':
   # Authored lamellar back under coat: plate rows + black lacing, not a copied photograph.
   layer[hm&(y%4==0)]=[48,28,24,255];layer[hm&(y%4==1)]=[154,64,42,255]
  layer[m]=source[m];t[n]=layer;Image.fromarray(layer).save(a/(n+'.png'));Image.fromarray(hm.astype('uint8')*255).save(a/(n+'-hidden.png'))
 for n in KEYS[2:]:
  m=np.zeros((96,64),bool)
  for points in d['body_boundary'].get(n,[]):m|=poly(points)
  m &= sil&~visible;hide=np.zeros_like(body);hide[:,:,3]=m*255;t['hide_'+n]=hide;Image.fromarray(hide).save(a/('hide_'+n+'.png'))
 hands=np.zeros_like(body);handmask=np.isin(owner,[6,7,8,9,10,11]);hands[handmask]=body[handmask];t['hands']=hands;Image.fromarray(hands).save(a/'hands.png')
 material=np.zeros((96,64),np.uint8);r0,g0,b0=source[:,:,:3].astype(int).transpose(2,0,1);material[observed]=1;red=visible&(r0>g0*1.7)&(r0>65);material[red&np.isin(owners,[d['ids'][n] for n in ['torso','shoulders','arms','waist','legs']])]=2;material[red&(owners==d['ids']['coat'])]=3;Image.fromarray(material).save(a/'material.png')
 full=compose(t,set(KEYS));assert np.array_equal(full,source),f'Restored source differs at {np.any(full!=source,axis=2).sum()} pixels';assert np.array_equal(compose(t,set()),body)
 Image.fromarray(full).save(a/'source-back.png');hashes={}
 for flags in range(1024):
  selected={n for i,n in enumerate(KEYS) if flags&(1<<i)};out=compose(t,selected);hashes[str(flags)]=hashlib.sha256(out.tobytes()).hexdigest();assert set(np.unique(out[:,:,3]))<={0,255}
 write(o/'combination-hashes.json',hashes);board([full,body,*[compose(t,set(KEYS)-{n}) for n in KEYS[1:]]],o/'removal-review.png',['全装備','裸素体',*[n+'なし' for n in KEYS[1:]]]);board([t[n] for n in KEYS[1:]],o/'isolated-review.png',KEYS[1:]);board([compose(t,{'hair'}),t['torso'],t['coat'],t['arms']],o/'hidden-review.png',['髪のみ','胴・補完込み','陣羽織','腕'])
 whiteblack=[]
 for bg in ['white','black','#bfc1b7']:
  im=Image.new('RGBA',(64,96),bg);im.alpha_composite(Image.fromarray(body));whiteblack.append(np.array(im))
 board(whiteblack,o/'body-background-review.png',['白','黒','灰'])
 write(o/'manifest.json',{'binding':binding,'canvas':[64,96],'display_scale':1,'direction':'back','character_left':'screen-left','keys':KEYS,'order':ORDER,'face_slot':'empty/no-op by direction','source_restore_difference_pixels':0,'all_off_body_difference_pixels':0,'combinations':1024,'visible_armor_pixels':{n:int((owners==d['ids'][n]).sum()) for n in KEYS[1:]},'observed_body_skin_pixels':int(observed.sum()),'body_backend':'Reuse mirrored V2 geometry + explicit rear material; new body imagegen blocked, no retry or model fallback','assets':{p.name:g.sha(p) for p in sorted(a.glob('*.png'))}});print('BACK_STATIC_1024_VERIFIED '+binding)
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--root',type=Path,default=Path(__file__).resolve().parents[1]);build(p.parse_args().root)
