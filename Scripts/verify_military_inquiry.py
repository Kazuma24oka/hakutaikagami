from pathlib import Path
import subprocess,json,uuid,sys,xml.etree.ElementTree as ET,hashlib
from PIL import Image,ImageChops
from verify_career_military import source_files as parent_sources,sha
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'Docs/Design/MilitaryInquiryV1';OUT=BASE/'runtime-local'
CASES=['offer','permission','handback','retry'];STEPS=6
def source_files():return sorted(set(parent_sources()+[Path(__file__),ROOT/'Scripts/check_military_inquiry_binding.py',BASE/'README.md',BASE/'DESIGN.md',BASE/'legacy-fixtures.json',ROOT/'Tests/Fixtures/military-bd631ba-initial.json',ROOT/'Tests/Fixtures/military-bd631ba-active.json']))
def main():
 expected=json.loads((ROOT/'Scripts/current_test_contract.json').read_bytes())['expected_passed'];assert expected>=1281
 before={p.relative_to(ROOT).as_posix():sha(p) for p in source_files()}
 legacy=json.loads((BASE/'legacy-fixtures.json').read_bytes());historic=subprocess.check_output(['git','show',legacy['verification_git_object']],cwd=ROOT);assert hashlib.sha256(historic).hexdigest()==legacy['verification_sha256'];assert json.loads(historic)['binaries']['Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Core.dll']==legacy['producer_core_sha256']
 for path,h in legacy['fixtures'].items():assert sha(ROOT/path)==h
 subprocess.run([sys.executable,str(ROOT/'Scripts/verify_career_military.py')],cwd=ROOT,check=True)
 assert before=={p.relative_to(ROOT).as_posix():sha(p) for p in source_files()}
 trx=ROOT/'Docs/Design/WorkHubStartupV1/runtime-local/tests/work-hub.trx';ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'};tests=[x for x in ET.parse(trx).findall('.//t:UnitTestResult',ns) if 'MilitaryInquiryTests.' in x.attrib['testName']];assert len(tests)==25 and all(x.attrib['outcome']=='Passed' for x in tests)
 key=uuid.uuid4().hex;images={};comparisons={};world_comparisons={};checks={};world_files={}
 exe=ROOT/'.tools/godot-4.5.2/Godot_v4.5.2-stable_mono_win64/Godot_v4.5.2-stable_mono_win64_console.exe'
 for case in CASES:
  folder=OUT/case;folder.mkdir(parents=True,exist_ok=True)
  for mode in ['check','restore']:
   run=subprocess.run([str(exe),'--path',str(ROOT/'Game'),'--resolution','1100x720','--','--start-selection-military-inquiry-'+mode,'--military-inquiry-case='+case,'--save-key='+key],cwd=ROOT,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,creationflags=subprocess.CREATE_NO_WINDOW,timeout=300)
   log=run.stdout.decode('utf8',errors='replace');(folder/(mode+'.log')).write_bytes(log.encode());assert run.returncode==0 and 'MILITARY_INQUIRY_PASSED' in log and 'ERROR:' not in log,(case,mode,log)
   checks[case+'/'+mode]=json.loads((folder/(mode+'-checks.json')).read_bytes());print('GPU PASS military inquiry',case,mode,flush=True)
  for n in range(STEPS):
   for a,b in [(f's{n}',f'restored-s{n}'),('final',f'continued-s{n}')]:
    pa=folder/(a+'.png');pb=folder/(b+'.png');x,y,w,h=map(int,json.loads((folder/(a+'-rect.json')).read_bytes()));ia=Image.open(pa).convert('RGB');ib=Image.open(pb).convert('RGB');assert ia.size==ib.size and 0<=x<x+w<=ia.width and 0<=y<y+h<=ia.height
    diff=sum(v!=(0,0,0) for v in ImageChops.difference(ia.crop((x,y,x+w,y+h)),ib.crop((x,y,x+w,y+h))).get_flattened_data());assert diff==0,(case,a,b,diff);comparisons[case+'/'+a+'/'+b]=diff
    fa=folder/(a+'-world.sha256');fb=folder/(b+'-world.sha256');assert fa.read_bytes()==fb.read_bytes(),(case,a,b,'full world differs');world_comparisons[case+'/'+a+'/'+b]=0
    for p in [pa,pb]:images[p.relative_to(ROOT).as_posix()]=sha(p)
    for p in [fa,fb]:world_files[p.relative_to(ROOT).as_posix()]=sha(p)
 assert len(comparisons)==48 and len(world_comparisons)==48 and before=={p.relative_to(ROOT).as_posix():sha(p) for p in source_files()}
 prior=ROOT/'Docs/Design/CareerMilitaryV1/verification.json';j=json.loads(prior.read_bytes());assert j['tests']==expected
 report=dict(status='PASSED',sources=before,binaries=j['binaries'],tests=expected,added=25,comparisons=comparisons,full_world_comparisons=world_comparisons,images=images,checks=checks,proofs={prior.relative_to(ROOT).as_posix():sha(prior),trx.relative_to(ROOT).as_posix():sha(trx),**world_files},visual_review=None)
 (BASE/'verification.json').write_bytes((json.dumps(report,ensure_ascii=False,indent=2)+'\n').encode());print(f'PASS military inquiry {expected} tests,356 world image comparisons plus3 lifetime;48 full-world inquiry comparisons')
if __name__=='__main__':main()
