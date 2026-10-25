from verify_terrain_cache import ROOT,BASE,sources,sha
import json
j=json.loads((BASE/'verification.json').read_bytes());assert j['status']=='PASSED' and j['completion']=='5/5' and j['sources']==sources()
assert j['tests']==2055 and j['core_inputs']==304 and j['core_tests_reused_exact'] and j['gpu_processes']==24
assert sum(j['comparisons'].values())==68 and sum(j['baseline_comparisons'].values())==12 and sum(j['numeric_pose_comparisons'].values())==36 and len(j['ground_material_comparisons'])==5
assert j['normal_shutdown_release_markers']==15
for kind in ['binaries','proofs']:
 for p,h in j[kind].items():assert sha(ROOT/p)==h,p
for p,h in j['visual_review']['inspected'].items():assert j['proofs'][p]==h==sha(ROOT/p),p
print('PASS terrain cache:24 current GPU,80exact world/image pairs,36numeric,5ground comparisons;2055exact Core reuse')
