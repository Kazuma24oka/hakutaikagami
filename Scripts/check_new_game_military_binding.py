from verify_new_game_military import ROOT,BASE,sources,sha
import json
j=json.loads((BASE/'verification.json').read_bytes());assert j['status']=='PASSED' and j['completion']=='5/5' and j['tests']==1405 and j['added']==28 and j['gpu_processes']==31
assert j['sources']==sources(),'source changed since verification'
assert sum(j['comparisons'].values())==228 and j['new_military_checkpoints']==51
for kind in ['binaries','proofs']:
 for p,h in j[kind].items():assert sha(ROOT/p)==h,p
for p,h in j['visual_review']['inspected'].items():assert j['proofs'][p]==h and sha(ROOT/p)==h,p
print('PASS current new-game military:1405 tests,31 GPU processes,228 exact world/image pairs,51 military save checkpoints')
