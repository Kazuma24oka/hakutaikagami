from pathlib import Path
import subprocess,sys,json,hashlib,uuid
from xml.etree import ElementTree as ET
from verify_assessment_notice import source_files as parent_sources
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'Docs/Design/TaskEntryV1';OUT=BASE/'runtime-local'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def source_files():return sorted(set(parent_sources()+[Path(__file__),ROOT/'Scripts/check_task_entry_binding.py',ROOT/'Game/Scenes/AssessmentNoticeTrial.tscn',BASE/'source-manifest.json',BASE/'README.md',ROOT/'Game/Scenes/Main.tscn']))
def main():
 OUT.mkdir(parents=True,exist_ok=True);quick='--quick' in sys.argv
 if not quick:
  subprocess.run(['dotnet','build','Game/HakutaiKagami.Game.csproj','--no-restore','--verbosity','minimal'],cwd=ROOT,check=True)
  subprocess.run(['dotnet','test','Tests/HakutaiKagami.Tests.csproj','--no-restore','--verbosity','minimal','--logger','trx;LogFileName=task-entry.trx','--results-directory',str(OUT/'tests')],cwd=ROOT,check=True)
 before={p.relative_to(ROOT).as_posix():sha(p) for p in source_files()};key=uuid.uuid4().hex
 exe=ROOT/'.tools/godot-4.5.2/Godot_v4.5.2-stable_mono_win64/Godot_v4.5.2-stable_mono_win64_console.exe'
 cases=['success','own','expiry','report-miss'];chosen=next((a[7:] for a in sys.argv if a.startswith('--case=')),None)
 if chosen:cases=[chosen]
 checks={};logs={}
 def run(args,name,marker):
  r=subprocess.run([str(exe),'--path',str(ROOT/'Game'),'--resolution','1100x720',*args],cwd=ROOT,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,creationflags=subprocess.CREATE_NO_WINDOW,timeout=180)
  try:log=r.stdout.decode('utf8')
  except UnicodeDecodeError:log=r.stdout.decode('cp932',errors='replace')
  (OUT/(name+'.log')).write_bytes(log.encode());assert r.returncode==0 and marker in log and 'ERROR:' not in log,(name,log)
  logs[name]=log;print('GPU PASS',name,flush=True)
 for c in cases:
  for mode in ['check','restore']:
   name=c+'-'+mode;run(['--','--task-entry-'+mode,'--case='+c,'--save-key='+key],name,'ASSESSMENT_NOTICE_PASSED')
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
  for i in range(4):
   compare(c+'-s'+str(i),c+'-restored-s'+str(i),json.loads((OUT/(c+'-s'+str(i)+'-rect.json')).read_bytes()))
   compare(c+'-final',c+'-continued-s'+str(i),json.loads((OUT/(c+'-final-rect.json')).read_bytes()))

 run(['res://Scenes/AssessmentNoticeTrial.tscn','--','--notice-check','--case=own','--save-key='+key],'standalone-notice','ASSESSMENT_NOTICE_PASSED')
 preserved={}
 for flag,marker,file in [('--smoke-test','SCENE_SMOKE_PASS','godot-foundation.png'),('--tax-smoke-test','TAX_SCENE_PASS','tax-vertical.png'),('--shared-protocol-smoke-test','SHARED_PROTOCOL_GUI_PASS','shared-protocol.png')]:
  evidence=ROOT/'Docs/Proposals/Evidence'/file;original=evidence.read_bytes() if evidence.exists() else None
  try:
   run(['--',flag],'legacy-gpu'+flag,marker)
   target=OUT/('legacy-'+file);target.write_bytes(evidence.read_bytes());images[target.name]=sha(target)
  finally:
   if original is not None:evidence.write_bytes(original)
   elif evidence.exists():evidence.unlink()
  assert (evidence.read_bytes() if evidence.exists() else None)==original
  preserved[file]=hashlib.sha256(original).hexdigest() if original is not None else None
 for c in cases:
  host=json.loads((OUT/(c+'-host.json')).read_bytes());assert host['Lazy'] and host['Main'] and host['Titles'][:3]==['武将・城','徴税・保存検証','応答・代理・共通検証'] and host['Titles'][-1]=='武将の任務'
  for name in ['startup','entry-open']:images[c+'-'+name+'.png']=sha(OUT/(c+'-'+name+'.png'))
 assert before=={p.relative_to(ROOT).as_posix():sha(p) for p in source_files()},'Sources changed during validation'
 counters=ET.parse(OUT/'tests/task-entry.trx').find('.//{*}Counters').attrib;assert int(counters['failed'])==0 and int(counters['passed'])>=879
 j={'status':'PASSED','sources':before,'binaries':{p.relative_to(ROOT).as_posix():sha(p) for p in (ROOT/'Game/.godot/mono/temp/bin/Debug').glob('HakutaiKagami.*.dll')},'summary':{'tests':counters,'gpu_processes':12,'headless_legacy_processes':0,'entry':'normal project Main with lazy native tab','cases':4,'save_stages':16,'equal_comparisons':len(comparisons),'images':len(images)},'comparisons':comparisons,'images':images,'checks':checks,'logs':logs,'trx_sha256':sha(OUT/'tests/task-entry.trx'),'visual_review':None,'preserved_legacy_evidence':preserved,'diagnostics':{'headless_tax_display_check':'FAILED, not an acceptance success; actual GPU display regression passed; failure log retained','headless_tax_log_sha256':sha(OUT/'diagnostic-headless-tax.log') if (OUT/'diagnostic-headless-tax.log').exists() else None}}
 (BASE/'verification.json').write_bytes((json.dumps(j,ensure_ascii=False,indent=2)+'\n').encode());print('PASS normal Main entry, lazy session, native tab roundtrip and restored display',flush=True)
if __name__=='__main__':main()
