import json
from verify_officer_library import ROOT,BASE,source_files,sha
j=json.loads((BASE/'verification.json').read_bytes())
assert j['status']=='PASSED' and j['tests']==1316 and j['added']==35
assert j['sources']=={p.relative_to(ROOT).as_posix():sha(p) for p in source_files()}
for field in ['binaries','proofs','images']:
    for p,h in j[field].items():assert sha(ROOT/p)==h,p
assert j['visual_review']['inspected']==j['images']
assert j['summary']['library_data_restore_identical'] and j['summary']['legacy_world_image_comparisons']==424
assert 'run/main_scene="res://Scenes/WorkHubTrial.tscn"' in (ROOT/'Game/project.godot').read_bytes().decode()
print('PASS current new-game/library sources, tested binary, reviewed images and exact library restore; default preserved')
