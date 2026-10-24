from verify_work_decision_ui import ROOT,BASE,sources,sha,verify_copy
import json
j=json.loads((BASE/'verification.json').read_bytes());assert j['status']=='PASSED' and j['completion']=='5/5' and j['sources']==sources();verify_copy()
assert j['gpu_processes']==13 and j['core_tests_reused']==2122 and j['core_inputs']==317 and sum(j['comparisons'].values())==sum(j['numeric'].values())==22
for kind in ['proofs','binaries']:
 for p,h in j[kind].items():assert sha(ROOT/p)==h,p
for p,h in j['visual_review']['inspected'].items():assert sha(ROOT/p)==h,p
assert sha(ROOT/j['video']['path'])==j['video']['sha256']
print('PASS current work decision UI sources,binaries,13GPU22world/image/pose pairs,exact2122Core reuse,video')
