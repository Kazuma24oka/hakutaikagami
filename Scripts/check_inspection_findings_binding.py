from verify_inspection_findings import ROOT,BASE,sources,sha,verify_copy
import json
j=json.loads((BASE/'verification.json').read_bytes());assert j['status']=='PASSED' and j['completion']=='5/5' and j['sources']==sources();verify_copy()
assert j['gpu_processes']==17 and j['core_tests_executed']==2143 and sum(j['comparisons'].values())==sum(j['numeric'].values())==36
for kind in ['proofs','binaries']:
 for p,h in j[kind].items():assert sha(ROOT/p)==h,p
for p,h in j['visual_review']['inspected'].items():assert sha(ROOT/p)==h,p
assert sha(ROOT/j['video']['path'])==j['video']['sha256']
print('PASS inspection findings current sources,binaries,17GPU36world/image/pose pairs,fresh2143Core tests,video')
