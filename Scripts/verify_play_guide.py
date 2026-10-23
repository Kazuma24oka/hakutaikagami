from pathlib import Path
import json,uuid,sys,xml.etree.ElementTree as ET
from PIL import Image
from verify_prepared_advance import sources as parent_sources,ROOT,EXE,sha,write,run,pairs,pose_pairs,collect
BASE=ROOT/'Docs/Design/PlayGuideV1';OUT=BASE/'runtime-local'
def sources():
 j=parent_sources()
 for p in [Path(__file__),ROOT/'Scripts/check_play_guide_binding.py',BASE/'contract.json']:j[p.relative_to(ROOT).as_posix()]=sha(p)
 return j
def core_sources(j):return {p:h for p,h in j.items() if p.startswith(('Core/','Tests/','Game/Data/')) or p=='Scripts/current_test_contract.json'}
def main():
 OUT.mkdir(parents=True,exist_ok=True);before=sources();proofs={};comparisons={};numeric={}
 prior=ROOT/'Docs/Design/PreparedAdvanceV1/runtime-local/test-manifest.json';m=json.loads(prior.read_bytes());assert core_sources(before)==m['sources'] and len(m['sources'])==302
 trx=ROOT/'Docs/Design/PreparedAdvanceV1/runtime-local/tests/all.trx';c=ET.parse(trx).find('.//{*}Counters').attrib;assert c['passed']=='2040' and c['failed']=='0' and c['notExecuted']=='0' and sha(trx)==m['trx']
 run(['dotnet','build','Game/HakutaiKagami.Game.csproj','--no-restore'],OUT/'build.log')
 core=ROOT/'Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Core.dll';assert sha(core)==m['core']==sha(ROOT/'Tests/bin/Debug/net8.0/HakutaiKagami.Core.dll')
 for case in ['group','historical','prefetch-notice']:
  folder=ROOT/('Docs/Design/PreparedAdvanceV1/runtime-local' if case.startswith('prefetch-') else 'Docs/Design/NewGameFlowV1/runtime-local')/case;key=uuid.uuid4().hex
  for mode in ['check','restore']:
   run([EXE,'--path',ROOT/'Game','--resolution','1280x900','--','--new-game-test-key='+key,'--new-game-case='+case,'--play-guide-check']+(['--new-game-restore'] if mode=='restore' else []),folder/(mode+'.log'),'PREPARED_ADVANCE_PASSED' if case.startswith('prefetch-') else 'NEW_GAME_FLOW_PASSED')
   checks=json.loads((folder/(mode+'-checks.json')).read_bytes());assert sum('guide open/read/close preserves exact world' in str(x) for x in checks)==2,(case,mode,'actual guide checks missing')
  count=2 if case.startswith('prefetch-') else 4;comparisons[case]=pairs(folder,count);numeric[case]=pose_pairs(folder,count);collect(folder,proofs)
  assert Image.open(folder/'play-guide.png').size==(1280,900)
  print('PASS guide and exact restore',case,comparisons[case],flush=True)
 for p in [prior,trx,OUT/'build.log']:proofs[p.relative_to(ROOT).as_posix()]=sha(p)
 binaries={p.relative_to(ROOT).as_posix():sha(p) for p in (ROOT/'Game/.godot/mono/temp/bin/Debug').glob('HakutaiKagami*.dll')}
 assert sources()==before and sum(comparisons.values())==20 and sum(numeric.values())==20
 write(OUT/'run-evidence.json',dict(sources=before,binaries=binaries,proofs=proofs,comparisons=comparisons,numeric_pose_comparisons=numeric,gpu_processes=6,guide_open_close_checks=12,tests=2040,core_tests_reused_exact=True,core_inputs=302,limits=['View-only UI addition. Only these six GPU processes rerun; prior full matrix is historical.','Exact unchanged302 Core/test/data inputs and2040-test TRX/Core DLL reused,not newly rerun.','Not a tutorial mission or full national NPC content. Start/pause clicks and faster modes remain synchronous.']))
 print('PASS automatic guide checks; direct image review required before finalize',flush=True)
def finalize():
 j=json.loads((OUT/'run-evidence.json').read_bytes());assert j['sources']==sources()
 for kind in ['binaries','proofs']:
  for p,h in j[kind].items():assert sha(ROOT/p)==h,p
 review=json.loads((OUT/'visual-review.json').read_bytes());assert review['method'] and len(review['inspected'])>=3
 for p,h in review['inspected'].items():assert j['proofs'][p]==h==sha(ROOT/p),p
 j.update(status='PASSED',completion='5/5',visual_review=review);j['proofs'][(OUT/'visual-review.json').relative_to(ROOT).as_posix()]=sha(OUT/'visual-review.json');write(BASE/'verification.json',j)
 print('PASS5/5 view-only play guide and current actual restoration binding')
if __name__=='__main__':
 if '--finalize' in sys.argv:finalize()
 else:main()
