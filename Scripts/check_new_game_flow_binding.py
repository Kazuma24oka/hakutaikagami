import json
from verify_new_game_flow import ROOT,BASE,source_files,sha
j=json.loads((BASE/'verification.json').read_bytes());assert j['status']=='PASSED' and j['tests']==1352 and j['added']==36
assert j['sources']=={p.relative_to(ROOT).as_posix():sha(p) for p in source_files()}
for field in ['binaries','images','proofs']:
 for p,h in j[field].items():assert sha(ROOT/p)==h,p
assert len(j['comparisons'])==56 and set(j['comparisons'].values())=={0}
assert len(j['personal_info_comparisons'])==56 and set(j['personal_info_comparisons'].values())=={0}
assert j['visual_review']['inspected']
for p,h in j['visual_review']['inspected'].items():assert j['images'][p]==h,p
assert 'run/main_scene="res://Scenes/WorkHubTrial.tscn"' in (ROOT/'Game/project.godot').read_bytes().decode()
assert j['video'].endswith('.mp4') and sha(ROOT/j['video'])==j['proofs'][j['video']]
print('PASS current five-stage candidate,1352 tests,56 exact new world/person/3D pairs,reviewed images and video; legacy default unchanged')
