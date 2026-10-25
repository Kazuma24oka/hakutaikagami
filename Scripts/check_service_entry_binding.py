from verify_service_entry import ROOT,BASE,sources,sha
import json
j=json.loads((BASE/'verification.json').read_bytes());assert j['status']=='PASSED' and j['tests']==1377 and j['completion']=='5/5'
assert j['sources']==sources(),'source changed since verification'
for kind in ['binaries','proofs']:
 for p,h in j[kind].items():assert sha(ROOT/p)==h,p
assert sum(j['comparisons'].values())==126
for p,h in j['visual_review']['inspected'].items():assert j['proofs'][p]==h and sha(ROOT/p)==h,p
print('PASS current service entry:1377 tests,25 GPU processes,126 exact world/image pairs,normal startup and legacy military retained')
