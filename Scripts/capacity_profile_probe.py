from pathlib import Path
import subprocess,json,hashlib,time,sys
R=Path(__file__).resolve().parents[1];OUT=R/'Docs/Design/PlayableLoopV1/runtime-local/population-probes';OUT.mkdir(parents=True,exist_ok=True)
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
with (OUT/'build.log').open('wb') as f:
 result=subprocess.run(['dotnet','build','Scripts/CapacityProbe/CapacityProbe.csproj','--ignore-failed-sources','-v','quiet'],cwd=R,stdout=f,stderr=subprocess.STDOUT,timeout=180)
assert result.returncode==0,OUT/'build.log'
dll=R/'Scripts/CapacityProbe/bin/Debug/net8.0/CapacityProbe.dll';core=R/'Scripts/CapacityProbe/bin/Debug/net8.0/HakutaiKagami.Core.dll'
assert sha(core)==sha(R/'Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Core.dll'),'probe Core differs from actual game'
baseline_path=OUT/'authority-baseline.json';baseline=json.loads(baseline_path.read_bytes()) if baseline_path.exists() else None
results=[]
for count in [0,100,1000]:
 pair=[]
 for layout,args in [('legacy',[]),('compact',['compact'])]:
  logfile=OUT/f'{layout}-{count}.log';start=time.time()
  with logfile.open('wb') as f:result=subprocess.run(['dotnet',str(dll),str(count),*args],cwd=R,stdout=f,stderr=subprocess.STDOUT,timeout=240)
  assert result.returncode==0,logfile
  lines=[s for s in logfile.read_bytes().decode().splitlines() if s.startswith('CAPACITY_RESULT ')];assert len(lines)==1,logfile
  j=json.loads(lines[0].split(' ',1)[1]);j.update(layout=layout,wall_seconds=time.time()-start,log=logfile.relative_to(R).as_posix(),log_sha256=sha(logfile))
  assert j['restore_exact'] and j['future_exact'] and j['actual_world_people']==count+3 and j['historical_people']==3 and j['route_nodes']==108 and j['route_edges']==118
  assert j['grants']==(2 if layout=='compact' else 109)*(count+3)
  if layout=='legacy' and baseline is not None:
   old=next(v for v in baseline['results'] if v['synthetic_registered_people']==count)
   assert j['start_hash']==old['start_hash'] and j['future_hash']==old['future_hash'] and j['payload_bytes']==old['payload_bytes'],('legacy bytes/future changed',count)
   j['exact_prechange_start_and_future_hash']=True
  pair.append(j);results.append(j);print('PASS population',count,layout,'payload',j['payload_bytes'],'grants',j['grants'],flush=True)
 assert pair[1]['payload_bytes']<pair[0]['payload_bytes']*(.3 if count==0 else .15),(count,'compact payload did not improve')
assert results[-1]['payload_bytes']<4*1024*1024
report=dict(core_sha256=sha(core),probe_sha256=sha(dll),results=results,baseline_present=baseline is not None,limits=['Synthetic stationary registered ronin plus3 sourced heads and selected-person movement; not nationwide autonomous NPCs or concurrent armies.','Each fresh process is measured separately on this PC. Wall time and allocated bytes are observations, not a frame-rate or product-quality certification.','Legacy and compact layouts are intentionally different world definitions. Legacy start/future hashes are also compared with the frozen prechange actual Core when that baseline is present.'])
(OUT/'results.json').write_bytes(json.dumps(report,ensure_ascii=False,indent=2).encode());print('CAPACITY_PROFILE_PROBE_PASSED six fresh-process population/movement/restore/continuation runs',flush=True)
