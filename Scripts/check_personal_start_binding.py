import json
from verify_personal_start import ROOT,BASE,source_files,sha
expected=json.loads((ROOT/'Scripts/current_test_contract.json').read_bytes())['expected_passed']
j=json.loads((BASE/'verification.json').read_bytes());assert j['status']=='PASSED' and j['tests']==expected and j['added']==15
assert j['sources']=={p.relative_to(ROOT).as_posix():sha(p) for p in source_files()}
for group in ['binaries','images','proofs']:
 for p,h in j[group].items():assert sha(ROOT/p)==h,p
assert len(j['comparisons'])==20 and set(j['comparisons'].values())=={0}
assert j['visual_review'] and j['visual_review']['inspected']
for p,h in j['visual_review']['inspected'].items():assert sha(ROOT/p)==h,p
prior=json.loads((ROOT/'Docs/Design/WorkHubStartupV1/verification.json').read_bytes());assert prior['summary']['tests']==expected and len(prior['comparisons'])==36 and set(prior['comparisons'].values())=={0} and prior['binaries']==j['binaries']
print('PASS personal start current source, fixed-count tested binary,36 legacy and20 independent-person comparisons')
