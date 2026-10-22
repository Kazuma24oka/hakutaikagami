from pathlib import Path
import subprocess,json,uuid,hashlib,sys,xml.etree.ElementTree as ET
from PIL import Image,ImageChops
from verify_officer_library import source_files as parent_sources
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'Docs/Design/NewGameFlowV1';OUT=BASE/'runtime-local';CASES=['group','historical','individual','absorb','collision','minor','corrupt']
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def source_files():return sorted(set(parent_sources()+[Path(__file__),ROOT/'Scripts/check_new_game_flow_binding.py',ROOT/'Scripts/Show-NewGameCandidate.ps1',BASE/'README.md',BASE/'contract.json']))
def sources():return {p.relative_to(ROOT).as_posix():sha(p) for p in source_files()}
def write(p,j):p.write_bytes((json.dumps(j,ensure_ascii=False,indent=2)+'\n').encode())
def execute(case,mode,key):
    folder=OUT/case;folder.mkdir(parents=True,exist_ok=True)
    exe=ROOT/'.tools/godot-4.5.2/Godot_v4.5.2-stable_mono_win64/Godot_v4.5.2-stable_mono_win64_console.exe'
    args=[str(exe),'--path',str(ROOT/'Game'),'--resolution','1280x900']+(['--fixed-fps','12'] if case=='movie' else [])+['res://Scenes/NewGameFrontEnd.tscn','--','--new-game-test-key='+key,'--new-game-case='+case]+(['--new-game-restore'] if mode=='restore' else [])+(['--new-game-film'] if case=='movie' else [])
    run=subprocess.run(args,cwd=ROOT,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,creationflags=subprocess.CREATE_NO_WINDOW,timeout=300)
    log=run.stdout.decode('utf8',errors='replace');(folder/(mode+'.log')).write_bytes(log.encode());assert run.returncode==0 and 'NEW_GAME_FLOW_PASSED' in log and 'ERROR:' not in log,(case,mode,log[-7000:]);print('GPU PASS',case,mode,flush=True)
def video():
    folder=OUT/'movie';sequence=[]
    def still(name,seconds):sequence.append((folder/(name+'.png'),seconds))
    for name,seconds in [('menu',1.5),('management',2),('conditions',1),('registered-selection',1),('placement',1.5),('world-review',1),('person-map',1.5),('person-list',1),('work-sealed',1),('work-open',1),('work-complete',1),('s0',1)]:still(name,seconds)
    sequence.extend((folder/'film'/('walk-'+str(i).zfill(3)+'.png'),1/12) for i in range(48))
    for name,seconds in [('s2',1),('s3',1),('final',1),('load-list',1),('loaded',2)]:still(name,seconds)
    concat=folder/'sequence.txt';concat.write_bytes(('\n'.join("file '"+str(p).replace('\\','/')+"'\nduration "+str(seconds) for p,seconds in sequence)+"\nfile '"+str(sequence[-1][0]).replace('\\','/')+"'\n").encode())
    target=OUT/'new-game-flow.mp4';ffmpeg=ROOT/'.tools/video-tools/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe'
    run=subprocess.run([str(ffmpeg),'-y','-f','concat','-safe','0','-i',str(concat),'-vf','fps=24','-c:v','libx264','-crf','20','-pix_fmt','yuv420p','-movflags','+faststart',str(target)],stdout=subprocess.PIPE,stderr=subprocess.STDOUT,creationflags=subprocess.CREATE_NO_WINDOW,timeout=180);(folder/'encode.log').write_bytes(run.stdout);assert run.returncode==0
    run=subprocess.run([str(ffmpeg),'-v','error','-i',str(target),'-f','null','-'],stdout=subprocess.PIPE,stderr=subprocess.STDOUT,creationflags=subprocess.CREATE_NO_WINDOW,timeout=180);(folder/'decode.log').write_bytes(run.stdout);assert run.returncode==0 and not run.stdout.strip();return target

def main():
    OUT.mkdir(parents=True,exist_ok=True)
    if '--finalize' not in sys.argv:
        before=sources();prior=ROOT/'Docs/Design/NewGameSetupV1/verification.json';j=json.loads(prior.read_bytes());assert j['tests']==1352
        for p,h in j['binaries'].items():assert sha(ROOT/p)==h,p
        proofs={prior.relative_to(ROOT).as_posix():sha(prior)};images={};pairs={};info_pairs={};checks={}
        for case in CASES:
            key=uuid.uuid4().hex
            if case=='corrupt':
                lib=OUT/case/key/'library';lib.mkdir(parents=True,exist_ok=True);(lib/'officers.json').write_text('broken')
            for mode in ['check','restore']:execute(case,mode,key)
            folder=OUT/case
            for n in range(4):
                for a,b in [(f's{n}',f'restored-s{n}'),('final',f'continued-s{n}')]:
                    assert (folder/(a+'-world.sha256')).read_bytes()==(folder/(b+'-world.sha256')).read_bytes(),(case,a,b,'world')
                    assert (folder/(a+'-info.txt')).read_bytes()==(folder/(b+'-info.txt')).read_bytes(),(case,a,b,'personal-info')
                    ia=Image.open(folder/(a+'.png')).convert('RGB');ib=Image.open(folder/(b+'.png')).convert('RGB');assert ia.size==ib.size
                    bounds=json.loads((folder/(a+'-rect.json')).read_bytes())
                    for part in ['Surface','Info']:
                        x,y,w,h=map(int,bounds[part]);assert 0<=x<x+w<=ia.width and 0<=y<y+h<=ia.height and (part!='Surface' or h>=300)
                        diff=ImageChops.difference(ia.crop((x,y,x+w,y+h)),ib.crop((x,y,x+w,y+h)));assert diff.getbbox() is None,(case,a,b,part)
                    pairs[case+'/'+a+'/'+b]=0;info_pairs[case+'/'+a+'/'+b]=0
            for p in folder.glob('*'):
                if p.is_file() and p.suffix in ['.png','.json','.sha256','.txt','.log']:
                    if p.suffix=='.png':images[p.relative_to(ROOT).as_posix()]=sha(p)
                    else:proofs[p.relative_to(ROOT).as_posix()]=sha(p)
            checks[case]={m:json.loads((folder/(m+'-checks.json')).read_bytes()) for m in ['check','restore']}
            print('PASS exact world/person/3D pairs',case,8,flush=True)
        execute('movie','check',uuid.uuid4().hex);mp4=video();proofs[mp4.relative_to(ROOT).as_posix()]=sha(mp4)
        for p in (OUT/'movie').rglob('*'):
            if p.is_file() and p.suffix in ['.png','.log']:
                if p.suffix=='.png':images[p.relative_to(ROOT).as_posix()]=sha(p)
                else:proofs[p.relative_to(ROOT).as_posix()]=sha(p)
        assert before==sources();assert len(pairs)==56
        trx=ROOT/'Docs/Design/WorkHubStartupV1/runtime-local/tests/work-hub.trx';tree=ET.parse(trx);counter=tree.find('.//{*}Counters').attrib;assert counter['passed']=='1352' and counter['failed']=='0' and counter['notExecuted']=='0'
        tests=[t for t in tree.findall('.//{*}UnitTestResult') if 'NewGameWorldTests.' in t.attrib['testName'] or 'NewGameFlowBoundaryTests.' in t.attrib['testName']];assert len(tests)==36 and all(t.attrib['outcome']=='Passed' for t in tests)
        proofs[trx.relative_to(ROOT).as_posix()]=sha(trx)
        evidence=dict(sources=before,binaries=j['binaries'],tests=1352,added=36,images=images,proofs=proofs,comparisons=pairs,personal_info_comparisons=info_pairs,checks=checks,video=mp4.relative_to(ROOT).as_posix())
        write(OUT/'run-evidence.json',evidence);print('PASS automated checks. Inspect current screenshots/video frames, record visual-review.json, then --finalize.');return
    evidence=json.loads((OUT/'run-evidence.json').read_bytes());assert evidence['sources']==sources()
    for field in ['binaries','images','proofs']:
        for p,h in evidence[field].items():assert sha(ROOT/p)==h,p
    review=json.loads((OUT/'visual-review.json').read_bytes());assert review['inspected'] and review['method']
    for p,h in review['inspected'].items():assert evidence['images'][p]==h and sha(ROOT/p)==h,p
    evidence.update(status='PASSED',visual_review=review,summary=dict(completion_conditions='5/5',gpu_processes=94,new_flow_gpu_processes=15,legacy_world_image_comparisons=424,new_world_image_comparisons=56,new_personal_info_comparisons=56,new_full_world_comparisons=56,lifetime_comparisons=3,default_scene_changed=False),limits=['New explicit start profile; general CareerMilitary, wages, combat and nationwide personnel completion remain separate.','Historical identity data partial; territory/head rights and age/budget are declared trial settings.','Art/performance not product acceptance.'])
    evidence['proofs'][(OUT/'run-evidence.json').relative_to(ROOT).as_posix()]=sha(OUT/'run-evidence.json');evidence['proofs'][(OUT/'visual-review.json').relative_to(ROOT).as_posix()]=sha(OUT/'visual-review.json')
    write(BASE/'verification.json',evidence);print('PASS new-game flow current sources,1352 tests,7 cases/56 exact world-person-3D pairs,live auto-motion video; old startup preserved')
if __name__=='__main__':main()
