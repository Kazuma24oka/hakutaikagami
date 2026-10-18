from pathlib import Path
import sys,json,copy
import numpy as np
from PIL import Image,ImageDraw
R=Path(r'C:/Workspace/Projects/Games/hakutaikagami');sys.path.insert(0,str(R/'Scripts'));import officer_art_pipeline as g
O=R/'Art/Officer/BackViewV9';O.mkdir(parents=True,exist_ok=True)
def write(p,v):p.write_bytes((json.dumps(v,ensure_ascii=False,indent=2)+'\n').encode())
def e(p):return {'path':p.relative_to(R).as_posix(),'sha256':g.sha(p)}
front=json.loads((R/'Art/Officer/BodyBaseV2/source-definition.json').read_text(encoding='utf-8'))
spec={'schema':1,'canvas':[64,96],'direction':'back','display_scale':1,'character_left':'screen-left','character_right':'screen-right','anchors':{n:[63-v['xy'][0],v['xy'][1]] for n,v in front['landmarks'].items()},'body_regions':[{**v,'polygon':[[63-x,y] for x,y in v['polygon']]} for v in front['regions']],'body_geometry_policy':'Horizontal registration of existing approved-for-continuation front geometry; rear shading newly authored. No new body proportions approved.','source_registration':{'scale':1/16,'translate':[0,0],'per_part_fit':False,'per_frame_fit':False},'face_slot':'empty in back view; same shared face selection retained as no-op','sword_side':'character-left = screen-left in back view','base_policy':'Adult faceless hairless unclothed skin surface, featureless pelvis, no underwear or genital detail.'}
write(O/'definition.json',spec)
prompt='''Use case: identity-preserve. Asset: ONE rear-facing pixel-art strategy-game officer sprite, transparent full 2:3 canvas, intended native64x96. Image1 original FRONT is character/costume/pixel-style reference; image2 RIGHT PROFILE fixes same identity; image3 COLOR GUIDE fixes full-canvas registration and proportions ONLY. Turn this SAME mature adult Japanese Sengoku officer to show his BACK, centered straight rear orthographic view, no visible face/eyes/nose/beard, no three-quarter turn. Messy tied black hair with rear knot/ponytail; no helmet. Same red lacquer lamellar shoulder/arm/shin armor, black sleeves and trousers, muted gold edging, red patterned jinbaori coat with two divided rear tails. Coat covers back cuirass, not a new giant cape, no new crest or large motif. Katana worn at CHARACTER LEFT hip, which is SCREEN LEFT in rear view, handle near left belt and scabbard angles outward/down-left. Respect guide: hair top row4, bald skull top13, head lower edge32, shoulder joints38, elbows49, wrists61, hip63, knees77, ankles87, soles91 of96 rows. Head+hair is28px tall; below head is only60px, adult compact proportions exactly like original, not tall realistic six-head proportions and not baby chibi. Keep shoulders broad and paired legs readable. Do NOT change costume, palette, stance, scale or landmarks. Professional readable square pixel clusters, subtle red/gold material shading matching original, no smooth painted illustration, no blur. Full body visible including both hands, back of boots and weapon. No ground shadow, text, frame, guides or extra figures. Replace the guide's flat colored masses with finished rear artwork without changing their placement.'''
(O/'armor-prompt.txt').write_bytes(prompt.encode())
w=json.loads((R/'Art/Officer/work-record.json').read_text(encoding='utf-8'));p=copy.deepcopy(w['plans']['right-parts-v7-authoring']);p['id']='back-v9-authoring';p['stage']='front_authoring'
for k in list(p):
 if k.endswith('_review') or k=='results':del p[k]
p['feedback']=['User accepts proceeding to back static body/armor parts, then back idle/walk and actual Godot validation.']
p['change_scope']=['New BACK authoring controls and generated rear image. Preserve all existing front/right originals.','Front baseline landmarks remain identity binding only; mirrored back targets are independently declared in definition.json.']
p['prior_failures']+=['Long-torso side generation rejected; guide and source scale must be inspected before parts.','L mask must be explicitly copied into alpha, never converted to opaqueRGBA erase mask.']
p['inputs']+=[e(O/'definition.json')];p['references']+=[e(R/'Docs/Proposals/OFFICER_RIGHT_MOTION_V8.md')]
p['backend']={'name':'builtin-imagegen','prompt':e(O/'armor-prompt.txt'),'transparent_background':True,'reference_images':[e(R/'Game/Assets/Trials/OfficerContextV1/front-64x96.png'),e(R/'Game/Assets/Trials/OfficerRightViewV7/source-right.png')],'seed':'not exposed','model':'not exposed'}
p['direction_scope']='back only; original front correspondence not relabeled as rear measurements'
p['regressions']['cross_direction']='Compare front/right/back scales and character sides; back only is new, left view remains unimplemented.'
w['plans'][p['id']]=p;write(R/'Art/Officer/work-record.json',w);g.require_plan(R,'front_authoring',p)
body=np.array(Image.open(R/'Game/Assets/Trials/OfficerBodyBaseV2/body-front.png').convert('RGBA'))[:,::-1].copy();alpha=body[:,:,3]>0
Image.fromarray((alpha*255).astype('uint8')).save(O/'body-silhouette.png')
flat=np.zeros_like(body);flat[alpha]=[203,154,111,255];Image.fromarray(flat).save(O/'body-guide.png');Image.fromarray(flat).resize((768,1152),Image.Resampling.NEAREST).save(O/'body-guide-12x.png')
for v in spec['body_regions']:
 mask=Image.new('L',(64,96));ImageDraw.Draw(mask).polygon(v['polygon'],fill=255);mask.save(O/('body-'+v['name']+'-mask.png'))
guide=Image.new('RGBA',(64,96));d=ImageDraw.Draw(guide)
shapes=[('hair',[(22,7),(35,4),(44,12),(43,26),(37,32),(25,30),(20,20)],'#302a2b'),('coat',[(23,34),(43,34),(47,58),(48,79),(34,76),(31,64),(28,78),(16,79),(19,58)],'#983f2e'),('legs',[(22,64),(44,64),(49,86),(38,88),(33,72),(29,87),(16,87)],'#343033'),('shoulders',[(13,35),(24,33),(24,44),(12,47)],'#bd5134'),('shoulders2',[(43,33),(53,35),(56,47),(43,44)],'#bd5134'),('arms',[(12,46),(22,46),(18,63),(12,65),(9,61)],'#6e3d31'),('arms2',[(44,46),(54,46),(56,61),(52,65),(46,63)],'#6e3d31'),('boots',[(16,85),(27,85),(27,91),(13,91)],'#403b38'),('boots2',[(39,85),(49,85),(52,91),(39,91)],'#403b38'),('sword',[(24,52),(26,54),(5,79),(3,77)],'#a68c52')]
for n,poly,color in shapes:d.polygon(poly,fill=color)
guide.save(O/'armor-guide.png');guide.resize((768,1152),Image.Resampling.NEAREST).save(O/'armor-guide-12x.png')
p['backend']['reference_images'].append(e(O/'armor-guide-12x.png'));p['inputs']+=[e(O/'armor-guide.png'),e(O/'body-silhouette.png')];w['plans'][p['id']]=p;write(R/'Art/Officer/work-record.json',w);print(g.require_plan(R,'front_authoring',p))
