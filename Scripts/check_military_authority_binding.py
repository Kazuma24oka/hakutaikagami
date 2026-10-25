from verify_military_authority import ROOT,BASE,sources,sha
import json
j=json.loads((BASE/'verification.json').read_bytes());assert j['status']=='PASSED' and j['completion']=='5/5' and j['tests']==1970 and j['added']==47 and j['gpu_processes']==27
assert len(j['terrain_surface_coverage'])==64 and min(j['terrain_surface_coverage'].values())>.15
assert j['sources']==sources(),'source changed since verification'
assert len(j['ground_material_comparisons'])==5 and set(j['ground_material_comparisons'].values())=={0}
assert sum(j['comparisons'].values())==188 and j['new_evidence_checkpoints']==32
assert sum(j['numeric_pose_comparisons'].values())==144 and j['normal_shutdown_release_markers']==18
for kind in ['binaries','proofs']:
 for p,h in j[kind].items():assert sha(ROOT/p)==h,p
for p,h in j['visual_review']['inspected'].items():assert j['proofs'][p]==h and sha(ROOT/p)==h,p
print('PASS actual source-backed military authority:1970 tests,27 GPU processes,188 exact world/image pairs,32 new save checkpoints')
