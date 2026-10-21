from pathlib import Path
import json,subprocess,uuid,hashlib
from PIL import Image,ImageChops
from verify_camera_pan import source_files as parent_sources
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'Docs/Design/VisualAcceptanceV1';OUT=BASE/'runtime-local'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def source_files():return sorted(set(parent_sources()+[Path(__file__),BASE/'README.md']))
def main():
 OUT.mkdir(parents=True,exist_ok=True);sources={p.relative_to(ROOT).as_posix():sha(p) for p in source_files()};images={};same={};logs={};checks={}
 exe=ROOT/'.tools/godot-4.5.2/Godot_v4.5.2-stable_mono_win64/Godot_v4.5.2-stable_mono_win64_console.exe';scene=ROOT/'Docs/Design/ContinuousWorldV1/runtime-local'
 for case in ['move','order','bridge']:
  key=uuid.uuid4().hex
  for mode in ['check','restore']:
   result=subprocess.run([str(exe),'--path',str(ROOT/'Game'),'--resolution','1100x720','res://Scenes/ContinuousWorldTrial.tscn','--','--continuous-'+mode,'--case='+case,'--save-key='+key],cwd=ROOT,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,creationflags=subprocess.CREATE_NO_WINDOW,timeout=180)
   log=result.stdout.decode('utf8',errors='replace');(OUT/(case+'-'+mode+'.log')).write_bytes(log.encode());logs[case+'-'+mode]=log;assert result.returncode==0 and 'CONTINUOUS_CHECK_PASSED '+case in log and 'ERROR:' not in log,log;print('GPU PASS visual acceptance',case,mode,flush=True)
  checks[case]=json.loads((scene/(case+'-check-checks.json')).read_bytes());x,y,w,h=map(int,checks[case]['ViewportRect']);box=(x,y,x+w,y+h)
  for p in scene.glob(case+'-*.png'):(OUT/p.name).write_bytes(p.read_bytes())
  def compare(a,b):
   pa=OUT/(case+'-'+a+'.png');pb=OUT/(case+'-'+b+'.png');ia=Image.open(pa).convert('RGB');ib=Image.open(pb).convert('RGB');assert ia.size==ib.size
   for p in [pa,pb]:images[p.relative_to(ROOT).as_posix()]=sha(p)
   diff=sum(p!=(0,0,0) for p in ImageChops.difference(ia.crop(box),ib.crop(box)).get_flattened_data());assert diff==0,(case,a,b,diff);same[case+'/'+a+'/'+b]=diff
  for i in range(3):compare('s'+str(i),'restored-s'+str(i));compare('final','continued-s'+str(i))
  compare('final','quality-back');compare('final','zoom-back')
 assert sources=={p.relative_to(ROOT).as_posix():sha(p) for p in source_files()}
 report={'status':'PASSED','sources':sources,'binaries':{p.relative_to(ROOT).as_posix():sha(p) for p in (ROOT/'Game/.godot/mono/temp/bin/Debug').glob('HakutaiKagami.*.dll')},'comparisons':same,'images':images,'checks':checks,'logs':logs,'visual_review':None}
 (BASE/'verification.json').write_bytes((json.dumps(report,ensure_ascii=False,indent=2)+'\n').encode());print('PASS move/courier/bridge, nine saves and 24 exact restored/continued views',flush=True)
if __name__=='__main__':main()
