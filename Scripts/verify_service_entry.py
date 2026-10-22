from pathlib import Path
import json,subprocess,uuid,hashlib,xml.etree.ElementTree as ET,sys,shutil
from PIL import Image,ImageChops
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'Docs/Design/ServiceEntryV1';OUT=BASE/'runtime-local'
EXE=ROOT/'.tools/godot-4.5.2/Godot_v4.5.2-stable_mono_win64/Godot_v4.5.2-stable_mono_win64_console.exe'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def write(p,j):p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes((json.dumps(j,ensure_ascii=False,indent=2)+'\n').encode())
def sources():
 files=[ROOT/'AGENTS.md',ROOT/'Docs/README.md',ROOT/'Docs/Development/CURRENT_STAGE.md',ROOT/'Scripts/current_test_contract.json',ROOT/'Scripts/Show-NewGameCandidate.ps1',Path(__file__),ROOT/'Scripts/check_service_entry_binding.py',BASE/'README.md',BASE/'contract.json',ROOT/'Game/project.godot']
 for folder,patterns in [('Core',['*.cs','*.csproj']),('Tests',['*.cs','*.csproj']),('Game/Scripts',['*.cs']),('Game/Scenes',['*.tscn'])]:
  for pat in patterns:files.extend(p for p in (ROOT/folder).rglob(pat) if 'obj' not in p.parts and 'bin' not in p.parts)
 files.extend(p for folder in ['Game/Assets','Game/Data','Tests/Fixtures'] for p in (ROOT/folder).rglob('*') if p.is_file() and p.suffix not in ['.import','.uid'])
 return {p.relative_to(ROOT).as_posix():sha(p) for p in sorted(set(files))}
def run(args,log,marker=None,timeout=400):
 q=subprocess.run([str(x) for x in args],cwd=ROOT,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,creationflags=subprocess.CREATE_NO_WINDOW,timeout=timeout);log.parent.mkdir(parents=True,exist_ok=True);log.write_bytes(q.stdout);t=q.stdout.decode('utf8',errors='replace');assert q.returncode==0 and 'ERROR:' not in t and (marker is None or marker in t),(str(log),t[-6000:]);print('PASS',log.relative_to(ROOT),flush=True)
def pairs(folder,count,info=True):
 for n in range(count):
  for a,b in [(f's{n}',f'restored-s{n}'),('final',f'continued-s{n}')]:
   assert (folder/(a+'-world.sha256')).read_bytes()==(folder/(b+'-world.sha256')).read_bytes(),(folder,a,b,'world')
   if info:assert (folder/(a+'-info.txt')).read_bytes()==(folder/(b+'-info.txt')).read_bytes(),(folder,a,b,'info')
   ia=Image.open(folder/(a+'.png')).convert('RGB');ib=Image.open(folder/(b+'.png')).convert('RGB');assert ia.size==ib.size
   bounds=json.loads((folder/(a+'-rect.json')).read_bytes());rects=[bounds['Surface'],bounds['Info']] if info else [bounds]
   for rect in rects:
    x,y,w,h=map(int,rect);assert 0<=x<x+w<=ia.width and 0<=y<y+h<=ia.height
    assert ImageChops.difference(ia.crop((x,y,x+w,y+h)),ib.crop((x,y,x+w,y+h))).getbbox() is None,(folder,a,b,'pixels')
 return count*2
def main():
 OUT.mkdir(parents=True,exist_ok=True);before=sources();proofs={};comparisons={}
 run(['dotnet','build','Game/HakutaiKagami.Game.csproj','--no-restore'],OUT/'build.log')
 if '--reuse-core' in sys.argv:
  prior=json.loads((OUT/'run-evidence.json').read_bytes())
  for p,h in prior['sources'].items():
   if p.startswith(('Core/','Tests/')) or p=='Scripts/current_test_contract.json':assert before[p]==h,p
  core='Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Core.dll';assert sha(ROOT/core)==prior['binaries'][core]
  trx=OUT/'tests/service.trx';assert sha(trx)==prior['proofs'][trx.relative_to(ROOT).as_posix()]
  print('PASS reuse exact unchanged Core/Tests source,Core DLL and1377-test TRX',flush=True)
 else:
  run(['dotnet','test','Tests/HakutaiKagami.Tests.csproj','--logger','trx;LogFileName=service.trx','--results-directory',OUT/'tests'],OUT/'test.log')
 trx=OUT/'tests/service.trx';c=ET.parse(trx).find('.//{*}Counters').attrib;assert c['passed']=='1377' and c['failed']=='0' and c['notExecuted']=='0',c
 for case in ['group','historical','individual','absorb','collision','minor','corrupt','service-ronin','service-retainer','service-odawara']:
  service=case.startswith('service-');folder=(OUT if service else ROOT/'Docs/Design/NewGameFlowV1/runtime-local')/case;folder.mkdir(parents=True,exist_ok=True);key=uuid.uuid4().hex
  if case=='corrupt':p=folder/key/'library';p.mkdir(parents=True);(p/'officers.json').write_bytes(b'broken')
  for mode in ['check','restore']:
   args=[EXE,'--path',ROOT/'Game','--resolution','1280x900']+(['--fixed-fps','60'] if service and case=='service-ronin' and mode=='check' else [])+['--','--new-game-test-key='+key,'--new-game-case='+case]+(['--new-game-restore'] if mode=='restore' else [])+(['--service-film'] if service and case=='service-ronin' and mode=='check' else [])
   run(args,folder/(mode+'.log'),'SERVICE_ENTRY_PASSED' if service else 'NEW_GAME_FLOW_PASSED')
  comparisons[case]=pairs(folder,6 if service else 4)
  for p in folder.glob('*'):
   if p.is_file() and p.suffix in ['.png','.json','.sha256','.txt','.log']:proofs[p.relative_to(ROOT).as_posix()]=sha(p)
 folder=ROOT/'Docs/Design/CareerMilitaryV1/runtime-local';key=uuid.uuid4().hex
 for mode in ['check','restore']:run([EXE,'--path',ROOT/'Game','--resolution','1100x720','--','--start-selection-military-'+mode,'--save-key='+key],folder/(mode+'.log'),'MILITARY_PASSED')
 comparisons['old-military']=pairs(folder,17,False)
 for p in folder.glob('*'):
  if p.is_file() and p.suffix in ['.png','.json','.sha256','.txt','.log']:proofs[p.relative_to(ROOT).as_posix()]=sha(p)
 film=OUT/'service-ronin/film';frames=sorted(film.glob('walk-*.png'));assert len(frames)==180
 sequence=[]
 for name in ['menu','registered-officer','s0','s1','s2','s3','s4']:sequence.extend([OUT/'service-ronin'/(name+'.png')]*90)
 sequence+=frames;sequence.extend([OUT/'service-ronin/final.png']*90)
 frames_dir=OUT/'video-frames';frames_dir.mkdir(exist_ok=True)
 for n,p in enumerate(sequence):shutil.copyfile(p,frames_dir/('frame-'+str(n).zfill(4)+'.png'))
 ff=ROOT/'.tools/video-tools/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe';movie=OUT/'service-entry.mp4'
 run([ff,'-y','-framerate','60','-i',frames_dir/'frame-%04d.png','-frames:v',str(len(sequence)),'-vf','pad=ceil(iw/2)*2:ceil(ih/2)*2','-c:v','libx264','-crf','20','-pix_fmt','yuv420p','-movflags','+faststart',movie],OUT/'encode.log')
 run([ff,'-v','error','-i',movie,'-f','null','-'],OUT/'decode.log')
 for p in [movie,trx,OUT/'build.log',OUT/'test.log']+frames:proofs[p.relative_to(ROOT).as_posix()]=sha(p)
 preserved={}
 for flag,marker,file in [('--smoke-test','SCENE_SMOKE_PASS','godot-foundation.png'),('--tax-smoke-test','TAX_SCENE_PASS','tax-vertical.png'),('--shared-protocol-smoke-test','SHARED_PROTOCOL_GUI_PASS','shared-protocol.png')]:
  p=ROOT/'Docs/Proposals/Evidence'/file;original=p.read_bytes();preserved[file]=sha(p)
  try:
   log=OUT/(flag[2:]+'.log');run([EXE,'--path',ROOT/'Game','--resolution','1100x720','--',flag],log,marker)
   target=OUT/('legacy-'+file);target.write_bytes(p.read_bytes());proofs[target.relative_to(ROOT).as_posix()]=sha(target);proofs[log.relative_to(ROOT).as_posix()]=sha(log)
  finally:p.write_bytes(original)
  assert sha(p)==preserved[file]
 for p in [OUT/'encode.log',OUT/'decode.log',OUT/'encode-first-failure.log',OUT/'shared-protocol-first-failure.log']:
  if p.exists():proofs[p.relative_to(ROOT).as_posix()]=sha(p)
 binaries={p.relative_to(ROOT).as_posix():sha(p) for p in (ROOT/'Game/.godot/mono/temp/bin/Debug').glob('HakutaiKagami*.dll')};assert binaries
 core=ROOT/'Tests/bin/Debug/net8.0/HakutaiKagami.Core.dll';assert sha(core)==sha(ROOT/'Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Core.dll')
 assert sources()==before
 write(OUT/'run-evidence.json',dict(sources=before,binaries=binaries,proofs=proofs,tests=1377,added=25,comparisons=comparisons,gpu_processes=25,preserved_legacy_evidence=preserved,video=movie.relative_to(ROOT).as_posix(),limits=['New-profile military lending, combat, wages and nationwide personnel AI are not completed.','Old reports are historical; only the stated legacy military and frontend cases were rerun with current binary.','Fixed60fps video is not a new realtime performance measurement.']))
 print('PASS automated service entry; visual inspection and finalization required',flush=True)
def finalize():
 j=json.loads((OUT/'run-evidence.json').read_bytes());assert j['sources']==sources()
 for kind in ['binaries','proofs']:
  for p,h in j[kind].items():assert sha(ROOT/p)==h,p
 review=json.loads((OUT/'visual-review.json').read_bytes());assert review['inspected'] and review['method']
 for p,h in review['inspected'].items():assert j['proofs'][p]==h and sha(ROOT/p)==h,p
 j.update(status='PASSED',completion='5/5',visual_review=review);j['proofs'][(OUT/'visual-review.json').relative_to(ROOT).as_posix()]=sha(OUT/'visual-review.json');write(BASE/'verification.json',j)
 print('PASS final service entry5/5; current sources,tests,GPU and visual review bound')
if __name__=='__main__':
 if '--finalize' in sys.argv:finalize()
 else:main()
