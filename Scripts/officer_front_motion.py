"""Deterministic front-only pixel surface rig. Pure functions, no writes."""
import json
from pathlib import Path
import numpy as np
from PIL import Image

def rgba(path):
    a=np.array(Image.open(path).convert('RGBA'));a[a[:,:,3]==0]=0;return a

def load(root):
    root=Path(root);m=json.loads((root/'Art/Officer/ArmorSplitV3/part-manifest.json').read_text(encoding='utf-8'))
    spec=json.loads((root/m['definition']['path']).read_text(encoding='utf-8'))
    textures={'body':rgba(root/m['base']['path']),**{k:rgba(root/v['path']) for k,v in m['face_hair'].items()}}
    textures.update({k:rgba(root/v['image']['path']) for k,v in m['parts'].items()})
    ids=np.array(Image.open(root/m['body_owners']['path']))
    hand=np.zeros_like(textures['body']);mask=np.isin(ids,[8,11]);hand[mask]=textures['body'][mask];textures['hands']=hand
    for k,item in m['parts'].items():
        a=np.zeros_like(hand);a[:,:,3]=np.array(Image.open(root/item['body_occlusion']['path']));textures['hide_'+k]=a
    return m,spec,textures

def smooth(v):
    t=np.clip(v,0,1);return t*t*(3-2*t)

def displacement(x,y,frame,rig,kind='surface'):
    """Continuous bounded field. All touching skin/gear use identical weights.

    Head remains a rigid translated cluster. Arms blend from shoulder to wrist;
    legs blend from hip through knee to ankle. Planting cancels torso bob at feet.
    Sword is a rigid belt child, never bent with the leg surface.
    """
    bob=frame['bob'];phase=frame['swing'];lift=frame['lift']
    if kind=='sword':return np.zeros_like(x),np.zeros_like(y)+bob*rig['sword_bob_weight']
    upper=1-smooth((y-rig['hip_y'])/(rig['sole_y']-rig['hip_y']))
    dy=bob*upper;dx=np.zeros_like(x)
    arm=smooth((y-rig['shoulder_y'])/(rig['wrist_y']-rig['shoulder_y']))*(1-smooth((y-rig['wrist_y'])/9))
    arm_right=1-smooth((x-17)/6);arm_left=smooth((x-39)/6)
    dx+=rig['arm_x']*phase*arm*(arm_right+arm_left)
    dy+=rig['arm_y']*phase*arm*(arm_right-arm_left)
    leg=smooth((y-rig['hip_y'])/(rig['ankle_y']-rig['hip_y']))
    right=1-smooth((x-28)/5);left=smooth((x-28)/5)
    dy-=rig['foot_lift']*leg*(max(0,lift)*right+max(0,-lift)*left)
    # Only the lifted foot travels sideways; the supporting foot stays planted.
    dx+=rig['foot_x']*leg*(max(0,lift)*right-max(0,-lift)*left)
    return dx,dy

def lookup(frame,rig,kind='surface'):
    yy,xx=np.mgrid[:96,:64].astype(float);sx=xx.copy();sy=yy.copy()
    # Fixed-point inverse of the shared continuous field; no independent fit.
    for _ in range(16):
        dx,dy=displacement(sx,sy,frame,rig,kind);sx=xx-dx;sy=yy-dy
    ix=np.floor(sx+.5).astype(int);iy=np.floor(sy+.5).astype(int)
    valid=(ix>=0)&(ix<64)&(iy>=0)&(iy<96)
    return np.clip(ix,0,63),np.clip(iy,0,95),valid

def warp(texture,mapping):
    x,y,v=mapping;a=texture[y,x].copy();a[~v]=0;return a

def animate(textures,frame,rig):
    common=lookup(frame,rig);sword=lookup(frame,rig,'sword')
    return {k:warp(v,sword if k=='sword' else common) for k,v in textures.items()}

def compose(t,spec,selected):
    body=t['body'].copy()
    for name in spec['parts']:
        if name in selected:body[t['hide_'+name][:,:,3]>0]=0
    im=Image.fromarray(body)
    if 'face' in selected:im.alpha_composite(Image.fromarray(t['face']))
    for k in spec['draw_order']:
        if k in selected:im.alpha_composite(Image.fromarray(t[k]))
    if 'arms' not in selected:im.alpha_composite(Image.fromarray(t['hands']))
    if 'hair' in selected:im.alpha_composite(Image.fromarray(t['hair']))
    return np.array(im)

def frames(rig):
    return [(name,i,f) for name,clip in rig['clips'].items() for i,f in enumerate(clip['frames'])]
