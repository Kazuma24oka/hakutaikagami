"""Source-bound semantic layers over immutable base; per-item body occlusion is separate."""
from pathlib import Path
import argparse,json,sys,itertools,io,base64
import numpy as np
from PIL import Image,ImageDraw,ImageFont

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--root',type=Path,required=True);ap.add_argument('--plan',default='equipment-front-authoring');a=ap.parse_args();r=a.root.resolve();sys.path.insert(0,str(r/'Scripts'));import officer_art_pipeline as g
    w=json.loads((r/'Art/Officer/work-record.json').read_text(encoding='utf-8'));p=w['plans'][a.plan];g.require_plan(r,p['stage'],p)
    f=r/'Art/Officer/EquipmentPartsV2';spec=json.loads((f/'part-definition.json').read_text(encoding='utf-8'));out=r/'Game/Assets/Trials/OfficerEquipmentV2';out.mkdir(parents=True,exist_ok=True)
    src=np.array(Image.open(r/'Game/Assets/Trials/OfficerContextV1/front-64x96.png').convert('RGBA'));base=np.array(Image.open(r/'Game/Assets/Trials/OfficerBodyBaseV2/body-front.png').convert('RGBA'));bodyids=np.array(Image.open(r/'Game/Assets/Trials/OfficerBodyBaseV2/ownership.png'));fg=src[:,:,3]>0;yy,xx=np.mgrid[:96,:64]
    face=np.zeros((96,64),bool)
    for y,span in spec['face_visible_rows'].items():face[int(y),span[0]:span[1]+1]=True
    face&=fg
    hair=fg&(yy<=spec['hair_top_rows'][1])&~face
    for y,spans in spec['hair_lower_spans'].items():
        for x1,x2 in spans:hair[int(y),x1:x2+1]=fg[int(y),x1:x2+1]&~face[int(y),x1:x2+1]
    shadow=np.zeros((96,64),bool)
    for x,y in spec.get('hair_cast_shadow_pixels',[]):shadow[y,x]=True
    if np.any(shadow&~face):raise g.Blocked('Cast shadow must be observed forehead skin')
    face &= ~shadow
    kit=fg&~(face|hair|shadow);owners=np.zeros((96,64),np.uint8);owners[face]=1;owners[hair]=2;owners[kit]=3;owners[shadow]=4
    protected=np.zeros((96,64),bool)
    for spans in spec['protected_features'].values():
        for x1,y1,x2,y2 in spans:protected[y1:y2+1,x1:x2+1]=True
    if np.any(protected&(hair|shadow)):raise g.Blocked('Hair consumed eye/nose/mouth pixels')
    if np.any(protected&fg&~face):raise g.Blocked('Equipment contains face features')
    g.inspect_native(src,owners,protected_face=protected,hair_ids=[2,4])
    # Skin completion only under current head, never a whole opaque armor/head image.
    faceart=np.zeros_like(base);head=bodyids==1
    faceart[head]=base[head]  # Existing blank-head volume, not a new flat opaque oval.
    faceart[face]=src[face]
    hairart=np.zeros_like(base);hairart[hair|shadow]=src[hair|shadow];kitart=np.zeros_like(base);kitart[kit]=src[kit]
    layers={'face':Image.fromarray(faceart),'hair':Image.fromarray(hairart),'armor':Image.fromarray(kitart)}
    occlude=np.isin(bodyids,spec['armor_body_occlusion_owner_ids'])
    for name,art in layers.items():art.save(out/(name+'.png'))
    for name,mask in [('source-owners',owners),('protected-features',protected.astype(np.uint8)*255),('hair-cast-shadow',shadow.astype(np.uint8)*255),('face-observed',face.astype(np.uint8)*255),('face-hidden-completion',(head&~face).astype(np.uint8)*255),('armor-body-occlusion',occlude.astype(np.uint8)*255)]:Image.fromarray(mask).save(out/(name+'.png'))
    def compose(enabled):
        visible=base.copy()
        if 'armor' in enabled:visible[occlude]=0
        result=Image.fromarray(visible)
        for name in ['face','armor','hair']:
            if name in enabled:result.alpha_composite(layers[name])
        return result
    results={};composites={};original=src.copy();original[~fg]=0
    for flags in itertools.product([False,True],repeat=3):
        selected={name for name,flag in zip(['face','hair','armor'],flags) if flag};key='-'.join(sorted(selected)) or 'base';im=compose(selected);im.save(out/('composite-'+key+'.png'));composites[key]=im
        if not selected and not np.array_equal(np.array(im),base):raise g.Blocked('All off changed base')
        results[key]={'selected':sorted(selected),'visible_pixels':int(np.count_nonzero(np.array(im)[:,:,3]))}
    full=np.array(compose(set(layers)));diff=int(np.count_nonzero(np.any(full!=original,axis=2)))
    if diff:raise g.Blocked('Full source appearance not restored; do not publish candidate')
    original_outside=int(np.count_nonzero((base[:,:,3]>0)&~fg))
    report={'status':'REQUIRES_SEMANTIC_AND_ACTUAL_IMAGE_REVIEW','canvas':[64,96],'display_scale':1,'source_foreground_pixels':int(fg.sum()),'full_reconstruction_pixel_difference':diff,'source_transparent_rgb_normalized':True,'base_outside_original_pixels_before_item_occlusion':original_outside,'body_occlusion_scope':spec['armor_body_occlusion_owner_ids'],'base_png_sha256':g.sha(r/'Game/Assets/Trials/OfficerBodyBaseV2/body-front.png'),'all_off_rgba_difference':int(np.count_nonzero(np.array(compose(set()))!=base)),'protected_feature_hair_overlap':0,'hair_cast_shadow_pixels':int(shadow.sum()),'combinations':results,'part_pixels':{name:int(np.count_nonzero(np.array(im)[:,:,3])) for name,im in layers.items()},'limits':['face includes beard','armor is complete kit, not separately interchangeable limb armor','hidden hair-side face skin author constructed','static only, no joint movement acceptance']}
    (f/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    original_im=Image.fromarray(original);board=Image.new('RGB',(1370,1060),(29,43,51));d=ImageDraw.Draw(board);font=ImageFont.truetype('C:/Windows/Fonts/meiryo.ttc',16)
    tiles=[('元の武将',original_im),('素体＋全パーツ',compose(set(layers))),('髪なし（顔を保持）',compose({'face','armor'})),('甲冑なし',compose({'face','hair'})),('すべて外す・元の素体',compose(set())),('顔・髭（単体）',layers['face']),('髪（単体）',layers['hair']),('甲冑一式（単体）',layers['armor']),('顔なし',compose({'armor','hair'})),('素体＋顔',compose({'face'}))]
    for i,(title,im) in enumerate(tiles):
        x=15+(i%5)*270;y=15+(i//5)*500;d.text((x,y),title,font=font,fill=(230,214,183));bg=Image.new('RGBA',(256,384),(185,188,179,255));bg.alpha_composite(im.resize((256,384),Image.Resampling.NEAREST));board.paste(bg.convert('RGB'),(x,y+35))
    d.text((20,1005),'原寸64×96／通常1倍。元絵と同座標。素体PNGは変更せず、装備の遮蔽は独立マスク。',font=font,fill='white');board.save(r/'Docs/Proposals/Evidence/officer-equipment-v2.png')
    wb=Image.new('RGB',(1120,520),(29,43,51));wd=ImageDraw.Draw(wb)
    qa=[('全装備・白',compose(set(layers)),'white'),('髪なし・白',compose({'face','armor'}),'white'),('全解除・白',compose(set()),'white'),('全装備・黒',compose(set(layers)),'black'),('髪なし・黒',compose({'face','armor'}),'black'),('全解除・黒',compose(set()),'black')]
    for i,(title,im,color) in enumerate(qa):
        x=10+i*184;wd.text((x,12),title,font=font,fill='white');tile=Image.new('RGBA',(128,192),color);tile.alpha_composite(im.resize((128,192),Image.Resampling.NEAREST));wb.paste(tile.convert('RGB'),(x,50))
    wd.text((20,290),'同じ64×96の実PNGを白黒背景で比較。全解除の素体は変更していない。',font=font,fill='white');wb.save(r/'Docs/Proposals/Evidence/officer-equipment-white-black.png')
    print(json.dumps(report,ensure_ascii=False))
if __name__=='__main__':main()
