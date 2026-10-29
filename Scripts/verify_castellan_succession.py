"""CLI verification of explicit administrative reauthorization and separate personal service. No GUI."""
from pathlib import Path
import argparse,collections,hashlib,json,subprocess,os,uuid,xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'Docs/Design/CastellanSuccessionV1';OUT=BASE/'runtime-local'
CLASSES=['CastellanSuccessionTests','StewardSuccessionTests','MilitarySuccessionTests','HouseSuccessionTests','CastellanReportsTests','CastleAdministrationTests','CastleHandoverTests','PersonalLifeTests','RoadConvoyTests','TrafficWorldTests','ArmyRoadTests','PreparedSubmitTests','FreightOperationsTests','FieldConvoyTests','NpcFreightTests','FactionStanceTests','FactionStanceKnowledgeTests','FieldEngagementTests','ContactCombatTests',
         'EngagementMissionTests','EngagementTaskDeliveryTests','MilitaryAuthorityPolicyTests',
         'MilitaryAuthorityScopeTests','MilitaryAuthorityKnowledgeTests','MilitaryIssuerAuthorityTests','NewGameWorldTests',
         'NpcArmyTests','TravelWorldTests','TravelControlTests','NewCampaignStoreTests','VoyageExpenseTests',
         'HouseholdMilitaryTests','NewGameNpcRoutineTests','FactionNoticeTests','FactionReactionTests',
         'FactionProclamationTests','SparseCombatTests','RegionalEconomyTests','PersonalCompanionTests','TruceNegotiationTests','PrivateInformationTests','BranchRouteTests','LocalInspectionFindingsTests','NewGameWorkTests','TransportWorkTests','TransportWorkLocationTests','PrivateStanddownTests','PrivateInspectionTests','CooperationTalkTests','PersonalServiceTests','NewGameServiceTests','RetainerReleaseTests','CommunityEconomyTests','RegionalTradeTests','HouseSalaryTests','VillageConsultationTests','NpcCommunityAdministrationTests','CommunityStewardshipTests','MaterialMarketTests','RegionalDomainTests','HouseLogisticsTests','HouseFreightReceivingTests','MilitaryEconomyTests','NewGameMilitaryTests','MilitaryDebriefTests','MilitaryRenewalTests','FieldSupplyTests']
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
    p=subprocess.run(command,cwd=ROOT,capture_output=True,creationflags=subprocess.CREATE_NO_WINDOW,timeout=2400)
    log.write_bytes(p.stdout+p.stderr);assert p.returncode==0,log

def prior_projection():
    oldmanifest=ROOT/'Docs/Design/StewardSuccessionV1/verification.json';old=json.loads(oldmanifest.read_bytes());ref=next(k for k in old['proofs'] if k.endswith('/projection-result.json'));assert sha(ROOT/ref)==old['proofs'][ref];return oldmanifest,ROOT/ref,json.loads((ROOT/ref).read_bytes())

def projection_counts(path):
    n=json.loads(path.read_bytes());_,_,prior=prior_projection()
    expected={k:len(json.loads(row)['Actors']) for k,row in prior['Results'].items()}
    for i in range(6):
        for mode in ['before','restored']:
            key=f'castle-succession-{i}.{mode}.json';w=json.loads((path.parent/key).read_bytes())
            expected[key]=1+sum(e['Input']['Action'] in ['Appoint','Reauthorize'] for e in w['Castellan']['Entries'])+sum(e['Terms'] is not None for e in w['Succession']['Entries'])
    assert n['Status']=='PASSED' and n['Files']==336 and n['Pairs']==168 and set(n['Results'])==set(expected)
    assert all(len(json.loads(row)['Actors'])==expected[k] for k,row in n['Results'].items()),'Saved-world actor inventory differs'
    assert n['Nodes']==sum(expected.values());return dict(files=336,pairs=168,nodes=n['Nodes'],gui='NOT_USED',screens='NOT_CAPTURED')

def check(v):
    assert v['status']=='PASSED' and v['scope']=='headless-castellan-succession-separate-personal-acceptance-custody'
    assert v['sources']==sources(),'current source changed'
    assert v['sources']==json.loads((OUT/'run-inputs.json').read_bytes()),'Run input checkpoint differs'
    for group in ['binaries','proofs']:
        for p,h in v[group].items():assert sha(ROOT/p)==h,p
    r=ET.parse(OUT/'tests/castellan-succession-current.trx');c=r.find('.//{*}Counters').attrib
    assert int(c['passed'])==1581 and c['passed']==c['total'] and c['failed']==c['notExecuted']=='0' and c==v['tests']['counters'],c
    counts=collections.Counter(m.attrib['className'].rsplit('.',1)[-1] for m in r.findall('.//{*}TestMethod'))
    assert counts['CastellanSuccessionTests']==27 and counts['StewardSuccessionTests']==61 and counts['MilitarySuccessionTests']==40 and counts['HouseSuccessionTests']==36 and counts['CastellanReportsTests']==20 and counts['CastleAdministrationTests']==31 and counts['CastleHandoverTests']==43 and counts['PersonalLifeTests']==47 and counts['RoadConvoyTests']==42 and counts['TrafficWorldTests']==17 and counts['ArmyRoadTests']==29 and counts['PreparedSubmitTests']==21 and counts['MilitaryEconomyTests']==38 and counts['NpcFreightTests']==28 and counts['FreightOperationsTests']==20 and counts['FieldConvoyTests']==21 and counts['NewGameMilitaryTests']==28 and counts['MilitaryDebriefTests']==11 and counts['MilitaryRenewalTests']==23 and counts['FieldSupplyTests']==26 and counts['HouseFreightReceivingTests']==17 and counts['HouseLogisticsTests']==37 and counts['RegionalDomainTests']==45 and counts['HouseSalaryTests']==24 and counts['VillageConsultationTests']==36 and counts['NpcCommunityAdministrationTests']==19 and counts['CommunityStewardshipTests']==32 and counts['MaterialMarketTests']==32 and counts['RegionalTradeTests']==25 and counts['CommunityEconomyTests']==20 and counts['RetainerReleaseTests']==14 and counts['PersonalServiceTests']==15 and counts['NewGameServiceTests']==25 and counts['CooperationTalkTests']==12 and counts['PrivateInspectionTests']==12 and counts['PrivateStanddownTests']==12 and counts['FactionStanceTests']==24 and counts['FactionStanceKnowledgeTests']==9 and counts['NpcArmyTests']==48 and counts['FieldEngagementTests']==35 and counts['FactionNoticeTests']==15 and counts['FactionReactionTests']==14 and counts['FactionProclamationTests']==5 and counts['SparseCombatTests']==9 and counts['RegionalEconomyTests']==19 and counts['PersonalCompanionTests']==23 and counts['TruceNegotiationTests']==23 and counts['PrivateInformationTests']==18 and counts['BranchRouteTests']==29 and counts['LocalInspectionFindingsTests']==21 and counts['NewGameWorkTests']==40 and counts['TransportWorkTests']==26 and counts['TransportWorkLocationTests']==3,counts
    assert v['gui']=='NOT_RUN' and v['full_core_suite']=='NOT_RUN'
    assert v['node3d']==projection_counts(ROOT/v['projection_result'])
    assert v['binaries']['Core/bin/Debug/net8.0/HakutaiKagami.Core.dll']==v['binaries']['Tests/bin/Debug/net8.0/HakutaiKagami.Core.dll']==v['binaries']['Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Core.dll']
def main():
    parser=argparse.ArgumentParser();parser.add_argument('--check-only',action='store_true');a=parser.parse_args()
    manifest=BASE/'verification.json'
    if a.check_only:check(json.loads(manifest.read_bytes()));print('PASS current administrative and personal service verification');return
    OUT.mkdir(parents=True,exist_ok=True);before=sources();(OUT/'run-inputs.json').write_bytes((json.dumps(before,ensure_ascii=False,indent=2)+'\n').encode());projection=OUT/"projection"/uuid.uuid4().hex;projection.mkdir(parents=True);economy=projection/"economy";economy.mkdir();os.environ["HAKUTAI_ECONOMY_QA_DIR"]=str(economy);os.environ["HAKUTAI_WORLD_CLASH_QA_DIR"]=str(projection);os.environ["HAKUTAI_PROJECTION_EXPECTED_PAIRS"]="168"
    run(['dotnet','test','Tests/HakutaiKagami.Tests.csproj','--filter','|'.join('FullyQualifiedName~'+x for x in CLASSES),'--logger','trx;LogFileName=castellan-succession-current.trx','--results-directory',str(OUT/'tests'),'--nologo','--verbosity','minimal'],OUT/'tests.log')
    run(['dotnet','build','Game/HakutaiKagami.Game.csproj','--no-restore','--nologo','--verbosity','quiet'],OUT/'game-build.log')
    exe=ROOT/'.tools/godot-4.5.2/Godot_v4.5.2-stable_mono_win64/Godot_v4.5.2-stable_mono_win64_console.exe'
    run([str(exe),'--headless','--path',str(ROOT/'Game'),'res://Scenes/WorldClashProjectionCheck.tscn'],OUT/'node3d.log')
    node3d=projection_counts(projection/'projection-result.json')
    assert before==sources(),'source changed during CLI verification'
    for name in [*[f'castle-succession-{i}' for i in range(6)],'steward-succession-0','steward-succession-1','steward-succession-2','steward-succession-3','steward-succession-4','steward-succession-5','steward-succession-6','steward-succession-7','household-service-0','household-service-1','household-service-2','household-service-3','retired-military-0','retired-military-1','retired-military-2','retired-military-3','military-succession-0','military-succession-1','military-succession-2','military-succession-3','military-succession-4','military-succession-5','house-succession-0','house-succession-1','house-succession-2','house-succession-3','house-succession-4','house-succession-5','house-succession-6','house-succession-7','house-succession-8','house-succession-9','castellan-report-0','castellan-report-1','castellan-report-2','castellan-report-3','castellan-0','castellan-1','castellan-2','castellan-3','castle-handover-0','castle-handover-1','castle-handover-2','castle-handover-3','castle-handover-4','castle-handover-5','castle-handover-6','castle-handover-7','personal-life-0','personal-life-1','personal-life-2','personal-life-3','personal-life-4','personal-life-5','personal-life-6','personal-life-7','road-supply-0','road-supply-1','road-supply-2','road-supply-3','road-supply-4','road-supply-5','road-supply-6','road-supply-7','road-supply-8','army-road-0','army-road-1','army-road-2','army-road-3','army-road-4','army-road-5','army-road-6','freight-letters-0','freight-letters-1','freight-letters-2','freight-letters-3','freight-letters-4','camp-convoy-0','camp-convoy-1','camp-convoy-2','camp-convoy-3','camp-convoy-4','npc-freight-0','npc-freight-1','npc-freight-2','npc-freight-3','npc-freight-4','mil-econ-0','mil-econ-1','mil-econ-2','mil-econ-3','mil-econ-4','mil-econ-5','mil-econ-6','mil-econ-7','mil-econ-8','mil-econ-9','proxy-freight-0','proxy-freight-1','proxy-freight-2','freight-0','freight-1','freight-2','freight-3','freight-4','freight-5','domains-0','domains-1','domains-2','domains-3','domains-4','domains-5','materials-0','materials-1','materials-2','materials-3','materials-4','steward-0','steward-1','steward-2','steward-3','steward-4','npc-community-0','npc-community-1','npc-community-2','village-0','village-1','village-2','village-3','village-4','salary-owed','salary-prepared','salary-paid','salary-paid-employee','trade-accepted','trade-travel','trade-delivered','trade-settled','trade-supplied','community-paused','community-resumed','companion-travel','companion-arrival','truce-counter-flight','truce-formed','info-sealed','info-paid','standdown-holding','standdown-released','private-inspection-working','private-inspection-opened','personal-service-before','personal-service-pledged','personal-service-released','retainer-release-request','retainer-release-reply','retainer-release-known']:
        assert (projection/(name+'.before.json')).read_bytes()==(projection/(name+'.restored.json')).read_bytes(),name
    for name in ['work','collected']:
        for suffix in ['.json','.view.json']:
            assert (economy/(name+'.before'+suffix)).read_bytes()==(economy/(name+'.restored'+suffix)).read_bytes(),name
    c=ET.parse(OUT/'tests/castellan-succession-current.trx').find('.//{*}Counters').attrib
    binaries=['Core/bin/Debug/net8.0/HakutaiKagami.Core.dll','Tests/bin/Debug/net8.0/HakutaiKagami.Core.dll','Tests/bin/Debug/net8.0/HakutaiKagami.Tests.dll','Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Core.dll','Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Game.dll']
    v=dict(schema=1,status='PASSED',scope='headless-castellan-succession-separate-personal-acceptance-custody',gui='NOT_RUN',full_core_suite='NOT_RUN',
           baseline_main='7704391da64e02302df23c87e5785a86bde93b47',tests=dict(passed=int(c['passed']),failed=0,skipped=0,counters=c),
           new_tests=27,supplemental_legacy_tests=38,sources=before,binaries={p:sha(ROOT/p) for p in binaries},
           projection_result=(projection/'projection-result.json').relative_to(ROOT).as_posix(),node3d=node3d,proofs={p.relative_to(ROOT).as_posix():sha(p) for p in [*prior_projection()[:2],OUT/'run-inputs.json',OUT/'tests.log',OUT/'tests/castellan-succession-current.trx',OUT/'game-build.log',OUT/'node3d.log',*projection.glob('*.json'),*economy.glob('*.json')]})
    manifest.write_bytes((json.dumps(v,ensure_ascii=False,indent=2)+'\n').encode('utf-8'))
    check(v);print('PASS '+c['passed']+' related Core tests,61 new administrative and personal service cases plus existing prepared submit/traffic cases; Game build and headless Node3D data; no screen/GUI operation',flush=True)
if __name__=='__main__':main()
