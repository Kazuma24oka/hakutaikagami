"""Original-character controls and fixed-canvas assembly; never fit generated parts."""
from pathlib import Path
import argparse, json, sys, hashlib, io
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--root',type=Path,required=True);ap.add_argument('--prepare',action='store_true');ap.add_argument('--raw',type=Path);ap.add_argument('--plan',default='body-source-v2');args=ap.parse_args()
    r=args.root.resolve();sys.path.insert(0,str(r/'Scripts'));import officer_art_pipeline as g
    w=json.loads((r/'Art/Officer/work-record.json').read_text(encoding='utf-8'))
    f=r/'Art/Officer/BodyBaseV2';spec=json.loads((f/'source-definition.json').read_text(encoding='utf-8'))
    if args.prepare:g.require_reference_preparation(r,w['reference_preparation'])
    else:g.require_plan(r,'front_authoring',w['plans'][args.plan])
    out=f/'controls';out.mkdir(exist_ok=True)
    ids=np.zeros((96,64),np.uint8);masks={}
    for region in spec['regions']:
        im=Image.new('L',(64,96));ImageDraw.Draw(im).polygon([tuple(p) for p in region['polygon']],fill=255);masks[region['id']]=np.array(im)>0
    for ident in spec['paint_order']:ids[masks[ident]]=ident
    for override in spec.get('ownership_overrides', []):
        x,y=override['xy'];ids[y,x]=override['id']
    solid=ids>0;pal=np.array(spec['palette_rgb'],np.uint8)
    eroded=np.array(Image.fromarray(solid.astype(np.uint8)*255).filter(ImageFilter.MinFilter(3)))>0
    guide=np.zeros((96,64,4),np.uint8);guide[solid]=[*pal[8],255];guide[solid&~eroded]=[*pal[1],255]
    yy,xx=np.mgrid[:96,:64]
    for region in spec['regions']:
        m=(ids==region['id'])&eroded
        for y in np.unique(np.where(m)[0]):
            xs=np.where(m[y])[0]
            if not len(xs):continue
            start,end=int(xs.min()),int(xs.max());width=end-start+1
            for x in xs:
                phase=(x-start)/max(1,width-1);level=9 if phase<.55 else 8 if phase<.8 else 6
                guide[y,x]=[*pal[level],255]
    # The guide is a coordinate/sculpture control, not the final art.
    for ident in [1,5]:
        m=(ids==ident)&eroded;guide[m]=[*pal[8],255]
    # A smooth volume on the blank head, deliberately no facial symbols.
    m=(ids==1)&eroded;guide[m&(((xx-28)/6)**2+((yy-21)/10)**2<.65)]=[*pal[9],255]
    guide[m&(xx>=33)]=[*pal[6],255];guide[m&(yy>=30)]=[*pal[7],255]
    # Authored skin-only volume repair: no feature dots or an underwear-like flat pelvic patch.
    head=(ids==1)&eroded
    guide[head]=[*pal[9],255]
    guide[head & (((xx-28)/6)**2+((yy-21)/9)**2<.72)]=[*pal[10],255]
    guide[head & (((xx-27)/3)**2+((yy-17)/4)**2<.6)]=[*pal[11],255]
    guide[head & (xx>=33)]=[*pal[7],255]
    guide[head & (yy>=30)]=[*pal[8],255]
    guide[head & (xx==34)]=[*pal[6],255]
    pelvis=(ids==5)&eroded
    guide[pelvis]=[*pal[9],255]
    guide[pelvis & (xx<=25)]=[*pal[8],255]
    guide[pelvis & (xx>=37)]=[*pal[7],255]
    guide[pelvis & (((xx-28)/6)**2+((yy-62)/7)**2<.7)]=[*pal[10],255]
    guide[pelvis & (yy>=67)]=[*pal[9],255]
    # Lighting crosses the waist with no garment edge, crotch line, or genital symbol.
    body_mask=solid.astype(np.uint8)*255
    observed=Image.new('L',(64,96));d=ImageDraw.Draw(observed)
    # Skin patches read manually against the source, never a dark/light RGB heuristic.
    d.rectangle((26,19,31,20),fill=255);d.rectangle((24,26,25,27),fill=255);d.rectangle((32,26,33,27),fill=255)
    observed_a=(np.array(observed)>0)&(ids==1)
    features=Image.new('L',(64,96));d=ImageDraw.Draw(features);d.rectangle((23,21,35,31),fill=255)
    hidden=solid&~observed_a
    # Preserve scalp/pelvis and the constructed outside silhouette in final assembly.
    preserve=solid&((~eroded)|np.isin(ids,[1,5]));edit=solid&~preserve
    arrays={'ownership':ids,'body-mask':body_mask,'observed-skin':observed_a.astype(np.uint8)*255,'feature-exclusion':np.array(features),'hidden-construction':hidden.astype(np.uint8)*255,'edit-mask':edit.astype(np.uint8)*255,'preserve-mask':preserve.astype(np.uint8)*255,'skin-mask':body_mask,'empty-mask':np.zeros((96,64),np.uint8)}
    src=Image.open(r/'Game/Assets/Trials/OfficerContextV1/front-64x96.png').convert('RGBA')
    if args.prepare:
        for name,a in arrays.items():Image.fromarray(a).save(out/(name+'.png'))
        Image.fromarray(guide).save(out/'body-guide.png')
        Image.fromarray(guide).resize((768,1152),Image.Resampling.NEAREST).save(out/'body-guide-12x.png')
        src.resize((768,1152),Image.Resampling.NEAREST).save(out/'reference-12x.png')
        overlay=src.copy();od=ImageDraw.Draw(overlay);pts={k:v['xy'] for k,v in spec['landmarks'].items()}
        for chain in [('scalp_top','chin','neck','pelvis'),('shoulder_right','elbow_right','wrist_right'),('shoulder_left','elbow_left','wrist_left'),('pelvis','knee_right','ankle_right','sole_right'),('pelvis','knee_left','ankle_left','sole_left')]:od.line([tuple(pts[k]) for k in chain],fill=(77,220,242,255),width=1)
        for x,y in pts.values():od.point((x,y),fill=(255,215,76,255))
        contour=solid&~eroded;edge=np.zeros_like(guide);edge[contour]=[42,229,222,255]
        sourceedge=src.copy();sourceedge.alpha_composite(Image.fromarray(edge))
        board=Image.new('RGB',(1120,710),(32,44,51));bd=ImageDraw.Draw(board);font=ImageFont.truetype('C:/Windows/Fonts/meiryo.ttc',18)
        for title,im,x in [('元絵・同座標',src,20),('身体制御輪郭（青）',sourceedge,290),('制御図・完成品ではない',Image.fromarray(guide),560),('基準点（隠れ面は作画値）',overlay,830)]:
            bd.text((x,12),title,font=font,fill=(226,210,178));p=Image.new('RGBA',(256,384),(184,187,180,255));p.alpha_composite(im.resize((256,384),Image.Resampling.NEAREST));board.paste(p.convert('RGB'),(x,50))
        bd.text((20,470),'元絵の倍率・位置は変更しない。頭・肩・肘・手首・骨盤・膝・足首は同じ座標で対応。',font=font,fill='white')
        bd.text((20,505),'髪頂・肩当て・草摺・靴底は人体寸法として使わない。隠れた人体は作者による構成。',font=font,fill='white')
        bd.text((20,540),'顔・髪・衣服・性器の細部は素体に含めない。線と陰影は生成後の実画像で別検査。',font=font,fill='white')
        board.save(f/'source-control-review.png');print('SOURCE_CONTROLS_PREPARED_NOT_FINAL_ART');return
    if not args.raw:raise ValueError('Raw generation required')
    generation=Image.open(args.raw).convert('RGBA')
    # The output canvas scale is known; no bbox alignment, centering or silhouette fit.
    if generation.width*3!=generation.height*2:raise ValueError('Output aspect differs; refuse nonuniform fit')
    native=np.array(generation.resize((64,96),Image.Resampling.BOX))
    distances=((native[:,:,:3,None].astype(float)-pal.T[None,None,:,:].astype(float))**2).sum(2)
    rgb=pal[distances.argmin(2)]
    final=np.zeros_like(guide);final[solid,:3]=rgb[solid];final[solid,3]=255
    # Blank scalp and pelvis are pre-authored smooth skin surfaces, not regenerated facial anatomy.
    final[preserve]=guide[preserve]
    # Guard against background sampled inside the fixed body silhouette.
    if np.any(native[edit,3]<128):raise ValueError('Generation did not cover editable body interior')
    target=r/'Game/Assets/Trials/OfficerBodyBaseV2';target.mkdir(parents=True,exist_ok=True);(target/'parts').mkdir(exist_ok=True)
    Image.fromarray(final).save(target/'body-front.png')
    for name,a in arrays.items():Image.fromarray(a).save(target/(name+'.png'))
    for region in spec['regions']:
        layer=np.zeros_like(final);layer[ids==region['id']]=final[ids==region['id']];Image.fromarray(layer).save(target/'parts'/(region['name']+'.png'))
    report={'raw_sha256':g.sha(args.raw),'native_size':[64,96],'fixed_silhouette_difference':int(np.count_nonzero((final[:,:,3]>0)!=solid)), 'preserved_rgba_difference':int(np.count_nonzero(final[preserve]!=guide[preserve])), 'generated_interior_changed_pixels':int(np.count_nonzero(np.any(final[edit]!=guide[edit],axis=1))), 'visible_pixels':int(solid.sum()),'generation_canvas':list(generation.size),'sampling':'whole-canvas BOX, fixed 12-color palette, no bbox registration; preserves blank head/pelvis and outer boundary','status':'REQUIRES_ACTUAL_IMAGE_REVIEW'}
    (f/'assembly-result.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8');print(json.dumps(report))

if __name__=='__main__':main()
