from pathlib import Path
import json,subprocess,uuid,hashlib
from verify_npc_work import source_files as parent_sources
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'Docs/Design/WorkHubStartupV1';OUT=BASE/'runtime-local'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def source_files():return sorted(set(parent_sources()+[Path(__file__),ROOT/'Scripts/check_work_hub_startup_binding.py',ROOT/'Game/Scenes/WorkHubTrial.tscn',ROOT/'Game/HakutaiKagami.Game.csproj',ROOT/'Docs/Proposals/WorkHubEntry.patch',BASE/'source-manifest.json',BASE/'README.md']))
def main():
 OUT.mkdir(parents=True,exist_ok=True)
 prior_path=ROOT/'Docs/Design/NpcWorkV1/verification.json';prior=json.loads(prior_path.read_bytes());assert prior['status']=='PASSED' and prior['summary']['tests']['passed']=='933'
 current={p.relative_to(ROOT).as_posix():sha(p) for p in source_files()}
 is_tested=lambda p:p.startswith('Core/') or p.startswith('Tests/')
 assert {p:h for p,h in current.items() if is_tested(p)}=={p:h for p,h in prior['sources'].items() if is_tested(p)},'Core/Tests changed; full tests must be rerun'
 assert sha(ROOT/'Docs/Design/NpcWorkV1/runtime-local/tests/npc-work.trx')==prior['trx_sha256']
 subprocess.run(['dotnet','build','Game/HakutaiKagami.Game.csproj','--no-restore','--verbosity','minimal'],cwd=ROOT,check=True)
 core='Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Core.dll'
 # Git revision metadata changes the compiled assembly after the previous commit. Retest this exact binary.
 cached=json.loads((BASE/'verification.json').read_bytes()) if (BASE/'verification.json').exists() else None
 reuse=bool(cached and cached['binaries'].get(core)==sha(ROOT/core) and cached['sources'] and {p:h for p,h in cached['sources'].items() if is_tested(p)}=={p:h for p,h in current.items() if is_tested(p)} and (OUT/'tests/work-hub.trx').exists() and cached['prior_test_proof']['trx_sha256']==sha(OUT/'tests/work-hub.trx'))
 if not reuse:subprocess.run(['dotnet','test','Tests/HakutaiKagami.Tests.csproj','--no-restore','--verbosity','minimal','--logger','trx;LogFileName=work-hub.trx','--results-directory',str(OUT/'tests')],cwd=ROOT,check=True)
 from xml.etree import ElementTree as ET
 counters=ET.parse(OUT/'tests/work-hub.trx').find('.//{*}Counters').attrib;assert counters['failed']=='0' and counters['passed']=='933'
 before={p.relative_to(ROOT).as_posix():sha(p) for p in source_files()};key=uuid.uuid4().hex
 exe=ROOT/'.tools/godot-4.5.2/Godot_v4.5.2-stable_mono_win64/Godot_v4.5.2-stable_mono_win64_console.exe';logs={};checks={};comparisons={};images={}
 def run(args,name,marker):
  result=subprocess.run([str(exe),'--path',str(ROOT/'Game'),'--resolution','1100x720',*args],cwd=ROOT,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,creationflags=subprocess.CREATE_NO_WINDOW,timeout=180)
  log=result.stdout.decode('utf8',errors='replace');(OUT/(name+'.log')).write_bytes(log.encode());assert result.returncode==0 and marker in log and 'ERROR:' not in log,(name,log)
  logs[name]=log;print('GPU PASS',name,flush=True)
 for mode in ['check','restore']:
  run(['--','--startup-entry','--work-hub-'+mode,'--case=success','--save-key='+key],'hub-'+mode,'WORK_HUB_PASSED');checks[mode]=json.loads((OUT/(mode+'-checks.json')).read_bytes())
 from PIL import Image,ImageChops
 def compare(folder,a,b,rect):
  x,y,w,h=map(int,rect);crop=(x,y,x+w,y+h);pa=folder/(a+'.png');pb=folder/(b+'.png');ia=Image.open(pa).convert('RGB');ib=Image.open(pb).convert('RGB');assert ia.size==ib.size
  assert x>=0 and y>=0 and x+w<=ia.width and y+h<=ia.height
  diff=sum(p!=(0,0,0) for p in ImageChops.difference(ia.crop(crop),ib.crop(crop)).get_flattened_data());assert diff==0,(pa,pb,diff)
  comparisons[pa.relative_to(ROOT).as_posix()+'/'+b]=diff
  for p in [pa,pb]:images[p.relative_to(ROOT).as_posix()]=sha(p)
 for n in range(5):
  compare(OUT,'s'+str(n),'restored-s'+str(n),json.loads((OUT/('s'+str(n)+'-rect.json')).read_bytes()))
  compare(OUT,'final','continued-s'+str(n),json.loads((OUT/'final-rect.json').read_bytes()))
 images[(OUT/'startup.png').relative_to(ROOT).as_posix()]=sha(OUT/'startup.png')
 for mode in ['check','restore']:
  run(['res://Scenes/NpcWorkTrial.tscn','--','--npc-work-'+mode,'--node=kofu','--case=success','--save-key='+key],'standalone-npc-'+mode,'NPC_WORK_PASSED')
 folder=ROOT/'Docs/Design/NpcWorkV1/runtime-local/kofu-success'
 for n in range(5):
  compare(folder,'success-s'+str(n),'success-restored-s'+str(n),json.loads((folder/('success-s'+str(n)+'-rect.json')).read_bytes()))
  compare(folder,'success-final','success-continued-s'+str(n),json.loads((folder/'success-final-rect.json').read_bytes()))
 for mode in ['check','restore']:
  run(['--','--personnel-catalog','--task-entry-'+mode,'--case=own','--save-key='+key],'normal-main-'+mode,'ASSESSMENT_NOTICE_PASSED')
 scene_folder=ROOT/'Docs/Design/NationalPersonnelV1/runtime-local';folder=OUT/'normal-main';folder.mkdir(exist_ok=True)
 for p in scene_folder.glob('own-*'):
  if p.is_file():(folder/p.name).write_bytes(p.read_bytes())
 for n in range(4):
  compare(folder,'own-s'+str(n),'own-restored-s'+str(n),json.loads((folder/('own-s'+str(n)+'-rect.json')).read_bytes()))
  compare(folder,'own-final','own-continued-s'+str(n),json.loads((folder/'own-final-rect.json').read_bytes()))
 compat_key=uuid.uuid4().hex
 for mode in ['check','restore']:
  run(['res://Scenes/Main.tscn','--','--personnel-catalog','--task-entry-'+mode,'--case=own','--save-key='+compat_key],'compat-normal-main-'+mode,'ASSESSMENT_NOTICE_PASSED')
 folder=ROOT/'Docs/Design/NationalPersonnelV1/runtime-local'
 for n in range(4):
  compare(folder,'own-s'+str(n),'own-restored-s'+str(n),json.loads((folder/('own-s'+str(n)+'-rect.json')).read_bytes()))
  compare(folder,'own-final','own-continued-s'+str(n),json.loads((folder/'own-final-rect.json').read_bytes()))
 preserved={}
 for flag,marker,file in [('--smoke-test','SCENE_SMOKE_PASS','godot-foundation.png'),('--tax-smoke-test','TAX_SCENE_PASS','tax-vertical.png'),('--shared-protocol-smoke-test','SHARED_PROTOCOL_GUI_PASS','shared-protocol.png')]:
  evidence=ROOT/'Docs/Proposals/Evidence'/file;original=evidence.read_bytes() if evidence.exists() else None
  try:
   run(['--',flag],'legacy'+flag,marker);target=OUT/('legacy-'+file);target.write_bytes(evidence.read_bytes());images[target.relative_to(ROOT).as_posix()]=sha(target)
  finally:
   if original is not None:evidence.write_bytes(original)
  assert (evidence.read_bytes() if evidence.exists() else None)==original
  try:
   run(['res://Scenes/Main.tscn','--',flag],'compat'+flag,marker);target=OUT/('compat-'+file);target.write_bytes(evidence.read_bytes());images[target.relative_to(ROOT).as_posix()]=sha(target)
  finally:
   if original is not None:evidence.write_bytes(original)
  assert (evidence.read_bytes() if evidence.exists() else None)==original;preserved[file]=hashlib.sha256(original).hexdigest() if original else None
 assert before=={p.relative_to(ROOT).as_posix():sha(p) for p in source_files()}
 manifest=json.loads((BASE/'source-manifest.json').read_bytes());assert all(sha(ROOT/p)==h for p,h in manifest['protected_files'].items())
 assert 'run/main_scene="res://Scenes/WorkHubTrial.tscn"' in (ROOT/'Game/project.godot').read_bytes().decode()
 subprocess.run(['git','apply','--reverse','--check','--unidiff-zero','Docs/Proposals/WorkHubEntry.patch'],cwd=ROOT,check=True)
 report={'status':'PASSED','sources':before,'binaries':{p.relative_to(ROOT).as_posix():sha(p) for p in (ROOT/'Game/.godot/mono/temp/bin/Debug').glob('HakutaiKagami.*.dll')},'summary':{'tests':933,'tests_rerun':not reuse,'test_evidence':'unchanged Core/Tests source; exact rebuilt binary retested because commit metadata changed DLL hash','gpu_processes':14,'hub_saves':5,'hub_comparisons':10,'standalone_npc_comparisons':10,'normal_entry_comparisons':8,'compatibility_entry_comparisons':8,'total_comparisons':len(comparisons),'images':len(images),'default_changed':True,'entry_switch_patch_applied':True},'prior_test_proof':{'report':prior_path.relative_to(ROOT).as_posix(),'report_sha256':sha(prior_path),'trx_path':(OUT/'tests/work-hub.trx').relative_to(ROOT).as_posix(),'trx_sha256':sha(OUT/'tests/work-hub.trx'),'core_sha256':sha(ROOT/core)},'comparisons':comparisons,'images':images,'checks':checks,'logs':logs,'preserved_legacy_evidence':preserved,'visual_review':None}
 (BASE/'verification.json').write_bytes((json.dumps(report,ensure_ascii=False,indent=2)+'\n').encode());print('PASS candidate work hub, lazy legacy entry and restored NPC actual 3D; approved startup applied',flush=True)
if __name__=='__main__':main()
