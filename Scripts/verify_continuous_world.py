from pathlib import Path
import sys,subprocess,json,hashlib,uuid,statistics,time,re
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'Docs/Design/ContinuousWorldV1';OUT=BASE/'runtime-local'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 OUT.mkdir(parents=True,exist_ok=True);quick='--quick' in sys.argv
 if not quick:
  subprocess.run(['dotnet','build','Game/HakutaiKagami.Game.csproj','--no-restore','--verbosity','minimal'],cwd=ROOT,check=True)
  subprocess.run(['dotnet','test','Tests/HakutaiKagami.Tests.csproj','--no-restore','--verbosity','minimal','--logger','trx;LogFileName=continuous.trx','--results-directory',str(OUT/'tests')],cwd=ROOT,check=True)
 paths=[*ROOT.glob('Core/**/*.cs'),*ROOT.glob('Core/Data/*.json'),*ROOT.glob('Tests/*.cs'),*ROOT.glob('Game/Scripts/**/*.cs'),*ROOT.glob('Game/Data/**/*.json'),*ROOT.glob('Game/Assets/Officer3D/**/*.glb'),*ROOT.glob('Game/Assets/Officer3D/**/*.png'),*ROOT.glob('Game/Assets/Scenery/**/*.tscn'),*ROOT.glob('Game/Assets/Scenery/**/*.tres'),*ROOT.glob('Game/Shaders/*.gdshader'),ROOT/'Core/HakutaiKagami.Core.csproj',ROOT/'Game/project.godot',ROOT/'Game/Scenes/ContinuousWorldTrial.tscn',Path(__file__),ROOT/'Scripts/check_continuous_binding.py',ROOT/'Scripts/Show-ContinuousWorld.ps1',BASE/'source-manifest.json'];paths=[p for p in paths if 'obj' not in p.parts and 'bin' not in p.parts];before={p.relative_to(ROOT).as_posix():sha(p) for p in paths}
 exe=ROOT/'.tools/godot-4.5.2/Godot_v4.5.2-stable_mono_win64/Godot_v4.5.2-stable_mono_win64_console.exe';key=uuid.uuid4().hex
 cases=['move','order','combat','rout','refuse','bridge','scale','north','kyushu','travel','coverage','assets'];chosen=next((a[7:] for a in sys.argv if a.startswith('--case=')),None)
 if chosen:cases=[chosen]
 logs={};checks={};elapsed={}
 for case in cases:
  for mode in ['check','restore']:
   args=[str(exe),'--path',str(ROOT/'Game'),'--resolution','1400x1100','res://Scenes/ContinuousWorldTrial.tscn','--','--continuous-'+mode,'--case='+case,'--save-key='+key]
   started=time.perf_counter();r=subprocess.run(args,cwd=ROOT,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,encoding='utf-8',errors='replace',creationflags=subprocess.CREATE_NO_WINDOW,timeout=600);name=case+'-'+mode;(OUT/(name+'.log')).write_bytes(r.stdout.encode());elapsed[name]=time.perf_counter()-started
   assert r.returncode==0 and 'CONTINUOUS_CHECK_PASSED' in r.stdout and 'ERROR:' not in r.stdout,(name,r.stdout)
   logs[name]=r.stdout;checks[name]=json.loads((OUT/(name+'-checks.json')).read_bytes());print('GPU PASS',name,round(elapsed[name],2),'seconds',flush=True)
 if quick:print('Quick GPU checks complete');return
 from PIL import Image,ImageChops
 images={};comparisons={};changes={}
 def compare(a,b,crop,equal=True):
  for n in [a,b]:images[n+'.png']=sha(OUT/(n+'.png'))
  diff=ImageChops.difference(Image.open(OUT/(a+'.png')).convert('RGB').crop(crop),Image.open(OUT/(b+'.png')).convert('RGB').crop(crop));count=sum(1 for p in diff.get_flattened_data() if p!=(0,0,0))
  if equal:assert count==0,(a,b,count);comparisons[a+'/'+b]=count
  else:assert count>500,(a,b,count);changes[a+'/'+b]=count
 for c in cases:
  x,y,w,h=map(int,checks[c+'-check']['ViewportRect']);crop=(x,y,x+w,y+h)
  for stage in ['s0','s1','s2']:compare(c+'-'+stage,c+'-restored-'+stage,crop);compare(c+'-final',c+'-continued-'+stage,crop)
  compare(c+'-quality-back',c+'-zoom-back',crop);compare(c+'-quality-back',c+'-quality-alternate',crop,False)
  images[c+'-nation.png']=sha(OUT/(c+'-nation.png'))
 args=[str(exe),'--path',str(ROOT/'Game'),'--resolution','1100x720','--write-movie',str(OUT/'continuous-world.avi'),'--fixed-fps','24','res://Scenes/ContinuousWorldTrial.tscn','--','--continuous-video','--case=move','--save-key='+key]
 r=subprocess.run(args,cwd=ROOT,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,encoding='utf8',errors='replace',creationflags=subprocess.CREATE_NO_WINDOW,timeout=600);(OUT/'video.log').write_bytes(r.stdout.encode());assert r.returncode==0 and 'CONTINUOUS_VIDEO_PASSED' in r.stdout and 'ERROR:' not in r.stdout,r.stdout;print('GPU PASS video',flush=True)
 sys.path.insert(0,str(ROOT/'.tools/video-tools'));import imageio_ffmpeg
 ffmpeg=imageio_ffmpeg.get_ffmpeg_exe();r=subprocess.run([ffmpeg,'-y','-i',str(OUT/'continuous-world.avi'),'-an','-c:v','libx264','-preset','medium','-crf','20','-pix_fmt','yuv420p','-movflags','+faststart',str(OUT/'continuous-world-short.mp4')],stdout=subprocess.PIPE,stderr=subprocess.STDOUT,encoding='utf8',errors='replace',check=True);(OUT/'encode.log').write_bytes(r.stdout.encode())
 r=subprocess.run([ffmpeg,'-i',str(OUT/'continuous-world-short.mp4'),'-f','null','-'],stdout=subprocess.PIPE,stderr=subprocess.STDOUT,encoding='utf8',errors='replace',check=True);assert re.search(r'frame=\s*624\b',r.stdout);(OUT/'video-decode.log').write_bytes(r.stdout.encode())
 assert before=={p.relative_to(ROOT).as_posix():sha(p) for p in paths},'Sources changed during GPU verification'
 from xml.etree import ElementTree as ET
 counters=ET.parse(OUT/'tests/continuous.trx').find('.//{*}Counters').attrib;assert int(counters['failed'])==0 and int(counters['passed'])>=774
 j={'status':'PASSED','sources':before,'binaries':{p.relative_to(ROOT).as_posix():sha(p) for p in (ROOT/'Game/.godot/mono/temp/bin/Debug').glob('HakutaiKagami.*.dll')},'summary':{'tests':counters,'gpu_processes':len(cases)*2+1,'cases':len(cases),'save_stages':len(cases)*3,'equal_comparisons':len(comparisons),'visible_changes':len(changes),'national_sites':108,'national_edges':118,'maximum_resident_chunks':max(x['Resident'] for x in checks.values()),'maximum_ground_triangles':max(x['Triangles'] for x in checks.values())},'comparisons':comparisons,'visible_changes':changes,'images':images,'checks':checks,'process_seconds':elapsed,'logs':logs,'trx_sha256':sha(OUT/'tests/continuous.trx'),'video':{'file':'runtime-local/continuous-world-short.mp4','sha256':sha(OUT/'continuous-world-short.mp4'),'decode_sha256':sha(OUT/'video-decode.log'),'frames':624,'seconds':26,'fps':24},'visual_review':'PENDING'}
 (BASE/'verification.json').write_bytes((json.dumps(j,ensure_ascii=False,indent=2)+'\n').encode());print('PASS full continuous world/GPU/restore/LOD/video',flush=True)
if __name__=='__main__':main()
