from verify_unit_cancellation_recovery import ROOT,BASE,sources,sha
import json
j=json.loads((BASE/'verification.json').read_bytes());assert j['status']=='PASSED' and j['completion']=='5/5' and j['tests']==1695 and j['added']==8 and j['gpu_processes']==25
assert j['sources']==sources(),'source changed since verification'
assert len(j['ground_material_comparisons'])==5 and set(j['ground_material_comparisons'].values())=={0}
assert sum(j['comparisons'].values())==278 and j['new_evidence_checkpoints']==16
assert sum(j['numeric_pose_comparisons'].values())==234 and j['normal_shutdown_release_markers']==16
for kind in ['binaries','proofs']:
 for p,h in j[kind].items():assert sha(ROOT/p)==h,p
for p,h in j['visual_review']['inspected'].items():assert j['proofs'][p]==h and sha(ROOT/p)==h,p
print('PASS current unit cancellation result recovery:1695 tests,25 GPU processes,278 exact world/image pairs,16 cancellation-result save checkpoints')
