from verify_supply_work import ROOT,BASE,sources,sha,verify_copy,reused_core
import json
j=json.loads((BASE/'verification.json').read_bytes());assert j['status']=='PASSED' and j['completion']=='5/5' and j['sources']==sources();verify_copy();assert reused_core(j['sources'])==j['core_tests']
assert j['gpu_processes']==17 and sum(j['comparisons'].values())==sum(j['numeric'].values())==104
for kind in ['proofs','binaries']:
 for p,h in j[kind].items():assert sha(ROOT/p)==h,p
for p,h in j['visual_review']['inspected'].items():assert sha(ROOT/p)==h,p
assert sha(ROOT/j['video']['path'])==j['video']['sha256']
print('PASS supply work,17GPU104comparisons,unchanged2208Core evidence,actual video')
