"""Isolated actual Godot GPU verification; never synthesizes OS input."""
from pathlib import Path
import hashlib,json,subprocess,time,xml.etree.ElementTree as ET
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'Docs/Design/JapanMapV1';OUT=BASE/'runtime-local'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def inputs():
 return sorted(set([*ROOT.glob('Core/**/*.cs'),*ROOT.glob('Game/Scripts/**/*.cs'),*ROOT.glob('Tests/*.cs'),ROOT/'Tests/HakutaiKagami.Tests.csproj',ROOT/'Core/HakutaiKagami.Core.csproj',ROOT/'Game/HakutaiKagami.Game.csproj',ROOT/'Game/Scenes/JapanMapTrial.tscn',ROOT/'Game/Data/Geography/japan-map-v1.json',ROOT/'Scripts/build_japan_catalog.py',Path(__file__),ROOT/'Scripts/check_japan_binding.py',ROOT/'Scripts/Show-JapanMap.ps1',ROOT/'Game/project.godot',ROOT/'global.json']),key=str)
def assets():return sorted([*ROOT.glob('Game/Assets/Officer3D/**/*.glb'),*ROOT.glob('Game/Assets/Officer3D/**/*.png'),*ROOT.glob('Game/Data/Officer3D/*.json'),*ROOT.glob('Game/Shaders/Officer3D*.gdshader')],key=str)
def main():
 OUT.mkdir(parents=True,exist_ok=True)
 subprocess.run(['dotnet','build','Game/HakutaiKagami.Game.csproj','--no-restore','--verbosity','minimal'],cwd=ROOT,check=True)
 subprocess.run(['dotnet','test','Tests/HakutaiKagami.Tests.csproj','--no-restore','--verbosity','minimal','--logger','trx;LogFileName=japan.trx','--results-directory',str(OUT/'tests')],cwd=ROOT,check=True)
 counters=ET.parse(OUT/'tests/japan.trx').find('.//{*}Counters').attrib;assert counters['failed']=='0'
 exe=ROOT/'.tools/godot-4.5.2/Godot_v4.5.2-stable_mono_win64/Godot_v4.5.2-stable_mono_win64_console.exe'
 startup=subprocess.STARTUPINFO();startup.dwFlags|=subprocess.STARTF_USESHOWWINDOW;startup.wShowWindow=0;started=time.time()
 r=subprocess.run([str(exe),'--path',str(ROOT/'Game'),'--resolution','1440x960','res://Scenes/JapanMapTrial.tscn','--','--japan-check'],cwd=ROOT,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,encoding='utf-8',errors='replace',startupinfo=startup,creationflags=subprocess.CREATE_NO_WINDOW,timeout=90)
 (OUT/'gpu.log').write_bytes(r.stdout.encode());assert r.returncode==0 and 'JAPAN_MAP_PASSED' in r.stdout and 'ERROR:' not in r.stdout,r.stdout
 from PIL import Image,ImageChops
 checks=json.loads((OUT/'checks.json').read_bytes());x,y,w,h=map(int,checks['ViewportRect']);crop=(x,y,x+w,y+h)
 a=Image.open(OUT/'national.png').convert('RGB').crop(crop);b=Image.open(OUT/'national-return.png').convert('RGB').crop(crop)
 assert ImageChops.difference(a,b).getbbox() is None,'national zoom return pixel mismatch'
 images={p.name:sha(p) for p in OUT.glob('*.png') if p.stat().st_mtime>=started-1};assert len(images)==12,images
 files=[p for p in inputs() if 'obj' not in p.parts and 'bin' not in p.parts]
 report={'status':'PASSED','date':'2026-10-20','scope':'Static public geographic candidates; same render world and one Core clock. No historical scenario placement or geographic troop movement.','tests':counters,'gpu_checks':checks['Checks'],'rendered_transition_frames':checks['Frames'],'national_return_changed_pixels':0,'viewport_rect':checks['ViewportRect'],'source_sha256':{p.relative_to(ROOT).as_posix():sha(p) for p in files},'binary_sha256':{p.relative_to(ROOT).as_posix():sha(p) for p in (ROOT/'Game/.godot/mono/temp/bin/Debug').glob('HakutaiKagami.*.dll')},'asset_sha256':{p.relative_to(ROOT).as_posix():sha(p) for p in assets()},'images':images,'trx_sha256':sha(OUT/'tests/japan.trx'),'checks_sha256':sha(OUT/'checks.json'),'visual_review':'PENDING','environment':{'Godot':'4.5.2.stable.mono.official.6ce3de25a','dotnet_sdk':subprocess.check_output(['dotnet','--version'],cwd=ROOT,text=True).strip(),'gpu_log':r.stdout}}
 (BASE/'verification.json').write_bytes((json.dumps(report,ensure_ascii=False,indent=2)+'\n').encode());print('PASS',counters['passed'],'Core tests;',len(checks['Checks']),'GPU checks;',len(images),'images; national return zero changed pixels')
if __name__=='__main__':main()
