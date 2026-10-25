import json
from verify_play_guide import ROOT,BASE,sources,core_sources,sha
j=json.loads((BASE/'verification.json').read_bytes());assert j['status']=='PASSED' and j['completion']=='5/5' and j['gpu_processes']==6 and j['guide_open_close_checks']==12 and j['tests']==2040 and j['core_tests_reused_exact'] and j['core_inputs']==302
assert j['sources']==sources(),'source changed since current guide verification'
assert sum(j['comparisons'].values())==20 and sum(j['numeric_pose_comparisons'].values())==20
m=json.loads((ROOT/'Docs/Design/PreparedAdvanceV1/runtime-local/test-manifest.json').read_bytes());assert core_sources(j['sources'])==m['sources']
assert sha(ROOT/'Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Core.dll')==m['core']
for kind in ['binaries','proofs']:
 for p,h in j[kind].items():assert sha(ROOT/p)==h,p
for p,h in j['visual_review']['inspected'].items():assert j['proofs'][p]==h==sha(ROOT/p),p
print('PASS view-only guide:6 current GPU processes,20 exact restore pairs,12 actual open/close checks;2040 exact unchanged Core tests reused')
