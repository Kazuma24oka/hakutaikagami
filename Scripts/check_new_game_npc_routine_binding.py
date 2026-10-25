from verify_new_game_npc_routine import ROOT,BASE,sources,sha
import json
j=json.loads((BASE/'verification.json').read_bytes());assert j['status']=='PASSED' and j['completion']=='5/5' and j['sources']==sources()
assert j['tests']==2122 and j['added']==18 and j['core_inputs']==317 and not j['core_tests_reused_exact'] and j['gpu_processes']==46
assert sum(j['comparisons'].values())==210 and sum(j['numeric_pose_comparisons'].values())==210 and j['normal_shutdown_release_markers']==46 and j['new_evidence_checkpoints']==10
for kind in ['binaries','proofs']:
 for p,h in j[kind].items():assert sha(ROOT/p)==h,p
for p,h in j['visual_review']['inspected'].items():assert j['proofs'][p]==h==sha(ROOT/p),p
print('PASS responsive historical bounded NPC routine46currentGPU210pairs;2122fresh tests18new')
