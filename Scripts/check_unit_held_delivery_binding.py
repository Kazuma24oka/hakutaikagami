from verify_unit_held_delivery import ROOT,BASE,sources,sha
import json
j=json.loads((BASE/'verification.json').read_bytes());assert j['status']=='PASSED' and j['completion']=='5/5' and j['tests']==1596 and j['added']==18 and j['gpu_processes']==35
assert j['sources']==sources(),'source changed since verification'
assert len(j['ground_material_comparisons'])==5 and set(j['ground_material_comparisons'].values())=={0}
assert sum(j['comparisons'].values())==348 and j['new_held_checkpoints']==56
assert sum(j['numeric_pose_comparisons'].values())==304 and j['normal_shutdown_release_markers']==26
for kind in ['binaries','proofs']:
 for p,h in j[kind].items():assert sha(ROOT/p)==h,p
for p,h in j['visual_review']['inspected'].items():assert j['proofs'][p]==h and sha(ROOT/p)==h,p
print('PASS current unit held-paper delivery:1596 tests,35 GPU processes,348 exact world/image pairs,56 held-paper save checkpoints')
