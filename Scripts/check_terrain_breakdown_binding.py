import json
from verify_terrain_breakdown import ROOT,BASE,source_files,sha
j=json.loads((BASE/'verification.json').read_bytes());assert j['status']=='PASSED'
assert j['sources']=={p.relative_to(ROOT).as_posix():sha(p) for p in source_files()}
for group in ['binaries','images','proofs']:
 for p,h in j[group].items():assert sha(ROOT/p)==h,p
assert len(j['comparisons'])==6 and set(j['comparisons'].values())=={0}
assert j['visual_review'] and j['visual_review']['inspected']
for p,h in j['visual_review']['inspected'].items():assert sha(ROOT/p)==h,p
print('PASS profiling source, tested binary, actual phase measurements and equal images')
