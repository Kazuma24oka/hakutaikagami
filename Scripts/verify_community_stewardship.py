"""CLI-only house rice salary, actual generation recovery and Node3D placement. No GUI."""
from pathlib import Path
import argparse,collections,hashlib,json,subprocess,os,uuid,xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'Docs/Design/CommunityStewardshipV1';OUT=BASE/'runtime-local'
CLASSES=['FactionStanceTests','FactionStanceKnowledgeTests','FieldEngagementTests','ContactCombatTests',
         'EngagementMissionTests','EngagementTaskDeliveryTests','MilitaryAuthorityPolicyTests',
         'MilitaryAuthorityScopeTests','MilitaryAuthorityKnowledgeTests','MilitaryIssuerAuthorityTests','NewGameWorldTests',
         'NpcArmyTests','TravelWorldTests','TravelControlTests','NewCampaignStoreTests','VoyageExpenseTests',
         'HouseholdMilitaryTests','NewGameNpcRoutineTests','FactionNoticeTests','FactionReactionTests',
         'FactionProclamationTests','SparseCombatTests','RegionalEconomyTests','PersonalCompanionTests','TruceNegotiationTests','PrivateInformationTests','BranchRouteTests','LocalInspectionFindingsTests','NewGameWorkTests','TransportWorkTests','TransportWorkLocationTests','PrivateStanddownTests','PrivateInspectionTests','CooperationTalkTests','PersonalServiceTests','NewGameServiceTests','RetainerReleaseTests','CommunityEconomyTests','RegionalTradeTests','HouseSalaryTests','VillageConsultationTests','NpcCommunityAdministrationTests','CommunityStewardshipTests']
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def sources():
    files=[Path(__file__),ROOT/'Scripts/current_test_contract.json']
    for folder in ['Core','Tests','Game']:
        files += [p for p in (ROOT/folder).rglob('*') if p.is_file() and p.suffix in
                  {'.cs','.csproj','.json','.props','.targets','.godot','.tscn'} and
                  not {'bin','obj','.godot','TestResults'}.intersection(p.parts)]
    for name in ['.gitattributes','global.json','Directory.Build.props','Directory.Build.targets','NuGet.Config']:
        if (ROOT/name).is_file():files.append(ROOT/name)
    return {p.relative_to(ROOT).as_posix():sha(p) for p in sorted(set(files))}
def run(command,log):
    p=subprocess.run(command,cwd=ROOT,capture_output=True,creationflags=subprocess.CREATE_NO_WINDOW,timeout=1200)
    log.write_bytes(p.stdout+p.stderr);assert p.returncode==0,log
def check(v):
    assert v['status']=='PASSED' and v['scope']=='headless-community-stewardship-and-local-npc-administration'
    assert v['sources']==sources(),'current source changed'
    for group in ['binaries','proofs']:
        for p,h in v[group].items():assert sha(ROOT/p)==h,p
    r=ET.parse(OUT/'tests/community-stewardship-current.trx');c=r.find('.//{*}Counters').attrib
    assert int(c['passed'])==841 and c['passed']==c['total'] and c['failed']==c['notExecuted']=='0' and c==v['tests']['counters'],c
    counts=collections.Counter(m.attrib['className'].rsplit('.',1)[-1] for m in r.findall('.//{*}TestMethod'))
    assert counts['HouseSalaryTests']==24 and counts['VillageConsultationTests']==36 and counts['NpcCommunityAdministrationTests']==19 and counts['CommunityStewardshipTests']==32 and counts['RegionalTradeTests']==25 and counts['CommunityEconomyTests']==20 and counts['RetainerReleaseTests']==14 and counts['PersonalServiceTests']==15 and counts['NewGameServiceTests']==25 and counts['CooperationTalkTests']==12 and counts['PrivateInspectionTests']==12 and counts['PrivateStanddownTests']==12 and counts['FactionStanceTests']==24 and counts['FactionStanceKnowledgeTests']==9 and counts['NpcArmyTests']==48 and counts['FieldEngagementTests']==35 and counts['FactionNoticeTests']==15 and counts['FactionReactionTests']==14 and counts['FactionProclamationTests']==5 and counts['SparseCombatTests']==9 and counts['RegionalEconomyTests']==19 and counts['PersonalCompanionTests']==23 and counts['TruceNegotiationTests']==23 and counts['PrivateInformationTests']==18 and counts['BranchRouteTests']==29 and counts['LocalInspectionFindingsTests']==21 and counts['NewGameWorkTests']==40 and counts['TransportWorkTests']==26 and counts['TransportWorkLocationTests']==3,counts
    assert v['gui']=='NOT_RUN' and v['full_core_suite']=='NOT_RUN'
    assert v['node3d']==dict(files=90,pairs=45,nodes=154,gui='NOT_USED',screens='NOT_CAPTURED')
    assert v['binaries']['Core/bin/Debug/net8.0/HakutaiKagami.Core.dll']==v['binaries']['Tests/bin/Debug/net8.0/HakutaiKagami.Core.dll']==v['binaries']['Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Core.dll']
def main():
    parser=argparse.ArgumentParser();parser.add_argument('--check-only',action='store_true');a=parser.parse_args()
    manifest=BASE/'verification.json'
    if a.check_only:check(json.loads(manifest.read_bytes()));print('PASS current CLI-only community stewardship verification');return
    OUT.mkdir(parents=True,exist_ok=True);before=sources();projection=OUT/"projection"/uuid.uuid4().hex;projection.mkdir(parents=True);economy=projection/"economy";economy.mkdir();os.environ["HAKUTAI_ECONOMY_QA_DIR"]=str(economy);os.environ["HAKUTAI_WORLD_CLASH_QA_DIR"]=str(projection);os.environ["HAKUTAI_PROJECTION_EXPECTED_PAIRS"]="45"
    run(['dotnet','test','Tests/HakutaiKagami.Tests.csproj','--filter','|'.join('FullyQualifiedName~'+x for x in CLASSES),'--logger','trx;LogFileName=community-stewardship-current.trx','--results-directory',str(OUT/'tests'),'--nologo','--verbosity','minimal'],OUT/'tests.log')
    run(['dotnet','build','Game/HakutaiKagami.Game.csproj','--no-restore','--nologo','--verbosity','quiet'],OUT/'game-build.log')
    exe=ROOT/'.tools/godot-4.5.2/Godot_v4.5.2-stable_mono_win64/Godot_v4.5.2-stable_mono_win64_console.exe'
    run([str(exe),'--headless','--path',str(ROOT/'Game'),'res://Scenes/WorldClashProjectionCheck.tscn'],OUT/'node3d.log')
    node3d=json.loads((projection/'projection-result.json').read_bytes());assert node3d['Status']=='PASSED' and node3d['Files']==90 and node3d['Pairs']==45 and node3d['Nodes']==154
    assert before==sources(),'source changed during CLI verification'
    for name in ['steward-0','steward-1','steward-2','steward-3','steward-4','npc-community-0','npc-community-1','npc-community-2','village-0','village-1','village-2','village-3','village-4','salary-owed','salary-prepared','salary-paid','salary-paid-employee','trade-accepted','trade-travel','trade-delivered','trade-settled','trade-supplied','community-paused','community-resumed','companion-travel','companion-arrival','truce-counter-flight','truce-formed','info-sealed','info-paid','standdown-holding','standdown-released','private-inspection-working','private-inspection-opened','personal-service-before','personal-service-pledged','personal-service-released','retainer-release-request','retainer-release-reply','retainer-release-known']:
        assert (projection/(name+'.before.json')).read_bytes()==(projection/(name+'.restored.json')).read_bytes(),name
    for name in ['work','collected']:
        for suffix in ['.json','.view.json']:
            assert (economy/(name+'.before'+suffix)).read_bytes()==(economy/(name+'.restored'+suffix)).read_bytes(),name
    c=ET.parse(OUT/'tests/community-stewardship-current.trx').find('.//{*}Counters').attrib
    binaries=['Core/bin/Debug/net8.0/HakutaiKagami.Core.dll','Tests/bin/Debug/net8.0/HakutaiKagami.Core.dll','Tests/bin/Debug/net8.0/HakutaiKagami.Tests.dll','Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Core.dll','Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Game.dll']
    v=dict(schema=1,status='PASSED',scope='headless-community-stewardship-and-local-npc-administration',gui='NOT_RUN',full_core_suite='NOT_RUN',
           baseline_main='0df60cc625e3759962639c4b2db56f3e0b1f32c0',tests=dict(passed=int(c['passed']),failed=0,skipped=0,counters=c),
           new_tests=32,sources=before,binaries={p:sha(ROOT/p) for p in binaries},
           node3d=dict(files=90,pairs=45,nodes=154,gui='NOT_USED',screens='NOT_CAPTURED'),proofs={p.relative_to(ROOT).as_posix():sha(p) for p in [OUT/'tests.log',OUT/'tests/community-stewardship-current.trx',OUT/'game-build.log',OUT/'node3d.log',*projection.glob('*.json'),*economy.glob('*.json')]})
    manifest.write_bytes((json.dumps(v,ensure_ascii=False,indent=2)+'\n').encode('utf-8'))
    check(v);print('PASS '+c['passed']+' related Core tests,32 new; Game build and headless Node3D data; no screen/GUI operation',flush=True)
if __name__=='__main__':main()
