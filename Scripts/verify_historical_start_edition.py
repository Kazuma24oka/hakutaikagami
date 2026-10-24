from pathlib import Path
import json,uuid,sys,xml.etree.ElementTree as ET
from verify_manual_advance import ROOT,EXE,run as parent_run,pairs,pose_pairs,sha,write,collect
from verify_responsive_local_intent import sources as parent_sources
BASE=ROOT/'Docs/Design/HistoricalStartEditionV1';OUT=BASE/'runtime-local'
def sources():
 j=parent_sources()
 for p in [Path(__file__),ROOT/'Scripts/check_historical_start_edition_binding.py',BASE/'README.md',BASE/'contract.json',BASE/'source-manifest.json']:j[p.relative_to(ROOT).as_posix()]=sha(p)
 return j
def run(args,log,marker=None):
 parent_run(args,log,marker);lines=log.read_bytes().splitlines();case=next((str(a).split('=',1)[1] for a in args if str(a).startswith('--new-game-case=')),None)
 assert [line for line in lines if line.startswith(b'DEPARTURE_UI_FAILURE ')]==([b'DEPARTURE_UI_FAILURE IOException'] if case=='depart-fault' else []),str(log)
def main():
 OUT.mkdir(parents=True,exist_ok=True);before=sources();proofs={};comparisons={};numeric={};observations={}
 previous=ROOT/'Docs/Design/ResponsiveLocalIntentV1/verification.json';old=json.loads(previous.read_bytes());allowed=set(json.loads((BASE/'contract.json').read_bytes())['allowed_source_changes']);delta={p for p in set(before)|set(old['sources']) if before.get(p)!=old['sources'].get(p)};assert delta<=allowed,sorted(delta-allowed)
 proofs[previous.relative_to(ROOT).as_posix()]=sha(previous)
 run(['dotnet','build','Game/HakutaiKagami.Game.csproj','--no-restore'],OUT/'build.log')
 inputs={p:h for p,h in before.items() if p.startswith(('Core/','Tests/','Game/Data/')) or p=='Scripts/current_test_contract.json'}
 from verify_service_entry import run as full_run
 trx=OUT/'tests/all.trx';full_run(['dotnet','test','Tests/HakutaiKagami.Tests.csproj','--no-restore','--logger','trx;LogFileName=all.trx','--results-directory',OUT/'tests'],OUT/'tests.log',timeout=5400)
 c=ET.parse(trx).find('.//{*}Counters').attrib;assert c['passed']=='2093' and c['failed']=='0' and c['notExecuted']=='0'
 core=sha(ROOT/'Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Core.dll');assert core==sha(ROOT/'Tests/bin/Debug/net8.0/HakutaiKagami.Core.dll')
 assert len(inputs)==310,len(inputs)
 write(OUT/'test-manifest.json',dict(sources=inputs,trx=sha(trx),core=core,tests=2093,added=17,reused=False))
 for p in [trx,OUT/'test-manifest.json',OUT/'tests.log']:proofs[p.relative_to(ROOT).as_posix()]=sha(p)
 run([sys.executable,ROOT/'Scripts/check_session_generations.py'],OUT/'ownership.log','SESSION_GENERATION_OWNERSHIP_PASSED')
 cases=[]
 for case in ['edition-otomo','edition-shimazu','edition-legacy']:cases.append((case,OUT/case,3,'HISTORICAL_START_EDITION_PASSED'))
 for case in ['group','historical','individual','collision','minor']:cases.append((case,ROOT/'Docs/Design/NewGameFlowV1/runtime-local'/case,4,'NEW_GAME_FLOW_PASSED'))
 for case in ['service-ronin','service-retainer','service-odawara']:cases.append((case,ROOT/'Docs/Design/ServiceEntryV1/runtime-local'/case,6,'SERVICE_ENTRY_PASSED'))
 cases.extend([('military-retainer',ROOT/'Docs/Design/NewGameMilitaryV1/runtime-local/military-retainer',17,'NEW_MILITARY_PASSED'),('withdrawal-retainer',ROOT/'Docs/Design/PersonalWithdrawalV1/runtime-local/withdrawal-retainer',12,'PERSONAL_WITHDRAWAL_PASSED'),('capacity-1000',ROOT/'Docs/Design/PlayableLoopV1/runtime-local/capacity-1000',4,'NEW_GAME_CAPACITY_PASSED'),('local-normal',ROOT/'Docs/Design/ResponsiveLocalIntentV1/runtime-local/local-normal',2,'RESPONSIVE_LOCAL_INTENT_PASSED'),('depart-auto',ROOT/'Docs/Design/ResponsiveDepartureV1/runtime-local/depart-auto',2,'RESPONSIVE_DEPARTURE_PASSED'),('lease-exit',ROOT/'Docs/Design/PreparationQueueV1/runtime-local/lease-exit',2,'PREPARATION_QUEUE_PASSED')])
 assert len(cases)==17
 for case,folder,n,marker in cases:
  key=uuid.uuid4().hex
  for mode in ['check','restore']:
   run([EXE,'--path',ROOT/'Game','--resolution','1280x900','--','--new-game-test-key='+key,'--new-game-case='+case]+(['--new-game-restore'] if mode=='restore' else [])+(['--play-guide-check'] if case in ['group','historical'] else []),folder/(mode+'.log'),marker)
  comparisons[case]=pairs(folder,n);numeric[case]=pose_pairs(folder,n);collect(folder,proofs)
  if case in ['depart-normal','depart-cancel','depart-fault']:
   obs=json.loads((folder/'check-departure.json').read_bytes());assert obs['RenderTicks']>0 and obs['MaximumPreparation']==1;observations[case]=obs
  if case.startswith('local-') and case!='local-menu':
   obs=json.loads((folder/'check-local.json').read_bytes());assert obs['RenderFrames']>0;observations[case]=obs
  print('PASS current world/image/numeric',case,comparisons[case],flush=True)
 for p in [OUT/'build.log',OUT/'ownership.log']:proofs[p.relative_to(ROOT).as_posix()]=sha(p)
 binaries={p.relative_to(ROOT).as_posix():sha(p) for p in (ROOT/'Game/.godot/mono/temp/bin/Debug').glob('HakutaiKagami*.dll')}
 assert sources()==before and sum(comparisons.values())==172 and sum(numeric.values())==172
 write(OUT/'run-evidence.json',dict(sources=before,binaries=binaries,proofs=proofs,comparisons=comparisons,numeric_pose_comparisons=numeric,observations=observations,tests=2093,added=17,core_inputs=310,core_tests_reused_exact=False,gpu_processes=34,normal_shutdown_release_markers=34,new_evidence_checkpoints=9,new_world_image_pairs=18,limits=['Original null edition stays three people with unchanged initial hash. New explicit edition adds two sourced western candidates;no silent roster replacement or unknown-version fallback.','34currentGPU172world-image/numeric pairs plus full2093tests(17new),310inputs. Previous52local/GPU matrix and old9legacy/renderer not rerun.','Western candidate/1560 naming and official sources are separate from declared trial initial head/one-site rights;no whole province ownership or exact Jan1location inferred.','Historical registry is scenario authoring reference,not hidden actor knowledge. Default abilities remain unrecorded.','1000stationary registered people plus selected actor,not national autonomous load/fps/completion guarantee.','New start/profile markers are explicit optional closed JSON fields;old omitted fields stay omitted. Old real profile golden hash is tested;existing payload replay and protected generations remain intact.']))
 print('PASS automatic34GPU;direct actual image review required',flush=True)
def finalize():
 j=json.loads((OUT/'run-evidence.json').read_bytes());assert j['sources']==sources()
 for kind in ['binaries','proofs']:
  for p,h in j[kind].items():assert sha(ROOT/p)==h,p
 review=json.loads((OUT/'visual-review.json').read_bytes());assert review['method'] and review['inspected']
 for p,h in review['inspected'].items():assert j['proofs'][p]==h==sha(ROOT/p),p
 j.update(status='PASSED',completion='5/5',visual_review=review);j['proofs'][(OUT/'visual-review.json').relative_to(ROOT).as_posix()]=sha(OUT/'visual-review.json');write(BASE/'verification.json',j);print('PASS5/5 responsive historical start34currentGPU')
if __name__=='__main__':
 if '--finalize' in sys.argv:finalize()
 else:main()
