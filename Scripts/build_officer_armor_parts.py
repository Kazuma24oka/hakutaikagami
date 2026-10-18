from pathlib import Path
import argparse,json,sys,itertools
import numpy as np
from PIL import Image,ImageDraw,ImageFont
ap=argparse.ArgumentParser();ap.add_argument('--root',type=Path,required=True);ap.add_argument('--plan',default='armor-split-build');args=ap.parse_args();r=args.root.resolve();sys.path.insert(0,str(r/'Scripts'));import officer_art_pipeline as g;import officer_armor_layers as shared
w=json.loads((r/'Art/Officer/work-record.json').read_text(encoding='utf-8'));p=w['plans'][args.plan];g.require_plan(r,p['stage'],p)
f=r/'Art/Officer/ArmorSplitV3';out=r/'Game/Assets/Trials/OfficerArmorSplitV3';out.mkdir(parents=True,exist_ok=True);spec=json.loads((f/'part-definition.json').read_text(encoding='utf-8'))
def load(rel):return np.array(Image.open(r/rel).convert('RGBA'))
base=load('Game/Assets/Trials/OfficerBodyBaseV2/body-front.png');kit=load('Game/Assets/Trials/OfficerEquipmentV2/armor.png');source=load('Game/Assets/Trials/OfficerContextV1/front-64x96.png');source[source[:,:,3]==0]=0;bodyids=np.array(Image.open(r/'Game/Assets/Trials/OfficerBodyBaseV2/ownership.png'));head={k:load('Game/Assets/Trials/OfficerEquipmentV2/'+k+'.png') for k in ['face','hair']}
ids,layers,hidden=shared.build(kit,bodyids,spec);g.inspect_native(kit,ids)
from art_shared.part_ownership import audit_texture
for name,art in layers.items():
    audit_texture(art,kit,ids,[spec['parts'][name]['id']],construction_mask=hidden[name],max_components=spec['parts'][name]['max_components'])
    Image.fromarray(art).save(out/(name+'.png'));Image.fromarray(hidden[name].astype(np.uint8)*255).save(out/(name+'-hidden.png'));Image.fromarray(np.isin(bodyids,spec['body_occlusion'][name]).astype(np.uint8)*255).save(out/(name+'-occlusion.png'))
Image.fromarray(ids).save(out/'source-owners.png')
hand=np.zeros_like(base);owned=np.isin(bodyids,spec['bare_hand_foreground']['body_owner_ids']);hand[owned]=base[owned];Image.fromarray(hand).save(out/'bare-hands-front.png')
keys=['face','hair',*spec['parts']];allkeys=set(keys)
full=shared.compose(base,head,layers,bodyids,spec,allkeys)
if not np.array_equal(full,source):raise g.Blocked('Full source appearance differs; do not publish')
if not np.array_equal(shared.compose(base,head,layers,bodyids,spec,set()),base):raise g.Blocked('Bare base changed')
examples={'full':full,'base':base,'armor-off':shared.compose(base,head,layers,bodyids,spec,{'face','hair'})}
for name in spec['parts']:examples['without-'+name]=shared.compose(base,head,layers,bodyids,spec,allkeys-{name});examples['only-'+name]=shared.compose(base,head,layers,bodyids,spec,{'face','hair',name})
for name,art in examples.items():Image.fromarray(art).save(out/('example-'+name+'.png'))
hashes={}
for mask in range(1<<len(keys)):
    selected={name for i,name in enumerate(keys) if mask&(1<<i)};image=shared.compose(base,head,layers,bodyids,spec,selected);hashes[str(mask)]=shared.rgba_sha(image)
(f/'combination-hashes.json').write_text(json.dumps({'keys':keys,'hashes':hashes},indent=2)+'\n',encoding='utf-8')
report={'status':'REQUIRES_ACTUAL_REMOVAL_AND_SEMANTIC_REVIEW','canvas':[64,96],'display_scale':1,'part_count':len(layers),'combinations':len(hashes),'original_visible_pixel_difference':0,'bare_base_rgba_difference':0,'base_sha256':g.sha(r/'Game/Assets/Trials/OfficerBodyBaseV2/body-front.png'),'source_owner_counts':{k:int(np.count_nonzero(ids==v['id'])) for k,v in spec['parts'].items()},'hidden_counts':{k:int(v.sum()) for k,v in hidden.items()},'body_occlusion':spec['body_occlusion'],'scope':'Front static, provisional body; hidden pixels author constructed within declared source occluders; not motion approval'}
(f/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
font=ImageFont.truetype('C:/Windows/Fonts/meiryo.ttc',16)
def board(tiles,file):
    cols=5;rows=(len(tiles)+cols-1)//cols;im=Image.new('RGB',(1370,rows*450+50),(29,43,51));d=ImageDraw.Draw(im)
    for i,(title,a) in enumerate(tiles):
        x=15+(i%cols)*270;y=12+(i//cols)*450;d.text((x,y),title,font=font,fill=(230,214,183));tile=Image.new('RGBA',(256,384),(187,189,180,255));tile.alpha_composite(Image.fromarray(a).resize((256,384),Image.Resampling.NEAREST));im.paste(tile.convert('RGB'),(x,y+34))
    d.text((15,rows*450+5),'64×96・通常1倍／同座標。原画の可視画素と補完面を分離。元の素体は未変更。',font=font,fill='white');im.save(r/'Docs/Proposals/Evidence'/file)
board([('元絵',source),('全装備',full)]+[(spec['parts'][k]['label']+'を外す',examples['without-'+k]) for k in spec['parts']]+[('甲冑を全部外す',examples['armor-off']),('顔・髪も全部外す',base)],'officer-armor-removal-v3.png')
board([(spec['parts'][k]['label']+'（単体）',layers[k]) for k in spec['parts']],'officer-armor-isolated-v3.png')
print(json.dumps(report,ensure_ascii=False))
