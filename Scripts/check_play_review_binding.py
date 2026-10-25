from verify_play_review import ROOT,BASE,sources,sha,verify_copy
import json
j=json.loads((BASE/'verification.json').read_bytes());assert j['status']=='PASSED' and j['completion']=='5/5' and j['sources']==sources()
assert j['tests']==2122 and j['added']==0 and j['core_inputs']==317 and j['core_tests_reused_exact'] and j['gpu_processes']==2 and j['normal_shutdown_release_markers']==2 and j['comparisons']==j['numeric_pose_comparisons']==2
verify_copy()
for kind in ['binaries','proofs']:
 for p,h in j[kind].items():assert sha(ROOT/p)==h,p
for p,h in j['visual_review']['inspected'].items():assert sha(ROOT/p)==h,p
assert sha(ROOT/j['video']['video'])==j['video']['video_sha256']
print('PASS review2GPU2world-image/numeric;exact2122Core reuse;recording copy provenance;video hash')
