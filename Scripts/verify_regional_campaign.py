from pathlib import Path
import subprocess,json,hashlib,uuid,time
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'Docs/Design/RegionalCampaignV1/runtime-local'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 OUT.mkdir(parents=True,exist_ok=True)
 subprocess.run(['dotnet','build','Game/HakutaiKagami.Game.csproj','--no-restore','--verbosity','minimal'],cwd=ROOT,check=True)
 subprocess.run(['dotnet','test','Tests/HakutaiKagami.Tests.csproj','--no-restore','--verbosity','minimal','--logger','trx;LogFileName=campaign.trx','--results-directory',str(OUT/'tests')],cwd=ROOT,check=True)
 exe=ROOT/'.tools/godot-4.5.2/Godot_v4.5.2-stable_mono_win64/Godot_v4.5.2-stable_mono_win64_console.exe'
 startup=subprocess.STARTUPINFO();startup.dwFlags|=subprocess.STARTF_USESHOWWINDOW;startup.wShowWindow=0
 sources=[*ROOT.glob('Core/**/*.cs'),*ROOT.glob('Tests/*.cs'),*ROOT.glob('Game/Scripts/**/*.cs'),ROOT/'Game/Scenes/RegionalCampaignTrial.tscn',ROOT/'Scripts/verify_regional_campaign.py',ROOT/'Core/Data/owari-branches-v1.json',ROOT/'Core/Data/regional-expansion-v1.json',ROOT/'Core/HakutaiKagami.Core.csproj',ROOT/'Game/project.godot',ROOT/'global.json',ROOT/'Game/Data/Geography/japan-map-v1.json']
 sources=[p for p in sources if 'obj' not in p.parts and 'bin' not in p.parts]
 before={str(p.relative_to(ROOT)).replace(chr(92),'/'):sha(p) for p in sources}
 assets=[*ROOT.glob('Game/Assets/Officer3D/**/*.glb'),*ROOT.glob('Game/Assets/Officer3D/**/*.png'),*ROOT.glob('Game/Data/Officer3D/*.json'),*ROOT.glob('Game/Shaders/Officer3D*.gdshader')]
 logs={};key=uuid.uuid4().hex;started=time.time()
 for mode in ['check','restore','flow','flow-restore']:
  r=subprocess.run([str(exe),'--path',str(ROOT/'Game'),'--resolution','1400x1100','res://Scenes/RegionalCampaignTrial.tscn','--','--campaign-'+mode,'--save-key='+key],cwd=ROOT,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,encoding='utf-8',errors='replace',startupinfo=startup,creationflags=subprocess.CREATE_NO_WINDOW,timeout=90)
  (OUT/(mode+'.log')).write_bytes(r.stdout.encode());logs[mode]=r.stdout
  assert r.returncode==0 and 'REGIONAL_CAMPAIGN_PASSED' in r.stdout and 'ERROR:' not in r.stdout,r.stdout
 from PIL import Image,ImageChops
 rect=json.loads((OUT/'check-checks.json').read_text())['ViewportRect'];x,y,w,h=map(int,rect);crop=(x,y,x+w,y+h)
 comparisons={}
 for stage in ['travel','wait','meeting','received','final','retry-enroute','release-inspected','regions-mid','regions-final']:
  a=Image.open(OUT/(stage+'.png')).convert('RGB').crop(crop);b=Image.open(OUT/('restored-'+stage+'.png')).convert('RGB').crop(crop)
  diff=ImageChops.difference(a,b);assert diff.getbbox() is None,stage
  comparisons[stage]={'changed_pixels':0,'rect':rect}
  if stage in ['retry-enroute','release-inspected','regions-mid','regions-final']:
   diff=ImageChops.difference(Image.open(OUT/(stage+'-final.png')).convert('RGB').crop(crop),Image.open(OUT/('continued-'+stage+'.png')).convert('RGB').crop(crop));assert diff.getbbox() is None,stage+' continued'
   comparisons[stage+' continued']={'changed_pixels':0,'rect':rect}
 for observer in ['officer','lord']:
  diff=ImageChops.difference(Image.open(OUT/('privacy-'+observer+'-False.png')).convert('RGB'),Image.open(OUT/('privacy-'+observer+'-True.png')).convert('RGB'));assert diff.getbbox() is None,observer+' privacy'
  comparisons[observer+' privacy']={'changed_pixels':0,'whole_frame':True}
 assert before=={str(p.relative_to(ROOT)).replace(chr(92),'/'):sha(p) for p in sources},'Sources changed during verification'
 report={'status':'PASSED','logs':logs,'pixel_checks':comparisons,'checks':{m:json.loads((OUT/(m+'-checks.json')).read_text())['Checks'] for m in ['check','restore','flow','flow-restore']},'sources':{str(p.relative_to(ROOT)).replace(chr(92),'/'):sha(p) for p in sources if 'obj' not in p.parts and 'bin' not in p.parts},'assets':{str(p.relative_to(ROOT)).replace(chr(92),'/'):sha(p) for p in assets},'binaries':{str(p.relative_to(ROOT)).replace(chr(92),'/'):sha(p) for p in (ROOT/'Game/.godot/mono/temp/bin/Debug').glob('HakutaiKagami.*.dll')},'trx_sha256':sha(OUT/'tests/campaign.trx'),'images':{p.name:sha(p) for p in OUT.glob('*.png') if p.stat().st_mtime>=started-1},'entry_tools':{name:sha(ROOT/name) for name in ['Scripts/Show-RegionalCampaign.ps1','Scripts/check_regional_campaign_binding.py','Scripts/build_regional_expansion.py']},'visual_review':'PENDING'}
 (OUT.parent/'verification.json').write_bytes((json.dumps(report,ensure_ascii=False,indent=2)+'\n').encode())
 print('PASSED: campaign Core/GPU/independent restoration')
if __name__=='__main__':main()
