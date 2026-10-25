from verify_repeated_exercise import ROOT,BASE,sources,sha
import json
j=json.loads((BASE/'verification.json').read_bytes());assert j['status']=='PASSED' and j['completion']=='5/5' and j['tests']==1739 and j['added']==21 and j['gpu_processes']==33
assert j['sources']==sources(),'source changed since verification'
assert len(j['ground_material_comparisons'])==5 and set(j['ground_material_comparisons'].values())=={0}
assert sum(j['comparisons'].values())==462 and j['new_evidence_checkpoints']==50
assert sum(j['numeric_pose_comparisons'].values())==418 and j['normal_shutdown_release_markers']==24
for kind in ['binaries','proofs']:
 for p,h in j[kind].items():assert sha(ROOT/p)==h,p
for p,h in j['visual_review']['inspected'].items():assert j['proofs'][p]==h and sha(ROOT/p)==h,p
print('PASS current repeated exercise lifetime losses:1739 tests,33 GPU processes,462 exact world/image pairs,50 repeated-exercise save checkpoints')
