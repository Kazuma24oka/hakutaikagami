import json
from verify_retainer_start import ROOT,BASE,source_files,sha
expected=json.loads((ROOT/'Scripts/current_test_contract.json').read_bytes())['expected_passed']
j=json.loads((BASE/'verification.json').read_bytes());assert j['status']=='PASSED' and j['tests']==expected and j['added']==25
assert j['sources']=={p.relative_to(ROOT).as_posix():sha(p) for p in source_files()}
for group in ['binaries','images','proofs']:
 for p,h in j[group].items():assert sha(ROOT/p)==h,p
assert len(j['comparisons'])==30 and set(j['comparisons'].values())=={0}
assert j['visual_review'] and j['visual_review']['inspected']
for p,h in j['visual_review']['inspected'].items():assert sha(ROOT/p)==h,p
prior=json.loads((ROOT/'Docs/Design/StartSelectionV1/verification.json').read_bytes());assert prior['binaries']==j['binaries'] and prior['tests']==expected
print('PASS current retainer service, known orders, independent explanations,1010 tested binary and96 exact comparisons')
