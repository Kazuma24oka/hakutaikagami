from pathlib import Path
import json,uuid,sys,xml.etree.ElementTree as ET
from verify_manual_advance import ROOT,EXE,run as parent_run,pairs,pose_pairs,sha,write,collect
from verify_prepared_submit import sources as parent_sources
BASE=ROOT/'Docs/Design/PreparationQueueV1';OUT=BASE/'runtime-local'
def sources():
 j=parent_sources()
 for p in [Path(__file__),ROOT/'Scripts/check_preparation_queue_binding.py',BASE/'README.md',BASE/'contract.json']:j[p.relative_to(ROOT).as_posix()]=sha(p)
 return j
def run(args,log,marker=None):parent_run(args,log,marker)
def main():
 OUT.mkdir(parents=True,exist_ok=True);before=sources();proofs={};comparisons={};numeric={};queues={}
 previous=ROOT/'Docs/Design/PreparedSubmitV1/verification.json';old=json.loads(previous.read_bytes());allowed=set(json.loads((BASE/'contract.json').read_bytes())['allowed_source_changes']);delta={p for p in set(before)|set(old['sources']) if before.get(p)!=old['sources'].get(p)};assert delta<=allowed,sorted(delta-allowed)
 proofs[previous.relative_to(ROOT).as_posix()]=sha(previous)
 run(['dotnet','build','Game/HakutaiKagami.Game.csproj','--no-restore'],OUT/'build.log')
 inputs={p:h for p,h in before.items() if p.startswith(('Core/','Tests/','Game/Data/')) or p=='Scripts/current_test_contract.json'}
 manifest=ROOT/'Docs/Design/PreparedSubmitV1/runtime-local/test-manifest.json';m=json.loads(manifest.read_bytes());trx=ROOT/'Docs/Design/PreparedSubmitV1/runtime-local/tests/all.trx'
 assert inputs==m['sources'] and len(inputs)==306 and sha(trx)==m['trx'] and sha(ROOT/'Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Core.dll')==m['core']==sha(ROOT/'Tests/bin/Debug/net8.0/HakutaiKagami.Core.dll')
 c=ET.parse(trx).find('.//{*}Counters').attrib;assert c['passed']=='2076' and c['failed']=='0' and c['notExecuted']=='0'
 write(OUT/'test-manifest.json',dict(sources=inputs,trx=sha(trx),core=m['core'],tests=2076,reused_from=manifest.relative_to(ROOT).as_posix()))
 for p in [manifest,trx,OUT/'test-manifest.json']:proofs[p.relative_to(ROOT).as_posix()]=sha(p)
 run([sys.executable,ROOT/'Scripts/check_session_generations.py'],OUT/'ownership.log','SESSION_GENERATION_OWNERSHIP_PASSED')
 cases=[]
 for case in ['lease-clock','lease-manual','lease-save','lease-exit']:cases.append((case,OUT/case,2,'PREPARATION_QUEUE_PASSED'))
 for case in ['manual-cancel','manual-menu','manual-fault','manual-notice']:cases.append((case,ROOT/'Docs/Design/ManualAdvanceV1/runtime-local'/case,2,'MANUAL_TIME_PASSED'))
 for case in ['clock-cancel','clock-fault']:cases.append((case,ROOT/'Docs/Design/ResponsiveClockV1/runtime-local'/case,2,'RESPONSIVE_CLOCK_PASSED'))
 for case in ['savecapture-normal','savecapture-prepare-fault','savecapture-write-fault']:cases.append((case,ROOT/'Docs/Design/SaveCaptureV1/runtime-local'/case,3 if case=='savecapture-normal' else 2,'SAVE_CAPTURE_PASSED'))
 cases.append(('saveui-failure',ROOT/'Docs/Design/ResponsiveSaveV1/runtime-local/saveui-failure',16,'RESPONSIVE_SAVE_PASSED'))
 for case in ['prefetch-stale','prefetch-save','prefetch-fault']:cases.append((case,ROOT/'Docs/Design/PreparedAdvanceV1/runtime-local'/case,2,'PREPARED_ADVANCE_PASSED'))
 for case in ['batch-switch','batch-save','batch-notice']:cases.append((case,ROOT/'Docs/Design/BatchedAdvanceV1/runtime-local'/case,2,'BATCHED_ADVANCE_PASSED'))
 cases.append(('group',ROOT/'Docs/Design/NewGameFlowV1/runtime-local/group',4,'NEW_GAME_FLOW_PASSED'));assert len(cases)==21
 for case,folder,n,marker in cases:
  key=uuid.uuid4().hex
  for mode in ['check','restore']:
   run([EXE,'--path',ROOT/'Game','--resolution','1280x900','--','--new-game-test-key='+key,'--new-game-case='+case]+(['--new-game-restore'] if mode=='restore' else [])+(['--play-guide-check'] if case=='group' else []),folder/(mode+'.log'),marker)
   if case.startswith('lease-'):
    q=json.loads((folder/(mode+'-queue.json')).read_bytes());assert q['Active']==0 and q['Maximum']==1
    if case=='lease-exit':assert q['Disposed'] and q['ActualRuntimeFreed']
    else:assert q['Waiting']==0
    if case=='lease-manual':assert 'queued manual publication immediately equals existing synchronous Core effects without reloading or repairing state' in json.loads((folder/(mode+'-checks.json')).read_bytes())
    queues[case+'/'+mode]=q
  comparisons[case]=pairs(folder,n);numeric[case]=pose_pairs(folder,n);collect(folder,proofs);print('PASS current world/image/numeric',case,comparisons[case],flush=True)
 for p in [OUT/'build.log',OUT/'ownership.log']:proofs[p.relative_to(ROOT).as_posix()]=sha(p)
 binaries={p.relative_to(ROOT).as_posix():sha(p) for p in (ROOT/'Game/.godot/mono/temp/bin/Debug').glob('HakutaiKagami*.dll')}
 assert sources()==before and sum(comparisons.values())==118 and sum(numeric.values())==118
 write(OUT/'run-evidence.json',dict(sources=before,binaries=binaries,proofs=proofs,comparisons=comparisons,numeric_pose_comparisons=numeric,queue_observations=queues,tests=2076,added=0,core_inputs=306,core_tests_reused_exact=True,gpu_processes=42,normal_shutdown_release_markers=42,new_evidence_checkpoints=8,new_world_image_pairs=16,extra_actual_runtime_exit_checks=2,limits=['Per-runtime tracked serial private preparation and accepted atomic save writer;42currentGPU118world/image/numeric pairs.','Four added lease cases include two additional real runtime Node exit checks within two processes;process and runtime counts differ.','2076Core reused only306exact inputs/TRX/matching Core DLL;no Core/schema/renderer changes. Prior8/40/legacy9not rerun.','Maximum active lease1 observed through held abandoned old clock plus immediate menu return to clock/manual/save;queued canceled work cannot run,exited runtime waits all tracked workers before disposing.','1000stationary synthetic people plus selected actor,not national simultaneous autonomous load/fps guarantee. Ordinary command UI connection remains next work.']))
 print('PASS automatic42GPU;direct actual image review required',flush=True)
def finalize():
 j=json.loads((OUT/'run-evidence.json').read_bytes());assert j['sources']==sources()
 for kind in ['binaries','proofs']:
  for p,h in j[kind].items():assert sha(ROOT/p)==h,p
 review=json.loads((OUT/'visual-review.json').read_bytes());assert review['method'] and review['inspected']
 for p,h in review['inspected'].items():assert j['proofs'][p]==h==sha(ROOT/p),p
 j.update(status='PASSED',completion='5/5',visual_review=review);j['proofs'][(OUT/'visual-review.json').relative_to(ROOT).as_posix()]=sha(OUT/'visual-review.json');write(BASE/'verification.json',j);print('PASS5/5 serial preparation42currentGPU')
if __name__=='__main__':
 if '--finalize' in sys.argv:finalize()
 else:main()
