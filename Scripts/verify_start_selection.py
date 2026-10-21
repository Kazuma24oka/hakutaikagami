from pathlib import Path
import subprocess,json,uuid,sys
from PIL import Image,ImageChops
from verify_personal_start import source_files as parent_sources,sha
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'Docs/Design/StartSelectionV1';OUT=BASE/'runtime-local'
def source_files():return sorted(set(parent_sources()+[Path(__file__),ROOT/'Scripts/check_start_selection_binding.py',BASE/'README.md']))
def main():
 subprocess.run([sys.executable,str(ROOT/'Scripts/verify_personal_start.py')],cwd=ROOT,check=True)
 before={p.relative_to(ROOT).as_posix():sha(p) for p in source_files()};key=uuid.uuid4().hex;OUT.mkdir(parents=True,exist_ok=True)
 exe=ROOT/'.tools/godot-4.5.2/Godot_v4.5.2-stable_mono_win64/Godot_v4.5.2-stable_mono_win64_console.exe';checks={};images={};comparisons={}
 for mode in ['check','restore']:
  run=subprocess.run([str(exe),'--path',str(ROOT/'Game'),'--resolution','1100x720','--','--start-selection-'+mode,'--save-key='+key],cwd=ROOT,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,creationflags=subprocess.CREATE_NO_WINDOW,timeout=180)
  log=run.stdout.decode('utf8',errors='replace');(OUT/(mode+'.log')).write_bytes(log.encode());assert run.returncode==0 and 'START_SELECTION_PASSED' in log and 'ERROR:' not in log,(mode,log)
  checks[mode]=json.loads((OUT/(mode+'-checks.json')).read_bytes());print('GPU PASS normal start selection',mode,flush=True)
 for n in range(5):
  for a,b in [(f's{n}',f'restored-s{n}'),('final',f'continued-s{n}')]:
   pa=OUT/(a+'.png');pb=OUT/(b+'.png');x,y,w,h=map(int,json.loads((OUT/(a+'-rect.json')).read_bytes()));ia=Image.open(pa).convert('RGB');ib=Image.open(pb).convert('RGB');assert ia.size==ib.size and 0<=x<x+w<=ia.width and 0<=y<y+h<=ia.height
   diff=sum(v!=(0,0,0) for v in ImageChops.difference(ia.crop((x,y,x+w,y+h)),ib.crop((x,y,x+w,y+h))).get_flattened_data());assert diff==0,(a,b,diff);comparisons[a+'/'+b]=diff
   for p in [pa,pb]:images[p.relative_to(ROOT).as_posix()]=sha(p)
 for name in ['form','second-person']:
  p=OUT/(name+'.png');images[p.relative_to(ROOT).as_posix()]=sha(p)
 assert before=={p.relative_to(ROOT).as_posix():sha(p) for p in source_files()}
 prior=ROOT/'Docs/Design/PersonalStartV1/verification.json';j=json.loads(prior.read_bytes());assert j['tests']==985 and len(j['comparisons'])==20
 report=dict(status='PASSED',sources=before,binaries=j['binaries'],tests=985,comparisons=comparisons,images=images,checks=checks,proofs={prior.relative_to(ROOT).as_posix():sha(prior)},visual_review=None)
 (BASE/'verification.json').write_bytes((json.dumps(report,ensure_ascii=False,indent=2)+'\n').encode());print('PASS startup selection, preserved runs, 10 exact view comparisons and prior56 on current binary')
if __name__=='__main__':main()
