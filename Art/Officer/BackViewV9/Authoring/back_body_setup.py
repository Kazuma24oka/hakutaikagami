from pathlib import Path
import json,sys,copy,shutil
import numpy as np
from PIL import Image
R=Path(r'C:/Workspace/Projects/Games/hakutaikagami');O=R/'Art/Officer/BackViewV9';sys.path.insert(0,str(R/'Scripts'));import officer_art_pipeline as g
def write(p,v):p.write_bytes((json.dumps(v,ensure_ascii=False,indent=2)+'\n').encode())
def e(p):return {'path':p.relative_to(R).as_posix(),'sha256':g.sha(p)}
W=R/'Art/Officer/work-record.json';w=json.loads(W.read_text(encoding='utf-8'));p=copy.deepcopy(w['plans']['back-v9-authoring'])
archive=O/'Authoring';archive.mkdir(exist_ok=True)
for name in ['back_setup.py','back_retry.py','back_body_setup.py']:
 target=archive/name;shutil.copy2(Path(__file__).parent/name,target);p['tools'].append(e(target))
w['plans'][p['id']]=p;write(W,w);g.require_plan(R,'front_authoring',p)
shutil.copy2(r'C:/Users/hizen/.codex/generated_images/01a14d72-71b3-7531-83f9-1b21fa289173/exec-b1669775-3f12-4b5e-b260-55b7a7bb37d2.png',O/'armor-raw.png')
im=Image.open(O/'armor-raw.png').convert('RGBA');assert im.size==(1024,1536);a=np.array(im.resize((64,96),Image.Resampling.NEAREST));mask=a[:,:,3]>=200;a[:,:,:3]=np.array(Image.fromarray(a[:,:,:3]).quantize(colors=48,method=Image.Quantize.MEDIANCUT,dither=Image.Dither.NONE).convert('RGB'));a[:,:,3]=mask*255;a[~mask]=0;Image.fromarray(a).save(O/'source-back-candidate.png')
front=Image.open(R/'Game/Assets/Trials/OfficerContextV1/front-64x96.png');right=Image.open(R/'Game/Assets/Trials/OfficerRightViewV7/source-right.png');board=Image.new('RGBA',(216,96),'#29343b')
for i,b in enumerate([front,right,Image.fromarray(a)]):board.alpha_composite(b,(72*i+4,0))
board.resize((864,384),Image.Resampling.NEAREST).save(O/'direction-comparison.png');print('back native',Image.fromarray(a).getbbox())
prompt='''Create ONE strict REAR VIEW base-body pixel sprite for this same ADULT male game officer. Image1 is the original bare FRONT, reference for skin palette and adult proportions ONLY. Image2 is the EXACT rear silhouette guide on a64x96 full2:3 canvas. Image3 is the matched rear armor for registration/context, not clothes to preserve. Geometry guide has priority: bald scalp starts y13, head bottom32, shoulders38, elbows49, wrists61, hips63,knees77,ankles87,soles91. Render professional muted warm skin pixel shading inside the silhouette without changing its position or shape. The figure faces AWAY, show back of bald head, back/shoulder blades, backs of arms/legs/heels, not chest or face. No hair, beard, facial features, clothing, underwear, shoes, equipment, jewelry, text, grids or floor shadow. Fully unclothed neutral ADULT game mannequin with a smooth SKIN-COLORED FEATURELESS pelvis, no genitals, buttock crease or suggestive detail. Adult nonsexual construction asset. Do not enlarge scalp to hair extent; top13/96 rows stay transparent. Keep same stance and aligned joints; both hands at side. Full transparent canvas, no crop/zoom, no extra figures. Coherent crisp square pixel clusters matching reference, no smooth painted anatomy, no photorealism. This is a skin-shading proposal constrained by the guide.'''
(O/'body-prompt.txt').write_bytes(prompt.encode())
p['id']='back-body-v9-authoring';p['feedback']=['User requests same body/parts mechanism for back view; fixed featureless adult skin base before clothing.'];p['backend']={'name':'builtin-imagegen','prompt':e(O/'body-prompt.txt'),'transparent_background':True,'reference_images':[e(R/'Game/Assets/Trials/OfficerBodyBaseV2/body-front.png'),e(O/'body-guide-12x.png'),e(O/'source-back-candidate.png')],'seed':'not exposed','model':'not exposed'};p['inputs'] +=[e(O/'armor-raw.png'),e(O/'body-silhouette.png')];p['change_scope']=['Generate rear skin shading within fixed mirrored front silhouette; blank head and pelvis remain explicit protected regions.'];w['plans'][p['id']]=p;write(W,w);print(g.require_plan(R,'front_authoring',p))
