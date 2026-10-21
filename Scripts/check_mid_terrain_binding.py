from verify_mid_terrain import ROOT,BASE,source_files,sha
import json
j=json.loads((BASE/'verification.json').read_bytes());assert j['status']=='PASSED'
assert j['sources']=={p.relative_to(ROOT).as_posix():sha(p) for p in source_files()}
for group in ['binaries','images']:
 for p,h in j[group].items():assert sha(ROOT/p)==h,p
assert len(j['comparisons'])==7 and set(j['comparisons'].values())=={0}
assert j['visual_review'] and j['visual_review']['inspected']
for p,h in j['visual_review']['inspected'].items():assert sha(ROOT/p)==h,p
prior=json.loads((ROOT/'Docs/Design/WorkHubStartupV1/verification.json').read_bytes())
assert j['binaries']==prior['binaries'] and prior['summary']['tests']==json.loads((ROOT/'Scripts/current_test_contract.json').read_bytes())['expected_passed'] and len(prior['comparisons'])==36
print('PASS mid terrain sources, binaries, exact restored images and startup regression')
