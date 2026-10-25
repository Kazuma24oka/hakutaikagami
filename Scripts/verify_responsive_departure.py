from pathlib import Path
import json,uuid,sys,xml.etree.ElementTree as ET
from verify_manual_advance import ROOT,EXE,run as parent_run,pairs,pose_pairs,sha,write,collect
from verify_preparation_queue import sources as parent_sources
BASE=ROOT/'Docs/Design/ResponsiveDepartureV1';OUT=BASE/'runtime-local'
def sources():
 j=parent_sources()
 for p in [Path(__file__),ROOT/'Scripts/check_responsive_departure_binding.py',BASE/'README.md',BASE/'contract.json']:j[p.relative_to(ROOT).as_posix()]=sha(p)
 return j
def run(args,log,marker=None):
 parent_run(args,log,marker);lines=log.read_bytes().splitlines();case=next((str(a).split('=',1)[1] for a in args if str(a).startswith('--new-game-case=')),None)
 assert [line for line in lines if line.startswith(b'DEPARTURE_UI_FAILURE ')]==([b'DEPARTURE_UI_FAILURE IOException'] if case=='depart-fault' else []),str(log)
def main():
 OUT.mkdir(parents=True,exist_ok=True);before=sources();proofs={};comparisons={};numeric={};observations={}
 previous=ROOT/'Docs/Design/PreparationQueueV1/verification.json';old=json.loads(previous.read_bytes());allowed=set(json.loads((BASE/'contract.json').read_bytes())['allowed_source_changes']);delta={p for p in set(before)|set(old['sources']) if before.get(p)!=old['sources'].get(p)};assert delta<=allowed,sorted(delta-allowed)
 proofs[previous.relative_to(ROOT).as_posix()]=sha(previous)
 run(['dotnet','build','Game/HakutaiKagami.Game.csproj','--no-restore'],OUT/'build.log')
 inputs={p:h for p,h in before.items() if p.startswith(('Core/','Tests/','Game/Data/')) or p=='Scripts/current_test_contract.json'}
 manifest=ROOT/'Docs/Design/PreparationQueueV1/runtime-local/test-manifest.json';m=json.loads(manifest.read_bytes());trx=ROOT/'Docs/Design/PreparedSubmitV1/runtime-local/tests/all.trx'
 assert inputs==m['sources'] and len(inputs)==306 and sha(trx)==m['trx'] and sha(ROOT/'Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Core.dll')==m['core']==sha(ROOT/'Tests/bin/Debug/net8.0/HakutaiKagami.Core.dll')
 c=ET.parse(trx).find('.//{*}Counters').attrib;assert c['passed']=='2076' and c['failed']=='0' and c['notExecuted']=='0'
 write(OUT/'test-manifest.json',dict(sources=inputs,trx=sha(trx),core=m['core'],tests=2076,reused_from=manifest.relative_to(ROOT).as_posix()))
 for p in [manifest,trx,OUT/'test-manifest.json']:proofs[p.relative_to(ROOT).as_posix()]=sha(p)
 run([sys.executable,ROOT/'Scripts/check_session_generations.py'],OUT/'ownership.log','SESSION_GENERATION_OWNERSHIP_PASSED')
 cases=[]
 for case in ['depart-normal','depart-cancel','depart-menu','depart-fault','depart-auto','depart-load']:cases.append((case,OUT/case,2,'RESPONSIVE_DEPARTURE_PASSED'))
 cases.append(('manual-ten',ROOT/'Docs/Design/ManualAdvanceV1/runtime-local/manual-ten',2,'MANUAL_TIME_PASSED'))
 for case in ['batch-switch','batch-save']:cases.append((case,ROOT/'Docs/Design/BatchedAdvanceV1/runtime-local'/case,2,'BATCHED_ADVANCE_PASSED'))
 cases.extend([('clock-1000',ROOT/'Docs/Design/ResponsiveClockV1/runtime-local/clock-1000',3,'RESPONSIVE_CLOCK_PASSED'),('prefetch-save',ROOT/'Docs/Design/PreparedAdvanceV1/runtime-local/prefetch-save',2,'PREPARED_ADVANCE_PASSED'),('savecapture-normal',ROOT/'Docs/Design/SaveCaptureV1/runtime-local/savecapture-normal',3,'SAVE_CAPTURE_PASSED')])
 for case in ['group','historical']:cases.append((case,ROOT/'Docs/Design/NewGameFlowV1/runtime-local'/case,4,'NEW_GAME_FLOW_PASSED'))
 for case in ['service-ronin','service-retainer','service-odawara']:cases.append((case,ROOT/'Docs/Design/ServiceEntryV1/runtime-local'/case,6,'SERVICE_ENTRY_PASSED'))
 cases.extend([('military-retainer',ROOT/'Docs/Design/NewGameMilitaryV1/runtime-local/military-retainer',17,'NEW_MILITARY_PASSED'),('withdrawal-retainer',ROOT/'Docs/Design/PersonalWithdrawalV1/runtime-local/withdrawal-retainer',12,'PERSONAL_WITHDRAWAL_PASSED')])
 for case in ['lease-clock','lease-exit']:cases.append((case,ROOT/'Docs/Design/PreparationQueueV1/runtime-local'/case,2,'PREPARATION_QUEUE_PASSED'))
 assert len(cases)==21
 for case,folder,n,marker in cases:
  key=uuid.uuid4().hex
  for mode in ['check','restore']:
   run([EXE,'--path',ROOT/'Game','--resolution','1280x900','--','--new-game-test-key='+key,'--new-game-case='+case]+(['--new-game-restore'] if mode=='restore' else [])+(['--play-guide-check'] if case in ['group','historical'] else []),folder/(mode+'.log'),marker)
  comparisons[case]=pairs(folder,n);numeric[case]=pose_pairs(folder,n);collect(folder,proofs)
  if case in ['depart-normal','depart-cancel','depart-fault']:
   obs=json.loads((folder/'check-departure.json').read_bytes());assert obs['RenderTicks']>0 and obs['MaximumPreparation']==1;observations[case]=obs
  print('PASS current world/image/numeric',case,comparisons[case],flush=True)
 for p in [OUT/'build.log',OUT/'ownership.log']:proofs[p.relative_to(ROOT).as_posix()]=sha(p)
 binaries={p.relative_to(ROOT).as_posix():sha(p) for p in (ROOT/'Game/.godot/mono/temp/bin/Debug').glob('HakutaiKagami*.dll')}
 assert sources()==before and sum(comparisons.values())==170 and sum(numeric.values())==170
 write(OUT/'run-evidence.json',dict(sources=before,binaries=binaries,proofs=proofs,comparisons=comparisons,numeric_pose_comparisons=numeric,observations=observations,tests=2076,added=0,core_inputs=306,core_tests_reused_exact=True,gpu_processes=42,normal_shutdown_release_markers=42,new_evidence_checkpoints=12,new_world_image_pairs=24,limits=['Departure only uses opaque existing Submit plus accepted-only unpause;Inspect/work remain synchronous.','42currentGPU170world/image/numeric pairs. Previous42/8and9legacy/renderer processes not rerun;Core/schema/renderer unchanged.','2076Core reused only306exact inputs/TRX/matching Core DLL.','Held private accepted departure has no visible command/world/save effects;stop/menu/load/exit discard it. Confirmed replacement generation publishes once;prior automatic preference restored.','Protected newer selected save may be loaded during older checkpoint replay;its exact validated saved world is required,not silently replaced by old source.','1000stationary registered people plus selected actor,not national autonomous load/fps/completion-time guarantee. Click and confirmation durations measured separately.','Changed existing departure QA waits actual completion and uses current session after host publication;original walking/return/withdrawal assertions remain.']))
 print('PASS automatic42GPU;direct actual image review required',flush=True)
def finalize():
 j=json.loads((OUT/'run-evidence.json').read_bytes());assert j['sources']==sources()
 for kind in ['binaries','proofs']:
  for p,h in j[kind].items():assert sha(ROOT/p)==h,p
 review=json.loads((OUT/'visual-review.json').read_bytes());assert review['method'] and review['inspected']
 for p,h in review['inspected'].items():assert j['proofs'][p]==h==sha(ROOT/p),p
 j.update(status='PASSED',completion='5/5',visual_review=review);j['proofs'][(OUT/'visual-review.json').relative_to(ROOT).as_posix()]=sha(OUT/'visual-review.json');write(BASE/'verification.json',j);print('PASS5/5 responsive departure42currentGPU')
if __name__=='__main__':
 if '--finalize' in sys.argv:finalize()
 else:main()
