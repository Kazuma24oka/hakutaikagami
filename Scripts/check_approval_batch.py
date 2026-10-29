"""Minimum implementation gain from immutable pre-code baselines, without re-rating credit."""
from pathlib import Path
from fractions import Fraction
import json,subprocess
R=Path(__file__).resolve().parents[1]
BASELINES={30:("deb4910","Docs/Development/ProgressV1/approval-baseline-v30.json"),29:("c64a4c3","Docs/Development/ProgressV1/approval-baseline-v29.json"),27:("eaf8d11","Docs/Development/ProgressV1/approval-baseline-v27.json"),25:("0ef5541","Docs/Development/ProgressV1/approval-baseline-v25.json"),23:("f728123","Docs/Development/ProgressV1/approval-baseline-v23.json"),21:("9aed08cc7465a2f79f2718e6ed72bdba65561856","Docs/Development/ProgressV1/approval-baseline-v21.json"),20:("e725e29170092c0f01cbc3b7d4dd69d25f90a28b","Docs/Development/ProgressV1/approval-baseline-v20.json"),19:("67f720978b58bad476f29e4619d279d324897d79","Docs/Development/ProgressV1/approval-baseline-v19.json"),18:("cc546dafb7ec6ff954a70a351ba7da39ddd5357f","Docs/Development/ProgressV1/approval-baseline-v18.json"),17:("b129764c5bb515180fecb0f980a95471786677c5","Docs/Development/ProgressV1/approval-baseline-v17.json"),16:("1176c9fc5adde0511ccebafbf50e3c86a44dccf2","Docs/Development/ProgressV1/approval-baseline-v16.json"),14:("a6215cac61ac160c35f531165b7270cc673480bc","Docs/Development/ProgressV1/approval-baseline-v14.json"),13:("8c6d86061101dee5aea78c908fccb9d7de965e35","Docs/Development/ProgressV1/approval-baseline-v13.json"),12:("d332fe3fba5cf9b330db0f5dd14ae573179010e3","Docs/Development/ProgressV1/approval-baseline-v12.json"),11:("1801e0b799e1ebda2ffb2db0ff2d8455657401c1","Docs/Development/ProgressV1/approval-baseline-v11.json"),10:("c7a41f7f4c41dbe16b5a58c6d155028a35005882","Docs/Development/ProgressV1/approval-baseline-v10.json"),9:("5478a5d6644f1a2037d38a266ccb1d91b24acfd2","Docs/Development/ProgressV1/approval-baseline-v9.json"),8:("db56cc00bc6c55767432cf7ca6ab217e92b929c2","Docs/Development/ProgressV1/approval-baseline-v8.json"),7:("6e6b2fa87c9c205b024c494f430f9825be1d9252","Docs/Development/ProgressV1/approval-baseline-v7.json"),6:("5960dc7deb55f500a8f3e2708fc53be77bc4a18c","Docs/Development/ProgressV1/approval-baseline-v6.json"),5:("6c51f21f33cf7804ce5e7e51f6025b87fde670ab","Docs/Development/ProgressV1/approval-baseline-v5.json"),1:("e304cb0018cbeb870a31b82b208ee1c3c18932a7","Docs/Development/ProgressV1/approval-baseline-v1.json"),2:("c67b815170681d8453cd36a8d5e8aa1411be54ab","Docs/Development/ProgressV1/approval-baseline-v2.json"),3:("09806b77d0f372b7edb5e72031ddfca0d6d533b7","Docs/Development/ProgressV1/approval-baseline-v3.json")}
def calculate(a,d,leaves):
    groups={g["id"]:g for g in d["groups"]};total=Fraction(0)
    for domain in a["domains"]:
        ids=[i["id"] for gid in domain["groups"] for i in groups[gid]["items"] if i["id"] not in a["excluded_from_rollup"]]
        total+=sum(Fraction(leaves[i]["percent"]) for i in ids)/len(ids)*Fraction(domain["weight"],100)
    return total
def check():
    a=json.loads((R/"Docs/Development/ProgressV1/estimate.json").read_bytes());version=a.get("approval_batch",{}).get("version",1);pin,base=BASELINES[version]
    frozen=subprocess.check_output(["git","show",pin+":"+base],cwd=R);assert (R/base).read_bytes()==frozen,"Frozen baseline changed"
    b=json.loads(frozen)
    if version==16:
        prior=b['prior_baseline'];prior_pin=b['prior_baseline_commit'];prior_bytes=subprocess.check_output(['git','show',prior_pin+':'+prior],cwd=R)
        assert (R/prior).read_bytes()==prior_bytes,'Original road supply baseline changed'
        earlier=json.loads(prior_bytes);assert b['baseline_leaves']==earlier['baseline_leaves'] and b['domains']==earlier['domains'] and b['base_main']==earlier['base_main']
        assert all(b['targets'][k]>=v for k,v in earlier['targets'].items())
        assert b['targets']['BD-28']==b['baseline_leaves']['BD-28']['percent']+5,'Pre-code notification correction changed'
    if version==10:
        prior=b['prior_baseline'];prior_pin=b['prior_baseline_commit'];prior_bytes=subprocess.check_output(['git','show',prior_pin+':'+prior],cwd=R)
        assert (R/prior).read_bytes()==prior_bytes,'Original freight pre-code baseline changed'
        earlier=json.loads(prior_bytes);assert b['baseline_leaves']==earlier['baseline_leaves'] and b['domains']==earlier['domains'] and b['base_main']==earlier['base_main']
        assert all(b['targets'][k]>=v for k,v in earlier['targets'].items())
        for commit,file in [(prior_pin,'Core/Contracts/HouseLogistics.cs'),(pin,'Core/Contracts/HouseFreightReceiving.cs')]:
            assert subprocess.run(['git','cat-file','-e',commit+':'+file],cwd=R,capture_output=True).returncode!=0,'Target not frozen before implementation'
    if version==20:
        prior=b['prior_baseline'];prior_pin=b['prior_baseline_commit'];prior_bytes=subprocess.check_output(['git','show',prior_pin+':'+prior],cwd=R)
        assert (R/prior).read_bytes()==prior_bytes,'Earlier administration baseline changed'
        earlier=json.loads(prior_bytes);assert b['baseline_leaves']==earlier['baseline_leaves'] and b['domains']==earlier['domains'] and b['base_main']==earlier['base_main']
        assert all(b['targets'][k]>=v for k,v in earlier['targets'].items())
        assert subprocess.run(['git','cat-file','-e',pin+':Core/Contracts/CastellanReports.cs'],cwd=R,capture_output=True).returncode!=0,'Report extension not frozen before implementation'
    if version==5:
        prior=b['prior_baseline'];prior_pin=b['prior_baseline_commit'];prior_bytes=subprocess.check_output(['git','show',prior_pin+':'+prior],cwd=R)
        assert (R/prior).read_bytes()==prior_bytes,'Earlier village pre-code baseline changed'
        earlier=json.loads(prior_bytes);assert b['baseline_leaves']==earlier['baseline_leaves'] and b['domains']==earlier['domains'] and b['base_main']==earlier['base_main']
        assert all(b['targets'][k]>=v for k,v in earlier['targets'].items())
        for commit,file in [(prior_pin,'Core/Contracts/VillageConsultation.cs'),(pin,'Core/Contracts/NpcCommunityAdministration.cs')]:
            assert subprocess.run(['git','cat-file','-e',commit+':'+file],cwd=R,capture_output=True).returncode!=0,'Target not frozen before implementation'
    p=json.loads((R/"Docs/Development/ProgressV1/progress.json").read_bytes());assert a["domains"]==b["domains"] and a["excluded_from_rollup"]==b["excluded_from_rollup"],"Weights/exclusions changed"
    design=p["design_source"];assert (R/design).read_bytes()==subprocess.check_output(["git","show",pin+":"+design],cwd=R),"Design denominator changed"
    d=json.loads((R/design).read_bytes());expected={k:v["percent"] for k,v in b["baseline_leaves"].items()};expected.update(b["targets"])
    if version==25:
        assert b['baseline_leaves']['BD-34']['percent']==95 and b['targets']['BD-34']==100
        # A conservative under-report against a frozen maximum never adds credit.
        expected['BD-34']=99
        assert a['approval_batch']['conservative_target_cap']['BD-34']['reported']==99
    assert {k:v["percent"] for k,v in a["leaves"].items()}==expected,"Only predeclared targets eligible"
    old=calculate(b,d,b["baseline_leaves"]);new=calculate(b,d,a["leaves"]);gain=new-old;assert gain>=Fraction(str(b["minimum_gain"])),"Less than1 earned point"
    if version==1:
        from verify_community_economy import check as verify
        from probe_community_reports import check as probe
        probe();folder="CommunityEconomyV2"
        assert "CommunityEconomy.Policy" in (R/"Game/Scripts/NewGameFrontEnd.cs").read_text(encoding="utf-8")
        assert "Session.CommunityEconomyView()!=null" in (R/"Game/Scripts/NewGameRuntime.WorkExperience.cs").read_text(encoding="utf-8")
    elif version==2:
        from verify_regional_trade import check as verify
        folder="RegionalTradeV1"
        assert "RegionalTrade.Policy" in (R/"Game/Scripts/NewGameFrontEnd.cs").read_text(encoding="utf-8")
        assert 'Session.RegionalTradeView()!=null' in (R/"Game/Scripts/NewGameRuntime.WorkExperience.cs").read_text(encoding="utf-8")
    elif version==30:
        from verify_representative_contact import check as verify
        folder="RepresentativeContactV1"
        assert subprocess.run(['git','cat-file','-e',pin+':Core/Contracts/RepresentativeKnowledge.cs'],cwd=R,capture_output=True).returncode!=0,'Knowledge target not frozen before implementation'
        assert subprocess.run(['git','cat-file','-e',pin+':Core/Contracts/TruceConfirmations.Representatives.cs'],cwd=R,capture_output=True).returncode!=0,'Contact target not frozen before implementation'
        assert 'RepresentativeCandidates(w)' in (R/'Game/Scripts/NewGameRuntime.TruceConfirmations.cs').read_text(encoding='utf-8')
        assert 'RepresentativeKnowledge.Known(w,signer,owner,at,before)' in (R/'Core/Contracts/TruceConfirmations.Representatives.cs').read_text(encoding='utf-8')
    elif version==29:
        from verify_truce_confirmation import check as verify
        folder="TruceConfirmationV1"
        prior=b['prior_baseline'];prior_pin=b['prior_baseline_commit'];old_bytes=subprocess.check_output(['git','show',prior_pin+':'+prior],cwd=R)
        assert (R/prior).read_bytes()==old_bytes
        earlier=json.loads(old_bytes);assert b['baseline_leaves']==earlier['baseline_leaves'] and b['domains']==earlier['domains'] and b['base_main']==earlier['base_main']
        assert all(b['targets'][k]>=v for k,v in earlier['targets'].items())
        assert subprocess.run(['git','cat-file','-e',prior_pin+':Core/Contracts/TruceConfirmations.cs'],cwd=R,capture_output=True).returncode!=0
        assert subprocess.run(['git','cat-file','-e',pin+':Core/Contracts/TruceConfirmations.ReportRetries.cs'],cwd=R,capture_output=True).returncode!=0
        assert 'AppendTruceConfirmations' in (R/'Game/Scripts/NewGameRuntime.HouseSuccession.cs').read_text(encoding='utf-8')
        assert 'ShowTruceConfirmationLetter' in (R/'Game/Scripts/NewGameRuntime.Mail.cs').read_text(encoding='utf-8')
    elif version==27:
        from verify_castellan_succession import check as verify
        folder="CastellanSuccessionV1"
        prior=b['prior_baseline'];prior_pin=b['prior_baseline_commit'];earlier_bytes=subprocess.check_output(['git','show',prior_pin+':'+prior],cwd=R)
        assert (R/prior).read_bytes()==earlier_bytes
        earlier=json.loads(earlier_bytes);assert b['baseline_leaves']==earlier['baseline_leaves'] and b['domains']==earlier['domains'] and b['base_main']==earlier['base_main']
        assert all(b['targets'][k]>=v for k,v in earlier['targets'].items())
        assert subprocess.run(['git','cat-file','-e',prior_pin+':Core/Contracts/CastleAdministration.Succession.cs'],cwd=R,capture_output=True).returncode!=0
        assert subprocess.run(['git','cat-file','-e',pin+':Core/Contracts/TruceSuccession.cs'],cwd=R,capture_output=True).returncode!=0
        assert 'CastellanReauthorizationView' in (R/'Game/Scripts/NewGameRuntime.CastleAdministration.cs').read_text(encoding='utf-8')
        assert 'TruceSuccessionView' in (R/'Game/Scripts/NewGameRuntime.HouseSuccession.cs').read_text(encoding='utf-8')
    elif version==25:
        from verify_steward_succession import check as verify
        folder="StewardSuccessionV1"
        prior=b['prior_baseline'];prior_pin=b['prior_baseline_commit'];frozen_prior=subprocess.check_output(['git','show',prior_pin+':'+prior],cwd=R)
        assert (R/prior).read_bytes()==frozen_prior
        earlier=json.loads(frozen_prior);assert b['baseline_leaves']==earlier['baseline_leaves'] and b['domains']==earlier['domains'] and b['base_main']==earlier['base_main']
        assert all(b['targets'][k]>=v for k,v in earlier['targets'].items())
        assert subprocess.run(['git','cat-file','-e',prior_pin+':Core/Contracts/HouseholdService.cs'],cwd=R,capture_output=True).returncode!=0
        assert subprocess.run(['git','cat-file','-e',prior_pin+':Core/Contracts/CommunityStewardship.Succession.cs'],cwd=R,capture_output=True).returncode!=0
        assert 'ShowHouseholdService' in (R/'Game/Scripts/NewGameRuntime.HouseSuccession.cs').read_text(encoding='utf-8')
        assert 'StewardReauthorizationView' in (R/'Game/Scripts/NewGameRuntime.Stewardship.cs').read_text(encoding='utf-8')
    elif version==23:
        from verify_military_succession import check as verify
        folder="MilitarySuccessionV1"
        prior=b['prior_baseline'];prior_pin=b['prior_baseline_commit'];frozen_prior=subprocess.check_output(['git','show',prior_pin+':'+prior],cwd=R)
        assert (R/prior).read_bytes()==frozen_prior
        earlier=json.loads(frozen_prior);assert b['baseline_leaves']==earlier['baseline_leaves'] and b['domains']==earlier['domains'] and b['base_main']==earlier['base_main']
        assert all(b['targets'][k]>=v for k,v in earlier['targets'].items())
        assert subprocess.run(['git','cat-file','-e',prior_pin+':Core/Contracts/NewGameMilitary.IssuerSuccession.cs'],cwd=R,capture_output=True).returncode!=0,'Military succession must be frozen before implementation'
        assert 'IssuerPolicyForRequest' in (R/'Game/Scripts/NewGameRuntime.Military.cs').read_text(encoding='utf-8')
        assert 'HouseholdMilitaryAgreementContract' in (R/'Core/Contracts/NewGameMilitaryAuthority.cs').read_text(encoding='utf-8')
    elif version==21:
        from verify_house_succession import check as verify
        folder="HouseSuccessionV1"
        assert "HouseSuccession.Policy" in (R/"Game/Scripts/NewGameFrontEnd.cs").read_text(encoding="utf-8")
        assert "ShowHouseSuccession" in (R/"Game/Scripts/NewGameRuntime.WorkExperience.cs").read_text(encoding="utf-8")
        assert "RefreshHouseSuccessionMarkers" in (R/"Game/Scripts/NewGameRuntime.cs").read_text(encoding="utf-8")
        assert subprocess.run(['git','cat-file','-e',pin+':Core/Contracts/HouseSuccession.cs'],cwd=R,capture_output=True).returncode!=0,'House succession not frozen before code'
    elif version in (19,20):
        from verify_castle_administration import check as verify
        folder="CastleAdministrationV1"
        assert "CastleAdministration.Policy" in (R/"Game/Scripts/NewGameFrontEnd.cs").read_text(encoding="utf-8")
        assert "ShowCastleAdministration" in (R/"Game/Scripts/NewGameRuntime.WorkExperience.cs").read_text(encoding="utf-8")
        assert "RefreshCastellanMarkers" in (R/"Game/Scripts/NewGameRuntime.cs").read_text(encoding="utf-8")
        assert subprocess.run(['git','cat-file','-e',('67f720978b58bad476f29e4619d279d324897d79' if version==20 else pin)+':Core/Contracts/CastleAdministration.cs'],cwd=R,capture_output=True).returncode!=0,'Castellan scope not frozen before implementation'
    elif version==18:
        from verify_castle_handover import check as verify
        folder="CastleHandoverV1"
        assert "CastleHandover.Policy" in (R/"Game/Scripts/NewGameFrontEnd.cs").read_text(encoding="utf-8")
        assert "ShowCastleHandover" in (R/"Game/Scripts/NewGameRuntime.WorkExperience.cs").read_text(encoding="utf-8")
        assert "RefreshCastleMarkers" in (R/"Game/Scripts/NewGameRuntime.cs").read_text(encoding="utf-8")
        assert subprocess.run(['git','cat-file','-e',pin+':Core/Contracts/CastleHandover.cs'],cwd=R,capture_output=True).returncode!=0,'Castle scope not frozen before code'
    elif version==17:
        from verify_personal_life import check as verify
        folder="PersonalLifeV1"
        assert "PersonalLife.Policy" in (R/"Game/Scripts/NewGameFrontEnd.cs").read_text(encoding="utf-8")
        assert "ShowPersonalLife" in (R/"Game/Scripts/NewGameRuntime.WorkExperience.cs").read_text(encoding="utf-8")
        assert subprocess.run(['git','cat-file','-e',pin+':Core/Contracts/PersonalLife.cs'],cwd=R,capture_output=True).returncode!=0,'Life scope not frozen before code'
    elif version==16:
        from verify_road_convoy import check as verify
        folder="RoadConvoyV1"
        assert "RoadConvoy.Policy" in (R/"Game/Scripts/NewGameFrontEnd.cs").read_text(encoding="utf-8")
        assert "AppendRoadSupply" in (R/"Game/Scripts/NewGameRuntime.CampSupply.cs").read_text(encoding="utf-8")
        assert subprocess.run(['git','cat-file','-e',pin+':Core/Contracts/RoadConvoy.cs'],cwd=R,capture_output=True).returncode!=0,'Road supply target not frozen before code'
    elif version==14:
        from verify_army_road import check as verify
        folder="ArmyRoadV1"
        assert "ArmyRoadMarch.Policy" in (R/"Game/Scripts/NewGameFrontEnd.cs").read_text(encoding="utf-8")
        assert "ShowArmyRice" in (R/"Game/Scripts/NewGameRuntime.WorkExperience.cs").read_text(encoding="utf-8")
        assert subprocess.run(['git','cat-file','-e',pin+':Core/Contracts/ArmyRoadMarch.cs'],cwd=R,capture_output=True).returncode!=0,'Army road target not frozen before code'
    elif version==13:
        from verify_freight_operations import check as verify
        folder="FreightOperationsV1"
        assert "FieldConvoy.Policy" in (R/"Game/Scripts/NewGameFrontEnd.cs").read_text(encoding="utf-8")
        assert 'ShowCampSupply' in (R/"Game/Scripts/NewGameRuntime.WorkExperience.cs").read_text(encoding="utf-8")
        for source in ['Core/Contracts/FreightOperations.cs','Core/Contracts/FieldConvoy.cs']:
            assert subprocess.run(['git','cat-file','-e',pin+':'+source],cwd=R,capture_output=True).returncode!=0,'Freight/camp targets not frozen before code'
    elif version==12:
        from verify_npc_freight import check as verify
        folder="NpcFreightV1"
        assert "NpcFreight.Policy" in (R/"Game/Scripts/NewGameFrontEnd.cs").read_text(encoding="utf-8")
        assert 'Session.DelegatedFreightView()' in (R/"Game/Scripts/NewGameRuntime.HouseFreight.cs").read_text(encoding="utf-8")
        assert subprocess.run(['git','cat-file','-e',pin+':Core/Contracts/NpcFreight.cs'],cwd=R,capture_output=True).returncode!=0,'Delegated freight targets not frozen before code'
    elif version==11:
        from verify_military_economy import check as verify
        folder="MilitaryEconomyV1"
        assert "MilitaryEconomy.Policy" in (R/"Game/Scripts/NewGameFrontEnd.cs").read_text(encoding="utf-8")
        assert 'Session.MilitaryEconomyView()!=null' in (R/"Game/Scripts/NewGameRuntime.WorkExperience.cs").read_text(encoding="utf-8")
        assert subprocess.run(['git','cat-file','-e',pin+':Core/Contracts/MilitaryEconomy.cs'],cwd=R,capture_output=True).returncode!=0,'Military economy targets not frozen before code'
    elif version==10:
        from verify_house_logistics import check as verify
        folder="HouseLogisticsV1"
        assert "HouseFreightReceiving.Policy" in (R/"Game/Scripts/NewGameFrontEnd.cs").read_text(encoding="utf-8")
        assert 'Session.HouseFreightView()!=null' in (R/"Game/Scripts/NewGameRuntime.WorkExperience.cs").read_text(encoding="utf-8")
    elif version==9:
        from verify_house_logistics import check as verify
        folder="HouseLogisticsV1"
        assert "HouseLogistics.Policy" in (R/"Game/Scripts/NewGameFrontEnd.cs").read_text(encoding="utf-8")
        assert 'Session.HouseFreightView()!=null' in (R/"Game/Scripts/NewGameRuntime.WorkExperience.cs").read_text(encoding="utf-8")
        assert subprocess.run(['git','cat-file','-e',pin+':Core/Contracts/HouseLogistics.cs'],cwd=R,capture_output=True).returncode!=0,'Freight targets not frozen before code'
    elif version==8:
        from verify_regional_domains import check as verify
        folder="RegionalDomainsV1"
        assert "RegionalDomains.Policy" in (R/"Game/Scripts/NewGameFrontEnd.cs").read_text(encoding="utf-8")
        assert 'Session.HouseTreasuryView()!=null' in (R/"Game/Scripts/NewGameRuntime.WorkExperience.cs").read_text(encoding="utf-8")
        assert subprocess.run(['git','cat-file','-e',pin+':Core/Contracts/RegionalDomains.cs'],cwd=R,capture_output=True).returncode!=0,'Domain targets not frozen before code'
    elif version==7:
        from verify_material_markets import check as verify
        folder="MaterialMarketsV1"
        assert "MaterialMarkets.Policy" in (R/"Game/Scripts/NewGameFrontEnd.cs").read_text(encoding="utf-8")
        assert 'ShowMaterialTrade();return;' in (R/"Game/Scripts/NewGameRuntime.Trade.cs").read_text(encoding="utf-8")
        assert subprocess.run(['git','cat-file','-e',pin+':Core/Contracts/MaterialMarkets.cs'],cwd=R,capture_output=True).returncode!=0,'Material targets not frozen before code'
    elif version==6:
        from verify_community_stewardship import check as verify
        folder="CommunityStewardshipV1"
        assert "CommunityStewardship.Policy" in (R/"Game/Scripts/NewGameFrontEnd.cs").read_text(encoding="utf-8")
        assert 'Session.StewardshipView()!=null' in (R/"Game/Scripts/NewGameRuntime.WorkExperience.cs").read_text(encoding="utf-8")
        assert subprocess.run(['git','cat-file','-e',pin+':Core/Contracts/CommunityStewardship.cs'],cwd=R,capture_output=True).returncode!=0,'Stewardship target not frozen before code'
    elif version==5:
        from verify_village_consultation import check as verify
        folder="VillageConsultationV1"
        assert "NpcCommunityAdministration.Policy" in (R/"Game/Scripts/NewGameFrontEnd.cs").read_text(encoding="utf-8")
        assert 'Session.VillageView()!=null' in (R/"Game/Scripts/NewGameRuntime.WorkExperience.cs").read_text(encoding="utf-8")
    else:
        from verify_house_salary import check as verify
        folder="HouseSalaryV1"
        assert "HouseSalary.Policy" in (R/"Game/Scripts/NewGameFrontEnd.cs").read_text(encoding="utf-8")
        assert 'Session.SalaryView()!=null' in (R/"Game/Scripts/NewGameRuntime.WorkExperience.cs").read_text(encoding="utf-8")
    v=json.loads((R/("Docs/Design/"+folder+"/verification.json")).read_bytes());verify(v)
    c=json.loads((R/("Docs/Design/"+folder+"/completion.json")).read_bytes());assert [x["contract"] for x in c["criteria"]]==b["criteria"]
    assert all(x["status"]=="PASSED" and x["evidence"] for x in c["criteria"])
    return dict(baseline_commit=pin,baseline=float(old),current=float(new),earned_gain=float(gain),excluded_reassessment=b["reassessment_delta_excluded"],minimum=1,status="PASSED")
if __name__=="__main__":print(json.dumps(check(),ensure_ascii=False,indent=2))
