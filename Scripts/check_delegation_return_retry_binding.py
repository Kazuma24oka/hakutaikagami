import json
from verify_delegation_return_retry import ROOT,BASE,source_files,sha
expected=json.loads((ROOT/'Scripts/current_test_contract.json').read_bytes())['expected_passed']
j=json.loads((BASE/'verification.json').read_bytes());assert j['status']=='PASSED' and j['tests']==expected and j['added']==21
assert j['sources']=={p.relative_to(ROOT).as_posix():sha(p) for p in source_files()}
for group in ['binaries','images','proofs']:
 for p,h in j[group].items():assert sha(ROOT/p)==h,p
assert len(j['comparisons'])==10 and set(j['comparisons'].values())=={0}
assert j['visual_review'] and j['visual_review']['inspected']
for p,h in j['visual_review']['inspected'].items():assert sha(ROOT/p)==h,p
prior=json.loads((ROOT/'Docs/Design/DelegationInquiryV1/verification.json').read_bytes());assert prior['binaries']==j['binaries'] and prior['tests']==expected
print(f'PASS delegation current sources,{expected} tested binary and216 world plus3 lifetime exact comparisons')
