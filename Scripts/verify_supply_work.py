from pathlib import Path
import json,uuid,sys,shutil,subprocess,re,time,xml.etree.ElementTree as ET
from verify_service_entry import ROOT,EXE,sha,write,run,pairs
from verify_batched_advance import pose_pairs
from verify_local_demand import sources as parent_sources
from prepare_play_review_recording import prepare
BASE=ROOT/'Docs/Design/SupplyWorkV1';OUT=BASE/'runtime-local'
def sources():
 j=parent_sources()
 for p in [Path(__file__),ROOT/'Scripts/check_supply_work_binding.py',BASE/'README.md']:j[p.relative_to(ROOT).as_posix()]=sha(p)
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

def reused_core(before):
 prior=json.loads((ROOT/'Docs/Design/LocalWorkDemandV1/verification.json').read_bytes());assert prior['status']=='PASSED'
 inputs={p:h for p,h in before.items() if p.startswith(('Core/','Tests/','Game/Data/')) or p=='Scripts/current_test_contract.json'}
 old={p:h for p,h in prior['sources'].items() if p.startswith(('Core/','Tests/','Game/Data/')) or p=='Scripts/current_test_contract.json'}
 assert inputs==old
 core='Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Core.dll';assert sha(ROOT/core)==prior['binaries'][core]==sha(ROOT/'Tests/bin/Debug/net8.0/HakutaiKagami.Core.dll')
 trx=ROOT/'Docs/Design/LocalWorkDemandV1/runtime-local/tests/all-2208.trx';assert sha(trx)==prior['proofs'][trx.relative_to(ROOT).as_posix()]
 counts=ET.parse(trx).find('.//{*}Counters').attrib;assert counts['passed']=='2208' and counts['failed']=='0' and counts['notExecuted']=='0'
 return dict(executed_this_stage=False,passed=2208,source_commit='d50fe4ba096caa106b399b469c676f4de4dc04ec',core_inputs=inputs,core_sha256=sha(ROOT/core),trx=trx.relative_to(ROOT).as_posix(),trx_sha256=sha(trx))

def main():
 OUT.mkdir(parents=True,exist_ok=True);before=sources();proofs={}
 run(['dotnet','build','Game/HakutaiKagami.Game.csproj','--no-restore'],OUT/'build.log');core=reused_core(before)
 comparisons={};numeric={}
 cases=[('supply-normal','SupplyWorkV1',6,'SUPPLY_WORK_PASSED','1280x900'),('supply-small','SupplyWorkV1',6,'SUPPLY_WORK_PASSED','1100x720'),('supply-own','SupplyWorkV1',6,'SUPPLY_WORK_PASSED','1280x900'),('milwork-normal','MilitaryWorkV1',7,'MILITARY_WORK_PASSED','1280x900'),('workcycle-normal','WorkCycleV1',None,'WORK_CYCLE_PASSED','1280x900'),('demand-normal','LocalWorkDemandV1',5,'LOCAL_DEMAND_PASSED','1280x900'),('military-retainer','NewGameMilitaryV1',17,'NEW_MILITARY_PASSED','1280x900'),('local-cancel','ResponsiveLocalIntentV1',2,'RESPONSIVE_LOCAL_INTENT_PASSED','1280x900')]
 for case,stage,count,marker,res in cases:
  key=uuid.uuid4().hex;folder=ROOT/'Docs/Design'/stage/'runtime-local'/case
  for restore in [False,True]:strict([EXE,'--path',ROOT/'Game','--resolution',res,'--','--new-game-test-key='+key,'--new-game-case='+case]+(['--new-game-restore'] if restore else []),folder/('supply-work-restore.log' if restore else 'supply-work-check.log'),marker)
  if count is not None:comparisons[case]=pairs(folder,count);numeric[case]=pose_pairs(folder,count)
  else:
   for a,b in [('s0','restored-s0'),('final','continued-s0'),('s1','restored-s1'),('self-complete','continued-s1'),('s2','restored-s2'),('travel-final','continued-s2')]:named_pair(folder,a,b)
   comparisons[case]=numeric[case]=6
  for p in folder.glob('*'):
   if p.is_file():proofs[p.relative_to(ROOT).as_posix()]=sha(p)
 key=uuid.uuid4().hex;copy=prepare(ROOT,OUT,key);folder=OUT/'movie';folder.mkdir(exist_ok=True);avi=folder/('native-'+key+'.avi')
 strict([EXE,'--path',copy/'Game','--resolution','1280x900','--write-movie',avi,'--fixed-fps','30','--disable-vsync','--','--new-game-test-key='+key,'--new-game-case=supply-normal','--supply-work-film'],folder/'record.log','SUPPLY_WORK_PASSED');verify_copy()
 video=avi.with_suffix('.mp4');ff=ROOT/'.tools/video-tools/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe'
 run([ff,'-y','-hide_banner','-i',avi,'-an','-c:v','libx264','-preset','medium','-crf','20','-pix_fmt','yuv420p','-movflags','+faststart',video],OUT/'encode.log')
 run([ff,'-hide_banner','-i',video,'-map','0:v:0','-an','-f','null','NUL'],OUT/'decode.log');text=(OUT/'decode.log').read_bytes().decode(errors='replace');assert '1280x900' in text and '30 fps' in text;frames=int(re.findall(r'frame=\s*(\d+)',text)[-1]);assert frames>300
 for base in [OUT,folder]:
  for p in base.glob('*'):
   if p.is_file() and p.name not in ['run-evidence.json','visual-review.json'] and p.suffix!='.avi':proofs[p.relative_to(ROOT).as_posix()]=sha(p)
 assert sources()==before;reused_core(before)
 write(OUT/'run-evidence.json',dict(sources=before,proofs=proofs,binaries={p.relative_to(ROOT).as_posix():sha(p) for p in (ROOT/'Game/.godot/mono/temp/bin/Debug').glob('HakutaiKagami*.dll')},gpu_processes=17,comparisons=comparisons,numeric=numeric,core_tests=core,completion='PENDING',video=dict(path=video.relative_to(ROOT).as_posix(),sha256=sha(video),frames=frames,fixed_recording_fps=30,scope='Actual Godot controls in isolated matching runtime copy. Fixed30fps recording is not realtime performance evidence.')))
 print('PASS actual17GPU104comparison pairs; visual review pending',flush=True)

def finalize():
 j=json.loads((OUT/'run-evidence.json').read_bytes());assert j['sources']==sources();verify_copy()
 for kind in ['proofs','binaries']:
  for p,h in j[kind].items():assert sha(ROOT/p)==h,p
 visual=json.loads((OUT/'visual-review.json').read_bytes());assert visual['inspected']
 for p,h in visual['inspected'].items():assert sha(ROOT/p)==h,p
 j.update(status='PASSED',completion='5/5',visual_review=visual);write(BASE/'verification.json',j);print('PASS supply work5/5')
if __name__=='__main__':finalize() if '--finalize' in sys.argv else main()
