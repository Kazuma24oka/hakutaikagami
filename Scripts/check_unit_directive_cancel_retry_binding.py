from verify_unit_directive_cancel_retry import ROOT,BASE,sources,sha
import json
j=json.loads((BASE/'verification.json').read_bytes());assert j['status']=='PASSED' and j['completion']=='5/5' and j['tests']==1905 and j['added']==20 and j['gpu_processes']==25
assert len(j['terrain_surface_coverage'])==56 and min(j['terrain_surface_coverage'].values())>.15
assert j['sources']==sources(),'source changed since verification'
assert len(j['ground_material_comparisons'])==5 and set(j['ground_material_comparisons'].values())=={0}
assert sum(j['comparisons'].values())==172 and j['new_evidence_checkpoints']==24
assert sum(j['numeric_pose_comparisons'].values())==128 and j['normal_shutdown_release_markers']==16
for kind in ['binaries','proofs']:
 for p,h in j[kind].items():assert sha(ROOT/p)==h,p
for p,h in j['visual_review']['inspected'].items():assert j['proofs'][p]==h and sha(ROOT/p)==h,p
print('PASS actual cancellation original retry:1905 tests,25 GPU processes,172 exact world/image pairs,24 new save checkpoints')
