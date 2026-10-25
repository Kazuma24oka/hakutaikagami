from pathlib import Path
import json,uuid,sys,xml.etree.ElementTree as ET
from verify_manual_advance import ROOT,EXE,run as parent_run,pairs,pose_pairs,sha,write,collect
from verify_historical_start_edition import sources as parent_sources
BASE=ROOT/'Docs/Design/HistoricalHouseholdV1';OUT=BASE/'runtime-local'
def sources():
 j=parent_sources()
 for p in [Path(__file__),ROOT/'Scripts/check_historical_household_binding.py',BASE/'README.md',BASE/'contract.json',BASE/'source-manifest.json']:j[p.relative_to(ROOT).as_posix()]=sha(p)
 return j
def run(args,log,marker=None):
 parent_run(args,log,marker);lines=log.read_bytes().splitlines();case=next((str(a).split('=',1)[1] for a in args if str(a).startswith('--new-game-case=')),None)
 assert [line for line in lines if line.startswith(b'DEPARTURE_UI_FAILURE ')]==([b'DEPARTURE_UI_FAILURE IOException'] if case=='depart-fault' else []),str(log)
def main():
 OUT.mkdir(parents=True,exist_ok=True);before=sources();proofs={};comparisons={};numeric={};observations={}
 previous=ROOT/'Docs/Design/HistoricalStartEditionV1/verification.json';old=json.loads(previous.read_bytes());allowed=set(json.loads((BASE/'contract.json').read_bytes())['allowed_source_changes']);delta={p for p in set(before)|set(old['sources']) if before.get(p)!=old['sources'].get(p)};assert delta<=allowed,sorted(delta-allowed)
 proofs[previous.relative_to(ROOT).as_posix()]=sha(previous)
 run(['dotnet','build','Game/HakutaiKagami.Game.csproj','--no-restore'],OUT/'build.log')
 inputs={p:h for p,h in before.items() if p.startswith(('Core/','Tests/','Game/Data/')) or p=='Scripts/current_test_contract.json'}
 from verify_service_entry import run as full_run
 trx=OUT/'tests/all.trx';full_run(['dotnet','test','Tests/HakutaiKagami.Tests.csproj','--no-restore','--logger','trx;LogFileName=all.trx','--results-directory',OUT/'tests'],OUT/'tests.log',timeout=5400)
 c=ET.parse(trx).find('.//{*}Counters').attrib;assert c['passed']=='2104' and c['failed']=='0' and c['notExecuted']=='0'
 core=sha(ROOT/'Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Core.dll');assert core==sha(ROOT/'Tests/bin/Debug/net8.0/HakutaiKagami.Core.dll')
 assert len(inputs)==314,len(inputs)
 write(OUT/'test-manifest.json',dict(sources=inputs,trx=sha(trx),core=core,tests=2104,added=11,reused=False))
 for p in [trx,OUT/'test-manifest.json',OUT/'tests.log']:proofs[p.relative_to(ROOT).as_posix()]=sha(p)
 run([sys.executable,ROOT/'Scripts/check_session_generations.py'],OUT/'ownership.log','SESSION_GENERATION_OWNERSHIP_PASSED')
 cases=[]
 for case,n in [('household-retainer',6),('household-west-v2',3)]:cases.append((case,OUT/case,n,'HISTORICAL_HOUSEHOLD_PASSED'))
 for case in ['edition-otomo','edition-shimazu','edition-legacy']:cases.append((case,ROOT/'Docs/Design/HistoricalStartEditionV1/runtime-local'/case,3,'HISTORICAL_START_EDITION_PASSED'))
 for case in ['group','historical','individual','collision','minor']:cases.append((case,ROOT/'Docs/Design/NewGameFlowV1/runtime-local'/case,4,'NEW_GAME_FLOW_PASSED'))
 for case in ['service-ronin','service-retainer','service-odawara']:cases.append((case,ROOT/'Docs/Design/ServiceEntryV1/runtime-local'/case,6,'SERVICE_ENTRY_PASSED'))
 cases.extend([('military-retainer',ROOT/'Docs/Design/NewGameMilitaryV1/runtime-local/military-retainer',17,'NEW_MILITARY_PASSED'),('withdrawal-retainer',ROOT/'Docs/Design/PersonalWithdrawalV1/runtime-local/withdrawal-retainer',12,'PERSONAL_WITHDRAWAL_PASSED'),('capacity-1000',ROOT/'Docs/Design/PlayableLoopV1/runtime-local/capacity-1000',4,'NEW_GAME_CAPACITY_PASSED'),('local-normal',ROOT/'Docs/Design/ResponsiveLocalIntentV1/runtime-local/local-normal',2,'RESPONSIVE_LOCAL_INTENT_PASSED'),('depart-auto',ROOT/'Docs/Design/ResponsiveDepartureV1/runtime-local/depart-auto',2,'RESPONSIVE_DEPARTURE_PASSED'),('lease-exit',ROOT/'Docs/Design/PreparationQueueV1/runtime-local/lease-exit',2,'PREPARATION_QUEUE_PASSED')])
 assert len(cases)==19
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
 assert sources()==before and sum(comparisons.values())==190 and sum(numeric.values())==190
 write(OUT/'run-evidence.json',dict(sources=before,binaries=binaries,proofs=proofs,comparisons=comparisons,numeric_pose_comparisons=numeric,observations=observations,tests=2104,added=11,core_inputs=314,core_tests_reused_exact=False,gpu_processes=38,normal_shutdown_release_markers=38,new_evidence_checkpoints=9,new_world_image_pairs=18,limits=['Null original edition remains3people;explicit west-v2 remains5people with frozen real profile initial hashes;new household-v3 alone has6people.','38currentGPU190world/image/numeric pairs plus fresh2104tests(11new),314inputs. Previous9legacy/renderer not rerun.','Nobushige name and service relationship sourced from NaganoCity explanatory page;Kofu placement/direct lord/house retainer are trial initialization,not exact1560Jan1presence,individualfollowing or military office evidence.','Historical retainer work uses existing Submit/delivery/open/report/release/travel. Membership alone grants no office,troops,supplies or armycommand.','1000stationary registered people plus selected actor,not national autonomous load/fps/completion guarantee.']))
 print('PASS automatic38GPU;direct actual image review required',flush=True)
def finalize():
 j=json.loads((OUT/'run-evidence.json').read_bytes());assert j['sources']==sources()
 for kind in ['binaries','proofs']:
  for p,h in j[kind].items():assert sha(ROOT/p)==h,p
 review=json.loads((OUT/'visual-review.json').read_bytes());assert review['method'] and review['inspected']
 for p,h in review['inspected'].items():assert j['proofs'][p]==h==sha(ROOT/p),p
 j.update(status='PASSED',completion='5/5',visual_review=review);j['proofs'][(OUT/'visual-review.json').relative_to(ROOT).as_posix()]=sha(OUT/'visual-review.json');write(BASE/'verification.json',j);print('PASS5/5 responsive historical household38currentGPU')
if __name__=='__main__':
 if '--finalize' in sys.argv:finalize()
 else:main()
