from verify_unit_reply_recovery import ROOT,BASE,sources,sha
import json
j=json.loads((BASE/'verification.json').read_bytes());assert j['status']=='PASSED' and j['completion']=='5/5' and j['tests']==1578 and j['added']==21 and j['gpu_processes']==33
assert j['sources']==sources(),'source changed since verification'
assert len(j['ground_material_comparisons'])==5 and set(j['ground_material_comparisons'].values())=={0}
assert sum(j['comparisons'].values())==308 and j['new_reply_checkpoints']==48
assert sum(j['numeric_pose_comparisons'].values())==264 and j['normal_shutdown_release_markers']==24
for kind in ['binaries','proofs']:
 for p,h in j[kind].items():assert sha(ROOT/p)==h,p
for p,h in j['visual_review']['inspected'].items():assert j['proofs'][p]==h and sha(ROOT/p)==h,p
print('PASS current unit reply recovery:1578 tests,33 GPU processes,308 exact world/image pairs,48 reply save checkpoints')
