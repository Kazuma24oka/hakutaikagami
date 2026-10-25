from verify_unit_recipient import ROOT,BASE,sources,sha
import json
j=json.loads((BASE/'verification.json').read_bytes());assert j['status']=='PASSED' and j['completion']=='5/5' and j['tests']==1795 and j['added']==20 and j['gpu_processes']==29
assert len(j['terrain_surface_coverage'])==80 and min(j['terrain_surface_coverage'].values())>.15
assert j['sources']==sources(),'source changed since verification'
assert len(j['ground_material_comparisons'])==5 and set(j['ground_material_comparisons'].values())=={0}
assert sum(j['comparisons'].values())==320 and j['new_evidence_checkpoints']==48
assert sum(j['numeric_pose_comparisons'].values())==276 and j['normal_shutdown_release_markers']==20
for kind in ['binaries','proofs']:
 for p,h in j[kind].items():assert sha(ROOT/p)==h,p
for p,h in j['visual_review']['inspected'].items():assert j['proofs'][p]==h and sha(ROOT/p)==h,p
print('PASS current unit-specific physical query:1795 tests,29 GPU processes,320 exact world/image pairs,48 selected-recipient save checkpoints')
