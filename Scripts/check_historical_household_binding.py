from verify_historical_household import ROOT,BASE,sources,sha
import json
j=json.loads((BASE/'verification.json').read_bytes());assert j['status']=='PASSED' and j['completion']=='5/5' and j['sources']==sources()
assert j['tests']==2104 and j['added']==11 and j['core_inputs']==314 and not j['core_tests_reused_exact'] and j['gpu_processes']==38
assert sum(j['comparisons'].values())==190 and sum(j['numeric_pose_comparisons'].values())==190 and j['normal_shutdown_release_markers']==38 and j['new_evidence_checkpoints']==9
for kind in ['binaries','proofs']:
 for p,h in j[kind].items():assert sha(ROOT/p)==h,p
for p,h in j['visual_review']['inspected'].items():assert j['proofs'][p]==h==sha(ROOT/p),p
print('PASS responsive historical household38currentGPU190pairs;2104fresh tests11new')
