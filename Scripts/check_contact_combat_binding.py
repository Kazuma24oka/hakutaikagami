from verify_contact_combat import ROOT,BASE,sources,sha
import json
j=json.loads((BASE/'verification.json').read_bytes());assert j['status']=='PASSED' and j['completion']=='5/5' and j['tests']==1454 and j['added']==49 and j['gpu_processes']==39
assert j['sources']==sources(),'source changed since verification'
assert sum(j['comparisons'].values())==292 and j['new_combat_checkpoints']==27
for kind in ['binaries','proofs']:
 for p,h in j[kind].items():assert sha(ROOT/p)==h,p
for p,h in j['visual_review']['inspected'].items():assert j['proofs'][p]==h and sha(ROOT/p)==h,p
print('PASS current contact combat:1454 tests,39 GPU processes,292 exact world/image pairs,27 combat save checkpoints')
