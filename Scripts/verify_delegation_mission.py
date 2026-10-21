from pathlib import Path
import subprocess,json,uuid,sys,xml.etree.ElementTree as ET
from PIL import Image,ImageChops
from verify_field_presence import source_files as parent_sources,sha
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'Docs/Design/DelegationMissionV1';OUT=BASE/'runtime-local'
def source_files():return sorted(set(parent_sources()+[Path(__file__),ROOT/'Scripts/check_delegation_mission_binding.py',BASE/'README.md',BASE/'DESIGN.md',BASE/'legacy-fixture.json',ROOT/'Tests/Fixtures/presence-d9f71d9-initial.json']))
def main():
 expected=json.loads((ROOT/'Scripts/current_test_contract.json').read_bytes())['expected_passed'];assert expected==1144
 subprocess.run([sys.executable,str(ROOT/'Scripts/verify_field_presence.py')],cwd=ROOT,check=True)
 before={p.relative_to(ROOT).as_posix():sha(p) for p in source_files()};key=uuid.uuid4().hex;images={};comparisons={};checks={}
 trx=ROOT/'Docs/Design/WorkHubStartupV1/runtime-local/tests/work-hub.trx';ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'};tests=[x for x in ET.parse(trx).findall('.//t:UnitTestResult',ns) if 'DelegationMissionTests.' in x.attrib['testName']];assert len(tests)==30 and all(x.attrib['outcome']=='Passed' for x in tests)
 exe=ROOT/'.tools/godot-4.5.2/Godot_v4.5.2-stable_mono_win64/Godot_v4.5.2-stable_mono_win64_console.exe'
 for case in ['success','refuse','expiry']:
  folder=OUT/case;folder.mkdir(parents=True,exist_ok=True)
  for mode in ['check','restore']:
   run=subprocess.run([str(exe),'--path',str(ROOT/'Game'),'--resolution','1100x720','--','--start-selection-delegation-'+mode,'--delegation-case='+case,'--save-key='+key],cwd=ROOT,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,creationflags=subprocess.CREATE_NO_WINDOW,timeout=180)
   log=run.stdout.decode('utf8',errors='replace');(folder/(mode+'.log')).write_bytes(log.encode());assert run.returncode==0 and 'DELEGATION_MISSION_PASSED' in log and 'ERROR:' not in log,(case,mode,log)
   checks[case+'/'+mode]=json.loads((folder/(mode+'-checks.json')).read_bytes());print('GPU PASS retainer',case,mode,flush=True)
  for n in range(5):
   for a,b in [(f's{n}',f'restored-s{n}'),('final',f'continued-s{n}')]:
    pa=folder/(a+'.png');pb=folder/(b+'.png');x,y,w,h=map(int,json.loads((folder/(a+'-rect.json')).read_bytes()));ia=Image.open(pa).convert('RGB');ib=Image.open(pb).convert('RGB');assert ia.size==ib.size and 0<=x<x+w<=ia.width and 0<=y<y+h<=ia.height
    diff=sum(v!=(0,0,0) for v in ImageChops.difference(ia.crop((x,y,x+w,y+h)),ib.crop((x,y,x+w,y+h))).get_flattened_data());assert diff==0,(case,a,b,diff);comparisons[case+'/'+a+'/'+b]=diff
    for p in [pa,pb]:images[p.relative_to(ROOT).as_posix()]=sha(p)
 assert before=={p.relative_to(ROOT).as_posix():sha(p) for p in source_files()}
 prior=ROOT/'Docs/Design/FieldPresenceV1/verification.json';j=json.loads(prior.read_bytes());assert j['tests']==expected and len(j['comparisons'])==10
 report=dict(status='PASSED',sources=before,binaries=j['binaries'],tests=expected,added=30,comparisons=comparisons,images=images,checks=checks,proofs={prior.relative_to(ROOT).as_posix():sha(prior),trx.relative_to(ROOT).as_posix():sha(trx)},visual_review=None)
 (BASE/'verification.json').write_bytes((json.dumps(report,ensure_ascii=False,indent=2)+'\n').encode());print(f'PASS delegation {expected} tests,186 world comparisons plus3 lifetime')
if __name__=='__main__':main()
