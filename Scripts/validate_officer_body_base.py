"""Validate actual body PNGs and create a native-scale review from those files."""
from pathlib import Path
import argparse,json,sys,hashlib,base64
import numpy as np
from PIL import Image,ImageDraw,ImageFont

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--root',type=Path,required=True);a=ap.parse_args();r=a.root.resolve()
    sys.path.insert(0,str(r/'Scripts'));import officer_art_pipeline as g
    from art_shared.part_ownership import component_sizes
    f=r/'Art/Officer/BodyBaseV1';out=r/'Game/Assets/Trials/OfficerBodyBaseV1'
    work=json.loads((r/'Art/Officer/work-record.json').read_text(encoding='utf-8'))
    g.require_plan(r,'front_authoring',work['plans']['body-front-v1-validation'])
    spec=json.loads((f/'geometry.json').read_text(encoding='utf-8'));body=np.array(Image.open(out/'body-front.png').convert('RGBA'));ids=np.array(Image.open(out/'ownership.png'))
    original=np.array(Image.open(f/'controls/authoring-body.png').convert('RGBA'));edit=np.array(Image.open(out/'edit-mask.png'))>0;solid=body[:,:,3]>0
    checks=g.inspect_native(body,ids,protected_face=np.array(Image.open(f/'controls/face-protection.png'))>0,hair_ids=[])
    assert np.array_equal(body[:,:,3],original[:,:,3]),'Native silhouette changed'
    assert np.array_equal(body[~edit],original[~edit]),'Protected blank head/pelvis/edge changed'
    assert set(np.unique(body[:,:,3]))=={0,255}
    assert component_sizes(solid)==[int(solid.sum())],'Unconnected full body'
    ys,xs=np.where(solid);bounds=[int(xs.min()),int(ys.min()),int(xs.max())+1,int(ys.max())+1]
    assert bounds==[11,8,53,92],bounds
    assert len(np.unique(body[solid,:3],axis=0))<=6
    def e(path):return {'path':str(path.relative_to(r)).replace('\\','/'),'sha256':g.sha(path)}
    expected_ids=np.array(Image.open(f/'controls/ownership.png'))
    assert np.array_equal(ids,expected_ids)
    geometry_regions={};criteria={};part_defs={};order=[];assembly=Image.new('RGBA',(64,96));metrics={}
    (f/'checks').mkdir(exist_ok=True)
    for region in spec['regions']:
        name=region['name'];i=region['id'];m=ids==i
        yy,xx=np.where(expected_ids==i);box=[int(xx.min()),int(yy.min()),int(xx.max())+1,int(yy.max())+1]
        mask_path=f/'checks'/(name+'-region.png');Image.fromarray(m.astype(np.uint8)*255).save(mask_path)
        geometry_regions[name]=e(mask_path);criteria[name]={'bounds':box,'tolerance_px':0,'basis':'Frozen construction controls; not inferred anatomy'}
        part_path=out/'parts'/(name+'.png');part=np.array(Image.open(part_path).convert('RGBA'))
        assert np.array_equal(part[m],body[m]) and not part[~m].any(),name
        pieces=component_sizes(m)
        assert len(pieces)==1,(name,pieces)
        part_defs[name]={'image':e(part_path),'hidden':e(f/'controls/empty-mask.png'),'owner_ids':[i],'max_components':1}
        order.append(name);assembly.alpha_composite(Image.fromarray(part));metrics[name]={'bounds':box,'pixels':int(m.sum()),'components':len(pieces)}
    assert np.array_equal(np.array(assembly),body)
    bundle={'source':e(out/'body-front.png'),'ownership':e(out/'ownership.png'),'protected_face':e(f/'controls/face-protection.png'),'hair_ids':[],'geometry_regions':geometry_regions,'geometry_criteria':criteria,'palette_rgb':spec['palette_rgb'],'parts':part_defs,'order':order,'recolor_cases':[]}
    (f/'candidate-bundle.json').write_text(json.dumps(bundle,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    result=g.validate_bundle(r,bundle)
    result.update({'body_bounds':bounds,'head_body_ratio':4,'minimum_margin_px':4,'per_part_metrics':metrics,'native_display_scale':1,'preserved_rgba_difference':0,'head_and_pelvis':'exact frozen featureless construction','facial_hair_clothes_genital_detail':'absent by separate protected construction; visual check still required','motion_approved':False,'user_appearance_accepted':False})
    (f/'validation.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    # Evidence contains ONLY the actual exported pixels, no re-generated showcase.
    font=ImageFont.truetype('C:/Windows/Fonts/meiryo.ttc',17);small=ImageFont.truetype('C:/Windows/Fonts/meiryo.ttc',14)
    board=Image.new('RGB',(1100,880),'#202d35');d=ImageDraw.Draw(board)
    d.text((20,12),'百代鏡｜正面・裸素体 64×96／顔・髪・服なし（制作候補）',font=font,fill='white')
    native=Image.fromarray(body)
    colors=['#e8e8dd','#141b23','#b8bab1']
    for i,(label,bg) in enumerate(zip(['白背景・5倍','黒背景・5倍','部位ID・5倍'],colors)):
        x=20+i*350;d.text((x,48),label,font=font,fill='#e7c78f')
        tile=Image.new('RGBA',(64,96),bg)
        if i==2:
            visual=np.zeros_like(body)
            for region in spec['regions']:
                ident=region['id'];visual[ids==ident]=(*((np.array([75,117,151])+ident*np.array([51,67,83]))%180+50).tolist(),255)
            tile.alpha_composite(Image.fromarray(visual))
        else:tile.alpha_composite(native)
        board.paste(tile.convert('RGB').resize((320,480),Image.Resampling.NEAREST),(x,80))
    d.text((20,580),'通常表示・1倍（64×96）',font=font,fill='white');tile=Image.new('RGBA',(64,96),'#d4d4ca');tile.alpha_composite(native);board.paste(tile.convert('RGB'),(25,615))
    d.text((125,620),'頭頂 y8／顎 y28／足底 y91　身長84px・頭長21px（構成値）',font=small,fill='white')
    d.text((125,652),'固定パレット6色、透明度0/255、17部位、再合成RGBA差分0',font=small,fill='white')
    d.text((125,684),'顔なし頭部・骨盤・輪郭を保持。生成は内部の陰影候補のみ。',font=small,fill='white')
    d.text((20,748),'正面静止だけの候補。側面・背面・可動域・関節補完・歩行は未検証。',font=small,fill='#d0b99a')
    d.text((20,778),'原寸と拡大は同じ実PNG。ユーザーの外見受入は未実施。',font=small,fill='#d0b99a')
    evidence=r/'Docs/Proposals/Evidence/officer-body-base-v1.png';board.save(evidence)
    print('BODY_BASE_TECHNICAL_PASS size=64x96 native_scale=1 bounds=11,8,53,92 owners=17 components=1 protected_difference=0 reconstruction=0')

if __name__=='__main__':main()
