import json
from verify_life_entry import ROOT,BASE,source_files,sha
expected=json.loads((ROOT/'Scripts/current_test_contract.json').read_bytes())['expected_passed']
j=json.loads((BASE/'verification.json').read_bytes());assert j['status']=='PASSED' and j['tests']==expected and j['added']==0
assert j['sources']=={p.relative_to(ROOT).as_posix():sha(p) for p in source_files()}
assert 'run/main_scene="res://Scenes/WorkHubTrial.tscn"' in (ROOT/'Game/project.godot').read_bytes().decode()
for group in ['binaries','images','proofs']:
 for p,h in j[group].items():assert sha(ROOT/p)==h,p
assert len(j['comparisons'])==68 and set(j['comparisons'].values())=={0}
assert len(j['full_world_comparisons'])==68 and set(j['full_world_comparisons'].values())=={0}
assert j['visual_review'] and j['visual_review']['inspected']
for p,h in j['visual_review']['inspected'].items():assert sha(ROOT/p)==h,p
prior=json.loads((ROOT/'Docs/Design/MilitaryInquiryV1/verification.json').read_bytes());assert prior['binaries']==j['binaries'] and prior['tests']==expected
print(f'PASS life entry current sources,{expected} tested binary,424 image plus3 lifetime,68 full-world candidate comparisons; default unchanged')
