from pathlib import Path
import json,uuid,sys,xml.etree.ElementTree as ET
from verify_manual_advance import ROOT,EXE,run,pairs,pose_pairs,sha,write,collect,sources as parent_sources
BASE=ROOT/'Docs/Design/GuideReuseV1';OUT=BASE/'runtime-local'
def sources():
 j=parent_sources()
 for p in [Path(__file__),ROOT/'Scripts/check_guide_reuse_binding.py',BASE/'README.md',BASE/'contract.json']:j[p.relative_to(ROOT).as_posix()]=sha(p)
 return j
def main():
 OUT.mkdir(parents=True,exist_ok=True);before=sources();proofs={};comparisons={};numeric={}
 previous=ROOT/'Docs/Design/ManualAdvanceV1/verification.json';old=json.loads(previous.read_bytes());allowed=set(json.loads((BASE/'contract.json').read_bytes())['allowed_source_changes']);delta={p for p in set(before)|set(old['sources']) if before.get(p)!=old['sources'].get(p)};assert delta<=allowed,sorted(delta-allowed)
 proofs[previous.relative_to(ROOT).as_posix()]=sha(previous)
 run(['dotnet','build','Game/HakutaiKagami.Game.csproj','--no-restore'],OUT/'build.log')
 inputs={p:h for p,h in before.items() if p.startswith(('Core/','Tests/','Game/Data/')) or p=='Scripts/current_test_contract.json'}
 manifest=ROOT/'Docs/Design/ManualAdvanceV1/runtime-local/test-manifest.json';m=json.loads(manifest.read_bytes());trx=ROOT/'Docs/Design/ResponsiveClockV1/runtime-local/tests/all.trx'
 assert inputs==m['sources'] and len(inputs)==304 and sha(trx)==m['trx'] and sha(ROOT/'Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Core.dll')==m['core']==sha(ROOT/'Tests/bin/Debug/net8.0/HakutaiKagami.Core.dll')
 c=ET.parse(trx).find('.//{*}Counters').attrib;assert c['passed']=='2055' and c['failed']=='0' and c['notExecuted']=='0'
 write(OUT/'test-manifest.json',dict(sources=inputs,trx=sha(trx),core=m['core'],reused_from=manifest.relative_to(ROOT).as_posix()))
 for p in [manifest,trx,OUT/'test-manifest.json']:proofs[p.relative_to(ROOT).as_posix()]=sha(p)
 run([sys.executable,ROOT/'Scripts/check_session_generations.py'],OUT/'ownership.log','SESSION_GENERATION_OWNERSHIP_PASSED')
 for case,folder,n,marker in [('manual-cancel',ROOT/'Docs/Design/ManualAdvanceV1/runtime-local/manual-cancel',2,'MANUAL_TIME_PASSED'),('manual-menu',ROOT/'Docs/Design/ManualAdvanceV1/runtime-local/manual-menu',2,'MANUAL_TIME_PASSED'),('clock-cancel',ROOT/'Docs/Design/ResponsiveClockV1/runtime-local/clock-cancel',2,'RESPONSIVE_CLOCK_PASSED'),('group',ROOT/'Docs/Design/NewGameFlowV1/runtime-local/group',4,'NEW_GAME_FLOW_PASSED')]:
  key=uuid.uuid4().hex
  for mode in ['check','restore']:
   run([EXE,'--path',ROOT/'Game','--resolution','1280x900','--','--new-game-test-key='+key,'--new-game-case='+case]+(['--new-game-restore'] if mode=='restore' else [])+(['--play-guide-check'] if case=='group' else []),folder/(mode+'.log'),marker)
   checks=json.loads((folder/(mode+'-checks.json')).read_bytes())
   if case.startswith('manual-'):assert 'previously created guide close stays enabled during manual wait' in checks
   elif case=='clock-cancel':assert 'previously created guide close stays enabled during clock wait' in checks
   else:assert sum('guide open/read/close preserves exact world' in str(s) for s in checks)==2
  comparisons[case]=pairs(folder,n);numeric[case]=pose_pairs(folder,n);collect(folder,proofs);print('PASS repeated guide world/image/numeric',case,comparisons[case],flush=True)
 for p in [OUT/'build.log',OUT/'ownership.log']:proofs[p.relative_to(ROOT).as_posix()]=sha(p)
 binaries={p.relative_to(ROOT).as_posix():sha(p) for p in (ROOT/'Game/.godot/mono/temp/bin/Debug').glob('HakutaiKagami*.dll')}
 assert sources()==before and sum(comparisons.values())==20 and sum(numeric.values())==20
 write(OUT/'run-evidence.json',dict(sources=before,binaries=binaries,proofs=proofs,comparisons=comparisons,numeric_pose_comparisons=numeric,tests=2055,added=0,core_inputs=304,core_tests_reused_exact=True,gpu_processes=8,normal_shutdown_release_markers=8,limits=['QA-only close-button enabled guard and existing-guide repeat scenario. Production operation,world,render and Core unchanged.','8currentGPU20world/image/numeric pairs. Prior40historical,not rerun.','2055Core reused only304exact source/TRX/DLL.1000stationary people plus selected travel,not national actor/fps acceptance.']))
 print('PASS automatic8GPU;direct actual dialog images required',flush=True)
def finalize():
 j=json.loads((OUT/'run-evidence.json').read_bytes());assert j['sources']==sources()
 for kind in ['binaries','proofs']:
  for p,h in j[kind].items():assert sha(ROOT/p)==h,p
 review=json.loads((OUT/'visual-review.json').read_bytes());assert review['method'] and review['inspected']
 for p,h in review['inspected'].items():assert j['proofs'][p]==h==sha(ROOT/p),p
 j.update(status='PASSED',completion='5/5',visual_review=review);j['proofs'][(OUT/'visual-review.json').relative_to(ROOT).as_posix()]=sha(OUT/'visual-review.json');write(BASE/'verification.json',j);print('PASS5/5 guide reuse8currentGPU')
if __name__=='__main__':
 if '--finalize' in sys.argv:finalize()
 else:main()
