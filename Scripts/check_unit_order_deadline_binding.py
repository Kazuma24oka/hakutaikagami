from verify_unit_order_deadline import ROOT,BASE,sources,sha
import json
j=json.loads((BASE/'verification.json').read_bytes());assert j['status']=='PASSED' and j['completion']=='5/5' and j['tests']==1644 and j['added']==17 and j['gpu_processes']==25
assert j['sources']==sources(),'source changed since verification'
assert len(j['ground_material_comparisons'])==5 and set(j['ground_material_comparisons'].values())=={0}
assert sum(j['comparisons'].values())==252 and j['new_deadline_checkpoints']==48
assert sum(j['numeric_pose_comparisons'].values())==208 and j['normal_shutdown_release_markers']==16
for kind in ['binaries','proofs']:
 for p,h in j[kind].items():assert sha(ROOT/p)==h,p
for p,h in j['visual_review']['inspected'].items():assert j['proofs'][p]==h and sha(ROOT/p)==h,p
print('PASS current unit order receipt deadline:1644 tests,25 GPU processes,252 exact world/image pairs,48 deadline save checkpoints')
