from pathlib import Path
import json
import officer_art_pipeline as g
R=Path(__file__).resolve().parents[1];w=json.loads((R/'Art/Officer/work-record.json').read_text(encoding='utf-8'));p=w['plans'][w['movement_plan']];binding=g.require_plan(R,'motion',p)
for key in ['front_walk_plan','right_motion_plan','back_motion_plan','left_motion_plan']:
 plan=w['plans'][w[key]];g.require_plan(R,'motion',plan)
for item in p['runtime_packages']:
 path=g.evidence(R,item);data=json.loads(path.read_text(encoding='utf-8'))
 assert data['canvas']==[64,96] and data['display_scale']==1
 for sheet in data['sheets'].values():assert g.sha(R/'Game'/sheet['path'].removeprefix('res://'))==sheet['sha256']
(R/'Art/Officer/MovementV13/active-plan.json').write_bytes((json.dumps({'binding':binding,'plan':p['id']},indent=2)+'\n').encode())
print('MOVEMENT_EXISTING_FOUR_PACKAGES_READY '+binding)
