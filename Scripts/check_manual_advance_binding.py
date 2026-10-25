from verify_manual_advance import ROOT,BASE,sources,sha
import json
j=json.loads((BASE/'verification.json').read_bytes());assert j['status']=='PASSED' and j['completion']=='5/5' and j['sources']==sources()
assert j['tests']==2055 and j['core_inputs']==304 and j['core_tests_reused_exact'] and j['gpu_processes']==40
assert sum(j['comparisons'].values())==116 and sum(j['numeric_pose_comparisons'].values())==116 and j['normal_shutdown_release_markers']==40 and j['new_evidence_checkpoints']==14
for kind in ['binaries','proofs']:
 for p,h in j[kind].items():assert sha(ROOT/p)==h,p
for p,h in j['visual_review']['inspected'].items():assert j['proofs'][p]==h==sha(ROOT/p),p
print('PASS manual-time40currentGPU116world-image/numeric pairs;2055exact unchanged Core reuse')
