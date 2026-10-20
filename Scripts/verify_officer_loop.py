from pathlib import Path
import sys,subprocess,json,hashlib,uuid,time,re
from xml.etree import ElementTree as ET
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'Docs/Design/OfficerLoopV1';OUT=BASE/'runtime-local'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def source_files():
 paths=[*ROOT.glob('Core/**/*.cs'),*ROOT.glob('Core/Data/*.json'),*ROOT.glob('Tests/*.cs'),*ROOT.glob('Game/Scripts/**/*.cs'),*ROOT.glob('Game/Data/**/*.json'),*ROOT.glob('Game/Assets/Officer3D/**/*.glb'),*ROOT.glob('Game/Assets/Officer3D/**/*.png'),*ROOT.glob('Game/Assets/Scenery/**/*.tscn'),*ROOT.glob('Game/Assets/Scenery/**/*.tres'),*ROOT.glob('Game/Shaders/*.gdshader'),ROOT/'Core/HakutaiKagami.Core.csproj',ROOT/'Game/project.godot',ROOT/'Game/Scenes/ContinuousWorldTrial.tscn',ROOT/'Game/Scenes/OfficerLoopTrial.tscn',Path(__file__),ROOT/'Scripts/check_officer_loop_binding.py',ROOT/'Scripts/Show-OfficerLoop.ps1',BASE/'source-manifest.json']
 return sorted(set(p for p in paths if 'obj' not in p.parts and 'bin' not in p.parts))
def main():
 OUT.mkdir(parents=True,exist_ok=True);quick='--quick' in sys.argv
 if not quick:
  subprocess.run(['dotnet','build','Game/HakutaiKagami.Game.csproj','--no-restore','--verbosity','minimal'],cwd=ROOT,check=True)
  subprocess.run(['dotnet','test','Tests/HakutaiKagami.Tests.csproj','--no-restore','--verbosity','minimal','--logger','trx;LogFileName=officer-loop.trx','--results-directory',str(OUT/'tests')],cwd=ROOT,check=True)
 paths=source_files();before={p.relative_to(ROOT).as_posix():sha(p) for p in paths}
 exe=ROOT/'.tools/godot-4.5.2/Godot_v4.5.2-stable_mono_win64/Godot_v4.5.2-stable_mono_win64_console.exe';key=uuid.uuid4().hex
 cases=['roundtrip','refuse','combat','report-miss'];chosen=next((a[7:] for a in sys.argv if a.startswith('--case=')),None)
 if chosen:cases=[chosen]
 checks={};logs={};elapsed={}
 def run(args,name,marker):
  started=time.perf_counter();r=subprocess.run([str(exe),'--path',str(ROOT/'Game'),*args],cwd=ROOT,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,encoding='utf8',errors='replace',creationflags=subprocess.CREATE_NO_WINDOW,timeout=600)
  (OUT/(name+'.log')).write_bytes(r.stdout.encode());elapsed[name]=time.perf_counter()-started
  assert r.returncode==0 and marker in r.stdout and 'ERROR:' not in r.stdout,(name,r.stdout)
  logs[name]=r.stdout;print('GPU PASS',name,round(elapsed[name],2),'seconds',flush=True)
 for case in cases:
  for mode in ['check','restore']:
   name=case+'-'+mode
   run(['--resolution','1100x720','res://Scenes/OfficerLoopTrial.tscn','--','--officer-loop','--loop-'+mode,'--case='+case,'--save-key='+key],name,'OFFICER_LOOP_PASSED')
   checks[name]=json.loads((OUT/(name+'-checks.json')).read_bytes())
 if quick:print('Quick GPU checks complete');return
 from PIL import Image,ImageChops
 images={};comparisons={}
 def compare(a,b,rect):
  x,y,w,h=map(int,rect);crop=(x,y,x+w,y+h)
  for n in [a,b]:images[n+'.png']=sha(OUT/(n+'.png'))
  ia=Image.open(OUT/(a+'.png')).convert('RGB');ib=Image.open(OUT/(b+'.png')).convert('RGB');assert ia.size==ib.size
  diff=ImageChops.difference(ia.crop(crop),ib.crop(crop));count=sum(1 for p in diff.get_flattened_data() if p!=(0,0,0));assert count==0,(a,b,count)
  comparisons[a+'/'+b]=count
 for c in cases:
  finalrect=json.loads((OUT/(c+'-final-rect.json')).read_bytes())
  for i in range(5):
   rect=json.loads((OUT/(c+'-s'+str(i)+'-rect.json')).read_bytes())
   compare(c+'-s'+str(i),c+'-restored-s'+str(i),rect)
   compare(c+'-final',c+'-continued-s'+str(i),finalrect)
 # Shared scene must still work with the old, separately versioned profile.
 run(['--resolution','1100x720','res://Scenes/ContinuousWorldTrial.tscn','--','--continuous-check','--case=travel','--save-key='+key],'legacy-travel','CONTINUOUS_CHECK_PASSED')
 run(['--resolution','1100x720','--write-movie',str(OUT/'officer-loop.avi'),'--fixed-fps','24','res://Scenes/OfficerLoopTrial.tscn','--','--officer-loop','--loop-video','--case=roundtrip','--save-key='+key],'video','OFFICER_LOOP_VIDEO_PASSED')
 sys.path.insert(0,str(ROOT/'.tools/video-tools'));import imageio_ffmpeg
 ffmpeg=imageio_ffmpeg.get_ffmpeg_exe();r=subprocess.run([ffmpeg,'-y','-i',str(OUT/'officer-loop.avi'),'-an','-c:v','libx264','-preset','medium','-crf','20','-pix_fmt','yuv420p','-movflags','+faststart',str(OUT/'officer-loop-short.mp4')],stdout=subprocess.PIPE,stderr=subprocess.STDOUT,encoding='utf8',errors='replace',check=True);(OUT/'encode.log').write_bytes(r.stdout.encode())
 r=subprocess.run([ffmpeg,'-i',str(OUT/'officer-loop-short.mp4'),'-f','null','-'],stdout=subprocess.PIPE,stderr=subprocess.STDOUT,encoding='utf8',errors='replace',check=True);assert re.search(r'frame=\s*576\b',r.stdout);(OUT/'video-decode.log').write_bytes(r.stdout.encode())
 assert before=={p.relative_to(ROOT).as_posix():sha(p) for p in source_files()},'Sources changed during verification'
 counters=ET.parse(OUT/'tests/officer-loop.trx').find('.//{*}Counters').attrib;assert int(counters['failed'])==0 and int(counters['passed'])>=787
 j={'status':'PASSED','sources':before,'binaries':{p.relative_to(ROOT).as_posix():sha(p) for p in (ROOT/'Game/.godot/mono/temp/bin/Debug').glob('HakutaiKagami.*.dll')},'summary':{'tests':counters,'gpu_processes':len(cases)*2+2,'cases':len(cases),'save_stages':len(cases)*5,'equal_comparisons':len(comparisons),'images':len(images)},'comparisons':comparisons,'images':images,'checks':checks,'process_seconds':elapsed,'logs':logs,'trx_sha256':sha(OUT/'tests/officer-loop.trx'),'video':{'file':'runtime-local/officer-loop-short.mp4','sha256':sha(OUT/'officer-loop-short.mp4'),'decode_sha256':sha(OUT/'video-decode.log'),'frames':576,'seconds':24,'fps':24},'visual_review':None}
 (BASE/'verification.json').write_bytes((json.dumps(j,ensure_ascii=False,indent=2)+'\n').encode());print('PASS officer missions/council/GPU/save/replay/video',flush=True)
if __name__=='__main__':main()
