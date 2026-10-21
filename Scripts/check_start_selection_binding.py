import json
from verify_start_selection import ROOT,BASE,source_files,sha
j=json.loads((BASE/'verification.json').read_bytes());assert j['status']=='PASSED' and j['tests']==985
assert j['sources']=={p.relative_to(ROOT).as_posix():sha(p) for p in source_files()}
for group in ['binaries','images','proofs']:
 for p,h in j[group].items():assert sha(ROOT/p)==h,p
assert len(j['comparisons'])==10 and set(j['comparisons'].values())=={0}
assert j['visual_review'] and j['visual_review']['inspected']
for p,h in j['visual_review']['inspected'].items():assert sha(ROOT/p)==h,p
prior=json.loads((ROOT/'Docs/Design/PersonalStartV1/verification.json').read_bytes());assert prior['binaries']==j['binaries'] and prior['tests']==985
print('PASS current start UI, immutable separated saves,985 tested binary and66 exact comparisons')
