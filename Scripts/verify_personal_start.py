from pathlib import Path
import subprocess,json,uuid,hashlib
from PIL import Image,ImageChops
from verify_work_hub_startup import source_files as parent_sources
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'Docs/Design/PersonalStartV1';OUT=BASE/'runtime-local'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def source_files():return sorted(set(parent_sources()+[Path(__file__),ROOT/'Scripts/check_personal_start_binding.py',ROOT/'Game/Scenes/PersonalStartTrial.tscn',BASE/'README.md']))
def main():
 subprocess.run([__import__('sys').executable,str(ROOT/'Scripts/verify_work_hub_startup.py')],cwd=ROOT,check=True)
 before={p.relative_to(ROOT).as_posix():sha(p) for p in source_files()};key=uuid.uuid4().hex
 prior=ROOT/'Docs/Design/WorkHubStartupV1/verification.json';j=json.loads(prior.read_bytes());assert j['summary']['tests']==985
 exe=ROOT/'.tools/godot-4.5.2/Godot_v4.5.2-stable_mono_win64/Godot_v4.5.2-stable_mono_win64_console.exe';images={};comparisons={};checks={}
 for case in ['job','journey']:
  folder=OUT/case;folder.mkdir(parents=True,exist_ok=True)
  for mode in ['check','restore']:
   run=subprocess.run([str(exe),'--path',str(ROOT/'Game'),'--resolution','1100x720','res://Scenes/PersonalStartTrial.tscn','--','--npc-work-'+mode,'--case='+case,'--save-key='+key],cwd=ROOT,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,creationflags=subprocess.CREATE_NO_WINDOW,timeout=180)
   log=run.stdout.decode('utf8',errors='replace');(folder/(mode+'.log')).write_bytes(log.encode());assert run.returncode==0 and 'PERSONAL_START_PASSED' in log and 'ERROR:' not in log,(case,mode,log)
   checks[case+'/'+mode]=json.loads((folder/(mode+'-checks.json')).read_bytes());print('GPU PASS personal',case,mode,flush=True)
  for n in range(5):
   for a,b in [(f's{n}',f'restored-s{n}'),('final',f'continued-s{n}')]:
    pa=folder/(a+'.png');pb=folder/(b+'.png');rect=json.loads((folder/(a+'-rect.json')).read_bytes());x,y,w,h=map(int,rect);ia=Image.open(pa).convert('RGB');ib=Image.open(pb).convert('RGB');assert ia.size==ib.size and 0<=x<x+w<=ia.width and 0<=y<y+h<=ia.height
    diff=sum(v!=(0,0,0) for v in ImageChops.difference(ia.crop((x,y,x+w,y+h)),ib.crop((x,y,x+w,y+h))).get_flattened_data());assert diff==0,(case,a,b,diff);comparisons[case+'/'+a+'/'+b]=diff
    for p in [pa,pb]:images[p.relative_to(ROOT).as_posix()]=sha(p)
 assert before=={p.relative_to(ROOT).as_posix():sha(p) for p in source_files()}
 report=dict(status='PASSED',sources=before,binaries=j['binaries'],tests=985,added=15,comparisons=comparisons,images=images,checks=checks,proofs={prior.relative_to(ROOT).as_posix():sha(prior)},visual_review=None)
 (BASE/'verification.json').write_bytes((json.dumps(report,ensure_ascii=False,indent=2)+'\n').encode());print('PASS personal independent identity,985 tests,20 exact GPU restore/continue comparisons')
if __name__=='__main__':main()
