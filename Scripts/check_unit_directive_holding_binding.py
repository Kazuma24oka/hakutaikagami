from verify_unit_directive_holding import ROOT,BASE,sources,sha
import json
j=json.loads((BASE/'verification.json').read_bytes());assert j['status']=='PASSED' and j['completion']=='5/5' and j['tests']==1858 and j['added']==24 and j['gpu_processes']==27
assert len(j['terrain_surface_coverage'])==56 and min(j['terrain_surface_coverage'].values())>.15
assert j['sources']==sources(),'source changed since verification'
assert len(j['ground_material_comparisons'])==5 and set(j['ground_material_comparisons'].values())=={0}
assert sum(j['comparisons'].values())==202 and j['new_evidence_checkpoints']==32
assert sum(j['numeric_pose_comparisons'].values())==158 and j['normal_shutdown_release_markers']==18
for kind in ['binaries','proofs']:
 for p,h in j[kind].items():assert sha(ROOT/p)==h,p
for p,h in j['visual_review']['inspected'].items():assert j['proofs'][p]==h and sha(ROOT/p)==h,p
print('PASS actual held directive originals:1858 tests,27 GPU processes,202 exact world/image pairs,32 new save checkpoints')
