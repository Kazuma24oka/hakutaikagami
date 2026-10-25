from pathlib import Path
import subprocess,json,uuid,sys,xml.etree.ElementTree as ET
from PIL import Image,ImageChops
from verify_military_inquiry import source_files as parent_sources,sha
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'Docs/Design/LifeEntryV1';OUT=BASE/'runtime-local'
CASES=['normal','long-name'];STEPS=17
def source_files():return sorted(set(parent_sources()+[Path(__file__),ROOT/'Scripts/check_life_entry_binding.py',BASE/'README.md',BASE/'DESIGN.md',ROOT/'Game/Scenes/LifeEntryTrial.tscn']))
def main():
 expected=json.loads((ROOT/'Scripts/current_test_contract.json').read_bytes())['expected_passed'];assert expected>=1281
 before={p.relative_to(ROOT).as_posix():sha(p) for p in source_files()}
 assert 'run/main_scene="res://Scenes/WorkHubTrial.tscn"' in (ROOT/'Game/project.godot').read_bytes().decode()
 subprocess.run([sys.executable,str(ROOT/'Scripts/verify_military_inquiry.py')],cwd=ROOT,check=True)
 assert before=={p.relative_to(ROOT).as_posix():sha(p) for p in source_files()}
 trx=ROOT/'Docs/Design/WorkHubStartupV1/runtime-local/tests/work-hub.trx';ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'};counters=ET.parse(trx).find('.//t:Counters',ns).attrib;assert counters['passed']==str(expected) and counters['failed']=='0'
 key=uuid.uuid4().hex;images={};comparisons={};world_comparisons={};checks={};world_files={}
 exe=ROOT/'.tools/godot-4.5.2/Godot_v4.5.2-stable_mono_win64/Godot_v4.5.2-stable_mono_win64_console.exe'
 for case in CASES:
  folder=OUT/case;folder.mkdir(parents=True,exist_ok=True)
  for mode in ['check','restore']:
   run=subprocess.run([str(exe),'--path',str(ROOT/'Game'),'--resolution','1100x720','res://Scenes/LifeEntryTrial.tscn','--','--start-selection-life-'+mode,'--save-key='+key]+(['--life-long-name'] if case=='long-name' else []),cwd=ROOT,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,creationflags=subprocess.CREATE_NO_WINDOW,timeout=300)
   log=run.stdout.decode('utf8',errors='replace');(folder/(mode+'.log')).write_bytes(log.encode());assert run.returncode==0 and 'LIFE_ENTRY_PASSED' in log and 'ERROR:' not in log,(case,mode,log)
   checks[case+'/'+mode]=json.loads((folder/(mode+'-checks.json')).read_bytes());print('GPU PASS life entry',case,mode,flush=True)
  for n in range(STEPS):
   for a,b in [(f's{n}',f'restored-s{n}'),('final',f'continued-s{n}')]:
    pa=folder/(a+'.png');pb=folder/(b+'.png');x,y,w,h=map(int,json.loads((folder/(a+'-rect.json')).read_bytes()));ia=Image.open(pa).convert('RGB');ib=Image.open(pb).convert('RGB');assert ia.size==ib.size and 0<=x<x+w<=ia.width and 0<=y<y+h<=ia.height
    diff=sum(v!=(0,0,0) for v in ImageChops.difference(ia.crop((x,y,x+w,y+h)),ib.crop((x,y,x+w,y+h))).get_flattened_data());assert diff==0,(case,a,b,diff);comparisons[case+'/'+a+'/'+b]=diff
    fa=folder/(a+'-world.sha256');fb=folder/(b+'-world.sha256');assert fa.read_bytes()==fb.read_bytes(),(case,a,b,'full world differs');world_comparisons[case+'/'+a+'/'+b]=0
    for p in [pa,pb]:images[p.relative_to(ROOT).as_posix()]=sha(p)
    for p in [fa,fb]:world_files[p.relative_to(ROOT).as_posix()]=sha(p)
  form=folder/'form.png';images[form.relative_to(ROOT).as_posix()]=sha(form)
 assert len(comparisons)==68 and len(world_comparisons)==68 and before=={p.relative_to(ROOT).as_posix():sha(p) for p in source_files()}
 prior=ROOT/'Docs/Design/MilitaryInquiryV1/verification.json';j=json.loads(prior.read_bytes());assert j['tests']==expected
 report=dict(status='PASSED',sources=before,binaries=j['binaries'],tests=expected,added=0,comparisons=comparisons,full_world_comparisons=world_comparisons,images=images,checks=checks,proofs={prior.relative_to(ROOT).as_posix():sha(prior),trx.relative_to(ROOT).as_posix():sha(trx),**world_files},visual_review=None)
 (BASE/'verification.json').write_bytes((json.dumps(report,ensure_ascii=False,indent=2)+'\n').encode());print(f'PASS life entry {expected} tests,424 world image comparisons plus3 lifetime;68 full-world life-entry comparisons; approved default unchanged')
if __name__=='__main__':main()
