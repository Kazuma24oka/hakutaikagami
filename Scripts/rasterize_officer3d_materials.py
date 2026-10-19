"""Deterministic source triangle ownership mask; no color inference or generated imagery."""
from pathlib import Path
import sys,json,numpy as np
from PIL import Image,ImageDraw
p=Path(sys.argv[1]);data=np.load(p/'source-material-uv.npz');uv=data['uv'];ids=data['owner'];size=2048
image=Image.new('L',(size,size));draw=ImageDraw.Draw(image)
for tri,owner in zip(uv,ids):draw.polygon([(float(x)*(size-1),(1-float(y))*(size-1)) for x,y in tri],fill=int(owner)+1)
a=np.array(image)
# Eight texel padding, never repaint occupied texels; fixed neighbor order for ties.
for step in range(8):
 b=a.copy()
 for dy,dx in [(-1,0),(1,0),(0,-1),(0,1)]:
  moved=np.roll(np.roll(a,dy,axis=0),dx,axis=1)
  if dy<0:moved[-1,:]=0
  if dy>0:moved[0,:]=0
  if dx<0:moved[:,-1]=0
  if dx>0:moved[:,0]=0
  take=(b==0)&(moved>0);b[take]=moved[take]
 a=b
rgb=np.stack([(a==i)*255 for i in [1,2,3]],axis=-1).astype('uint8');Image.fromarray(rgb).save(p/'materials.png')
counts={name:int(np.count_nonzero(a==i+1)) for i,name in enumerate(['cloth','lacquer','metal'])};assert min(counts.values())>1000
(p/'mask-counts.json').write_text(json.dumps(counts),encoding='utf-8');print('V2_MASK_RASTERIZED',len(uv),counts,flush=True)
