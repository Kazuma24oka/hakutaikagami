"""Pure shared native layer logic. No writes, scaling or model invocation."""
import numpy as np
from PIL import Image,ImageDraw,ImageFilter
import hashlib

def polygon(points):
    im=Image.new('L',(64,96));ImageDraw.Draw(im).polygon([tuple(p) for p in points],fill=255);return np.array(im)>0

def ownership(kit,spec):
    fg=kit[:,:,3]>0;yy,xx=np.mgrid[:96,:64];ids=np.zeros((96,64),np.uint8)
    for band in spec['bands']:ids[fg&(yy>=band['y'][0])&(yy<=band['y'][1])]=spec['parts'][band['part']]['id']
    for name in spec['ownership_priority'][1:]:
        if name=='sword':
            mask=np.zeros_like(fg)
            for y,(lo,hi) in spec['sword_rows'].items():mask[int(y),lo:hi+1]=True
        elif name in spec.get('regions_by_row',{}):
            mask=np.zeros_like(fg)
            for y,spans in spec['regions_by_row'][name].items():
                for lo,hi in spans:mask[int(y),lo:hi+1]=True
        else:
            mask=np.zeros_like(fg)
            for item in spec['polygons']:
                if item['part']==name:mask |= polygon(item['points'])
        ids[mask&fg]=spec['parts'][name]['id']
    for override in spec.get('ownership_overrides',[]):
        x,y=override['xy']
        if not fg[y,x]:raise ValueError('Semantic correction outside original kit')
        ids[y,x]=spec['parts'][override['part']]['id']
    if not np.array_equal(ids>0,fg):raise ValueError('Source ownership incomplete')
    return ids

def material(kind,x,y,pal):
    if kind=='red_lamellar':
        if y%4 in [0,1]:return pal['red'] if x%3 else pal['red_dark']
        return pal['cord'] if x%3 else pal['gold']
    if kind=='dark_trousers_and_knee_guard':
        if 75<=y<=79:return pal['red_dark'] if x%3 else pal['gold']
        return pal['cloth_light'] if x%5==1 else pal['cord']
    if kind=='dark_sleeve':return pal['cloth_light'] if x%4==1 else pal['cord']
    if kind=='red_coat':return pal['red'] if x%4 in [0,1,2] else pal['red_dark']
    if kind=='dark_scabbard':return pal['edge'] if (x+y)%3==0 else pal['dark']
    if kind=='dark_footwear':return pal['gold'] if y==86 and x%4==1 else pal['cord']
    raise ValueError('Undeclared material')

def build(kit,bodyids,spec):
    ids=ownership(kit,spec);fg=kit[:,:,3]>0;yy,xx=np.mgrid[:96,:64];layers={};hidden={};rank={k:i for i,k in enumerate(spec['draw_order'])}
    for name,item in spec['parts'].items():
        own=ids==item['id'];arr=np.zeros_like(kit);arr[own]=kit[own];extra=np.zeros_like(own);rule=spec['completion_policy'].get(name)
        if rule:
            if 'body_ids' in rule:
                target=np.isin(bodyids,rule['body_ids']);d=rule['dilation'];target=np.array(Image.fromarray(target.astype(np.uint8)*255).filter(ImageFilter.MaxFilter(2*d+1)))>0
            elif 'polygon' in rule:target=polygon(rule['polygon'])
            elif 'polygons' in rule:
                target=np.zeros_like(own)
                for points in rule['polygons']:target |= polygon(points)
            elif 'line' in rule:
                im=Image.new('L',(64,96));ImageDraw.Draw(im).line([tuple(p) for p in rule['line']],fill=255,width=rule['width']);target=np.array(im)>0
            else:raise ValueError('Unbounded completion target')
            if 'rows' in rule:target &= (yy>=rule['rows'][0])&(yy<=rule['rows'][1])
            allowed=np.zeros_like(own)
            for occluder in rule['occluders']:
                if rank[occluder]<=rank[name]:raise ValueError('Completion occluder must draw later')
                allowed |= ids==spec['parts'][occluder]['id']
            extra=target&allowed&~own
            for y,x in np.argwhere(extra):arr[y,x]=[*material(rule['material'],int(x),int(y),spec['palette']),255]
            if np.any(extra&~fg):raise ValueError('Completion outside source occluder')
        layers[name]=arr;hidden[name]=extra
    return ids,layers,hidden

def compose(base,headparts,layers,bodyids,spec,selected):
    b=base.copy();occluded=[]
    for name in layers:
        if name in selected:occluded.extend(spec['body_occlusion'][name])
    b[np.isin(bodyids,occluded)]=0
    image=Image.fromarray(b)
    if 'face' in selected:image.alpha_composite(Image.fromarray(headparts['face']))
    for name in spec['draw_order']:
        if name in selected:image.alpha_composite(Image.fromarray(layers[name]))
    front=spec.get('bare_hand_foreground')
    if front and front['when_part_absent'] not in selected:
        hand=np.zeros_like(base);owned=np.isin(bodyids,front['body_owner_ids']);hand[owned]=base[owned];image.alpha_composite(Image.fromarray(hand))
    if 'hair' in selected:image.alpha_composite(Image.fromarray(headparts['hair']))
    return np.array(image)

def rgba_sha(arr):return hashlib.sha256(arr.tobytes()).hexdigest()
