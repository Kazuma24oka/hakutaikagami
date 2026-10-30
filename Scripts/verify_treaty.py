"""Actual active garrison and stored-ledger succession; CLI only, no screenshots/GUI."""
from pathlib import Path
import argparse,collections,hashlib,json,os,subprocess,uuid,xml.etree.ElementTree as ET
from verify_remote_military_succession import CLASSES as PRIOR_CLASSES
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'Docs/Design/TreatyV1';OUT=BASE/'runtime-local'
CLASSES=['TreatyNegotiationsTests',*PRIOR_CLASSES]
NAMES=[f'treaty-{i}' for i in range(12)]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def sources():
    files=[Path(__file__),ROOT/'Scripts/verify_remote_military_succession.py',ROOT/'Scripts/probe_saved_worlds.py',ROOT/'Scripts/current_test_contract.json']
    for folder in ['Core','Tests','Game']:
        files.extend(p for p in (ROOT/folder).rglob('*') if p.is_file() and p.suffix in {'.cs','.csproj','.json','.props','.targets','.godot','.tscn'} and not {'bin','obj','.godot','TestResults'}.intersection(p.parts))
    files.extend(ROOT/f for f in ['.gitattributes','global.json','Directory.Build.props','Directory.Build.targets','NuGet.Config'] if (ROOT/f).is_file())
    return {p.relative_to(ROOT).as_posix():sha(p) for p in sorted(set(files))}
def run(args,log):
    print('RUN '+str(log),flush=True)
    with log.open('wb') as output:
        p=subprocess.run(args,cwd=ROOT,stdout=output,stderr=subprocess.STDOUT,creationflags=subprocess.CREATE_NO_WINDOW,timeout=3000)
    assert p.returncode==0,log
def prior():
    path=ROOT/'Docs/Design/RemoteMilitarySuccessionV1/verification.json';v=json.loads(path.read_bytes());ref=v['projection_result'];assert sha(ROOT/ref)==v['proofs'][ref]
    trx=next(k for k in v['proofs'] if k.endswith('/tests/remote-military-current.trx'));assert sha(ROOT/trx)==v['proofs'][trx]
    return path,ROOT/ref,ROOT/trx
def counts(trx):return collections.Counter(m.attrib['className'].rsplit('.',1)[-1] for m in ET.parse(trx).findall('.//{*}TestMethod'))
def unit_ids(w):
    ledgers=([w['NewMilitary']] if w.get('NewMilitary') else [])+w.get('OtherMilitaryLedgers',[])
    return {u for s in ledgers for u in s.get('Binding',{}).get('Units',['unit:new-military/0','unit:new-military/1','unit:new-military/2'])}
def expected_nodes(path):
    _,prior_path,_=prior();old=json.loads(prior_path.read_bytes());expected={}
    for key,value in old['Results'].items():
        w=json.loads((path.parent/key).read_bytes());expected[key]={a['Id'] for a in json.loads(value)['Actors']}|unit_ids(w)
    for name in NAMES:
        for mode in ['before','restored']:
            key=name+'.'+mode+'.json';w=json.loads((path.parent/key).read_bytes());actors={w['PlayerControl']}|unit_ids(w)
            for s in ([w['NewMilitary']] if w.get('NewMilitary') else [])+w.get('OtherMilitaryLedgers',[]):
                for e in s.get('ActiveSuccession',{}).get('Entries',[]):actors.update([e['Previous'],e['Principal'],e['Agent']])
            for state in ([w['NewMilitary']] if w.get('NewMilitary') else [])+w.get('OtherMilitaryLedgers',[]):
                for paper in state.get('RemoteSuccession',{}).get('Papers',[]):actors.update([paper['Sender'],paper['Recipient']])
            for paper in w.get('Treaties',{}).get('Papers',[])+w.get('Treaties',{}).get('Operations',[]):actors.update([paper['Author'],paper['Recipient']])
            here=next(l for l in w['Locations'] if l['Actor']==w['PlayerControl'])
            for e in w.get('Succession',{}).get('Entries',[]):
                t=e.get('Terms')
                if t and here['Kind']=='AtSite' and t['Home']==here['Site']:actors.add('house-succession:'+t['Id'])
            expected[key]=actors
    n=json.loads(path.read_bytes());assert n['Status']=='PASSED' and n['Files']==460 and n['Pairs']==230 and set(n['Results'])==set(expected)
    for key,value in n['Results'].items():
        actual=[a['Id'] for a in json.loads(value)['Actors']];assert len(actual)==len(set(actual)) and set(actual)==expected[key],(key,actual,expected[key])
    assert n['Nodes']==sum(map(len,expected.values()))
    return dict(files=460,pairs=230,nodes=n['Nodes'],gui='NOT_USED',screens='NOT_CAPTURED')
def check(v):
    assert v['status']=='PASSED' and v['scope']=='headless-versioned-multiclause-treaty-termination-and-requests'
    assert v['sources']==sources()==json.loads((OUT/'run-inputs.json').read_bytes()),'Verification inputs changed'
    for group in ['binaries','proofs']:
        for f,h in v[group].items():assert sha(ROOT/f)==h,f
    _,_,old_trx=prior();expected=counts(old_trx);expected['TreatyNegotiationsTests']=49
    trx=OUT/'tests/treaty-current.trx';c=ET.parse(trx).find('.//{*}Counters').attrib
    assert counts(trx)==expected and int(c['passed'])==1838 and c['passed']==c['total'] and c['failed']==c['notExecuted']=='0' and c==v['tests']['counters']
    assert v['node3d']==expected_nodes(ROOT/v['projection_result'])
    assert v['binaries']['Core/bin/Debug/net8.0/HakutaiKagami.Core.dll']==v['binaries']['Tests/bin/Debug/net8.0/HakutaiKagami.Core.dll']==v['binaries']['Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Core.dll']
    assert v['gui']==v['full_core_suite']=='NOT_RUN'
def main():
    parser=argparse.ArgumentParser();parser.add_argument('--check-only',action='store_true');a=parser.parse_args();manifest=BASE/'verification.json'
    if a.check_only:check(json.loads(manifest.read_bytes()));print('PASS current treaty verification');return
    OUT.mkdir(parents=True,exist_ok=True);before=sources();(OUT/'run-inputs.json').write_text(json.dumps(before,indent=2)+'\n',encoding='utf-8')
    projection=OUT/'projection'/uuid.uuid4().hex;projection.mkdir(parents=True);economy=projection/'economy';economy.mkdir();os.environ['HAKUTAI_ECONOMY_QA_DIR']=str(economy);os.environ['HAKUTAI_WORLD_CLASH_QA_DIR']=str(projection);os.environ['HAKUTAI_PROJECTION_EXPECTED_PAIRS']='230'
    run(['dotnet','test','Tests/HakutaiKagami.Tests.csproj','--filter','|'.join('FullyQualifiedName~'+c for c in CLASSES),'--logger','trx;LogFileName=treaty-current.trx','--results-directory',str(OUT/'tests'),'--nologo','--verbosity','minimal'],OUT/'tests.log')
    run(['dotnet','build','Game/HakutaiKagami.Game.csproj','--no-restore','--nologo','--verbosity','quiet'],OUT/'game-build.log')
    exe=ROOT/'.tools/godot-4.5.2/Godot_v4.5.2-stable_mono_win64/Godot_v4.5.2-stable_mono_win64_console.exe'
    run([str(exe),'--headless','--path',str(ROOT/'Game'),'res://Scenes/WorldClashProjectionCheck.tscn'],OUT/'node3d.log')
    n=expected_nodes(projection/'projection-result.json');assert before==sources(),'Inputs changed during verification'
    for f in projection.glob('*.before.json'):assert f.read_bytes()==f.with_name(f.name.replace('.before.json','.restored.json')).read_bytes(),f
    for name in ['work','collected']:
        for suffix in ['.json','.view.json']:assert (economy/(name+'.before'+suffix)).read_bytes()==(economy/(name+'.restored'+suffix)).read_bytes()
    c=ET.parse(OUT/'tests/treaty-current.trx').find('.//{*}Counters').attrib
    binaries=['Core/bin/Debug/net8.0/HakutaiKagami.Core.dll','Tests/bin/Debug/net8.0/HakutaiKagami.Core.dll','Tests/bin/Debug/net8.0/HakutaiKagami.Tests.dll','Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Core.dll','Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Game.dll']
    proofs=[*prior(),OUT/'run-inputs.json',OUT/'tests.log',OUT/'tests/treaty-current.trx',OUT/'game-build.log',OUT/'node3d.log',*projection.glob('*.json'),*economy.glob('*.json')]
    v=dict(schema=1,status='PASSED',scope='headless-versioned-multiclause-treaty-termination-and-requests',baseline_main='7f741be1fc830a6462faa2e7322a5a009df7b137',gui='NOT_RUN',full_core_suite='NOT_RUN',tests=dict(passed=int(c['passed']),failed=0,skipped=0,counters=c),new_tests=49,recovery_phases=12,sources=before,binaries={f:sha(ROOT/f) for f in binaries},proofs={f.relative_to(ROOT).as_posix():sha(f) for f in proofs},projection_result=(projection/'projection-result.json').relative_to(ROOT).as_posix(),node3d=n)
    manifest.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf-8');check(v);print('PASS1838 related tests,49 new cases,12 real generation recoveries,Game build,230 headless pairs; GUI/full3723 NOT_RUN',flush=True)
if __name__=='__main__':main()
