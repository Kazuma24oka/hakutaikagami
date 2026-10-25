from verify_unit_directive_retry import ROOT,BASE,sources,sha
import json
j=json.loads((BASE/'verification.json').read_bytes());assert j['status']=='PASSED' and j['completion']=='5/5' and j['tests']==1834 and j['added']==12 and j['gpu_processes']==23
assert len(j['terrain_surface_coverage'])==48 and min(j['terrain_surface_coverage'].values())>.15
assert j['sources']==sources(),'source changed since verification'
assert len(j['ground_material_comparisons'])==5 and set(j['ground_material_comparisons'].values())=={0}
assert sum(j['comparisons'].values())==156 and j['new_evidence_checkpoints']==16
assert sum(j['numeric_pose_comparisons'].values())==112 and j['normal_shutdown_release_markers']==14
for kind in ['binaries','proofs']:
 for p,h in j[kind].items():assert sha(ROOT/p)==h,p
for p,h in j['visual_review']['inspected'].items():assert j['proofs'][p]==h and sha(ROOT/p)==h,p
print('PASS explicit original retry:1834 tests,23 GPU processes,156 exact world/image pairs,16 new save checkpoints')
