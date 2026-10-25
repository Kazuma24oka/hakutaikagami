from verify_prepared_submit import ROOT,BASE,sources,sha
import json
j=json.loads((BASE/'verification.json').read_bytes());assert j['status']=='PASSED' and j['completion']=='5/5' and j['sources']==sources()
assert j['tests']==2076 and j['added']==21 and j['core_inputs']==306 and j['full_test_rerun'] and not j['core_tests_reused_exact'] and j['gpu_processes']==8
assert sum(j['comparisons'].values())==24 and sum(j['numeric_pose_comparisons'].values())==24 and j['normal_shutdown_release_markers']==8
for kind in ['binaries','proofs']:
 for p,h in j[kind].items():assert sha(ROOT/p)==h,p
for p,h in j['visual_review']['inspected'].items():assert j['proofs'][p]==h==sha(ROOT/p),p
print('PASS prepared Submit:2076full current tests/8GPU/24world-image,numeric pairs')
