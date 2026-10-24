from verify_save_capture import ROOT,BASE,sources,sha
import json,xml.etree.ElementTree as ET
j=json.loads((BASE/'verification.json').read_bytes());assert j['status']=='PASSED' and j['completion']=='5/5' and j['tests']==2055 and j['added']==0 and j['core_tests_reused_exact'] and j['core_inputs']==304 and j['gpu_processes']==50
assert j['sources']==sources(),'source changed since current save-capture verification'
assert sum(j['comparisons'].values())==278 and sum(j['numeric_pose_comparisons'].values())==278 and j['normal_shutdown_release_markers']==50 and j['new_evidence_checkpoints']==7 and j['guide_open_close_checks']==12
assert len(j['terrain_surface_coverage'])==131 and min(j['terrain_surface_coverage'].values())>.15 and j['ground_material_comparisons']=={}
m=json.loads((ROOT/'Docs/Design/ResponsiveClockV1/runtime-local/test-manifest.json').read_bytes());selected={p:h for p,h in j['sources'].items() if p.startswith(('Core/','Tests/','Game/Data/')) or p=='Scripts/current_test_contract.json'};assert selected==m['sources'] and len(selected)==304
assert sha(ROOT/'Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Core.dll')==m['core']==sha(ROOT/'Tests/bin/Debug/net8.0/HakutaiKagami.Core.dll')
for kind in ['binaries','proofs']:
 for p,h in j[kind].items():assert sha(ROOT/p)==h,p
for p,h in j['visual_review']['inspected'].items():assert j['proofs'][p]==h==sha(ROOT/p),p
print('PASS confirmed-world save capture:50 current GPU/278 world-image and numeric pairs;2055 exact unchanged Core tests reused')
