"""Rear-native controls. Mirror the authored coordinate field, NOT the artwork."""
from pathlib import Path
import numpy as np
import build_officer_back as static
import officer_front_walk as front
from officer_right_motion import compose,frames
KEYS=static.KEYS
def load(root):
 images=static.load(root);t={n:(a,'sword' if n=='sword' else 'surface') for n,a in images.items()};ops=[{'sheet':'body'}]
 ops +=[{'sheet':'hide_'+n,'when':n,'erase':True} for n in KEYS[2:]]
 for n in static.ORDER:
  if n=='shoulders':ops.append({'sheet':'hands','unless':'arms'})
  ops.append({'sheet':n,'when':n})
 return t,ops
def field(x,y,f,rig,kind):
 dx,dy=front.displacement(63-x,y,f,rig,kind='sword' if kind=='sword' else 'surface');return -dx,dy
def mapping(f,rig,kind):
 yy,xx=np.mgrid[:96,:64].astype(float);x=xx.copy();y=yy.copy()
 for _ in range(50):dx,dy=field(x,y,f,rig,kind);x=xx-dx;y=yy-dy
 ix=np.floor(x+.5).astype(int);iy=np.floor(y+.5).astype(int);v=(ix>=0)&(ix<64)&(iy>=0)&(iy<96);return np.clip(ix,0,63),np.clip(iy,0,95),v
def animate(t,f,rig):
 maps={k:mapping(f,rig,k) for k in ['surface','sword']};out={}
 for n,(a,k) in t.items():
  x,y,v=maps[k];b=a[y,x].copy();b[~v]=0;out[n]=b
 return out
