"""Normal Godot entry + scoped permission/cancellation delivery; no war simulation claim."""
from pathlib import Path
import argparse,json,uuid,subprocess,xml.etree.ElementTree as ET
from verify_service_entry import ROOT,EXE,run,pairs,sha,write
from verify_household_military import sources as game_sources

BASE=ROOT/'Docs/Design/EngagementTaskDeliveryV1'
OUT=BASE/'runtime-local'
CASE='engagementtask-recovery'
def sources():
    s=game_sources()
    for p in [Path(__file__),ROOT/'Scripts/current_test_contract.json']:
        s[p.relative_to(ROOT).as_posix()]=sha(p)
    return s

def check(v):
    assert v['status']=='PASSED' and v['scope']=='normal-game-conditional-engagement-delivery'
    assert v['sources']==sources(),'sources changed since verification'
    for group in ['binaries','proofs']:
        for p,h in v[group].items():assert sha(ROOT/p)==h,p
    trx=ET.parse(OUT/'tests/delivery-current.trx');c=trx.find('.//{*}Counters').attrib
    assert c==v['core_tests']['counters'] and c['passed']=='166' and c['failed']=='0' and c['notExecuted']=='0'
    assert v['gpu']==dict(processes=2,savepoints=6,comparisons=12)
    assert v['real_war_loop']=='NOT_CONNECTED'

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--check-only',action='store_true');args=parser.parse_args()
    manifest=BASE/'verification.json'
    if args.check_only:check(json.loads(manifest.read_bytes()));print('PASS bound engagement delivery verification');return
    OUT.mkdir(parents=True,exist_ok=True)
    initial=sources()
    filter='|'.join('FullyQualifiedName~'+x for x in ['FieldEngagementTests','ContactCombatTests','EngagementMissionTests','EngagementTaskDeliveryTests','MilitaryAuthorityPolicyTests','MilitaryAuthorityScopeTests','MilitaryAuthorityKnowledgeTests','MilitaryIssuerAuthorityTests','NewGameWorldTests'])
    run(['dotnet','test','Tests/HakutaiKagami.Tests.csproj','--filter',filter,'--logger','trx;LogFileName=delivery-current.trx','--results-directory',OUT/'tests','--nologo','--verbosity','minimal'],OUT/'test-current.log',timeout=900)
    c=ET.parse(OUT/'tests/delivery-current.trx').find('.//{*}Counters').attrib
    assert c['failed']=='0' and c['notExecuted']=='0' and int(c['passed'])>=99,c
    run(['dotnet','build','Game/HakutaiKagami.Game.csproj','--no-restore','--nologo','--verbosity','quiet'],OUT/'build.log',timeout=180)
    binaries={p:sha(ROOT/p) for p in ['Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Core.dll','Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Game.dll','Tests/bin/Debug/net8.0/HakutaiKagami.Core.dll','Tests/bin/Debug/net8.0/HakutaiKagami.Tests.dll']}
    assert binaries['Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Core.dll']==binaries['Tests/bin/Debug/net8.0/HakutaiKagami.Core.dll']
    folder=OUT/CASE;folder.mkdir(parents=True,exist_ok=True);key=uuid.uuid4().hex
    command=[EXE,'--path',ROOT/'Game','--resolution','1280x900','--','--new-game-test-key='+key,'--new-game-case='+CASE]
    run(command,folder/'check.log','ENGAGEMENT_TASK_DELIVERY_PASSED',timeout=900)
    run(command+['--new-game-restore'],folder/'restore.log','ENGAGEMENT_TASK_DELIVERY_PASSED',timeout=1500)
    assert sources()==initial,'source changed during actual run'
    assert pairs(folder,6)==12
    for n in range(6):
        for a,b in [(f's{n}',f'restored-s{n}'),('final',f'continued-s{n}')]:
            assert (folder/(a+'-pose.json')).read_bytes()==(folder/(b+'-pose.json')).read_bytes(),(a,b,'pose')
    proof=[OUT/'test-current.log',OUT/'tests/delivery-current.trx',OUT/'build.log']
    proof += [p for p in folder.iterdir() if p.is_file() and p.suffix in {'.png','.json','.sha256','.txt','.log'}]
    v=dict(schema=1,status='PASSED',scope='normal-game-conditional-engagement-delivery',
           baseline_revision=subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip(),
           core_tests=dict(passed=int(c['passed']),failed=0,skipped=0,counters=c),
           gpu=dict(processes=2,savepoints=6,comparisons=12),
           real_war_loop='NOT_CONNECTED',full_core_suite='NOT_RUN',sources=initial,binaries=binaries,
           proofs={p.relative_to(ROOT).as_posix():sha(p) for p in proof})
    manifest.write_bytes((json.dumps(v,ensure_ascii=False,indent=2)+'\n').encode('utf-8'))
    check(v);print('PASS normal entry permission/cancellation, six saves, twelve world/text/pixel/pose comparisons')
if __name__=='__main__':main()
