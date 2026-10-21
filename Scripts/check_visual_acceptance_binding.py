from pathlib import Path
import json
from verify_visual_acceptance import ROOT,BASE,source_files,sha
j=json.loads((BASE/'verification.json').read_bytes());assert j['status']=='PASSED'
assert j['sources']=={p.relative_to(ROOT).as_posix():sha(p) for p in source_files()}
assert sha(Path(__file__))==j['binding_checker_sha256']
for group in ['binaries','images','proofs']:
 for p,h in j[group].items():assert sha(ROOT/p)==h,p
assert len(j['comparisons'])==24 and set(j['comparisons'].values())=={0}
assert j['performance']['working_bytes_max']<1024**3 and j['performance']['godot_video_bytes_max']<512*1024**2
assert j['visual_review'] and j['visual_review']['inspected']
for p,h in j['visual_review']['inspected'].items():assert sha(ROOT/p)==h,p
for stage in ['CameraPanV1','GroundMaterialV1','FieldModelsV1','ForceLodV1','CameraContinuityV1','LodAppearanceV1','MidTerrainV1','WorkHubStartupV1']:
 prior=json.loads((ROOT/'Docs/Design'/stage/'verification.json').read_bytes());assert j['binaries']==prior['binaries']
print('PASS current visual action integration, nine saves, exact images, tested binary and nationwide performance')
