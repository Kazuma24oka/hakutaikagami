from pathlib import Path
import subprocess,sys,json,hashlib,uuid
from xml.etree import ElementTree as ET
from verify_officer_loop import source_files as parent_sources
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'Docs/Design/MissionEntryV1';OUT=BASE/'runtime-local'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def source_files():return sorted(set(parent_sources()+[Path(__file__),ROOT/'Scripts/check_mission_entry_binding.py',ROOT/'Scripts/Show-MissionEntry.ps1',ROOT/'Game/Scenes/MissionEntryTrial.tscn',BASE/'source-manifest.json',ROOT/'Docs/Development/OPERATING_POLICY.md']))
def main():
 OUT.mkdir(parents=True,exist_ok=True);quick='--quick' in sys.argv
 if not quick:
  subprocess.run(['dotnet','build','Game/HakutaiKagami.Game.csproj','--no-restore','--verbosity','minimal'],cwd=ROOT,check=True)
  subprocess.run(['dotnet','test','Tests/HakutaiKagami.Tests.csproj','--no-restore','--verbosity','minimal','--logger','trx;LogFileName=mission-entry.trx','--results-directory',str(OUT/'tests')],cwd=ROOT,check=True)
 before={p.relative_to(ROOT).as_posix():sha(p) for p in source_files()};key=uuid.uuid4().hex
 exe=ROOT/'.tools/godot-4.5.2/Godot_v4.5.2-stable_mono_win64/Godot_v4.5.2-stable_mono_win64_console.exe'
 cases=['success','expiry','refuse','self'];chosen=next((a[7:] for a in sys.argv if a.startswith('--case=')),None)
 if chosen:cases=[chosen]
 checks={};logs={}
 def run(args,name,marker):
  r=subprocess.run([str(exe),'--path',str(ROOT/'Game'),'--resolution','1100x720',*args],cwd=ROOT,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,encoding='utf8',errors='replace',creationflags=subprocess.CREATE_NO_WINDOW,timeout=180)
  (OUT/(name+'.log')).write_bytes(r.stdout.encode());assert r.returncode==0 and marker in r.stdout and 'ERROR:' not in r.stdout,(name,r.stdout)
  logs[name]=r.stdout;print('GPU PASS',name,flush=True)
 for c in cases:
  for mode in ['check','restore']:
   name=c+'-'+mode;run(['res://Scenes/MissionEntryTrial.tscn','--','--mission-entry','--entry-'+mode,'--case='+c,'--save-key='+key],name,'MISSION_ENTRY_PASSED')
   checks[name]=json.loads((OUT/(name+'-checks.json')).read_bytes())
 if quick:return
 from PIL import Image,ImageChops
 images={};comparisons={}
 def compare(a,b,rect):
  x,y,w,h=map(int,rect);crop=(x,y,x+w,y+h)
  ia=Image.open(OUT/(a+'.png')).convert('RGB');ib=Image.open(OUT/(b+'.png')).convert('RGB');assert ia.size==ib.size
  assert x>=0 and y>=0 and x+w<=ia.width and y+h<=ia.height
  count=sum(p!=(0,0,0) for p in ImageChops.difference(ia.crop(crop),ib.crop(crop)).get_flattened_data());assert count==0,(a,b,count)
  comparisons[a+'/'+b]=count
  for n in [a,b]:images[n+'.png']=sha(OUT/(n+'.png'))
 for c in cases:
  for i in range(3):
   compare(c+'-s'+str(i),c+'-restored-s'+str(i),json.loads((OUT/(c+'-s'+str(i)+'-rect.json')).read_bytes()))
   compare(c+'-final',c+'-continued-s'+str(i),json.loads((OUT/(c+'-final-rect.json')).read_bytes()))
  images[c+'-draft.png']=sha(OUT/(c+'-draft.png'))
 run(['res://Scenes/OfficerLoopTrial.tscn','--','--officer-loop','--loop-check','--case=roundtrip','--save-key='+key],'legacy-loop','OFFICER_LOOP_PASSED')
 assert before=={p.relative_to(ROOT).as_posix():sha(p) for p in source_files()},'Sources changed during validation'
 counters=ET.parse(OUT/'tests/mission-entry.trx').find('.//{*}Counters').attrib;assert int(counters['failed'])==0 and int(counters['passed'])>=802
 j={'status':'PASSED','sources':before,'binaries':{p.relative_to(ROOT).as_posix():sha(p) for p in (ROOT/'Game/.godot/mono/temp/bin/Debug').glob('HakutaiKagami.*.dll')},'summary':{'tests':counters,'gpu_processes':9,'cases':4,'save_stages':12,'equal_comparisons':len(comparisons),'images':len(images)},'comparisons':comparisons,'images':images,'checks':checks,'logs':logs,'trx_sha256':sha(OUT/'tests/mission-entry.trx'),'visual_review':None}
 (BASE/'verification.json').write_bytes((json.dumps(j,ensure_ascii=False,indent=2)+'\n').encode());print('PASS mission editor and restored UI binding',flush=True)
if __name__=='__main__':main()
