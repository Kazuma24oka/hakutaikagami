from verify_local_demand import ROOT,BASE,sources,sha,verify_copy,fresh_core
import json
j=json.loads((BASE/'verification.json').read_bytes());assert j['status']=='PASSED' and j['completion']=='5/5' and j['sources']==sources();verify_copy();assert fresh_core(j['sources'])==j['core_tests']
assert j['gpu_processes']==19 and sum(j['comparisons'].values())==sum(j['numeric'].values())==104
for kind in ['proofs','binaries']:
 for p,h in j[kind].items():assert sha(ROOT/p)==h,p
for p,h in j['visual_review']['inspected'].items():assert sha(ROOT/p)==h,p
assert sha(ROOT/j['video']['path'])==j['video']['sha256']
print('PASS current local demand,19GPU104comparisons,fresh2208Core tests,actual video')
