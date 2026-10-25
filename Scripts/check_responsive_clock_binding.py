from verify_responsive_clock import ROOT,BASE,sources,sha
import json
j=json.loads((BASE/'verification.json').read_bytes());assert j['status']=='PASSED' and j['completion']=='5/5' and j['tests']==2055 and j['added']==15 and j['gpu_processes']==49
assert len(j['terrain_surface_coverage'])==92 and min(j['terrain_surface_coverage'].values())>.15
assert j['sources']==sources(),'source changed since verification'
assert len(j['ground_material_comparisons'])==5 and set(j['ground_material_comparisons'].values())=={0}
assert sum(j['comparisons'].values())==244 and j['new_evidence_checkpoints']==9
assert sum(j['numeric_pose_comparisons'].values())==200 and j['normal_shutdown_release_markers']==40
for case,count in [('capacity-100',100),('capacity-1000',1000)]:
 p=ROOT/'Docs/Design/PlayableLoopV1/runtime-local'/case/'population-observation.json';v=json.loads(p.read_bytes());assert v['SyntheticRegisteredPeople']==count and v['HistoricalPeople']==3 and len(v['Frames'])>=120 and v['ObservationSeconds']>=3 and v['AutoElapsedSeconds']>=2 and v['AutoClickMilliseconds']>=0 and v['PauseClickMilliseconds']>=0
probe=json.loads((ROOT/'Docs/Design/PlayableLoopV1/runtime-local/population-probes/results.json').read_bytes());assert probe['baseline_present'] and len(probe['results'])==6 and all(v.get('exact_prechange_start_and_future_hash') for v in probe['results'] if v['layout']=='legacy')
from check_session_generations import check
assert check(ROOT)['assignments'] and len(check(ROOT)['assignments'])==2
assert j['guide_open_close_checks']==12
for kind in ['binaries','proofs']:
 for p,h in j[kind].items():assert sha(ROOT/p)==h,p
for p,h in j['visual_review']['inspected'].items():assert j['proofs'][p]==h and sha(ROOT/p)==h,p
print('PASS responsive opaque clock:2055 tests,49 GPU processes,244 exact world/image pairs,9 new save checkpoints')

for n in [1,2]:
 v=json.loads((BASE/'runtime-local/clock-1000'/('clock-click-'+str(n)+'.json')).read_bytes())
 for prefix in ['Start','Stop']:
  assert 0<=v[prefix+'ClickMilliseconds']<=v[prefix+'CompletionMilliseconds'] and v[prefix+'RenderTicks']>0

print('PASS responsive opaque clock:2055 tests,49 GPU processes,244 exact world/image pairs,9 new save checkpoints')
