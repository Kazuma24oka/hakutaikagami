from verify_responsive_departure import ROOT,BASE,sources,sha
import json
j=json.loads((BASE/'verification.json').read_bytes());assert j['status']=='PASSED' and j['completion']=='5/5' and j['sources']==sources()
assert j['tests']==2076 and j['core_inputs']==306 and j['core_tests_reused_exact'] and j['gpu_processes']==42
assert sum(j['comparisons'].values())==170 and sum(j['numeric_pose_comparisons'].values())==170 and j['normal_shutdown_release_markers']==42 and j['new_evidence_checkpoints']==12
for kind in ['binaries','proofs']:
 for p,h in j[kind].items():assert sha(ROOT/p)==h,p
for p,h in j['visual_review']['inspected'].items():assert j['proofs'][p]==h==sha(ROOT/p),p
print('PASS responsive departure42currentGPU170world-image/numeric pairs;2076exact unchanged Core reuse')
