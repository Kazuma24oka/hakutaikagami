"""Versioned growth, saved-world compatibility and headless projection; no GUI automation."""
from pathlib import Path
import argparse,collections,hashlib,json,os,shutil,subprocess,uuid,xml.etree.ElementTree as ET
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'Docs/Design/NpcContinuingV1';OUT=BASE/'runtime-local'
CLASSES=['NpcContinuingTests','NewGameNpcRoutineTests','LocalWorkDemandTests','CustodyReturnTests','TrafficWorldTests','TravelControlTests','TravelWorldTests','BranchRouteTests','RegionalBranchTests','CustodyFoodTests','CommunityEconomyTests','HouseSalaryTests','MilitaryEconomyTests','TreatySupplyTests','NpcCommunityAdministrationTests','CommunityStewardshipTests','CustodySuccessionTests','HostageCustodyTests','AdultMarriageTests','AdultAdoptionTests','HouseSuccessionTests','CharacterRelationsTests','PersonalCompanionTests','PrivateInspectionTests','PersonalServiceTests','RetainerReleaseTests','CharacterDevelopmentTests','NewGameWorldTests','NewGameSetupTests','NewGameCompactAuthorityTests','NewGameFlowBoundaryTests','CommonContractTests','LocalInspectionFindingsTests','NewGameWorkTests','WorkFollowupTests','PersonalLifeTests','MovementControlTests','RouteContractTests']
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def sources():
    files=[Path(__file__),ROOT/'Scripts/probe_saved_worlds.py',ROOT/'Scripts/current_test_contract.json']
    for folder in ['Core','Tests','Game']:
        files.extend(p for p in (ROOT/folder).rglob('*') if p.is_file() and p.suffix in {'.cs','.csproj','.json','.props','.targets','.godot','.tscn'} and not {'bin','obj','.godot','TestResults'}.intersection(p.parts))
    files.extend(ROOT/f for f in ['.gitattributes','global.json','Directory.Build.props','Directory.Build.targets','NuGet.Config'] if (ROOT/f).is_file())
    return {p.relative_to(ROOT).as_posix():sha(p) for p in sorted(set(files))}
def run(args,log):
    print('RUN '+str(log),flush=True)
    with log.open('wb') as out:p=subprocess.run(args,cwd=ROOT,stdout=out,stderr=subprocess.STDOUT,creationflags=subprocess.CREATE_NO_WINDOW,timeout=10800)
    assert p.returncode==0,log
def prior():
    path=ROOT/'Docs/Design/CustodyReturnV1/verification.json';v=json.loads(path.read_bytes());ref=ROOT/v['projection_result'];assert sha(ref)==v['proofs'][ref.relative_to(ROOT).as_posix()]
    trx=ROOT/next(k for k in v['proofs'] if k.endswith('/tests/custody-return-current.trx'));assert sha(trx)==v['proofs'][trx.relative_to(ROOT).as_posix()]
    return path,ref,trx,v
def counts(trx):return collections.Counter(m.attrib['className'].rsplit('.',1)[-1] for m in ET.parse(trx).findall('.//{*}TestMethod'))
def expected_nodes(path):
    _,old_path,_,_=prior();old=json.loads(old_path.read_bytes());current=json.loads(path.read_bytes())
    expected={k:json.loads(value) for k,value in old['Results'].items()}
    for n in range(5):
        for mode in ['before','restored']:
            key=f'npc-continuing-{n}.{mode}.json';w=json.loads((path.parent/key).read_bytes());row=json.loads(current['Results'][key]);ids={w['PlayerControl']}|{d['Actor'] for d in w['NpcWork']['Decisions']};assert {x['Id'] for x in row['Actors']}==ids;expected[key]=row
    assert current['Status']=='PASSED' and current['Files']==602 and current['Pairs']==301 and set(current['Results'])==set(expected)
    for key,value in expected.items():assert json.loads(current['Results'][key])==value,key
    assert current['Nodes']==1742
    return dict(files=602,pairs=301,nodes=1742,gui='NOT_USED',screens='NOT_CAPTURED')
def check(v):
    assert v['status']=='PASSED' and v['scope']=='headless-adult-npc-own-demand-continuing-work-and-occupancy'
    assert v['sources']==sources()==json.loads((OUT/'run-inputs.json').read_bytes())
    for group in ['binaries','proofs']:
        for f,h in v[group].items():assert sha(ROOT/f)==h,f
    _,_,old_trx,_=prior();old=counts(ROOT/'Docs/Design/TreatyDefenseV1/runtime-local/tests/treaty-defense-current.trx');old=dict(old);old.update(dict(counts(old_trx)));old['CharacterDevelopmentTests']=31;old['CharacterRelationsTests']=19;old['AdultAdoptionTests']=26;expected={c:18 if c=='NpcContinuingTests' else old[c] for c in CLASSES};assert all(expected.values())
    trx=OUT/'tests/npc-continuing-current.trx';assert dict(counts(trx))==expected
    c=ET.parse(trx).find('.//{*}Counters').attrib;assert int(c['passed'])==sum(expected.values()) and c['passed']==c['total'] and c['failed']==c['notExecuted']=='0' and c==v['tests']['counters']
    assert v['node3d']==expected_nodes(ROOT/v['projection_result'])
    assert v['binaries']['Core/bin/Debug/net8.0/HakutaiKagami.Core.dll']==v['binaries']['Tests/bin/Debug/net8.0/HakutaiKagami.Core.dll']==v['binaries']['Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Core.dll']
    assert v['gui']=='NOT_RUN' and v['full_core_suite']=='NOT_RUN' and v['new_tests']==18
def main():
    p=argparse.ArgumentParser();p.add_argument('--check-only',action='store_true');a=p.parse_args();manifest=BASE/'verification.json'
    if a.check_only:check(json.loads(manifest.read_bytes()));print('PASS character development verification');return
    OUT.mkdir(parents=True,exist_ok=True);before=sources();(OUT/'run-inputs.json').write_bytes((json.dumps(before,indent=2)+'\n').encode())
    projection=OUT/'projection'/uuid.uuid4().hex;projection.mkdir(parents=True);_,oldpath,_,old=prior()
    for key in json.loads(oldpath.read_bytes())['Results']:
        src=oldpath.parent/key;assert sha(src)==old['proofs'][src.relative_to(ROOT).as_posix()];shutil.copyfile(src,projection/key)
    os.environ['HAKUTAI_WORLD_CLASH_QA_DIR']=str(projection);os.environ['HAKUTAI_PROJECTION_EXPECTED_PAIRS']='301'
    # PersonalLife's repeated old snapshots must remain byte-identical; prove this after execution.
    filt='|'.join('FullyQualifiedName~'+c for c in CLASSES)
    run(['dotnet','test','Tests/HakutaiKagami.Tests.csproj','--filter',filt,'--logger','trx;LogFileName=npc-continuing-current.trx','--results-directory',str(OUT/'tests'),'--nologo','--verbosity','minimal'],OUT/'tests.log')
    for key in json.loads(oldpath.read_bytes())['Results']:assert (projection/key).read_bytes()==(oldpath.parent/key).read_bytes(),key
    run(['dotnet','build','Game/HakutaiKagami.Game.csproj','--no-restore','--nologo','--verbosity','quiet'],OUT/'game-build.log')
    exe=ROOT/'.tools/godot-4.5.2/Godot_v4.5.2-stable_mono_win64/Godot_v4.5.2-stable_mono_win64_console.exe'
    run([str(exe),'--headless','--path',str(ROOT/'Game'),'res://Scenes/WorldClashProjectionCheck.tscn'],OUT/'node3d.log')
    n=expected_nodes(projection/'projection-result.json');assert before==sources()
    for f in projection.glob('*.before.json'):assert f.read_bytes()==f.with_name(f.name.replace('.before.json','.restored.json')).read_bytes(),f
    c=ET.parse(OUT/'tests/npc-continuing-current.trx').find('.//{*}Counters').attrib
    binaries=['Core/bin/Debug/net8.0/HakutaiKagami.Core.dll','Tests/bin/Debug/net8.0/HakutaiKagami.Core.dll','Tests/bin/Debug/net8.0/HakutaiKagami.Tests.dll','Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Core.dll','Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Game.dll'];proofs=[*prior()[:3],OUT/'run-inputs.json',OUT/'tests.log',OUT/'tests/npc-continuing-current.trx',OUT/'game-build.log',OUT/'node3d.log',*projection.glob('*.json'),*projection.glob('*.txt')]
    v=dict(schema=1,status='PASSED',scope='headless-adult-npc-own-demand-continuing-work-and-occupancy',gui='NOT_RUN',full_core_suite='NOT_RUN',prior_full_core=3793,tests=dict(passed=int(c['passed']),failed=0,skipped=0,counters=c),new_tests=18,recovery_phases=5,sources=before,binaries={f:sha(ROOT/f) for f in binaries},proofs={f.relative_to(ROOT).as_posix():sha(f) for f in proofs},projection_result=(projection/'projection-result.json').relative_to(ROOT).as_posix(),node3d=n)
    manifest.write_bytes((json.dumps(v,ensure_ascii=False,indent=2)+'\n').encode());check(v);print('PASS selected current tests='+c['passed']+';18 new;5 recoveries;301 headless pairs;full current/GUI NOT_RUN',flush=True)
if __name__=='__main__':main()
