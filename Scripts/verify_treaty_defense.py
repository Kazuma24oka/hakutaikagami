"""Stock-backed real defense sortie, actual generation recovery and CLI-only Godot Node3D."""
from pathlib import Path
import argparse,collections,hashlib,json,os,subprocess,uuid,xml.etree.ElementTree as ET
from verify_treaty_supply import CLASSES as PRIOR_CLASSES
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'Docs/Design/TreatyDefenseV1';OUT=BASE/'runtime-local'
CLASSES=['TreatyDefenseTests',*PRIOR_CLASSES];NAMES=[f'treaty-defense-{i}' for i in range(14)]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def sources():
    files=[Path(__file__),ROOT/'Scripts/verify_treaty_supply.py',ROOT/'Scripts/probe_saved_worlds.py',ROOT/'Scripts/current_test_contract.json']
    for folder in ['Core','Tests','Game']:
        files.extend(p for p in (ROOT/folder).rglob('*') if p.is_file() and p.suffix in {'.cs','.csproj','.json','.props','.targets','.godot','.tscn'} and not {'bin','obj','.godot','TestResults'}.intersection(p.parts))
    files.extend(ROOT/f for f in ['.gitattributes','global.json','Directory.Build.props','Directory.Build.targets','NuGet.Config'] if (ROOT/f).is_file())
    return {p.relative_to(ROOT).as_posix():sha(p) for p in sorted(set(files))}
def run(args,log):
    print('RUN '+str(log),flush=True)
    with log.open('wb') as out:p=subprocess.run(args,cwd=ROOT,stdout=out,stderr=subprocess.STDOUT,creationflags=subprocess.CREATE_NO_WINDOW,timeout=10800)
    assert p.returncode==0,log
def prior():
    path=ROOT/'Docs/Design/TreatySupplyV1/verification.json';v=json.loads(path.read_bytes());ref=v['projection_result'];assert sha(ROOT/ref)==v['proofs'][ref]
    trx=next(k for k in v['proofs'] if k.endswith('/tests/treaty-supply-current.trx'));assert sha(ROOT/trx)==v['proofs'][trx]
    return path,ROOT/ref,ROOT/trx
def counts(trx):return collections.Counter(m.attrib['className'].rsplit('.',1)[-1] for m in ET.parse(trx).findall('.//{*}TestMethod'))
def expected_nodes(path):
    _,old_path,_=prior();old=json.loads(old_path.read_bytes());current=json.loads(path.read_bytes());expected={}
    for key,value in old['Results'].items():expected[key]={a['Id'] for a in json.loads(value)['Actors']}
    for name in NAMES:
        for mode in ['before','restored']:
            key=name+'.'+mode+'.json';w=json.loads((path.parent/key).read_bytes());actors={w['PlayerControl']};actual={a['Id']:a for a in json.loads(current['Results'][key])['Actors']}
            for p in w.get('Treaties',{}).get('Papers',[])+w.get('Treaties',{}).get('Operations',[]):actors.update([p['Author'],p['Recipient']])
            state=w.get('Treaties',{}).get('Defense') or {}
            for p in state.get('Papers',[]):actors.update([p['Sender'],p['Recipient']])
            for e in state.get('Entries',[]):
                if e['Input']['Action']!='Dispatch':continue
                t=e['Terms'];muster=t['Muster'];m=next(m for m in w['NpcArmies']['Musters'] if m['Id']==muster)
                army_units=[u for u in w['Units'] if u['Organization']==m['Owner'] and u['Commander']==m['Commander']];army_ids={u['Army'] for u in army_units};assert len(army_ids)==1;army=next(iter(army_ids))
                returned=any(r['Muster']==muster for r in w['NpcArmies']['Returns'])
                if returned:assert army not in actual;continue
                marker='defense-army:'+muster;actors.add(marker);assert marker in actual
                commander=actual[m['Commander']]['Position'];own=actual[w['PlayerControl']]['Position'];mark=actual[marker]
                assert all(abs(a-b)<=0.001 for a,b in zip(commander,mark['Position']))
                assert mark['Soldiers']==sum(int(u['Soldiers']) for u in army_units) and not mark['Attack']
                visible=abs(commander[0]-own[0])<=80.001 and abs(commander[2]-own[2])<=80.001
                if visible:actors.add(army);assert actual[army]['Position']==commander and actual[army]['Soldiers']==mark['Soldiers']
                else:assert army not in actual,'Remote live army must not be projected into local sight'
            expected[key]=actors
    assert current['Status']=='PASSED' and current['Files']==512 and current['Pairs']==256 and set(current['Results'])==set(expected)
    for key,value in current['Results'].items():
        rows=json.loads(value)['Actors'];ids=[a['Id'] for a in rows];assert len(ids)==len(set(ids)) and set(ids)==expected[key],(key,ids,expected[key])
    assert current['Nodes']==sum(map(len,expected.values()))
    return dict(files=512,pairs=256,nodes=current['Nodes'],gui='NOT_USED',screens='NOT_CAPTURED')
def check(v):
    assert v['status']=='PASSED' and v['scope']=='headless-stock-backed-owned-garrison-defense-duty-physical-reports'
    assert v['sources']==sources()==json.loads((OUT/'run-inputs.json').read_bytes()),'Verification inputs changed'
    for group in ['binaries','proofs']:
        for f,h in v[group].items():assert sha(ROOT/f)==h,f
    _,_,old_trx=prior();expected=counts(old_trx);expected['TreatyDefenseTests']=35;trx=OUT/'tests/treaty-defense-current.trx';c=ET.parse(trx).find('.//{*}Counters').attrib
    assert all(counts(trx)[k]==n for k,n in expected.items()) and sum(expected.values())==1908 and int(c['passed'])==3793 and c['passed']==c['total'] and c['failed']==c['notExecuted']=='0' and c==v['tests']['counters']
    assert v['node3d']==expected_nodes(ROOT/v['projection_result'])
    assert v['binaries']['Core/bin/Debug/net8.0/HakutaiKagami.Core.dll']==v['binaries']['Tests/bin/Debug/net8.0/HakutaiKagami.Core.dll']==v['binaries']['Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Core.dll']
    assert v['gui']=='NOT_RUN' and v['full_core_suite']=='PASSED' and v['related_tests']==1908
def main():
    parser=argparse.ArgumentParser();parser.add_argument('--check-only',action='store_true');parser.add_argument('--collect-existing',action='store_true');a=parser.parse_args();manifest=BASE/'verification.json'
    if a.check_only:check(json.loads(manifest.read_bytes()));print('PASS current defense aid verification');return
    OUT.mkdir(parents=True,exist_ok=True);before=sources();collector=None
    if a.collect_existing:
        old_inputs=json.loads((OUT/'pre-collector-inputs.json').read_bytes());changed={k for k in set(before)|set(old_inputs) if before.get(k)!=old_inputs.get(k)}
        assert changed=={'Scripts/verify_treaty_defense.py'} and sha(OUT/'pre-collector-verifier.py')==old_inputs['Scripts/verify_treaty_defense.py']
        # Preserve original pre-collector inputs across collector-only retries.
        collector=dict(reason='Orchestrator timed out at55min; original native VSTest and testhost continued to completion. No Core/Tests/Game input or binary was changed; native full TRX retained, no test rerun.',changed=sorted(changed),original_inputs=(OUT/'pre-collector-inputs.json').relative_to(ROOT).as_posix())
        dirs=list((OUT/'projection').iterdir());assert len(dirs)==1;projection=dirs[0];economy=projection/'economy'
        c=ET.parse(OUT/'tests/treaty-defense-current.trx').find('.//{*}Counters').attrib;assert c['passed']==c['total']=='3793' and c['failed']==c['notExecuted']=='0'
        (OUT/'run-inputs.json').write_bytes((json.dumps(before,indent=2)+'\n').encode())
    else:
        OUT.mkdir(parents=True,exist_ok=True);before=sources();(OUT/'run-inputs.json').write_bytes((json.dumps(before,indent=2)+'\n').encode())
        projection=OUT/'projection'/uuid.uuid4().hex;projection.mkdir(parents=True);economy=projection/'economy';economy.mkdir();os.environ['HAKUTAI_ECONOMY_QA_DIR']=str(economy);os.environ['HAKUTAI_WORLD_CLASH_QA_DIR']=str(projection);os.environ['HAKUTAI_PROJECTION_EXPECTED_PAIRS']='256'
        run(['dotnet','test','Tests/HakutaiKagami.Tests.csproj','--logger','trx;LogFileName=treaty-defense-current.trx','--logger','console;verbosity=normal','--results-directory',str(OUT/'tests'),'--nologo','--verbosity','minimal'],OUT/'tests.log')
    os.environ['HAKUTAI_ECONOMY_QA_DIR']=str(economy);os.environ['HAKUTAI_WORLD_CLASH_QA_DIR']=str(projection);os.environ['HAKUTAI_PROJECTION_EXPECTED_PAIRS']='256'
    run(['dotnet','build','Game/HakutaiKagami.Game.csproj','--no-restore','--nologo','--verbosity','quiet'],OUT/'game-build.log')
    exe=ROOT/'.tools/godot-4.5.2/Godot_v4.5.2-stable_mono_win64/Godot_v4.5.2-stable_mono_win64_console.exe';run([str(exe),'--headless','--path',str(ROOT/'Game'),'res://Scenes/WorldClashProjectionCheck.tscn'],OUT/'node3d.log')
    n=expected_nodes(projection/'projection-result.json');assert before==sources(),'Inputs changed during verification'
    for f in projection.glob('*.before.json'):assert f.read_bytes()==f.with_name(f.name.replace('.before.json','.restored.json')).read_bytes(),f
    for name in ['work','collected']:
        for suffix in ['.json','.view.json']:assert (economy/(name+'.before'+suffix)).read_bytes()==(economy/(name+'.restored'+suffix)).read_bytes()
    c=ET.parse(OUT/'tests/treaty-defense-current.trx').find('.//{*}Counters').attrib
    binaries=['Core/bin/Debug/net8.0/HakutaiKagami.Core.dll','Tests/bin/Debug/net8.0/HakutaiKagami.Core.dll','Tests/bin/Debug/net8.0/HakutaiKagami.Tests.dll','Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Core.dll','Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Game.dll'];proofs=[*prior(),OUT/'run-inputs.json',OUT/'tests.log',OUT/'tests/treaty-defense-current.trx',OUT/'game-build.log',OUT/'node3d.log',*projection.glob('*.json'),*economy.glob('*.json')]
    v=dict(schema=1,status='PASSED',scope='headless-stock-backed-owned-garrison-defense-duty-physical-reports',baseline_main='19a5c2cd49b9228dfcf492d8206693dc17504ed6',gui='NOT_RUN',full_core_suite='PASSED',related_tests=1908,tests=dict(passed=int(c['passed']),failed=0,skipped=0,counters=c),new_tests=35,recovery_phases=14,sources=before,binaries={f:sha(ROOT/f) for f in binaries},proofs={f.relative_to(ROOT).as_posix():sha(f) for f in proofs},projection_result=(projection/'projection-result.json').relative_to(ROOT).as_posix(),node3d=n)
    if collector:
        v['collector_followup']=collector
        for f in [OUT/'pre-collector-verifier.py',OUT/'pre-collector-inputs.json',OUT/'discovery.log']:v['proofs'][f.relative_to(ROOT).as_posix()]=sha(f)
    manifest.write_bytes((json.dumps(v,ensure_ascii=False,indent=2)+'\n').encode());check(v);print('PASS3793 full Core tests (1908 related,35 new),14 real generation recoveries,Game build,256 headless pairs; GUI NOT_RUN',flush=True)
if __name__=='__main__':main()
