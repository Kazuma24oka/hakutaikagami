"""Own Godot processes, scene callbacks and GPU images; never uses native input."""
from pathlib import Path
import hashlib,json,subprocess,time,uuid
ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'Docs/Design/RegionalBranchV1'
OUT=BASE/'runtime-local'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def sources():
 return sorted(set([*ROOT.glob('Core/**/*.cs'),*ROOT.glob('Game/Scripts/**/*.cs'),*ROOT.glob('Tests/*.cs'),ROOT/'Core/HakutaiKagami.Core.csproj',ROOT/'Game/HakutaiKagami.Game.csproj',ROOT/'Game/Scenes/RegionalBranchTrial.tscn',Path(__file__),ROOT/'Scripts/Show-RegionalBranch.ps1',ROOT/'Scripts/check_regional_branch_binding.py',ROOT/'Game/project.godot',ROOT/'global.json',ROOT/'Core/Data/owari-route-v1.json',ROOT/'Core/Data/owari-branches-v1.json',ROOT/'Game/Data/Geography/japan-map-v1.json',ROOT/'Scripts/build_regional_branches.py']),key=str)
def assets():
 return sorted([*ROOT.glob('Game/Assets/Officer3D/**/*.glb'),*ROOT.glob('Game/Assets/Officer3D/**/*.png'),*ROOT.glob('Game/Data/Officer3D/*.json'),*ROOT.glob('Game/Shaders/Officer3D*.gdshader')],key=str)
def main():
 OUT.mkdir(parents=True,exist_ok=True)
 subprocess.run(['dotnet','build','Game/HakutaiKagami.Game.csproj','--no-restore','--verbosity','minimal'],cwd=ROOT,check=True)
 subprocess.run(['dotnet','test','Tests/HakutaiKagami.Tests.csproj','--no-restore','--verbosity','minimal','--logger','trx;LogFileName=route.trx','--results-directory',str(OUT/'tests')],cwd=ROOT,check=True)
 exe=ROOT/'.tools/godot-4.5.2/Godot_v4.5.2-stable_mono_win64/Godot_v4.5.2-stable_mono_win64_console.exe'
 startup=subprocess.STARTUPINFO();startup.dwFlags|=subprocess.STARTF_USESHOWWINDOW;startup.wShowWindow=0
 logs={};started=time.time();save_key=uuid.uuid4().hex
 for mode in ['check','restore','open','trapped']:
  result=subprocess.run([str(exe),'--path',str(ROOT/'Game'),'--resolution','1400x1050','res://Scenes/RegionalBranchTrial.tscn','--','--branchgeo-'+mode,'--save-key='+save_key]+(['--open-pass'] if mode=='open' else []),cwd=ROOT,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,encoding='utf-8',errors='replace',startupinfo=startup,creationflags=subprocess.CREATE_NO_WINDOW,timeout=90)
  (OUT/(mode+'.log')).write_text(result.stdout,encoding='utf-8');logs[mode]=result.stdout
  assert result.returncode==0 and 'REGIONAL_BRANCH_PASSED' in result.stdout and 'ERROR:' not in result.stdout,result.stdout
 images={p.name:sha(p) for p in OUT.glob('*.png') if p.stat().st_mtime>=started-1}
 assert len(images)==19,images
 from PIL import Image,ImageChops
 rect=json.loads((OUT/'checks.json').read_text())['ViewportRect'];x,y,w,h=map(int,rect);crop=(x,y,x+w,y+h)
 pixel_checks={}
 for before,after,full in [('closed-unseen-officer','open-unseen-officer',True),('closed-unseen-lord','open-unseen-lord',True),('closed-unseen-officer','same-restored-unseen',False),('closed-unseen-officer','restored-unseen',False),('detour','same-restored-detour',False),('detour','restored-detour',False),('final','continued-unseen',False),('final','continued-detour',False)]:
  a=Image.open(OUT/(before+'.png')).convert('RGB');b=Image.open(OUT/(after+'.png')).convert('RGB')
  if not full:a=a.crop(crop);b=b.crop(crop)
  difference=ImageChops.difference(a,b);assert difference.getbbox() is None,f'GPU differs: {before}/{after}'
  pixel_checks[before+'/'+after]={'changed_pixels':0,'full_frame':full,'viewport_rect':None if full else rect}
 report={'status':'PASSED','scope':'Owari coarse geographic branches: unknown closure non-disclosure, physical observation, detour, delayed reports, safe stop, restore before observation and during detour. Roads and closure are prototype conditions, not historical reconstruction.','date':'2026-10-20','logs':logs,'pixel_checks':pixel_checks,'images':images,'checks':{name:json.loads((OUT/(name+'.json')).read_text())['Checks'] for name in ['checks','restore','open','trapped-checks']},'source_sha256':{p.relative_to(ROOT).as_posix():sha(p) for p in sources() if 'obj' not in p.parts and 'bin' not in p.parts},'binary_sha256':{p.relative_to(ROOT).as_posix():sha(p) for p in (ROOT/'Game/.godot/mono/temp/bin/Debug').glob('HakutaiKagami.*.dll')},'asset_sha256':{p.relative_to(ROOT).as_posix():sha(p) for p in assets()},'trx_sha256':sha(OUT/'tests/route.trx'),'visual_review':'PENDING'}
 (BASE/'verification.json').write_bytes((json.dumps(report,ensure_ascii=False,indent=2)+'\n').encode('utf-8'))
 print('PASS: Core, GPU callbacks, separate-process restore; screenshots remain local')
if __name__=='__main__':main()
