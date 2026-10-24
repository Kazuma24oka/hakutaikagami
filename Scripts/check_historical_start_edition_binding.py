from verify_historical_start_edition import ROOT,BASE,sources,sha
import json
j=json.loads((BASE/'verification.json').read_bytes());assert j['status']=='PASSED' and j['completion']=='5/5' and j['sources']==sources()
assert j['tests']==2093 and j['added']==17 and j['core_inputs']==310 and not j['core_tests_reused_exact'] and j['gpu_processes']==34
assert sum(j['comparisons'].values())==172 and sum(j['numeric_pose_comparisons'].values())==172 and j['normal_shutdown_release_markers']==34 and j['new_evidence_checkpoints']==9
for kind in ['binaries','proofs']:
 for p,h in j[kind].items():assert sha(ROOT/p)==h,p
for p,h in j['visual_review']['inspected'].items():assert j['proofs'][p]==h==sha(ROOT/p),p
print('PASS responsive historical edition34currentGPU172pairs;2093fresh tests17new')
