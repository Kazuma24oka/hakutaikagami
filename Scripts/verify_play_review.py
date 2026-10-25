from pathlib import Path
import json,uuid,sys,shutil,xml.etree.ElementTree as ET,re
from verify_new_game_npc_routine import ROOT,EXE,run,sha,write,collect,sources as parent_sources
from verify_service_entry import pairs
from verify_batched_advance import pose_pairs
from prepare_play_review_recording import prepare
from encode_play_review import encode
BASE=ROOT/'Docs/Design/PlayReviewV1';OUT=BASE/'runtime-local'
def sources():
 j=parent_sources()
 for p in [Path(__file__),ROOT/'Scripts/check_play_review_binding.py',ROOT/'Scripts/prepare_play_review_recording.py',ROOT/'Scripts/encode_play_review.py',BASE/'README.md',BASE/'contract.json']:j[p.relative_to(ROOT).as_posix()]=sha(p)
 return j

def verify_copy():
 j=json.loads((OUT/'recording-project.json').read_bytes());record=Path(j['record_project']);assert record.resolve().is_relative_to(OUT.resolve())
 assert j['actual_project_sha256']==sha(ROOT/'Game/project.godot') and j['recording_project_sha256']==sha(record/'Game/project.godot')
 text=(ROOT/'Game/project.godot').read_bytes().decode('utf-8-sig')
 for name,value in [('viewport_width',1280),('viewport_height',900)]:text,n=re.subn(r'(?m)^window/size/'+name+r'=\d+$','window/size/'+name+'='+str(value),text);assert n==1
 assert (record/'Game/project.godot').read_bytes()==text.encode()
 for rel,h in j['copied_runtime_inputs'].items():assert sha(ROOT/'Game'/rel)==sha(record/'Game'/rel)==h,rel
 return j

def main():
 OUT.mkdir(parents=True,exist_ok=True);before=sources();proofs={};previous=ROOT/'Docs/Design/NewGameNpcRoutineV1/verification.json';old=json.loads(previous.read_bytes());allowed=set(json.loads((BASE/'contract.json').read_bytes())['allowed_source_changes']);delta={p for p in set(before)|set(old['sources']) if before.get(p)!=old['sources'].get(p)};assert delta<=allowed,sorted(delta-allowed);proofs[previous.relative_to(ROOT).as_posix()]=sha(previous)
 run(['dotnet','build','Game/HakutaiKagami.Game.csproj','--no-restore'],OUT/'build.log')
 inputs={p:h for p,h in before.items() if p.startswith(('Core/','Tests/','Game/Data/')) or p=='Scripts/current_test_contract.json'}
 mpath=ROOT/'Docs/Design/NewGameNpcRoutineV1/runtime-local/test-manifest.json';m=json.loads(mpath.read_bytes());trx=ROOT/'Docs/Design/NewGameNpcRoutineV1/runtime-local/tests/all.trx';c=ET.parse(trx).find('.//{*}Counters').attrib
 assert len(inputs)==317 and inputs==m['sources'] and sha(trx)==m['trx'] and c['passed']=='2122' and c['failed']=='0' and c['notExecuted']=='0' and sha(ROOT/'Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Core.dll')==m['core']==sha(ROOT/'Tests/bin/Debug/net8.0/HakutaiKagami.Core.dll')
 write(OUT/'test-manifest.json',dict(sources=inputs,trx=sha(trx),core=m['core'],tests=2122,reused_from=mpath.relative_to(ROOT).as_posix()))
 for p in [mpath,trx,OUT/'test-manifest.json']:proofs[p.relative_to(ROOT).as_posix()]=sha(p)
 key=uuid.uuid4().hex;folder=OUT/'playreview-personal';folder.mkdir(parents=True,exist_ok=True);record=prepare(ROOT,OUT,key);movie=folder/('native-'+key+'.avi')
 run([EXE,'--path',record/'Game','--resolution','1280x900','--write-movie',movie,'--fixed-fps','30','--disable-vsync','--','--new-game-test-key='+key,'--new-game-case=playreview-personal','--play-review-movie'],folder/'check.log','PLAY_REVIEW_PASSED')
 shutil.copytree(record/'Docs/Design/PlayReviewV1/runtime-local/playreview-personal',folder,dirs_exist_ok=True);copy=verify_copy()
 run([EXE,'--path',ROOT/'Game','--resolution','1280x900','--','--new-game-test-key='+key,'--new-game-case=playreview-personal','--new-game-restore'],folder/'restore.log','PLAY_REVIEW_PASSED')
 comparisons=pairs(folder,1);numeric=pose_pairs(folder,1);assert comparisons==numeric==2;video=encode(ROOT);collect(folder,proofs)
 for p in [OUT/'build.log',OUT/'recording-project.json']:proofs[p.relative_to(ROOT).as_posix()]=sha(p)
 binaries={p.relative_to(ROOT).as_posix():sha(p) for p in (ROOT/'Game/.godot/mono/temp/bin/Debug').glob('HakutaiKagami*.dll')};assert before==sources()
 write(OUT/'run-evidence.json',dict(sources=before,binaries=binaries,proofs=proofs,comparisons=2,numeric_pose_comparisons=2,gpu_processes=2,normal_shutdown_release_markers=2,tests=2122,added=0,core_inputs=317,core_tests_reused_exact=True,recording_copy=copy,video=video,limits=['Godot built-in fixed30fps recording disables realtime synchronization;illustrates controls/state only,not fps/latency performance.','Recording copy has exact runtime code/scenes/data/Game+Core DLLs;only viewport dimensions overridden to1280x900. Actual project settings unchanged.','Actual work/delivered sealed letter/open/own completion/save-load/departure/national view/walking;latest saved continuation replayed with exact recorded actual public targets.','2currentGPU2world-image/numeric pairs;full2122Core reused only exact317inputs/TRX/matchingDLL. Previous46NPC matrix remains historical,not rerun.','No general strategic or army AI load claim.']))
 print('PASS2GPU2world-image/numeric;direct image/video-frame review required',flush=True)
def finalize():
 j=json.loads((OUT/'run-evidence.json').read_bytes());assert j['sources']==sources();verify_copy()
 for kind in ['binaries','proofs']:
  for p,h in j[kind].items():assert sha(ROOT/p)==h,p
 review=json.loads((OUT/'visual-review.json').read_bytes());assert review['inspected']
 for p,h in review['inspected'].items():assert sha(ROOT/p)==h,p
 j.update(status='PASSED',completion='5/5',visual_review=review);write(BASE/'verification.json',j);print('PASS5/5 actual control review and recording')
if __name__=='__main__':finalize() if '--finalize' in sys.argv else main()
