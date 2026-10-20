from pathlib import Path
import subprocess,json,hashlib,uuid,sys,re
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'Docs/Design/SceneryAppearanceV1';OUT=BASE/'runtime-local'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 OUT.mkdir(parents=True,exist_ok=True)
 subprocess.run(['dotnet','build','Game/HakutaiKagami.Game.csproj','--no-restore','--verbosity','minimal'],cwd=ROOT,check=True)
 subprocess.run(['dotnet','test','Tests/HakutaiKagami.Tests.csproj','--no-restore','--verbosity','minimal','--logger','trx;LogFileName=appearance.trx','--results-directory',str(OUT/'tests')],cwd=ROOT,check=True)
 sources=[*ROOT.glob('Core/**/*.cs'),*ROOT.glob('Tests/*.cs'),*ROOT.glob('Game/Scripts/**/*.cs'),*ROOT.glob('Core/Data/*.json'),*ROOT.glob('Game/Data/Scenery/*.json'),ROOT/'Core/HakutaiKagami.Core.csproj',ROOT/'Game/Scenes/RegionalSceneryTrial.tscn',Path(__file__),ROOT/'Game/project.godot',BASE/'source-manifest.json']
 sources=[p for p in sources if 'obj' not in p.parts and 'bin' not in p.parts];before={p.relative_to(ROOT).as_posix():sha(p) for p in sources}
 assets=[*ROOT.glob('Game/Assets/Officer3D/**/*.glb'),*ROOT.glob('Game/Assets/Officer3D/**/*.png'),*ROOT.glob('Game/Data/Officer3D/*.json'),*ROOT.glob('Game/Shaders/Officer3D*.gdshader'),*ROOT.glob('Game/Assets/Scenery/**/*.tscn'),*ROOT.glob('Game/Assets/Scenery/**/*.tres')];asset_before={p.relative_to(ROOT).as_posix():sha(p) for p in assets}
 exe=ROOT/'.tools/godot-4.5.2/Godot_v4.5.2-stable_mono_win64/Godot_v4.5.2-stable_mono_win64_console.exe';key=uuid.uuid4().hex
 startup=subprocess.STARTUPINFO();startup.dwFlags|=subprocess.STARTF_USESHOWWINDOW;startup.wShowWindow=0;logs={}
 cases=[(c,s) for c in ['forest','bridge','castle','ridge'] for s in ['skin:baseline','skin:swap-demo']]+[('forest','skin:fallback-test'),('forest','skin:invalid-test')]
 def name(c,s):return c+'-'+s.replace(':','-')
 for c,s,mode in [(c,s,m) for c,s in cases for m in ['check','restore']]+[('overview','skin:baseline','video')]:
  args=[str(exe),'--path',str(ROOT/'Game'),'--resolution','1100x720' if mode=='video' else '1400x1100']
  if mode=='video':args+=['--write-movie',str(OUT/'scenery-appearance.avi'),'--fixed-fps','24']
  args+=['res://Scenes/RegionalSceneryTrial.tscn','--','--appearance-'+mode,'--case='+c,'--skin='+s,'--save-key='+key]
  result=subprocess.run(args,cwd=ROOT,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,encoding='utf-8',errors='replace',startupinfo=startup,creationflags=subprocess.CREATE_NO_WINDOW,timeout=300)
  n=name(c,s)+'-'+mode;(OUT/(n+'.log')).write_bytes(result.stdout.encode());logs[n]=result.stdout
  assert result.returncode==0 and ('SCENERY_VIDEO_PASSED' if mode=='video' else 'SCENERY_CHECK_PASSED') in result.stdout and 'ERROR:' not in result.stdout,result.stdout
 from PIL import Image,ImageChops
 comparisons={};changes={};images={};checks={};layout=set()
 def compare(a,b,crop,equal=True):
  for n in [a,b]:images[n+'.png']=sha(OUT/(n+'.png'))
  diff=ImageChops.difference(Image.open(OUT/(a+'.png')).convert('RGB').crop(crop),Image.open(OUT/(b+'.png')).convert('RGB').crop(crop));count=sum(1 for p in diff.getdata() if p!=(0,0,0))
  if equal:assert count==0,(a,b,count);comparisons[a+'/'+b]={'changed_pixels':0,'crop':crop}
  else:assert count>500,(a,b,count);changes[a+'/'+b]={'changed_pixels':count,'crop':crop}
 for c,s in cases:
  n=name(c,s);j=json.loads((OUT/(n+'-check-checks.json')).read_bytes());x,y,w,h=map(int,j['ViewportRect']);crop=(x,y,x+w,y+h);layout.add(j['Layout'])
  if s=='skin:swap-demo':assert j['Packed']>0 and not j['Fallback'],j
  if s in ['skin:fallback-test','skin:invalid-test']:assert j['Packed']==0 and j['Fallback'],j
  for m in ['check','restore']:checks[n+'-'+m]=json.loads((OUT/(n+'-'+m+'-checks.json')).read_bytes())
  for stage in ['s0','s1','s2']:
   compare(n+'-'+stage,n+'-restored-'+stage,crop);compare(n+'-final',n+'-continued-'+stage,crop)
  if s=='skin:baseline':compare(n+'-final',n+'-swap-back',crop);compare(n+'-final',n+'-swap-preview',crop,False)
  if s in ['skin:fallback-test','skin:invalid-test']:compare(name(c,'skin:baseline')+'-final',n+'-final',crop)
 assert len(layout)==1,'Asset changes altered placement identities'
 sys.path.insert(0,str(ROOT/'.tools/video-tools'));import imageio_ffmpeg
 ffmpeg=imageio_ffmpeg.get_ffmpeg_exe();r=subprocess.run([ffmpeg,'-y','-i',str(OUT/'scenery-appearance.avi'),'-an','-c:v','libx264','-preset','medium','-crf','20','-pix_fmt','yuv420p','-movflags','+faststart',str(OUT/'scenery-appearance-short.mp4')],stdout=subprocess.PIPE,stderr=subprocess.STDOUT,encoding='utf-8',errors='replace',check=True);(OUT/'encode.log').write_bytes(r.stdout.encode())
 decoded=subprocess.run([ffmpeg,'-i',str(OUT/'scenery-appearance-short.mp4'),'-f','null','-'],stdout=subprocess.PIPE,stderr=subprocess.STDOUT,encoding='utf-8',errors='replace',check=True);assert re.search(r'frame=\s*624\b',decoded.stdout) and '1100x720' in decoded.stdout; (OUT/'video-decode-check.log').write_bytes(decoded.stdout.encode())
 assert before=={p.relative_to(ROOT).as_posix():sha(p) for p in sources},'Sources changed during verification'
 assert asset_before=={p.relative_to(ROOT).as_posix():sha(p) for p in assets},'Assets changed during verification'
 j={'status':'PASSED','sources':before,'assets':asset_before,'binaries':{p.relative_to(ROOT).as_posix():sha(p) for p in (ROOT/'Game/.godot/mono/temp/bin/Debug').glob('HakutaiKagami.*.dll')},'images':images,'pixel_checks':comparisons,'visible_changes':changes,'layout_hash':next(iter(layout)),'logs':logs,'checks':checks,'entry_tools':{n:sha(ROOT/n) for n in ['Scripts/check_appearance_binding.py','Scripts/Show-SceneryAppearance.ps1']},'trx_sha256':sha(OUT/'tests/appearance.trx'),'video':{'file':'runtime-local/scenery-appearance-short.mp4','sha256':sha(OUT/'scenery-appearance-short.mp4'),'decode_log_sha256':sha(OUT/'video-decode-check.log'),'frames_decoded':624,'fps':24,'duration_seconds':26,'width':1100,'height':720,'scope':'Appearance comparison with fixed world time, real road material and rock scenes, save/load and swap back'},'visual_review':'PENDING'}
 (BASE/'verification.json').write_bytes((json.dumps(j,ensure_ascii=False,indent=2)+'\n').encode());print('PASS appearance catalog/GPU/restore/roundtrip/fallback/video')
if __name__=='__main__':main()
