from pathlib import Path
import json,subprocess,uuid,hashlib
from PIL import Image,ImageChops
from verify_force_lod import source_files as parent_sources
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'Docs/Design/FieldModelsV1';OUT=BASE/'runtime-local'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def source_files():return sorted(set(parent_sources()+[Path(__file__),ROOT/'Scripts/check_field_models_binding.py',ROOT/'Scripts/generate_field_models.py',ROOT/'Game/Tools/author_field_rocks.gd',BASE/'README.md',BASE/'source-manifest.json']))
def main():
 OUT.mkdir(parents=True,exist_ok=True);assets=sorted([*(ROOT/'Game/Assets/Scenery/Models').glob('field-*.tscn'),*(ROOT/'Game/Assets/Scenery/Models').glob('field-*.tres')]);hashes={p:sha(p) for p in assets};subprocess.run([str(Path(__import__('sys').executable)),str(ROOT/'Scripts/generate_field_models.py')],cwd=ROOT,check=True);assert hashes=={p:sha(p) for p in assets},'model generator must be repeatable'
 sources={p.relative_to(ROOT).as_posix():sha(p) for p in source_files()};key=uuid.uuid4().hex;checks={};logs={};images={};same={};changes={}
 exe=ROOT/'.tools/godot-4.5.2/Godot_v4.5.2-stable_mono_win64/Godot_v4.5.2-stable_mono_win64_console.exe'
 for mode in ['check','restore']:
  result=subprocess.run([str(exe),'--path',str(ROOT/'Game'),'--resolution','1100x720','res://Scenes/ContinuousWorldTrial.tscn','--','--continuous-'+mode,'--field-models','--case=coverage','--save-key='+key],cwd=ROOT,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,creationflags=subprocess.CREATE_NO_WINDOW,timeout=180)
  log=result.stdout.decode('utf8',errors='replace');(OUT/(mode+'.log')).write_bytes(log.encode());logs[mode]=log;assert result.returncode==0 and 'FIELD_MODELS_PASSED' in log and 'ERROR:' not in log,log;checks[mode]=json.loads((OUT/(mode+'.json')).read_bytes());print('GPU PASS field models',mode,flush=True)
 x,y,w,h=map(int,checks['check']['ViewportRect']);box=(x,y,x+w,y+h)
 def diff(a,b):
  pa=OUT/('coverage-'+a+'.png');pb=OUT/('coverage-'+b+'.png');ia=Image.open(pa).convert('RGB');ib=Image.open(pb).convert('RGB');assert ia.size==ib.size
  for p in [pa,pb]:images[p.relative_to(ROOT).as_posix()]=sha(p)
  return sum(p!=(0,0,0) for p in ImageChops.difference(ia.crop(box),ib.crop(box)).get_flattened_data())
 for i in range(3):same[f'back-{i}']=diff(f'stage-{i}',f'back-{i}');same[f'restore-{i}']=diff(f'stage-{i}',f'restored-{i}');changes[str(i)]=diff(f'old-{i}',f'stage-{i}')
 assert set(same.values())=={0},same;assert all(d>0 for d in changes.values()),changes;assert sources=={p.relative_to(ROOT).as_posix():sha(p) for p in source_files()}
 j={'status':'PASSED','sources':sources,'binaries':{p.relative_to(ROOT).as_posix():sha(p) for p in (ROOT/'Game/.godot/mono/temp/bin/Debug').glob('HakutaiKagami.*.dll')},'comparisons':same,'visible_changes':changes,'images':images,'checks':checks,'logs':logs,'visual_review':None}
 (BASE/'verification.json').write_bytes((json.dumps(j,ensure_ascii=False,indent=2)+'\n').encode());print('PASS deterministic models, bounds/fallbacks, three changed scenes and six exact restored views',flush=True)
if __name__=='__main__':main()
