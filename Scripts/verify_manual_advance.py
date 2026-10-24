from pathlib import Path
import json,uuid,sys,xml.etree.ElementTree as ET
from verify_batched_advance import ROOT,EXE,pairs,pose_pairs,sha,write,collect
from verify_service_entry import run as base_run
from verify_terrain_cache import sources as parent_sources
BASE=ROOT/'Docs/Design/ManualAdvanceV1';OUT=BASE/'runtime-local'
def sources():
 j=parent_sources()
 for p in [Path(__file__),ROOT/'Scripts/check_manual_advance_binding.py',BASE/'README.md',BASE/'contract.json']:j[p.relative_to(ROOT).as_posix()]=sha(p)
 return j
def run(args,log,marker=None):
 base_run(args,log,marker,timeout=1200)
 lines=log.read_bytes().splitlines();assert not any(b'RUNTIME_REJECT' in line for line in lines),str(log)
 cases={str(a).split('=',1)[1] for a in args if str(a).startswith('--new-game-case=')}
 mapping={b'MANUAL_ADVANCE_UI_FAILURE':{'manual-fault'},b'ADVANCE_UI_FAILURE':{'prefetch-fault'},b'CLOCK_UI_FAILURE':{'clock-fault'},b'SAVE_UI_FAILURE':{'savecapture-prepare-fault','savecapture-write-fault','saveui-failure'}}
 for prefix,faultcases in mapping.items():
  observed=[line for line in lines if line.startswith(prefix+b' ')];expected=[prefix+b' IOException'] if cases&faultcases else []
  assert observed==expected,(str(log),prefix,observed,expected)
 if cases:assert lines.count(b'GAME_PROCESS_EXIT_RELEASED')==1,str(log)
def main():
 OUT.mkdir(parents=True,exist_ok=True);before=sources();proofs={};comparison={};numeric={};observations={}
 previous=ROOT/'Docs/Design/TerrainCacheV1/verification.json';old=json.loads(previous.read_bytes());allowed=set(json.loads((BASE/'contract.json').read_bytes())['allowed_source_changes']);delta={p for p in set(before)|set(old['sources']) if before.get(p)!=old['sources'].get(p)};assert delta<=allowed,sorted(delta-allowed)
 proofs[previous.relative_to(ROOT).as_posix()]=sha(previous)
 run(['dotnet','build','Game/HakutaiKagami.Game.csproj','--no-restore'],OUT/'build.log')
 core_inputs={p:h for p,h in before.items() if p.startswith(('Core/','Tests/','Game/Data/')) or p=='Scripts/current_test_contract.json'}
 manifest=ROOT/'Docs/Design/TerrainCacheV1/runtime-local/test-manifest.json';m=json.loads(manifest.read_bytes());trx=ROOT/'Docs/Design/ResponsiveClockV1/runtime-local/tests/all.trx'
 assert core_inputs==m['sources'] and len(core_inputs)==304 and sha(trx)==m['trx'] and sha(ROOT/'Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Core.dll')==m['core']==sha(ROOT/'Tests/bin/Debug/net8.0/HakutaiKagami.Core.dll')
 c=ET.parse(trx).find('.//{*}Counters').attrib;assert c['passed']=='2055' and c['failed']=='0' and c['notExecuted']=='0'
 write(OUT/'test-manifest.json',dict(sources=core_inputs,trx=sha(trx),core=m['core'],reused_from=manifest.relative_to(ROOT).as_posix()))
 for p in [manifest,trx,OUT/'test-manifest.json']:proofs[p.relative_to(ROOT).as_posix()]=sha(p)
 run([sys.executable,ROOT/'Scripts/check_session_generations.py'],OUT/'ownership.log','SESSION_GENERATION_OWNERSHIP_PASSED')
 cases=[]
 for case in ['manual-ten','manual-arrive','manual-notice','manual-cancel','manual-menu','manual-fault','manual-auto']:cases.append((case,OUT/case,2,'MANUAL_TIME_PASSED'))
 for case in ['batch-switch','batch-save','batch-notice']:cases.append((case,ROOT/'Docs/Design/BatchedAdvanceV1/runtime-local'/case,2,'BATCHED_ADVANCE_PASSED'))
 for case in ['clock-1000','clock-cancel','clock-fault']:cases.append((case,ROOT/'Docs/Design/ResponsiveClockV1/runtime-local'/case,3 if case=='clock-1000' else 2,'RESPONSIVE_CLOCK_PASSED'))
 cases.extend([('savecapture-normal',ROOT/'Docs/Design/SaveCaptureV1/runtime-local/savecapture-normal',3,'SAVE_CAPTURE_PASSED'),('prefetch-save',ROOT/'Docs/Design/PreparedAdvanceV1/runtime-local/prefetch-save',2,'PREPARED_ADVANCE_PASSED')])
 for case in ['group','historical']:cases.append((case,ROOT/'Docs/Design/NewGameFlowV1/runtime-local'/case,4,'NEW_GAME_FLOW_PASSED'))
 for case in ['service-ronin','service-retainer','service-odawara']:cases.append((case,ROOT/'Docs/Design/ServiceEntryV1/runtime-local'/case,6,'SERVICE_ENTRY_PASSED'))
 assert len(cases)==20
 for case,folder,n,marker in cases:
  key=uuid.uuid4().hex
  for mode in ['check','restore']:
   args=[EXE,'--path',ROOT/'Game','--resolution','1280x900','--','--new-game-test-key='+key,'--new-game-case='+case]+(['--new-game-restore'] if mode=='restore' else [])+(['--play-guide-check'] if case in ['group','historical'] else [])
   run(args,folder/(mode+'.log'),marker)
  comparison[case]=pairs(folder,n);numeric[case]=pose_pairs(folder,n);collect(folder,proofs)
  if case in ['manual-ten','manual-arrive','manual-notice']:
   observations[case]=json.loads((folder/'check-action.json').read_bytes());assert observations[case]['RenderTicks']>0
  print('PASS current world/image/numeric',case,comparison[case],flush=True)
 proofs[(OUT/'build.log').relative_to(ROOT).as_posix()]=sha(OUT/'build.log');proofs[(OUT/'ownership.log').relative_to(ROOT).as_posix()]=sha(OUT/'ownership.log')
 for p in (OUT/'corrections').rglob('*'):
  if p.is_file():proofs[p.relative_to(ROOT).as_posix()]=sha(p)
 binaries={p.relative_to(ROOT).as_posix():sha(p) for p in (ROOT/'Game/.godot/mono/temp/bin/Debug').glob('HakutaiKagami*.dll')}
 assert sources()==before and sum(comparison.values())==116 and sum(numeric.values())==116
 write(OUT/'run-evidence.json',dict(sources=before,binaries=binaries,proofs=proofs,comparisons=comparison,numeric_pose_comparisons=numeric,observations=observations,tests=2055,added=0,core_inputs=304,core_tests_reused_exact=True,gpu_processes=40,normal_shutdown_release_markers=40,new_evidence_checkpoints=14,new_world_image_pairs=28,limits=['40currentGPU selected for changed new-game input/time/arrive path;previous62/24historical,not current reruns.9shared-renderer/legacy processes not rerun;those inputs unchanged.','2055Core tests reused only304 exact unchanged sources,TRX,Core DLL. No Core/schema/renderer change.','1000stationary synthetic people plus selected travel;not national autonomous load,fps or completion-time guarantee.','Explicit-time click returns before confirmation;private pause/advance worlds never visible or saved before accepted same-generation publication.','Pending explicit time locks save/world commands;pause/menu/camera/guide allowed. Canceled worker is reaped before next private clock/advance/save worker. Important arrival stops earlier than requested target,opening separate.','Synchronous internal QA advance helper remains separate. Ordinary decisions,initial/load/menu pause and content/load work remain.']))
 print('PASS automatic40GPU;direct real-image review required',flush=True)
def finalize():
 j=json.loads((OUT/'run-evidence.json').read_bytes());assert j['sources']==sources()
 for kind in ['binaries','proofs']:
  for p,h in j[kind].items():assert sha(ROOT/p)==h,p
 review=json.loads((OUT/'visual-review.json').read_bytes());assert review['method'] and review['inspected']
 for p,h in review['inspected'].items():assert j['proofs'][p]==h==sha(ROOT/p),p
 j.update(status='PASSED',completion='5/5',visual_review=review);j['proofs'][(OUT/'visual-review.json').relative_to(ROOT).as_posix()]=sha(OUT/'visual-review.json');write(BASE/'verification.json',j);print('PASS5/5 manual-time40currentGPU')
if __name__=='__main__':
 if '--finalize' in sys.argv:finalize()
 else:main()
