from pathlib import Path
import json,subprocess,uuid,hashlib,sys
from PIL import Image,ImageChops
from verify_field_models import source_files as parent_sources
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'Docs/Design/GroundMaterialV1';OUT=BASE/'runtime-local'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def source_files():return sorted(set(parent_sources()+[Path(__file__),ROOT/'Scripts/check_ground_material_binding.py',ROOT/'Scripts/generate_ground_material.py',ROOT/'Game/Tools/author_ground_material.gd',BASE/'README.md',BASE/'source-manifest.json']))
def main():
 OUT.mkdir(parents=True,exist_ok=True);asset=ROOT/'Game/Assets/Scenery/Materials/field-earth-grain.tres';before=sha(asset);subprocess.run([sys.executable,str(ROOT/'Scripts/generate_ground_material.py')],cwd=ROOT,check=True);assert before==sha(asset),'texture must regenerate identically'
 sources={p.relative_to(ROOT).as_posix():sha(p) for p in source_files()};key=uuid.uuid4().hex;checks={};logs={};images={};same={}
 exe=ROOT/'.tools/godot-4.5.2/Godot_v4.5.2-stable_mono_win64/Godot_v4.5.2-stable_mono_win64_console.exe'
 for mode in ['check','restore']:
  result=subprocess.run([str(exe),'--path',str(ROOT/'Game'),'--resolution','1100x720','res://Scenes/ContinuousWorldTrial.tscn','--','--continuous-'+mode,'--ground-material','--case=coverage','--save-key='+key],cwd=ROOT,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,creationflags=subprocess.CREATE_NO_WINDOW,timeout=180)
  log=result.stdout.decode('utf8',errors='replace');(OUT/(mode+'.log')).write_bytes(log.encode());logs[mode]=log;assert result.returncode==0 and 'GROUND_MATERIAL_PASSED' in log and 'ERROR:' not in log,log;checks[mode]=json.loads((OUT/(mode+'.json')).read_bytes());print('GPU PASS ground material',mode,flush=True)
 x,y,w,h=map(int,checks['check']['ViewportRect']);box=(x,y,x+w,y+h)
 def diff(a,b):
  pa=OUT/('coverage-'+a+'.png');pb=OUT/('coverage-'+b+'.png');ia=Image.open(pa).convert('RGB');ib=Image.open(pb).convert('RGB');assert ia.size==ib.size
  for p in [pa,pb]:images[p.relative_to(ROOT).as_posix()]=sha(p)
  return sum(p!=(0,0,0) for p in ImageChops.difference(ia.crop(box),ib.crop(box)).get_flattened_data())
 for i in range(4):same[str(i)]=diff(f'stage-{i}',f'restored-{i}')
 same['back']=diff('stage-0','back-0');assert set(same.values())=={0},same;assert checks['check']['UvSamples']>100000 and checks['restore']['UvSamples']>100000
 assert sources=={p.relative_to(ROOT).as_posix():sha(p) for p in source_files()}
 j={'status':'PASSED','sources':sources,'binaries':{p.relative_to(ROOT).as_posix():sha(p) for p in (ROOT/'Game/.godot/mono/temp/bin/Debug').glob('HakutaiKagami.*.dll')},'comparisons':same,'images':images,'checks':checks,'logs':logs,'texture_sha256':sha(asset),'visual_review':None}
 (BASE/'verification.json').write_bytes((json.dumps(j,ensure_ascii=False,indent=2)+'\n').encode());print('PASS regenerated texture, actual mesh UV periods and five exact restored views',flush=True)
if __name__=='__main__':main()
