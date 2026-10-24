from verify_work_cycle import ROOT,BASE,sources,sha,verify_copy
import json
j=json.loads((BASE/'verification.json').read_bytes());assert j['status']=='PASSED' and j['completion']=='5/5' and j['sources']==sources();verify_copy()
assert j['gpu_processes']==29 and j['core_tests_executed']==2183 and sum(j['comparisons'].values())==sum(j['numeric'].values())==100
for kind in ['proofs','binaries']:
 for p,h in j[kind].items():assert sha(ROOT/p)==h,p
for p,h in j['visual_review']['inspected'].items():assert sha(ROOT/p)==h,p
assert sha(ROOT/j['video']['path'])==j['video']['sha256']
print('PASS current work cycle sources,binaries,29GPU100world/3D/pose comparisons,fresh2183Core tests,video')
