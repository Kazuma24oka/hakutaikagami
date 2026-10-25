from pathlib import Path
import json,subprocess,uuid,hashlib
from PIL import Image,ImageChops
from verify_camera_continuity import source_files as parent_sources
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'Docs/Design/ForceLodV1';OUT=BASE/'runtime-local'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def source_files():return sorted(set(parent_sources()+[Path(__file__),ROOT/'Scripts/check_force_lod_binding.py',BASE/'README.md']))
def main():
 OUT.mkdir(parents=True,exist_ok=True);sources={p.relative_to(ROOT).as_posix():sha(p) for p in source_files()};key=uuid.uuid4().hex;checks={};logs={};images={};same={}
 exe=ROOT/'.tools/godot-4.5.2/Godot_v4.5.2-stable_mono_win64/Godot_v4.5.2-stable_mono_win64_console.exe'
 for mode in ['check','restore']:
  result=subprocess.run([str(exe),'--path',str(ROOT/'Game'),'--resolution','1100x720','res://Scenes/ContinuousWorldTrial.tscn','--','--continuous-'+mode,'--force-lod','--case=coverage','--save-key='+key],cwd=ROOT,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,creationflags=subprocess.CREATE_NO_WINDOW,timeout=180)
  log=result.stdout.decode('utf8',errors='replace');(OUT/(mode+'.log')).write_bytes(log.encode());logs[mode]=log;assert result.returncode==0 and 'FORCE_LOD_PASSED' in log and 'ERROR:' not in log,log;checks[mode]=json.loads((OUT/(mode+'.json')).read_bytes());print('GPU PASS force LOD',mode,flush=True)
 x,y,w,h=map(int,checks['check']['ViewportRect']);box=(x,y,x+w,y+h)
 for i in range(4):
  pa=OUT/f'coverage-stage-{i}.png';pb=OUT/f'coverage-restored-{i}.png';ia=Image.open(pa).convert('RGB');ib=Image.open(pb).convert('RGB');assert ia.size==ib.size
  for p in [pa,pb]:images[p.relative_to(ROOT).as_posix()]=sha(p)
  same[str(i)]=sum(p!=(0,0,0) for p in ImageChops.difference(ia.crop(box),ib.crop(box)).get_flattened_data())
 assert set(same.values())=={0},same;assert sources=={p.relative_to(ROOT).as_posix():sha(p) for p in source_files()}
 j={'status':'PASSED','sources':sources,'binaries':{p.relative_to(ROOT).as_posix():sha(p) for p in (ROOT/'Game/.godot/mono/temp/bin/Debug').glob('HakutaiKagami.*.dll')},'comparisons':same,'images':images,'checks':checks,'logs':logs,'visual_review':None}
 (BASE/'verification.json').write_bytes((json.dumps(j,ensure_ascii=False,indent=2)+'\n').encode());print('PASS observed force markers and four exact restored views',flush=True)
if __name__=='__main__':main()
