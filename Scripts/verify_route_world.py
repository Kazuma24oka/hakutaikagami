"""Own Godot processes, scene callbacks and GPU images; never uses native input."""
from pathlib import Path
import hashlib,json,subprocess,time,uuid
ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'Docs/Design/RouteWorldV1'
OUT=BASE/'runtime-local'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def sources():
 return sorted(set([*ROOT.glob('Core/**/*.cs'),*ROOT.glob('Game/Scripts/**/*.cs'),*ROOT.glob('Tests/*.cs'),ROOT/'Core/HakutaiKagami.Core.csproj',ROOT/'Game/HakutaiKagami.Game.csproj',ROOT/'Game/Scenes/RouteWorldTrial.tscn',Path(__file__),ROOT/'Scripts/Show-RouteWorld.ps1',ROOT/'Scripts/check_route_binding.py',ROOT/'Game/project.godot',ROOT/'global.json']),key=str)
def assets():
 return sorted([*ROOT.glob('Game/Assets/Officer3D/**/*.glb'),*ROOT.glob('Game/Assets/Officer3D/**/*.png'),*ROOT.glob('Game/Data/Officer3D/*.json'),*ROOT.glob('Game/Shaders/Officer3D*.gdshader')],key=str)
def main():
 OUT.mkdir(parents=True,exist_ok=True)
 subprocess.run(['dotnet','build','Game/HakutaiKagami.Game.csproj','--no-restore','--verbosity','minimal'],cwd=ROOT,check=True)
 subprocess.run(['dotnet','test','Tests/HakutaiKagami.Tests.csproj','--no-restore','--verbosity','minimal','--logger','trx;LogFileName=route.trx','--results-directory',str(OUT/'tests')],cwd=ROOT,check=True)
 exe=ROOT/'.tools/godot-4.5.2/Godot_v4.5.2-stable_mono_win64/Godot_v4.5.2-stable_mono_win64_console.exe'
 startup=subprocess.STARTUPINFO();startup.dwFlags|=subprocess.STARTF_USESHOWWINDOW;startup.wShowWindow=0
 logs={};started=time.time();save_key=uuid.uuid4().hex
 for mode in ['check','restore']:
  result=subprocess.run([str(exe),'--path',str(ROOT/'Game'),'--resolution','1400x1000','res://Scenes/RouteWorldTrial.tscn','--','--route-'+mode,'--route-save-key='+save_key],cwd=ROOT,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,encoding='utf-8',errors='replace',startupinfo=startup,creationflags=subprocess.CREATE_NO_WINDOW,timeout=90)
  (OUT/(mode+'.log')).write_text(result.stdout,encoding='utf-8');logs[mode]=result.stdout
  assert result.returncode==0 and 'ROUTE_WORLD_PASSED' in result.stdout and 'ERROR:' not in result.stdout,result.stdout
 images={p.name:sha(p) for p in OUT.glob('*.png') if p.stat().st_mtime>=started-1}
 assert len(images)==20,images
 from PIL import Image,ImageChops
 rect=json.loads((OUT/'checks.json').read_text())['ViewportRect'];x,y,w,h=map(int,rect);crop=(x,y,x+w,y+h)
 pixel_checks={}
 for before,after in [('midpoint','restored'),('midpoint','restored-process'),('complete','final-restored')]:
  a=Image.open(OUT/(before+'.png')).convert('RGB').crop(crop);b=Image.open(OUT/(after+'.png')).convert('RGB').crop(crop)
  difference=ImageChops.difference(a,b);assert difference.getbbox() is None,f'GPU pixels differ: {before}/{after}'
  pixel_checks[before+'/'+after]={'changed_pixels':0,'viewport_rect':rect}
 frames=[Image.open(OUT/f'motion-{i:02d}.png') for i in range(12)]
 frames[0].save(OUT/'motion.gif',save_all=True,append_images=frames[1:],duration=120,loop=0)
 report={'status':'PASSED','scope':'Versioned synthetic corridor, seconds-domain Core and existing 3D actor, explicit entry; normal Main unchanged','date':'2026-10-20','logs':logs,'pixel_checks':pixel_checks,'images':images,'gif_sha256':sha(OUT/'motion.gif'),'checks':json.loads((OUT/'checks.json').read_text())['Checks'],'restore_checks':json.loads((OUT/'restore-checks.json').read_text())['Checks'],'source_sha256':{p.relative_to(ROOT).as_posix():sha(p) for p in sources() if 'obj' not in p.parts and 'bin' not in p.parts},'binary_sha256':{p.relative_to(ROOT).as_posix():sha(p) for p in (ROOT/'Game/.godot/mono/temp/bin/Debug').glob('HakutaiKagami.*.dll')},'asset_sha256':{p.relative_to(ROOT).as_posix():sha(p) for p in assets()},'trx_sha256':sha(OUT/'tests/route.trx'),'visual_review':'PENDING'}
 (BASE/'verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
 print('PASS: Core, GPU callbacks, separate-process restore; screenshots remain local')
if __name__=='__main__':main()
