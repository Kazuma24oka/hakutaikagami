from verify_playable_loop import ROOT,BASE,sources,sha
import json
j=json.loads((BASE/'verification.json').read_bytes());assert j['status']=='PASSED' and j['completion']=='5/5' and j['tests']==2010 and j['added']==40 and j['gpu_processes']==31
assert len(j['terrain_surface_coverage'])==72 and min(j['terrain_surface_coverage'].values())>.15
assert j['sources']==sources(),'source changed since verification'
assert len(j['ground_material_comparisons'])==5 and set(j['ground_material_comparisons'].values())=={0}
assert sum(j['comparisons'].values())==204 and j['new_evidence_checkpoints']==8
assert sum(j['numeric_pose_comparisons'].values())==160 and j['normal_shutdown_release_markers']==22
for case,count in [('capacity-100',100),('capacity-1000',1000)]:
 p=BASE/'runtime-local'/case/'population-observation.json';v=json.loads(p.read_bytes());assert v['SyntheticRegisteredPeople']==count and v['HistoricalPeople']==3 and len(v['Frames'])>=120 and v['ObservationSeconds']>=3 and v['AutoElapsedSeconds']>=2 and v['AutoClickMilliseconds']>=0 and v['PauseClickMilliseconds']>=0
probe=json.loads((BASE/'runtime-local/population-probes/results.json').read_bytes());assert probe['baseline_present'] and len(probe['results'])==6 and all(v.get('exact_prechange_start_and_future_hash') for v in probe['results'] if v['layout']=='legacy')
for kind in ['binaries','proofs']:
 for p,h in j[kind].items():assert sha(ROOT/p)==h,p
for p,h in j['visual_review']['inspected'].items():assert j['proofs'][p]==h and sha(ROOT/p)==h,p
print('PASS playable entry population and saved operations:2010 tests,31 GPU processes,204 exact world/image pairs,8 new population save checkpoints')
