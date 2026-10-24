from pathlib import Path
import json,uuid,sys,shutil,subprocess,re,time,xml.etree.ElementTree as ET
from verify_service_entry import ROOT,EXE,sha,write,run,pairs
from verify_batched_advance import pose_pairs
from verify_bottom_dialogue import sources as parent_sources
from prepare_play_review_recording import prepare
BASE=ROOT/'Docs/Design/InspectionFindingsV1';OUT=BASE/'runtime-local'
def sources():
 j=parent_sources()
 for p in [Path(__file__),ROOT/'Scripts/check_inspection_findings_binding.py',BASE/'README.md']:j[p.relative_to(ROOT).as_posix()]=sha(p)
 return j
def strict(args,log,marker):
 run(args,log,marker,timeout=800);s=log.read_bytes().decode('utf-8-sig');assert s.splitlines().count('GAME_PROCESS_EXIT_RELEASED')==1
 assert not any(x in s for x in ['RUNTIME_REJECT','UI_FAILURE','Leaked instance','RID allocations']),str(log)
def verify_copy():
 m=json.loads((OUT/'recording-project.json').read_bytes());copy=Path(m['record_project']);assert copy.resolve().is_relative_to(OUT.resolve())
 assert sha(ROOT/'Game/project.godot')==m['actual_project_sha256'] and sha(copy/'Game/project.godot')==m['recording_project_sha256']
 text=(ROOT/'Game/project.godot').read_bytes().decode('utf-8-sig')
 for n,v in [('viewport_width',1280),('viewport_height',900)]:text,count=re.subn(r'(?m)^window/size/'+n+r'=\d+$','window/size/'+n+'='+str(v),text);assert count==1
 assert (copy/'Game/project.godot').read_bytes()==text.encode()
 for p,h in m['copied_runtime_inputs'].items():assert sha(ROOT/'Game'/p)==sha(copy/'Game'/p)==h,p
 return m
def named_pair(folder,a,b):
 from PIL import Image,ImageChops
 for suffix in ['-world.sha256','-info.txt','-pose.json']:assert (folder/(a+suffix)).read_bytes()==(folder/(b+suffix)).read_bytes(),(folder,a,b,suffix)
 ia=Image.open(folder/(a+'.png')).convert('RGB');ib=Image.open(folder/(b+'.png')).convert('RGB')
 bounds=json.loads((folder/(a+'-rect.json')).read_bytes())
 for rect in [bounds['Surface'],bounds['Info']]:
  x,y,w,h=map(int,rect);assert ImageChops.difference(ia.crop((x,y,x+w,y+h)),ib.crop((x,y,x+w,y+h))).getbbox() is None,(a,b,'pixels')

def main():
 OUT.mkdir(parents=True,exist_ok=True);before=sources();proofs={}
 run(['dotnet','build','Game/HakutaiKagami.Game.csproj','--no-restore'],OUT/'build.log')
 inputs={p:h for p,h in before.items() if p.startswith(('Core/','Tests/','Game/Data/')) or p=='Scripts/current_test_contract.json'}
 comparisons={};numeric={}
 for case in ['findings-normal','findings-small','worktalk-voluntary','worktalk-binding','worktalk-small']:
  key=uuid.uuid4().hex;folder=(OUT if case.startswith('findings-') else ROOT/'Docs/Design/BottomDialogueV1/runtime-local')/case;folder.mkdir(parents=True,exist_ok=True);resolution='1100x720' if case in ['worktalk-small','findings-small'] else '1280x900'
  strict([EXE,'--path',ROOT/'Game','--resolution',resolution,'--','--new-game-test-key='+key,'--new-game-case='+case],folder/'check.log','WORK_CONVERSATION_PASSED')
  strict([EXE,'--path',ROOT/'Game','--resolution',resolution,'--','--new-game-test-key='+key,'--new-game-case='+case,'--new-game-restore'],folder/'restore.log','WORK_CONVERSATION_PASSED')
  comparisons[case]=pairs(folder,2);numeric[case]=pose_pairs(folder,2)
  if case.startswith('findings-'):
   for a,b in [('result','restored-result'),('s2','restored-s2'),('movement-final','continued-s2')]:named_pair(folder,a,b)
   comparisons[case]+=3;numeric[case]+=3
 for case,stage,count,marker in [('local-work','ResponsiveLocalIntentV1',2,'RESPONSIVE_LOCAL_INTENT_PASSED'),('local-cancel','ResponsiveLocalIntentV1',2,'RESPONSIVE_LOCAL_INTENT_PASSED'),('playreview-personal','PlayReviewV1',1,'PLAY_REVIEW_PASSED')]:
  key=uuid.uuid4().hex;folder=ROOT/'Docs/Design'/stage/'runtime-local'/case
  for mode in ['check','restore']:strict([EXE,'--path',ROOT/'Game','--resolution','1280x900','--','--new-game-test-key='+key,'--new-game-case='+case]+(['--new-game-restore'] if mode=='restore' else []),folder/('work-conversation-'+mode+'.log'),marker)
  comparisons[case]=pairs(folder,count);numeric[case]=pose_pairs(folder,count)
 key=uuid.uuid4().hex;copy=prepare(ROOT,OUT,key);folder=OUT/'movie';folder.mkdir(exist_ok=True);movie=folder/('native-'+key+'.avi')
 strict([EXE,'--path',copy/'Game','--resolution','1280x900','--write-movie',movie,'--fixed-fps','30','--disable-vsync','--','--new-game-test-key='+key,'--new-game-case=findings-normal','--work-conversation-film'],folder/'record.log','WORK_CONVERSATION_PASSED');verify_copy()
 shutil.copytree(copy/'Docs/Design/InspectionFindingsV1/runtime-local/findings-normal',folder/'recorded-evidence',dirs_exist_ok=True)
 video=folder/('native-'+key+'.mp4')
 source=video.with_suffix('.avi');ff=ROOT/'.tools/video-tools/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe'
 run([ff,'-y','-hide_banner','-i',source,'-an','-c:v','libx264','-preset','medium','-crf','20','-pix_fmt','yuv420p','-movflags','+faststart',video],OUT/'encode.log')
 run([ff,'-hide_banner','-i',video,'-map','0:v:0','-an','-f','null','NUL'],OUT/'decode.log');text=(OUT/'decode.log').read_bytes().decode(errors='replace');assert '1280x900' in text and '30 fps' in text;frames=int(re.findall(r'frame=\s*(\d+)',text)[-1]);assert frames>300
 for base in [OUT,ROOT/'Docs/Design/ResponsiveLocalIntentV1/runtime-local/local-work',ROOT/'Docs/Design/ResponsiveLocalIntentV1/runtime-local/local-cancel',ROOT/'Docs/Design/PlayReviewV1/runtime-local/playreview-personal']:
  for p in base.glob('*'):
   if p.is_file() and p.name not in ['run-evidence.json','visual-review.json'] and p.suffix in ['.log','.json','.png','.sha256','.txt']:proofs[p.relative_to(ROOT).as_posix()]=sha(p)
 for case in ['findings-normal','findings-small','worktalk-voluntary','worktalk-binding','worktalk-small']:
  for p in ((OUT if case.startswith('findings-') else ROOT/'Docs/Design/BottomDialogueV1/runtime-local')/case).glob('*'):
   if p.is_file():proofs[p.relative_to(ROOT).as_posix()]=sha(p)
 manifest=OUT/'test-manifest.json';deadline=time.monotonic()+5400
 while not manifest.exists():
  assert time.monotonic()<deadline,'Core test manifest was not produced'
  print('WAIT fresh Core test evidence; GPU checks completed',flush=True);time.sleep(30)
 m=json.loads(manifest.read_bytes());trx=OUT/'tests/all.trx';c=ET.parse(trx).find('.//{*}Counters').attrib
 assert inputs==m['sources'] and sha(trx)==m['trx'] and c['passed']=='2143' and c['failed']=='0' and c['notExecuted']=='0'
 assert sha(ROOT/'Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Core.dll')==sha(ROOT/'Tests/bin/Debug/net8.0/HakutaiKagami.Core.dll')==m['core']
 for p in [manifest,trx]:proofs[p.relative_to(ROOT).as_posix()]=sha(p)
 assert sources()==before
 for p in folder.rglob('*'):
  if p.is_file() and p.suffix!='.avi':proofs[p.relative_to(ROOT).as_posix()]=sha(p)
 write(OUT/'run-evidence.json',dict(sources=before,proofs=proofs,binaries={p.relative_to(ROOT).as_posix():sha(p) for p in (ROOT/'Game/.godot/mono/temp/bin/Debug').glob('HakutaiKagami*.dll')},gpu_processes=17,comparisons=comparisons,numeric=numeric,core_tests_executed=2143,core_inputs=len(inputs),completion='PENDING',video=dict(path=video.relative_to(ROOT).as_posix(),sha256=sha(video),frames=frames,fixed_recording_fps=30,scope='Separate actual Godot controls recorded with fixed30fps;not realtime latency/fps measurement. Save restoration compares normal renderer processes with zero image tolerance.')))
 print('PASS actual17GPU; visual review still required',flush=True)
def finalize():
 j=json.loads((OUT/'run-evidence.json').read_bytes());assert j['sources']==sources();verify_copy()
 for kind in ['proofs','binaries']:
  for p,h in j[kind].items():assert sha(ROOT/p)==h,p
 visual=json.loads((OUT/'visual-review.json').read_bytes());assert visual['inspected']
 for p,h in visual['inspected'].items():assert sha(ROOT/p)==h,p
 j.update(status='PASSED',completion='5/5',visual_review=visual);write(BASE/'verification.json',j);print('PASS inspection findings5/5')
if __name__=='__main__':finalize() if '--finalize' in sys.argv else main()
