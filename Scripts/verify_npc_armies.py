"""CLI-only NPC garrison/common land travel/return verification. Never captures or controls a GUI."""
from pathlib import Path
import argparse,collections,hashlib,json,subprocess,xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'Docs/Design/NpcArmyV1';OUT=BASE/'runtime-local'
CLASSES=['FactionStanceTests','FactionStanceKnowledgeTests','FieldEngagementTests','ContactCombatTests',
         'EngagementMissionTests','EngagementTaskDeliveryTests','MilitaryAuthorityPolicyTests',
         'MilitaryAuthorityScopeTests','MilitaryAuthorityKnowledgeTests','MilitaryIssuerAuthorityTests','NewGameWorldTests',
         'NpcArmyTests','TravelWorldTests','TravelControlTests','NewCampaignStoreTests','VoyageExpenseTests',
         'HouseholdMilitaryTests','NewGameNpcRoutineTests']
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def sources():
    files=[Path(__file__),ROOT/'Scripts/current_test_contract.json']
    for folder in ['Core','Tests','Game']:
        files += [p for p in (ROOT/folder).rglob('*') if p.is_file() and p.suffix in
                  {'.cs','.csproj','.json','.props','.targets','.godot','.tscn'} and
                  not {'bin','obj','.godot','TestResults'}.intersection(p.parts)]
    for name in ['global.json','Directory.Build.props','Directory.Build.targets','NuGet.Config']:
        if (ROOT/name).is_file():files.append(ROOT/name)
    return {p.relative_to(ROOT).as_posix():sha(p) for p in sorted(set(files))}
def run(command,log):
    p=subprocess.run(command,cwd=ROOT,capture_output=True,creationflags=subprocess.CREATE_NO_WINDOW,timeout=1200)
    log.write_bytes(p.stdout+p.stderr);assert p.returncode==0,log
def check(v):
    assert v['status']=='PASSED' and v['scope']=='headless-npc-garrison-common-land-travel-return'
    assert v['sources']==sources(),'current source changed'
    for group in ['binaries','proofs']:
        for p,h in v[group].items():assert sha(ROOT/p)==h,p
    r=ET.parse(OUT/'tests/npc-army-current.trx');c=r.find('.//{*}Counters').attrib
    assert int(c['passed'])>=231 and c['passed']==c['total'] and c['failed']==c['notExecuted']=='0' and c==v['tests']['counters'],c
    counts=collections.Counter(m.attrib['className'].rsplit('.',1)[-1] for m in r.findall('.//{*}TestMethod'))
    assert counts['FactionStanceTests']==24 and counts['FactionStanceKnowledgeTests']==9 and counts['NpcArmyTests']==34,counts
    assert v['gui']=='NOT_RUN' and v['full_core_suite']=='NOT_RUN'
    assert v['binaries']['Core/bin/Debug/net8.0/HakutaiKagami.Core.dll']==v['binaries']['Tests/bin/Debug/net8.0/HakutaiKagami.Core.dll']==v['binaries']['Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Core.dll']
def main():
    parser=argparse.ArgumentParser();parser.add_argument('--check-only',action='store_true');a=parser.parse_args()
    manifest=BASE/'verification.json'
    if a.check_only:check(json.loads(manifest.read_bytes()));print('PASS current CLI-only NPC army verification');return
    OUT.mkdir(parents=True,exist_ok=True);before=sources()
    run(['dotnet','test','Tests/HakutaiKagami.Tests.csproj','--filter','|'.join('FullyQualifiedName~'+x for x in CLASSES),'--logger','trx;LogFileName=npc-army-current.trx','--results-directory',str(OUT/'tests'),'--nologo','--verbosity','minimal'],OUT/'tests.log')
    run(['dotnet','build','Game/HakutaiKagami.Game.csproj','--no-restore','--nologo','--verbosity','quiet'],OUT/'game-build.log')
    assert before==sources(),'source changed during CLI verification'
    c=ET.parse(OUT/'tests/npc-army-current.trx').find('.//{*}Counters').attrib
    binaries=['Core/bin/Debug/net8.0/HakutaiKagami.Core.dll','Tests/bin/Debug/net8.0/HakutaiKagami.Core.dll','Tests/bin/Debug/net8.0/HakutaiKagami.Tests.dll','Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Core.dll','Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Game.dll']
    v=dict(schema=1,status='PASSED',scope='headless-npc-garrison-common-land-travel-return',gui='NOT_RUN',full_core_suite='NOT_RUN',
           baseline_main='9762c59994e1fc9b721b1356eb1534343d5e2fca',tests=dict(passed=int(c['passed']),failed=0,skipped=0,counters=c),
           new_tests=34,sources=before,binaries={p:sha(ROOT/p) for p in binaries},
           proofs={p.relative_to(ROOT).as_posix():sha(p) for p in [OUT/'tests.log',OUT/'tests/npc-army-current.trx',OUT/'game-build.log']})
    manifest.write_bytes((json.dumps(v,ensure_ascii=False,indent=2)+'\n').encode('utf-8'))
    check(v);print('PASS '+c['passed']+' related Core tests,34 new; Game build only; no screen/GUI operation',flush=True)
if __name__=='__main__':main()
