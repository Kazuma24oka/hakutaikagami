from pathlib import Path
import sys,subprocess,json,hashlib,uuid
from xml.etree import ElementTree as ET
from verify_regional_work import source_files as parent_sources
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'Docs/Design/NpcWorkV1';OUT=BASE/'runtime-local'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def source_files():return sorted(set(parent_sources()+[Path(__file__),ROOT/'Scripts/check_npc_work_binding.py',ROOT/'Game/Scenes/NpcWorkTrial.tscn',BASE/'source-manifest.json',BASE/'README.md']))
def main():
 OUT.mkdir(parents=True,exist_ok=True);quick='--quick' in sys.argv
 if not quick:
  subprocess.run(['dotnet','build','Game/HakutaiKagami.Game.csproj','--no-restore','--verbosity','minimal'],cwd=ROOT,check=True)
  subprocess.run(['dotnet','test','Tests/HakutaiKagami.Tests.csproj','--no-restore','--verbosity','minimal','--logger','trx;LogFileName=npc-work.trx','--results-directory',str(OUT/'tests')],cwd=ROOT,check=True)
 before={p.relative_to(ROOT).as_posix():sha(p) for p in source_files()};key=uuid.uuid4().hex
 exe=ROOT/'.tools/godot-4.5.2/Godot_v4.5.2-stable_mono_win64/Godot_v4.5.2-stable_mono_win64_console.exe';logs={};checks={};comparisons={};images={}
 scenarios=[('castle','success'),('kofu','success'),('odawara','refuse'),('kofu','report-miss')]
 def run(args,name,marker):
  result=subprocess.run([str(exe),'--path',str(ROOT/'Game'),'--resolution','1100x720',*args],cwd=ROOT,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,creationflags=subprocess.CREATE_NO_WINDOW,timeout=180)
  log=result.stdout.decode('utf8',errors='replace');(OUT/(name+'.log')).write_bytes(log.encode());assert result.returncode==0 and marker in log and 'ERROR:' not in log,(name,log)
  logs[name]=log;print('GPU PASS',name,flush=True)
 for node,case in scenarios:
  for mode in ['check','restore']:
   name=node+'-'+case+'-'+mode
   run(['res://Scenes/NpcWorkTrial.tscn','--','--npc-work-'+mode,'--node='+node,'--case='+case,'--save-key='+key],name,'NPC_WORK_PASSED')
   checks[name]=json.loads((OUT/(node+'-'+case)/(case+'-'+mode+'-checks.json')).read_bytes())
 if quick:return
 from PIL import Image,ImageChops
 def compare(folder,a,b,rect):
  x,y,w,h=map(int,rect);crop=(x,y,x+w,y+h);pa=folder/(a+'.png');pb=folder/(b+'.png');ia=Image.open(pa).convert('RGB');ib=Image.open(pb).convert('RGB');assert ia.size==ib.size
  assert x>=0 and y>=0 and x+w<=ia.width and y+h<=ia.height
  diff=sum(p!=(0,0,0) for p in ImageChops.difference(ia.crop(crop),ib.crop(crop)).get_flattened_data());assert diff==0,(pa,pb,diff)
  comparisons[str(pa.relative_to(ROOT))+'/'+b]=diff
  for p in [pa,pb]:images[p.relative_to(ROOT).as_posix()]=sha(p)
 for node,c in scenarios:
  folder=OUT/(node+'-'+c)
  for n in range(5):
   compare(folder,c+'-s'+str(n),c+'-restored-s'+str(n),json.loads((folder/(c+'-s'+str(n)+'-rect.json')).read_bytes()))
   compare(folder,c+'-final',c+'-continued-s'+str(n),json.loads((folder/(c+'-final-rect.json')).read_bytes()))
 # Old manual regional work remains separate and retains its protocol.
 for mode in ['check','restore']:
  run(['res://Scenes/RegionalWorkTrial.tscn','--','--regional-work-'+mode,'--node=kofu','--case=success','--save-key='+key],'manual-regional-'+mode,'REGIONAL_WORK_PASSED')
 folder=ROOT/'Docs/Design/RegionalWorkV1/runtime-local/kofu-success'
 for n in range(5):
  compare(folder,'success-s'+str(n),'success-restored-s'+str(n),json.loads((folder/('success-s'+str(n)+'-rect.json')).read_bytes()))
  compare(folder,'success-final','success-continued-s'+str(n),json.loads((folder/'success-final-rect.json').read_bytes()))
 # Current normal-entry regression; older reports remain historical and are not overwritten.
 for mode in ['check','restore']:
  run(['--','--personnel-catalog','--task-entry-'+mode,'--case=own','--save-key='+key],'normal-main-'+mode,'ASSESSMENT_NOTICE_PASSED')
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
  assert (evidence.read_bytes() if evidence.exists() else None)==original;preserved[file]=hashlib.sha256(original).hexdigest() if original else None
 assert before=={p.relative_to(ROOT).as_posix():sha(p) for p in source_files()}
 counters=ET.parse(OUT/'tests/npc-work.trx').find('.//{*}Counters').attrib;assert int(counters['failed'])==0 and int(counters['passed'])>=933
 report={'status':'PASSED','sources':before,'binaries':{p.relative_to(ROOT).as_posix():sha(p) for p in (ROOT/'Game/.godot/mono/temp/bin/Debug').glob('HakutaiKagami.*.dll')},'summary':{'tests':counters,'gpu_processes':15,'npc_cases':4,'npc_saves':20,'npc_comparisons':40,'manual_regional_comparisons':10,'normal_entry_comparisons':8,'total_comparisons':len(comparisons),'images':len(images)},'comparisons':comparisons,'images':images,'checks':checks,'logs':logs,'trx_sha256':sha(OUT/'tests/npc-work.trx'),'preserved_legacy_evidence':preserved,'visual_review':None}
 (BASE/'verification.json').write_bytes((json.dumps(report,ensure_ascii=False,indent=2)+'\n').encode());print('PASS autonomous NPC work, stable knowledge input and restored actual 3D',flush=True)
if __name__=='__main__':main()
