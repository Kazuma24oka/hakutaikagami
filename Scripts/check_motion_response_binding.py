import json
from verify_motion_response import ROOT,BASE,source_files,sha
j=json.loads((BASE/'verification.json').read_bytes());assert j['status']=='PASSED' and j['tests']==1352 and j['completion']=='4/4'
assert j['sources']=={p.relative_to(ROOT).as_posix():sha(p) for p in source_files()}
for group in ['binaries','proofs']:
 for p,h in j[group].items():assert sha(ROOT/p)==h,p
for m in j['measurements']['capped']:assert m['P95Ms']<=20 and m['P99Ms']<=33.34 and max(m['ButtonToDrawMs'])<=50
assert j['boundary_checks']==15 and j['new_world_person_image_comparisons']==56
print('PASS current local motion sources/binaries/measurements/visual review/video; nationwide performance unverified')
