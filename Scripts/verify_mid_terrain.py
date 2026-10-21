from pathlib import Path
import json,subprocess,uuid,hashlib,sys
from PIL import Image,ImageChops
from verify_work_hub_startup import source_files as parent_sources
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'Docs/Design/MidTerrainV1';OUT=BASE/'runtime-local'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def source_files():return sorted(set(parent_sources()+[Path(__file__),ROOT/'Scripts/check_mid_terrain_binding.py',BASE/'README.md']))
def main():
 OUT.mkdir(parents=True,exist_ok=True);before={p.relative_to(ROOT).as_posix():sha(p) for p in source_files()};logs={};checks={};key=uuid.uuid4().hex
 exe=ROOT/'.tools/godot-4.5.2/Godot_v4.5.2-stable_mono_win64/Godot_v4.5.2-stable_mono_win64_console.exe'
 for mode in ['check','restore']:
  run=subprocess.run([str(exe),'--path',str(ROOT/'Game'),'--resolution','1100x720','res://Scenes/ContinuousWorldTrial.tscn','--','--continuous-'+mode,'--mid-terrain','--case=coverage','--save-key='+key],cwd=ROOT,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,creationflags=subprocess.CREATE_NO_WINDOW,timeout=180)
  log=run.stdout.decode('utf8',errors='replace');logs[mode]=log;(OUT/(mode+'.log')).write_bytes(log.encode());assert run.returncode==0 and 'MID_TERRAIN_PASSED' in log and 'ERROR:' not in log,log;checks[mode]=json.loads((OUT/(mode+'.json')).read_bytes());print('GPU PASS mid',mode,flush=True)
 rect=checks['check']['ViewportRect'];x,y,w,h=map(int,rect);box=(x,y,x+w,y+h);images={};comparisons={}
 for i in range(7):
  a=OUT/('coverage-stage-'+str(i)+'.png');b=OUT/('coverage-restored-'+str(i)+'.png');ia=Image.open(a).convert('RGB');ib=Image.open(b).convert('RGB');assert ia.size==ib.size
  diff=sum(p!=(0,0,0) for p in ImageChops.difference(ia.crop(box),ib.crop(box)).get_flattened_data());assert diff==0,(i,diff);comparisons[str(i)]=diff
  for p in [a,b]:images[p.relative_to(ROOT).as_posix()]=sha(p)
 assert before=={p.relative_to(ROOT).as_posix():sha(p) for p in source_files()}
 timings=checks['check']['RefreshMs'];ordered=sorted(timings)
 report={'status':'PASSED','sources':before,'images':images,'comparisons':comparisons,'checks':checks,'logs':logs,'binaries':{p.relative_to(ROOT).as_posix():sha(p) for p in (ROOT/'Game/.godot/mono/temp/bin/Debug').glob('HakutaiKagami.*.dll')},'performance':{'samples':len(timings),'refresh_ms_max':max(timings),'refresh_ms_p95':ordered[min(len(ordered)-1,int(len(ordered)*.95))],'measurement':'CPU refresh incl mesh rebuild; not GPU frame time'},'visual_review':None}
 (BASE/'verification.json').write_bytes((json.dumps(report,ensure_ascii=False,indent=2)+'\n').encode());print('PASS seven scales actual GPU exact restored pixels',flush=True)
if __name__=='__main__':main()
