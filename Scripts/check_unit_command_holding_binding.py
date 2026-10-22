from verify_unit_command_holding import ROOT,BASE,sources,sha
import json
j=json.loads((BASE/'verification.json').read_bytes());assert j['status']=='PASSED' and j['completion']=='5/5' and j['tests']==1627 and j['added']==31 and j['gpu_processes']==25
assert j['sources']==sources(),'source changed since verification'
assert len(j['ground_material_comparisons'])==5 and set(j['ground_material_comparisons'].values())=={0}
assert sum(j['comparisons'].values())==256 and j['new_command_checkpoints']==45
assert sum(j['numeric_pose_comparisons'].values())==212 and j['normal_shutdown_release_markers']==16
for kind in ['binaries','proofs']:
 for p,h in j[kind].items():assert sha(ROOT/p)==h,p
for p,h in j['visual_review']['inspected'].items():assert j['proofs'][p]==h and sha(ROOT/p)==h,p
print('PASS current unit command original delivery:1627 tests,25 GPU processes,256 exact world/image pairs,45 command save checkpoints')
