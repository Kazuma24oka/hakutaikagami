from verify_military_renewal import ROOT,BASE,sources,sha
import json
j=json.loads((BASE/'verification.json').read_bytes());assert j['status']=='PASSED' and j['completion']=='5/5' and j['tests']==1718 and j['added']==23 and j['gpu_processes']==29
assert j['sources']==sources(),'source changed since verification'
assert len(j['ground_material_comparisons'])==5 and set(j['ground_material_comparisons'].values())=={0}
assert sum(j['comparisons'].values())==362 and j['new_evidence_checkpoints']==42
assert sum(j['numeric_pose_comparisons'].values())==318 and j['normal_shutdown_release_markers']==20
for kind in ['binaries','proofs']:
 for p,h in j[kind].items():assert sha(ROOT/p)==h,p
for p,h in j['visual_review']['inspected'].items():assert j['proofs'][p]==h and sha(ROOT/p)==h,p
print('PASS current military lifetime renewal:1718 tests,29 GPU processes,362 exact world/image pairs,42 military-renewal save checkpoints')
