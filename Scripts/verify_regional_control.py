"""Own Godot processes, scene callbacks and GPU images; never uses native input."""
from pathlib import Path
import hashlib,json,subprocess,time,uuid
ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'Docs/Design/RegionalControlV1'
OUT=BASE/'runtime-local'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def sources():
 return sorted(set([*ROOT.glob('Core/**/*.cs'),*ROOT.glob('Game/Scripts/**/*.cs'),*ROOT.glob('Tests/*.cs'),ROOT/'Core/HakutaiKagami.Core.csproj',ROOT/'Game/HakutaiKagami.Game.csproj',ROOT/'Game/Scenes/RegionalControlTrial.tscn',Path(__file__),ROOT/'Scripts/Show-RegionalControl.ps1',ROOT/'Scripts/check_regional_control_binding.py',ROOT/'Game/project.godot',ROOT/'global.json',ROOT/'Core/Data/owari-branches-v1.json',ROOT/'Game/Data/Geography/japan-map-v1.json']),key=str)
def assets():
 return sorted([*ROOT.glob('Game/Assets/Officer3D/**/*.glb'),*ROOT.glob('Game/Assets/Officer3D/**/*.png'),*ROOT.glob('Game/Data/Officer3D/*.json'),*ROOT.glob('Game/Shaders/Officer3D*.gdshader')],key=str)
def main():
 OUT.mkdir(parents=True,exist_ok=True)
 subprocess.run(['dotnet','build','Game/HakutaiKagami.Game.csproj','--no-restore','--verbosity','minimal'],cwd=ROOT,check=True)
 subprocess.run(['dotnet','test','Tests/HakutaiKagami.Tests.csproj','--no-restore','--verbosity','minimal','--logger','trx;LogFileName=route.trx','--results-directory',str(OUT/'tests')],cwd=ROOT,check=True)
 exe=ROOT/'.tools/godot-4.5.2/Godot_v4.5.2-stable_mono_win64/Godot_v4.5.2-stable_mono_win64_console.exe'
 startup=subprocess.STARTUPINFO();startup.dwFlags|=subprocess.STARTF_USESHOWWINDOW;startup.wShowWindow=0
 logs={};started=time.time();save_key=uuid.uuid4().hex
 for mode in ['cancel-check','redirect-check','cancel-restore','redirect-restore']:
  result=subprocess.run([str(exe),'--path',str(ROOT/'Game'),'--resolution','1400x1100','res://Scenes/RegionalControlTrial.tscn','--','--regional-'+mode,'--save-key='+save_key],cwd=ROOT,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,encoding='utf-8',errors='replace',startupinfo=startup,creationflags=subprocess.CREATE_NO_WINDOW,timeout=90)
  (OUT/(mode+'.log')).write_text(result.stdout,encoding='utf-8');logs[mode]=result.stdout
  assert result.returncode==0 and 'REGIONAL_CONTROL_PASSED' in result.stdout and 'ERROR:' not in result.stdout,result.stdout
 images={p.name:sha(p) for p in OUT.glob('*.png') if p.stat().st_mtime>=started-1}
 assert len(images)==26,images
 from PIL import Image,ImageChops
 rect=json.loads((OUT/'cancel-checks.json').read_text())['ViewportRect'];x,y,w,h=map(int,rect);crop=(x,y,x+w,y+h)
 pixel_checks={}
 for scenario in ['cancel','redirect']:
  for stage in ['transit','sealed','scheduled','applied']:
   for before,after in [(scenario+'-'+stage,scenario+'-restored-'+stage),(scenario+'-final',scenario+'-continued-'+stage)]:
    difference=ImageChops.difference(Image.open(OUT/(before+'.png')).convert('RGB').crop(crop),Image.open(OUT/(after+'.png')).convert('RGB').crop(crop))
    assert difference.getbbox() is None,f'GPU restore differs: {before}/{after}'
    pixel_checks[before+'/'+after]={'changed_pixels':0,'viewport_rect':rect}
 report={'status':'PASSED','scope':'Owari Atsuta rendezvous; cancellation at current position and next-junction return; receipt/opening/effect distinction; four saved stages per command; no new HTML report; normal Main unchanged','date':'2026-10-20','logs':logs,'pixel_checks':pixel_checks,'images':images,'checks':{name:json.loads((OUT/(name+'-checks.json')).read_text())['Checks'] for name in ['cancel','redirect','cancel-restore','redirect-restore']},'source_sha256':{p.relative_to(ROOT).as_posix():sha(p) for p in sources() if 'obj' not in p.parts and 'bin' not in p.parts},'binary_sha256':{p.relative_to(ROOT).as_posix():sha(p) for p in (ROOT/'Game/.godot/mono/temp/bin/Debug').glob('HakutaiKagami.*.dll')},'asset_sha256':{p.relative_to(ROOT).as_posix():sha(p) for p in assets()},'trx_sha256':sha(OUT/'tests/route.trx'),'visual_review':'PENDING'}
 (BASE/'verification.json').write_bytes((json.dumps(report,ensure_ascii=False,indent=2)+'\n').encode('utf-8'))
 print('PASS: Core, GPU callbacks, separate-process restore; screenshots remain local')
if __name__=='__main__':main()
