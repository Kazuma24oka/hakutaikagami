from pathlib import Path
import subprocess,json,hashlib,uuid,time,sys,re
ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'Docs/Design/HistoricalRoadV1';OUT=BASE/'runtime-local'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 OUT.mkdir(parents=True,exist_ok=True)
 evidence=json.loads((ROOT/'Core/Data/historical-roads-v1.json').read_bytes())
 assert sha(OUT/'sources/heritage.html')==next(s['SnapshotSha256'] for s in evidence['Sources'] if s['Id']=='bunka-futamura'),'Changed primary evidence snapshot'
 subprocess.run(['dotnet','build','Game/HakutaiKagami.Game.csproj','--no-restore','--verbosity','minimal'],cwd=ROOT,check=True)
 subprocess.run(['dotnet','test','Tests/HakutaiKagami.Tests.csproj','--no-restore','--verbosity','minimal','--logger','trx;LogFileName=history.trx','--results-directory',str(OUT/'tests')],cwd=ROOT,check=True)
 sources=[*ROOT.glob('Core/**/*.cs'),*ROOT.glob('Tests/*.cs'),*ROOT.glob('Game/Scripts/**/*.cs'),*ROOT.glob('Core/Data/*.json'),ROOT/'Core/HakutaiKagami.Core.csproj',ROOT/'Game/Scenes/HistoricalRoadTrial.tscn',Path(__file__),ROOT/'Game/project.godot']
 sources=[p for p in sources if 'obj' not in p.parts and 'bin' not in p.parts];before={p.relative_to(ROOT).as_posix():sha(p) for p in sources}
 exe=ROOT/'.tools/godot-4.5.2/Godot_v4.5.2-stable_mono_win64/Godot_v4.5.2-stable_mono_win64_console.exe';key=uuid.uuid4().hex
 startup=subprocess.STARTUPINFO();startup.dwFlags|=subprocess.STARTF_USESHOWWINDOW;startup.wShowWindow=0;logs={}
 for mode in ['check','restore','video']:
  args=[str(exe),'--path',str(ROOT/'Game'),'--resolution','1100x720' if mode=='video' else '1400x1100']
  if mode=='video':args+=['--write-movie',str(OUT/'historical-roads.avi'),'--fixed-fps','24']
  args+=['res://Scenes/HistoricalRoadTrial.tscn','--','--history-'+mode,'--save-key='+key]
  r=subprocess.run(args,cwd=ROOT,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,encoding='utf-8',errors='replace',startupinfo=startup,creationflags=subprocess.CREATE_NO_WINDOW,timeout=240)
  (OUT/(mode+'.log')).write_bytes(r.stdout.encode());logs[mode]=r.stdout
  assert r.returncode==0 and ('HISTORY_VIDEO_PASSED' if mode=='video' else 'HISTORY_CHECK_PASSED') in r.stdout and 'ERROR:' not in r.stdout,r.stdout
 from PIL import Image,ImageChops
 rect=json.loads((OUT/'check-checks.json').read_bytes())['ViewportRect'];x,y,w,h=map(int,rect);crop=(x,y,x+w,y+h);comparisons={}
 for stage in ['approach','quarter','half','passed']:
  for before_name,after in [(stage,'restored-'+stage),('final','continued-'+stage)]:
   diff=ImageChops.difference(Image.open(OUT/(before_name+'.png')).convert('RGB').crop(crop),Image.open(OUT/(after+'.png')).convert('RGB').crop(crop));assert diff.getbbox() is None,(before_name,after)
   comparisons[before_name+'/'+after]={'changed_pixels':0,'rect':rect}
 sys.path.insert(0,str(ROOT/'.tools/video-tools'));import imageio_ffmpeg
 ffmpeg=imageio_ffmpeg.get_ffmpeg_exe()
 r=subprocess.run([ffmpeg,'-y','-i',str(OUT/'historical-roads.avi'),'-an','-c:v','libx264','-preset','medium','-crf','20','-pix_fmt','yuv420p','-movflags','+faststart',str(OUT/'historical-roads-short.mp4')],stdout=subprocess.PIPE,stderr=subprocess.STDOUT,encoding='utf-8',errors='replace',check=True)
 (OUT/'encode.log').write_bytes(r.stdout.encode())
 decoded=subprocess.run([ffmpeg,'-i',str(OUT/'historical-roads-short.mp4'),'-f','null','-'],stdout=subprocess.PIPE,stderr=subprocess.STDOUT,encoding='utf-8',errors='replace',check=True)
 assert re.search(r'frame=\s*624\b',decoded.stdout) and '1100x720' in decoded.stdout,decoded.stdout
 (OUT/'video-decode-check.log').write_bytes(decoded.stdout.encode())
 assert before=={p.relative_to(ROOT).as_posix():sha(p) for p in sources},'Sources changed during verification'
 report={'status':'PASSED','sources':before,'binaries':{p.relative_to(ROOT).as_posix():sha(p) for p in (ROOT/'Game/.godot/mono/temp/bin/Debug').glob('HakutaiKagami.*.dll')},'images':{p.name:sha(p) for p in OUT.glob('*.png') if p.stem in ['approach','quarter','half','passed','final'] or p.stem in [prefix+stage for prefix in ['restored-','continued-'] for stage in ['approach','quarter','half','passed']]},'video':{'file':'runtime-local/historical-roads-short.mp4','sha256':sha(OUT/'historical-roads-short.mp4'),'capture':'Actual Godot Movie Maker frames; scripted game actions, no native input','fps':24,'frames_expected':624,'frames_decoded':624,'duration_seconds':26,'width':1100,'height':720,'decode_log_sha256':sha(OUT/'video-decode-check.log'),'bytes':(OUT/'historical-roads-short.mp4').stat().st_size,'scope':'Published approximate 200m remnant length; synthetic straight display and access; NOT a surveyed 1560 reconstruction'},'pixel_checks':comparisons,'logs':logs,'checks':{m:json.loads((OUT/(m+'-checks.json')).read_bytes())['Checks'] for m in ['check','restore']},'assets':{p.relative_to(ROOT).as_posix():sha(p) for p in [*ROOT.glob('Game/Assets/Officer3D/**/*.glb'),*ROOT.glob('Game/Assets/Officer3D/**/*.png'),*ROOT.glob('Game/Data/Officer3D/*.json'),*ROOT.glob('Game/Shaders/Officer3D*.gdshader')]},'entry_tools':{n:sha(ROOT/n) for n in ['Scripts/check_historical_binding.py','Scripts/Show-HistoricalRoad.ps1']},'trx_sha256':sha(OUT/'tests/history.trx'),'visual_review':'PENDING'}
 (BASE/'verification.json').write_bytes((json.dumps(report,ensure_ascii=False,indent=2)+'\n').encode());print('PASS historical-distance Core/GPU/independent restore/video')
if __name__=='__main__':main()
