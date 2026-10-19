"""Deterministic construction controls and audited masked-material assembly.
This owns the silhouette; SDXL only proposes interior shading, never anatomy.
"""
import argparse, json, sys, hashlib
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

def digest(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--root',type=Path,required=True);ap.add_argument('--prepare',action='store_true');ap.add_argument('--finish',type=Path);args=ap.parse_args()
    root=args.root.resolve();sys.path.insert(0,str(root/'Scripts'))
    import officer_art_pipeline as gate
    f=root/'Art/Officer/BodyBaseV1';spec_path=f/'geometry.json';spec=json.loads(spec_path.read_text(encoding='utf-8'))
    wr=json.loads((root/'Art/Officer/work-record.json').read_text(encoding='utf-8'))
    if args.prepare:
        gate.require_control_plan(root,wr['control_preparation'])
    else:
        gate.require_plan(root,'front_authoring',wr['plans']['body-front-v1'])
    palette=np.array(spec['palette_rgb'],dtype=np.uint8)
    masks={};ids=np.zeros((96,64),np.uint8)
    for r in spec['regions']:
        im=Image.new('L',(64,96));ImageDraw.Draw(im).polygon([tuple(p) for p in r['polygon']],fill=255);masks[r['id']]=np.array(im)>0
    for i in spec['paint_order']:ids[masks[i]]=i
    solid=ids>0
    # Contour, broad forms and highlights are authored together on one native grid.
    eroded=np.array(Image.fromarray(solid.astype(np.uint8)*255).filter(ImageFilter.MinFilter(3)))>0
    inner=np.array(Image.fromarray(solid.astype(np.uint8)*255).filter(ImageFilter.MinFilter(5)))>0
    body=np.zeros((96,64,4),np.uint8);body[solid]=[*palette[3],255];body[solid&~eroded]=[*palette[0],255]
    yy,xx=np.mgrid[:96,:64]
    # Simple stable illumination on the left and lower-right shadow, no texture noise.
    for r in spec['regions']:
        own=(ids==r['id'])&eroded
        xs=np.where(own)[1]
        if not len(xs):continue
        center=(xs.min()+xs.max())/2
        body[own&(xx<center-1)]=[*palette[4],255]
        body[own&(xx>center+2)]=[*palette[2],255]
    # Smooth bald adult cranial volume without facial marks or a two-tone split.
    head=(ids==1)&eroded
    body[head]=[*palette[3],255]
    body[head & (((xx-30)/8)**2+((yy-17)/12)**2<.7)]=[*palette[4],255]
    body[head & (((xx-28)/5)**2+((yy-13)/6)**2<.5)]=[*palette[5],255]
    body[head & (xx>=38)]=[*palette[2],255]
    body[head & (yy>=25)]=[*palette[2],255]
    # Pelvis is one continuous skin surface; never an underwear boundary or genital line.
    pelvis=(ids==5)&eroded
    body[pelvis]=[*palette[3],255]
    body[pelvis & (xx<=27)]=[*palette[4],255]
    body[pelvis & (xx>=37)]=[*palette[2],255]
    def stroke(points,color,width=1):
        im=Image.fromarray(body);ImageDraw.Draw(im).line(points,fill=tuple(palette[color])+ (255,),width=width);return np.array(im)
    # Clavicle/chest/abdominal volumes, deliberately no facial features or groin lines.
    body=stroke([(25,35),(29,36)],2);body=stroke([(34,36),(38,35)],2)
    body=stroke([(24,42),(28,43),(30,42)],2);body=stroke([(33,42),(35,43),(39,42)],2)
    body=stroke([(28,49),(30,50)],2);body=stroke([(34,50),(36,49)],2)
    body=stroke([(28,53),(30,54)],2);body=stroke([(34,54),(36,53)],2)
    body[~solid]=0
    preserve=(~inner)|np.isin(ids,[1,2,5])
    edit=solid&~preserve
    if args.prepare:
        (f/'controls').mkdir(exist_ok=True)
        Image.fromarray(body).save(f/'controls/authoring-body.png')
        Image.fromarray(ids).save(f/'controls/ownership.png')
        Image.fromarray(edit.astype(np.uint8)*255).save(f/'controls/edit-mask.png')
        Image.fromarray((solid&~edit).astype(np.uint8)*255).save(f/'controls/preserve-mask.png')
        Image.fromarray(solid.astype(np.uint8)*255).save(f/'controls/skin-mask.png')
        Image.fromarray(np.zeros((96,64),np.uint8)).save(f/'controls/empty-mask.png')
        Image.fromarray(np.isin(ids,[1]).astype(np.uint8)*255).save(f/'controls/face-protection.png')
        rgb=Image.new('RGB',(64,96),(192,194,188));rgb.paste(Image.fromarray(body),(0,0),Image.fromarray(body))
        rgb.resize((768,1152),Image.Resampling.NEAREST).save(f/'controls/comfy-input.png')
        Image.fromarray(edit.astype(np.uint8)*255).resize((768,1152),Image.Resampling.NEAREST).save(f/'controls/comfy-edit-mask.png')
        print('BODY_CONTROLS_READY canvas=64x96 body_height=84 head_height=21 regions=17')
        return
    if not args.finish:raise ValueError('Missing actual generated material')
    generated=Image.open(args.finish).convert('RGB')
    if generated.size!=(768,1152):raise ValueError('Unexpected generated material size')
    native=np.array(generated.resize((64,96),Image.Resampling.BOX))
    distance=((native[:,:,None,:].astype(float)-palette[None,None,:,:].astype(float))**2).sum(3)
    colors=palette[distance.argmin(2)]
    result=body.copy();result[edit,:3]=colors[edit]
    # Actual sampled material cannot change face, pelvis, outer edge, alpha or registration.
    if not np.array_equal(result[~edit],body[~edit]):raise ValueError('Protected pixels altered')
    out=root/'Game/Assets/Trials/OfficerBodyBaseV1';out.mkdir(parents=True,exist_ok=True);(out/'parts').mkdir(exist_ok=True)
    Image.fromarray(result).save(out/'body-front.png')
    for name,arr in [('ownership',ids),('skin-mask',solid.astype(np.uint8)*255),('edit-mask',edit.astype(np.uint8)*255),('preserve-mask',(solid&~edit).astype(np.uint8)*255)]:Image.fromarray(arr).save(out/(name+'.png'))
    for r in spec['regions']:
        part=np.zeros_like(result);part[ids==r['id']]=result[ids==r['id']];Image.fromarray(part).save(out/'parts'/(r['name']+'.png'))
    report={'source_material_sha256':digest(args.finish),'geometry_sha256':digest(spec_path),'native_size':[64,96],'visible_pixels':int(solid.sum()),'generated_interior_pixels':int(edit.sum()),'protected_rgba_difference':0,'alpha':[0,255],'palette_colors':len(np.unique(result[solid,:3],axis=0)),'status':'TECHNICAL_CANDIDATE_REQUIRES_VISUAL_INSPECTION','provenance':'Author constructed silhouette/ownership/blank head/pelvis and broad shading; ComfyUI masked interior material; fixed BOX reduction and palette mapping; no generated alpha claimed'}
    (f/'assembly-result.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(report))

if __name__=='__main__':main()
