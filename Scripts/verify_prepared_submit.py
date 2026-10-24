from pathlib import Path
import json,uuid,sys,xml.etree.ElementTree as ET
from verify_manual_advance import ROOT,EXE,run,pairs,pose_pairs,sha,write,collect
from verify_guide_reuse import sources as parent_sources
from verify_service_entry import run as long_run
BASE=ROOT/'Docs/Design/PreparedSubmitV1';OUT=BASE/'runtime-local'
def sources():
 j=parent_sources()
 for p in [Path(__file__),ROOT/'Scripts/check_prepared_submit_binding.py',BASE/'README.md',BASE/'contract.json']:j[p.relative_to(ROOT).as_posix()]=sha(p)
 return j
def main():
 OUT.mkdir(parents=True,exist_ok=True);before=sources();proofs={};comparisons={};numeric={}
 previous=ROOT/'Docs/Design/GuideReuseV1/verification.json';old=json.loads(previous.read_bytes());allowed=set(json.loads((BASE/'contract.json').read_bytes())['allowed_source_changes']);delta={p for p in set(before)|set(old['sources']) if before.get(p)!=old['sources'].get(p)};assert delta<=allowed,sorted(delta-allowed)
 proofs[previous.relative_to(ROOT).as_posix()]=sha(previous)
 run(['dotnet','build','Game/HakutaiKagami.Game.csproj','--no-restore'],OUT/'build.log')
 long_run(['dotnet','test','Tests/HakutaiKagami.Tests.csproj','--no-restore','--logger','trx;LogFileName=all.trx','--results-directory',OUT/'tests'],OUT/'test.log',timeout=7200)
 trx=OUT/'tests/all.trx';c=ET.parse(trx).find('.//{*}Counters').attrib;assert c['passed']=='2076' and c['failed']=='0' and c['notExecuted']=='0'
 results=ET.parse(trx).findall('.//{*}UnitTestResult');assert sum('PreparedSubmitTests.' in q.attrib.get('testName','') for q in results)==21
 inputs={p:h for p,h in before.items() if p.startswith(('Core/','Tests/','Game/Data/')) or p=='Scripts/current_test_contract.json'};assert len(inputs)==306
 assert sha(ROOT/'Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Core.dll')==sha(ROOT/'Tests/bin/Debug/net8.0/HakutaiKagami.Core.dll')
 write(OUT/'test-manifest.json',dict(sources=inputs,trx=sha(trx),core=sha(ROOT/'Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Core.dll'),tests=2076,added=21,full_rerun=True))
 run([sys.executable,ROOT/'Scripts/check_session_generations.py'],OUT/'ownership.log','SESSION_GENERATION_OWNERSHIP_PASSED')
 cases=[('group',ROOT/'Docs/Design/NewGameFlowV1/runtime-local/group',4,'NEW_GAME_FLOW_PASSED'),('historical',ROOT/'Docs/Design/NewGameFlowV1/runtime-local/historical',4,'NEW_GAME_FLOW_PASSED'),('manual-ten',ROOT/'Docs/Design/ManualAdvanceV1/runtime-local/manual-ten',2,'MANUAL_TIME_PASSED'),('manual-notice',ROOT/'Docs/Design/ManualAdvanceV1/runtime-local/manual-notice',2,'MANUAL_TIME_PASSED')]
 for case,folder,n,marker in cases:
  key=uuid.uuid4().hex
  for mode in ['check','restore']:run([EXE,'--path',ROOT/'Game','--resolution','1280x900','--','--new-game-test-key='+key,'--new-game-case='+case]+(['--new-game-restore'] if mode=='restore' else [])+(['--play-guide-check'] if case in ['group','historical'] else []),folder/(mode+'.log'),marker)
  comparisons[case]=pairs(folder,n);numeric[case]=pose_pairs(folder,n);collect(folder,proofs);print('PASS current world/image/numeric',case,comparisons[case],flush=True)
 for p in [OUT/'build.log',OUT/'test.log',trx,OUT/'test-manifest.json',OUT/'ownership.log']:proofs[p.relative_to(ROOT).as_posix()]=sha(p)
 binaries={p.relative_to(ROOT).as_posix():sha(p) for p in (ROOT/'Game/.godot/mono/temp/bin/Debug').glob('HakutaiKagami*.dll')}
 assert sources()==before and sum(comparisons.values())==24 and sum(numeric.values())==24
 write(OUT/'run-evidence.json',dict(sources=before,binaries=binaries,proofs=proofs,comparisons=comparisons,numeric_pose_comparisons=numeric,tests=2076,added=21,core_inputs=306,core_tests_reused_exact=False,full_test_rerun=True,gpu_processes=8,normal_shutdown_release_markers=8,limits=['Core API foundation only;current UI ordinary commands remain synchronous pending later connection.','All2076tests rerun;no prior2055substitution.306current Core/test/data inputs bound to matching Game/Test Core DLL.','8currentGPU24world/image/numeric pairs;previous8/40historical,not rerun. Existing Core/world/save DTO and runtime operation code unchanged.','Immutable value-only Intent graph tested;private generation/results not public until exact one-shot host acceptance.','1000stationary people plus selected travel is not national autonomous load or fps acceptance.']))
 print('PASS automatic2076tests8GPU;direct image review required',flush=True)
def finalize():
 j=json.loads((OUT/'run-evidence.json').read_bytes());assert j['sources']==sources()
 for kind in ['binaries','proofs']:
  for p,h in j[kind].items():assert sha(ROOT/p)==h,p
 review=json.loads((OUT/'visual-review.json').read_bytes());assert review['method'] and review['inspected']
 for p,h in review['inspected'].items():assert j['proofs'][p]==h==sha(ROOT/p),p
 j.update(status='PASSED',completion='5/5',visual_review=review);j['proofs'][(OUT/'visual-review.json').relative_to(ROOT).as_posix()]=sha(OUT/'visual-review.json');write(BASE/'verification.json',j);print('PASS5/5 prepared Submit2076current full tests8GPU')
if __name__=='__main__':
 if '--finalize' in sys.argv:finalize()
 else:main()
