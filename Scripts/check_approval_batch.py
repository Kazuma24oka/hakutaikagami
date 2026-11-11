"""Minimum implementation gain from immutable pre-code baselines, without re-rating credit."""
from pathlib import Path
from fractions import Fraction
import json,subprocess
R=Path(__file__).resolve().parents[1]
BASELINES={105:("a018a589","Docs/Development/ProgressV1/approval-baseline-v105.json"),104:("5b2dfb31","Docs/Development/ProgressV1/approval-baseline-v104.json"),103:("845951bf","Docs/Development/ProgressV1/approval-baseline-v103.json"),102:("cbfe4d39","Docs/Development/ProgressV1/approval-baseline-v102.json"),101:("4ef90c1d","Docs/Development/ProgressV1/approval-baseline-v101.json"),100:("d61e1952","Docs/Development/ProgressV1/approval-baseline-v100.json"),99:("c3a7d7e5","Docs/Development/ProgressV1/approval-baseline-v99.json"),98:("cbf61842","Docs/Development/ProgressV1/approval-baseline-v98.json"),97:("a74251e8","Docs/Development/ProgressV1/approval-baseline-v97.json"),96:("69a04c82","Docs/Development/ProgressV1/approval-baseline-v96.json"),95:("81b4097e","Docs/Development/ProgressV1/approval-baseline-v95.json"),94:("b6145858","Docs/Development/ProgressV1/approval-baseline-v94.json"),93:("2086672c","Docs/Development/ProgressV1/approval-baseline-v93.json"),92:("d3f8103e","Docs/Development/ProgressV1/approval-baseline-v92.json"),91:("729d1baf","Docs/Development/ProgressV1/approval-baseline-v91.json"),90:("0c496d36","Docs/Development/ProgressV1/approval-baseline-v90.json"),89:("fdc5a41f","Docs/Development/ProgressV1/approval-baseline-v89.json"),88:("f0cd81f5","Docs/Development/ProgressV1/approval-baseline-v88.json"),87:("fba1f862","Docs/Development/ProgressV1/approval-baseline-v87.json"),86:("f19b7e22","Docs/Development/ProgressV1/approval-baseline-v86.json"),85:("deb77783","Docs/Development/ProgressV1/approval-baseline-v85.json"),84:("240979b6","Docs/Development/ProgressV1/approval-baseline-v84.json"),83:("e2cafa6b","Docs/Development/ProgressV1/approval-baseline-v83.json"),82:("9de47771","Docs/Development/ProgressV1/approval-baseline-v82.json"),81:("394b78b2","Docs/Development/ProgressV1/approval-baseline-v81.json"),80:("b9eaef4","Docs/Development/ProgressV1/approval-baseline-v80.json"),79:("c616e72","Docs/Development/ProgressV1/approval-baseline-v79.json"),78:("e15f61c","Docs/Development/ProgressV1/approval-baseline-v78.json"),77:("738e1a2","Docs/Development/ProgressV1/approval-baseline-v77.json"),76:("81d75a4","Docs/Development/ProgressV1/approval-baseline-v76.json"),75:("a35da9e","Docs/Development/ProgressV1/approval-baseline-v75.json"),74:("ab3ebe4","Docs/Development/ProgressV1/approval-baseline-v74.json"),73:("87a3655","Docs/Development/ProgressV1/approval-baseline-v73.json"),72:("9574ae9","Docs/Development/ProgressV1/approval-baseline-v72.json"),71:("3e7b529","Docs/Development/ProgressV1/approval-baseline-v71.json"),70:("d4a5f1d","Docs/Development/ProgressV1/approval-baseline-v70.json"),69:("b5edc04","Docs/Development/ProgressV1/approval-baseline-v69.json"),68:("ccbb68f","Docs/Development/ProgressV1/approval-baseline-v68.json"),67:("0354102","Docs/Development/ProgressV1/approval-baseline-v67.json"),66:("1ffdd92","Docs/Development/ProgressV1/approval-baseline-v66.json"),65:("4e2eb7a","Docs/Development/ProgressV1/approval-baseline-v65.json"),64:("194ef11","Docs/Development/ProgressV1/approval-baseline-v64.json"),63:("f86d728","Docs/Development/ProgressV1/approval-baseline-v63.json"),62:("54bfa0e","Docs/Development/ProgressV1/approval-baseline-v62.json"),61:("6e853f1","Docs/Development/ProgressV1/approval-baseline-v61.json"),60:("7e70be313df6a2427943eceecb4ac7a31c2cef02","Docs/Development/ProgressV1/approval-baseline-v60.json"),59:("47f56cd6c7697acd5873634de4bb3afc89f4a5a2","Docs/Development/ProgressV1/approval-baseline-v59.json"),58:("f57283778bda0e8f442ae12da5ca94a17b357472","Docs/Development/ProgressV1/approval-baseline-v58.json"),57:("368a8597db3ce27f5a9cf264c35db7b8cd8da071","Docs/Development/ProgressV1/approval-baseline-v57.json"),56:("a517981b1e4b0a413c3db7fec4e5faa9a41cbd1b","Docs/Development/ProgressV1/approval-baseline-v56.json"),55:("55f720a3e67058d910df471a5148b4ca3383131d","Docs/Development/ProgressV1/approval-baseline-v55.json"),54:("5cc8256cc504f4e193a40da53a8f1ce2ee98255a","Docs/Development/ProgressV1/approval-baseline-v54.json"),53:("e7cedfb31fd6a316e97282df6536e62a4b24a387","Docs/Development/ProgressV1/approval-baseline-v53.json"),52:("ef1ab2649d3854eeca4b3a274fe09f99e7088fd2","Docs/Development/ProgressV1/approval-baseline-v52.json"),51:("3614d394a08891e55353f01309ed756de0108eae","Docs/Development/ProgressV1/approval-baseline-v51.json"),50:("3614d394a08891e55353f01309ed756de0108eae","Docs/Development/ProgressV1/approval-baseline-v50-scope.json"),49:("442c0e2c7f5927a4220526ffcd2f8c2bf4f3f01e","Docs/Development/ProgressV1/approval-baseline-v49-scope.json"),48:("1b973fee53d6ccad3fa00019bb8d7f3ac458edb5","Docs/Development/ProgressV1/approval-baseline-v48-scope.json"),47:("3f0b74ae3ca2a2c1afa49ffded3d30db737e2497","Docs/Development/ProgressV1/approval-baseline-v47-scope.json"),46:("2d57d0bef34d78ea98f9cf9e90946bee4ebd724a","Docs/Development/ProgressV1/approval-baseline-v46-scope.json"),45:("7b7585ca51f24b1ca9aaf56d6e56dee7eedc72b6","Docs/Development/ProgressV1/approval-baseline-v45.json"),44:("aa329cd31227dd80bb6eeeaebb8ec6d1ee5d46a6","Docs/Development/ProgressV1/approval-baseline-v44.json"),43:("a94d9ae5b445e0fa1c873a338845b5490a3b3085","Docs/Development/ProgressV1/approval-baseline-v43.json"),42:("fc6748cd8460b77c48d1faf40e516510819528fd","Docs/Development/ProgressV1/approval-baseline-v42.json"),41:("1905ae8af54470180f0f97a4cde5014e7aa41d7f","Docs/Development/ProgressV1/approval-baseline-v41.json"),40:("a0656ca562bc39f67765000478e94f7d1080f6f3","Docs/Development/ProgressV1/approval-baseline-v40.json"),39:("30298c9e03cab33eedf183a44222277d5946cac1","Docs/Development/ProgressV1/approval-baseline-v39.json"),38:("889b983da594615e5aade9fcb8fe9a6b26484d1a","Docs/Development/ProgressV1/approval-baseline-v38.json"),37:("c39f7eb77e70f2adafbfa9393b34b24c3fca4dbc","Docs/Development/ProgressV1/approval-baseline-v37.json"),36:("5e9a1923d6aa00e2a1146553b3626577ed4b5132","Docs/Development/ProgressV1/approval-baseline-v36.json"),35:("65c6a0e2f6b8023dfd08e55143f5c065bb612f7f","Docs/Development/ProgressV1/approval-baseline-v35.json"),34:("4a44fac29fff821c34f514a2128deb63a76d55f9","Docs/Development/ProgressV1/approval-baseline-v34.json"),33:("7236506","Docs/Development/ProgressV1/approval-baseline-v33.json"),32:("02c82bd","Docs/Development/ProgressV1/approval-baseline-v32.json"),31:("175b9fe","Docs/Development/ProgressV1/approval-baseline-v31.json"),30:("deb4910","Docs/Development/ProgressV1/approval-baseline-v30.json"),29:("c64a4c3","Docs/Development/ProgressV1/approval-baseline-v29.json"),27:("eaf8d11","Docs/Development/ProgressV1/approval-baseline-v27.json"),25:("0ef5541","Docs/Development/ProgressV1/approval-baseline-v25.json"),23:("f728123","Docs/Development/ProgressV1/approval-baseline-v23.json"),21:("9aed08cc7465a2f79f2718e6ed72bdba65561856","Docs/Development/ProgressV1/approval-baseline-v21.json"),20:("e725e29170092c0f01cbc3b7d4dd69d25f90a28b","Docs/Development/ProgressV1/approval-baseline-v20.json"),19:("67f720978b58bad476f29e4619d279d324897d79","Docs/Development/ProgressV1/approval-baseline-v19.json"),18:("cc546dafb7ec6ff954a70a351ba7da39ddd5357f","Docs/Development/ProgressV1/approval-baseline-v18.json"),17:("b129764c5bb515180fecb0f980a95471786677c5","Docs/Development/ProgressV1/approval-baseline-v17.json"),16:("1176c9fc5adde0511ccebafbf50e3c86a44dccf2","Docs/Development/ProgressV1/approval-baseline-v16.json"),14:("a6215cac61ac160c35f531165b7270cc673480bc","Docs/Development/ProgressV1/approval-baseline-v14.json"),13:("8c6d86061101dee5aea78c908fccb9d7de965e35","Docs/Development/ProgressV1/approval-baseline-v13.json"),12:("d332fe3fba5cf9b330db0f5dd14ae573179010e3","Docs/Development/ProgressV1/approval-baseline-v12.json"),11:("1801e0b799e1ebda2ffb2db0ff2d8455657401c1","Docs/Development/ProgressV1/approval-baseline-v11.json"),10:("c7a41f7f4c41dbe16b5a58c6d155028a35005882","Docs/Development/ProgressV1/approval-baseline-v10.json"),9:("5478a5d6644f1a2037d38a266ccb1d91b24acfd2","Docs/Development/ProgressV1/approval-baseline-v9.json"),8:("db56cc00bc6c55767432cf7ca6ab217e92b929c2","Docs/Development/ProgressV1/approval-baseline-v8.json"),7:("6e6b2fa87c9c205b024c494f430f9825be1d9252","Docs/Development/ProgressV1/approval-baseline-v7.json"),6:("5960dc7deb55f500a8f3e2708fc53be77bc4a18c","Docs/Development/ProgressV1/approval-baseline-v6.json"),5:("6c51f21f33cf7804ce5e7e51f6025b87fde670ab","Docs/Development/ProgressV1/approval-baseline-v5.json"),1:("e304cb0018cbeb870a31b82b208ee1c3c18932a7","Docs/Development/ProgressV1/approval-baseline-v1.json"),2:("c67b815170681d8453cd36a8d5e8aa1411be54ab","Docs/Development/ProgressV1/approval-baseline-v2.json"),3:("09806b77d0f372b7edb5e72031ddfca0d6d533b7","Docs/Development/ProgressV1/approval-baseline-v3.json")}
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
    old=calculate(b,d,b["baseline_leaves"]);new=calculate(b,d,a["leaves"]);gain=new-old;assert gain>=Fraction(str(b["minimum_gain"])),"Less than frozen required earned progress"
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
    elif version==105:
        folder="MapTerrainV1"
        assert subprocess.run(['git','cat-file','-e',pin+':Game/Data/Geography/terrain-v1.json'],cwd=R,capture_output=True).returncode!=0,'Terrain targets not frozen before work'
        assert "terrain-v1.json" in (R/"Game/Scripts/NewGameMap.cs").read_text(encoding="utf-8") and "地球地図日本" in (R/"Game/Data/Geography/CREDITS.md").read_text(encoding="utf-8")
        def verify(record):
            assert record["godot_ui"]=="PASSED"
    elif version==104:
        folder="MapBordersV1"
        assert subprocess.run(['git','cat-file','-e',pin+':Game/Data/Geography/borders-v1.json'],cwd=R,capture_output=True).returncode!=0,'Border targets not frozen before work'
        assert "borders-v1.json" in (R/"Game/Scripts/NewGameMap.cs").read_text(encoding="utf-8") and "CC BY 4.0" in (R/"Game/Data/Geography/CREDITS.md").read_text(encoding="utf-8")
        def verify(record):
            assert record["godot_ui"]=="PASSED"
    elif version==103:
        folder="MapExtentV1"
        assert subprocess.run(['git','cat-file','-e',pin+':Game/Data/Geography/northern-territories-v1.json'],cwd=R,capture_output=True).returncode!=0,'Map targets not frozen before work'
        assert "northern-territories-v1.json" in (R/"Game/Scripts/NewGameMap.cs").read_text(encoding="utf-8")
        def verify(record):
            assert record["godot_ui"]=="PASSED" and record["core_related_passed"]>=24
    elif version==102:
        folder="CastleNetworkWorldV1"
        assert subprocess.run(['git','cat-file','-e',pin+':Core/Contracts/WorldNetwork.cs'],cwd=R,capture_output=True).returncode!=0,'Network targets not frozen before code'
        assert "WorldNetwork.Nodes(w)" in (R/"Core/Contracts/BranchRoutes.cs").read_text(encoding="utf-8")
        def verify(record):
            assert record["core_castle_network_tests"]==3 and record["core_related_passed"]>=516
    elif version==101:
        folder="HakutaiDbV3"
        assert subprocess.run(['git','cat-file','-e',pin+':Docs/Proposals/NATIONAL_PLACEMENT_DESIGN_V1.md'],cwd=R,capture_output=True).returncode!=0,'V3 targets not frozen before work'
        assert "Castle" in (R/"Core/Contracts/HakutaiDbStart.cs").read_text(encoding="utf-8")
        def verify(record):
            assert record["core_db_tests"]==11 and record["core_related_passed"]>=87 and record["godot_ui"]=="PASSED"
    elif version==100:
        folder="HakutaiDbV1"
        assert "ShowHakutaiDb" in (R/"Game/Scripts/NewGameFrontEnd.cs").read_text(encoding="utf-8")
        assert subprocess.run(['git','cat-file','-e',pin+':Core/Contracts/HakutaiDb.cs'],cwd=R,capture_output=True).returncode!=0,'DB targets not frozen before code'
        def verify(record):
            assert record["core_db_tests"]==7 and record["core_related_passed"]>=83 and record["godot_ui"]=="PASSED" and len(record["source_commit"])==40
    elif version==99:
        folder="PersonalHealthV1"
        assert "HealthEdition=PersonalHealth.Edition" in (R/"Game/Scripts/NewGameFrontEnd.cs").read_text(encoding="utf-8")
        assert subprocess.run(['git','cat-file','-e',pin+':Core/Contracts/PersonalHealth.cs'],cwd=R,capture_output=True).returncode!=0,'Health targets not frozen before code'
        def verify(record):
            assert record["core_health_tests"]==10 and record["core_related_passed"]>=97 and record["godot_ui"]=="PASSED" and record["legacy_fixture_failed"]==0
    elif version==98:
        folder="RegisteredAspirationV1"
        def verify(record):
            assert len(set(record["checked_skills"]))==4 and record["legacy_saves"]==6 and record["editor_runs"]==2
    elif version==97:
        folder="ImagawaCampaignV1"
        def verify(record):
            assert record["ui"]=="IMAGAWA_START_PASS"
            assert "ArmyRoadAction.March" in (R/"Game/Scripts/NewGameFrontEnd.Judgment.cs").read_text(encoding="utf-8")
    elif version==96:
        folder="ImagawaStartV1"
        def verify(record):
            assert record["ui"]=="IMAGAWA_START_PASS" and record["passed"]==len(set(record["tests"]))==7
            assert any("LegacyLiteralWorlds" in name for name in record["tests"])
    elif version==95:
        folder="OwariHistoricalOpportunityV1"
        def verify(record):
            assert record["ui"]=="OWARI_SCOUTING_UI_PASS" and record["passed"]==len(set(record["tests"]))==11
            assert any("OwariRequiresPresentNobunaga" in name for name in record["tests"])
    elif version==94:
        folder="CastleOfficeOrderV1"
        def verify(record):
            assert record["passed"]==len(set(record["tests"]))==10
            assert len(record["competition"])==3 and set(record["competition"])<=set(record["tests"])
    elif version==93:
        folder="AlliedKeepReportsV1"
        def verify(record):
            assert record["ui"]=="ALLIED_KEEP_REPORT_UI_PASS" and record["passed"]==len(set(record["tests"]))==5
            assert any("LegacyLiteralWorlds" in name for name in record["tests"])
    elif version==92:
        folder="AlliedCastleGateV1"
        def verify(record):
            assert record["ui"]=="ALLIED_GATE_UI_PASS" and record["passed"]==len(set(record["tests"]))==5
            assert sum("RemoteRetainerAnswers" in name for name in record["tests"])==3
    elif version==91:
        folder="IndependenceLettersV1"
        def verify(record):
            assert record["ui"]=="INDEPENDENCE_LETTER_UI_PASS" and record["passed"]==3
            for case in ("RemoteRetainerAnswers","PublicDeclarationUsesKnownCastle","LegacyLiteralWorlds"):
                assert any(case in name for name in record["tests"])
    elif version==90:
        folder="IndependenceGarrisonV1"
        def verify(record):
            assert record["passed"]==len(set(record["tests"]))==4
            assert record["ui"]=="INDEPENDENCE_GATE_UI_PASS"
            for case in ("IndependencePreserves","LocalCastellanBrings","HandingBackCommandLeaves","LegacyLiteralWorlds"):
                assert any(case in name for name in record["tests"])
    elif version==89:
        folder="CoalitionV1"
        def verify(record):
            assert record["passed"]==len(set(record["tests"]))==2
            assert record["ui"]=="COALITION_UI_PASS"
            for case in ("InvitesDifferentHomes", "LegacyLiteralWorlds"):
                assert any(case in name for name in record["tests"])
    elif version==88:
        folder="SupremeOfficeV1"
        def verify(record):
            assert record["passed"]==len(set(record["tests"]))==26 and record["legacy_bytes"]==2
            assert record["ui"]=="SUPREME_OFFICE_UI_PASS"
            for case in ("ImperialInvitationHonors","RegentChangesAtDecision","AdoptedChildQualifies","LegacyLiteralWorlds"):
                assert any(case in t for t in record["tests"])
    elif version==87:
        folder="CentralMediationV1"
        def verify(record):
            assert record["passed"]==len(record["tests"])==76 and record["legacy_bytes"]==3
            assert record["ui"]=="CENTRAL_MEDIATION_UI_PASS"
            for case in ("BrokerSendsSamePeaceTerms","RetainerMustMeetCurrentLord","ShogunMustReadRecommendation"):
                assert any(case in t for t in record["tests"])
    elif version==86:
        folder="HouseholdDetachmentV1"
        def verify(record):
            assert record["passed"]==len(record["tests"])==81 and record["legacy_bytes"]==3
            assert record["ui"]=="HOUSEHOLD_DETACHMENT_UI_PASS"
            for case in ("DeployedCaptainSigns","PaidPrivateCompany","StandingCompanyPays","OwnerCanBringPaidCompany","VillageLaborReturns","LegacyLiteralWorlds"):
                assert any(case in t for t in record["tests"])
    elif version==85:
        folder="KokujinCouncilV1"
        def verify(record):
            assert record["passed"]==len(record["tests"])==55 and record["ui"]=="KOKUJIN_COUNCIL_UI_PASS"
            for case in ("CharterMembershipRequires","OnlyOpenedMemberVote","MemberSubmissionIsLearned","LegacyLiteralWorlds"):
                assert any(case in t for t in record["tests"])
    elif version==84:
        folder="SubmissionDemandV1"
        def verify(record):
            assert record["passed"]==len(record["tests"])==54 and record["ui"]=="SUBMISSION_DEMAND_UI_PASS"
            for case in ("WrittenSubmissionTravels", "VisitingTreatyCommander", "LegacyLiteralWorlds"):
                assert any(case in t for t in record["tests"])
    elif version==83:
        folder="VillageResistanceV1"
        def verify(record):
            assert record["passed"]==len(record["tests"])==75 and record["legacy"]==2
            assert record["ui"]=="VILLAGE_RESISTANCE_UI_PASS"
            assert any("HungerPetitionKeepsOnlyUndeliveredRelief" in name for name in record["tests"])
    elif version==82:
        folder="RecruitmentMusterV1"
        def verify(record):
            assert record["passed"]==len(record["tests"])==108 and record["legacy"]==2
            assert record["ui"]=="MUSTER_UI_PASS"
            for test in ("VisitingTreatyCommanderObservesThirdHouseAttackOnHost","NpcAttendsAtOrderedTimeAcrossDiskSaveAndLongAdvance","LegacyLiteralWorldsStillRoundTripWithoutNewModule"):
                assert any(test in name for name in record["tests"])
    elif version==81:
        from verify_house_restoration import check as verify
        folder="HouseRestorationV1"
    elif version==80:
        from verify_house_reform import check as verify
        folder="HouseReformV1"
    elif version==79:
        from verify_house_scale import check as verify
        folder="HouseScaleV1"
    elif version==78:
        from verify_defection_pacts import check as verify
        folder="DefectionPactV1"
    elif version==77:
        from verify_guardianship import check as verify
        folder="GuardianshipV1"
    elif version==76:
        from verify_heir_designation import check as verify
        folder="HeirDesignationV1"
    elif version==75:
        from verify_family_life import check as verify
        folder="FamilyLifeV1"
    elif version==74:
        from verify_assassination import check as verify
        folder="AssassinationV1"
    elif version==73:
        from verify_destructive_schemes import check as verify
        folder="DestructiveSchemesV1"
    elif version==72:
        from verify_castle_siege import check as verify
        folder="CastleSiegeV1"
    elif version==71:
        from verify_castle_works import check as verify
        folder="CastleWorksV1"
    elif version==70:
        from verify_local_security import check as verify
        folder="LocalSecurityV1"
    elif version==69:
        from verify_battlefield_rules import check as verify
        folder="BattlefieldRulesV1"
    elif version==68:
        from verify_bandit_encounter import check as verify
        folder="BanditEncounterV1"
    elif version==67:
        from verify_ability_effects import check as verify
        folder="AbilityEffectsV1"
    elif version==66:
        from verify_regional_weather import check as verify
        folder="RegionalWeatherV1"
    elif version==65:
        from verify_land_survey import check as verify
        folder="LandSurveyV1"
    elif version==64:
        from verify_craft_trade import check as verify
        folder="CraftTradeV1"
        assert subprocess.run(["git","cat-file","-e",pin+":Core/Contracts/CraftTrade.cs"],cwd=R,capture_output=True).returncode!=0
    elif version==63:
        from verify_standing_administration import check as verify
        folder="StandingAdministrationV1"
        assert subprocess.run(["git","cat-file","-e",pin+":Core/Contracts/StandingAdministration.cs"],cwd=R,capture_output=True).returncode!=0
    elif version==62:
        from verify_public_works import check as verify
        folder="PublicWorksV1"
        assert subprocess.run(["git","cat-file","-e",pin+":Core/Contracts/PublicWorks.cs"],cwd=R,capture_output=True).returncode!=0
    elif version==61:
        from verify_broker_work import check as verify
        folder="BrokerWorkV1"
        assert subprocess.run(["git","cat-file","-e",pin+":Core/Contracts/BrokerWork.cs"],cwd=R,capture_output=True).returncode!=0
    elif version==60:
        from verify_household_court import check as verify
        folder="HouseholdCourtV1"
        assert subprocess.run(["git","cat-file","-e",pin+":Core/Contracts/HouseholdCourt.cs"],cwd=R,capture_output=True).returncode!=0
    elif version==59:
        from verify_information_schemes import check as verify
        folder="InformationSchemesV1"
        assert subprocess.run(["git","cat-file","-e",pin+":Core/Contracts/InformationSchemes.cs"],cwd=R,capture_output=True).returncode!=0
    elif version==58:
        from verify_merit_rewards import check as verify
        folder="MeritRewardsV1"
        assert subprocess.run(["git","cat-file","-e",pin+":Core/Contracts/MeritRewards.cs"],cwd=R,capture_output=True).returncode!=0
    elif version==57:
        from verify_social_assets import check as verify
        folder="SocialAssetsV1"
        assert subprocess.run(["git","cat-file","-e",pin+":Core/Contracts/SocialAssets.cs"],cwd=R,capture_output=True).returncode!=0
    elif version==56:
        from verify_social_world import check as verify
        folder="SocialWorldV1"
        assert subprocess.run(["git","cat-file","-e",pin+":Core/Contracts/SocialWorld.cs"],cwd=R,capture_output=True).returncode!=0
    elif version==55:
        from verify_verb_notebook import check as verify
        folder="VerbNotebookV1"
        assert subprocess.run(["git","cat-file","-e",pin+":Game/Scripts/NewGameRuntime.VerbNotebook.cs"],cwd=R,capture_output=True).returncode!=0
    elif version==54:
        from verify_historical_chronicle import check as verify
        folder="HistoricalChronicleV1"
        assert subprocess.run(["git","cat-file","-e",pin+":Core/Contracts/HistoricalEventRules.cs"],cwd=R,capture_output=True).returncode!=0
    elif version==53:
        from verify_npc_judgment import check as verify
        folder="NpcJudgmentV1"
        assert b.get('automatic_main') is True and b['minimum_gain']==0
        assert subprocess.run(['git','cat-file','-e',pin+':Core/Contracts/NpcJudgment.cs'],cwd=R,capture_output=True).returncode!=0
        assert abs(float(old)-47.00286796536797)<1e-10
        assert 'NpcPolicyForEntry=>TestKey==null?NpcJudgment.Policy:NewGameNpcRoutine.Policy' in (R/'Game/Scripts/NewGameFrontEnd.cs').read_text(encoding='utf-8')
    elif version==52:
        from verify_world_time import check as verify
        folder="WorldTimeV1"
        assert b.get('automatic_main') is True and b['minimum_gain']==0
        assert subprocess.run(['git','cat-file','-e',pin+':Core/Contracts/WorldTimeControl.cs'],cwd=R,capture_output=True).returncode!=0
        assert 'TestKey==null?WorldTimeControl.Policy:null' in (R/'Game/Scripts/NewGameFrontEnd.cs').read_text(encoding='utf-8')
    elif version==51:
        from verify_npc_purpose import check as verify
        folder="NpcPurposeV1"
        assert b.get('automatic_main') is True and b['minimum_gain']==0
        assert subprocess.run(['git','cat-file','-e',pin+':Core/Contracts/NpcPurpose.cs'],cwd=R,capture_output=True).returncode!=0
        assert 'NpcPolicyForEntry=>TestKey==null?NewGameNpcRoutine.PurposePolicy:NewGameNpcRoutine.Policy' in (R/'Game/Scripts/NewGameFrontEnd.cs').read_text(encoding='utf-8')
    elif version==50:
        from verify_progress_scope import check as verify
        folder="ProgressScopeV5"
        assert b.get('kind')=='scope-reassessment' and b['minimum_gain']==0 and b['targets']=={} and b['reassessment_delta_excluded']<0
    elif version==49:
        from verify_progress_scope import check as verify
        folder="ProgressScopeV4"
        assert b.get('kind')=='scope-reassessment' and b['minimum_gain']==0 and b['targets']=={} and b['reassessment_delta_excluded']<0
    elif version==48:
        from verify_progress_scope import check as verify
        folder="ProgressScopeV3"
        assert b.get('kind')=='scope-reassessment' and b['minimum_gain']==0 and b['targets']=={} and b['reassessment_delta_excluded']<0
    elif version==47:
        from verify_progress_scope import check as verify
        folder="ProgressScopeV2"
        assert b.get('kind')=='scope-reassessment' and b['minimum_gain']==0 and b['targets']=={} and b['reassessment_delta_excluded']<0
    elif version==46:
        from verify_progress_scope import check as verify
        folder="ProgressScopeV1"
        assert b.get('kind')=='scope-reassessment' and b['minimum_gain']==0 and b['targets']=={} and b['reassessment_delta_excluded']<0
    elif version==45:
        from verify_npc_performance import check as verify
        folder="NpcPerformanceV1"
        assert b.get('automatic_main') is True and b['minimum_gain']==0 and b['targets']=={}
        assert subprocess.run(['git','cat-file','-e',pin+':Core/Contracts/WorldSession.NpcRoutine.cs'],cwd=R,capture_output=True).returncode!=0
    elif version==44:
        from verify_npc_continuing import check as verify
        folder="NpcContinuingV1"
        assert b.get('automatic_main') is True and b['minimum_gain']==0
        assert subprocess.run(['git','cat-file','-e',pin+':Core/Contracts/NpcContinuing.cs'],cwd=R,capture_output=True).returncode!=0
        assert 'NpcPolicyForEntry=>TestKey==null?NewGameNpcRoutine.ContinuingPolicy:NewGameNpcRoutine.Policy' in (R/'Game/Scripts/NewGameFrontEnd.cs').read_text(encoding='utf-8')
    elif version==43:
        from verify_custody_return import check as verify
        folder="CustodyReturnV1"
        assert b.get('automatic_main') is True and b['minimum_gain']==0
        assert subprocess.run(['git','cat-file','-e',pin+':Core/Contracts/HostageCustody.Return.cs'],cwd=R,capture_output=True).returncode!=0
        assert 'CustodyAction.StartReturn' in (R/'Game/Scripts/NewGameRuntime.Custody.cs').read_text(encoding='utf-8')
        assert 'HostageCustody.ReturnJourney' in (R/'Core/Contracts/BranchRoutes.cs').read_text(encoding='utf-8')
    elif version==42:
        from verify_custody_food import check as verify
        folder="CustodyFoodV1"
        assert b.get('automatic_main') is True and b['minimum_gain']==0
        assert subprocess.run(['git','cat-file','-e',pin+':Core/Contracts/HostageCustody.Food.cs'],cwd=R,capture_output=True).returncode!=0
        assert 'CustodyAction.RequestMeal' in (R/'Game/Scripts/NewGameRuntime.Custody.cs').read_text(encoding='utf-8')
        assert 'HostageCustody.HouseTransfers' in (R/'Core/Contracts/CommunityEconomy.cs').read_text(encoding='utf-8')
    elif version==41:
        from verify_custody_succession import check as verify
        folder="CustodySuccessionV1"
        assert b.get('automatic_main') is True and b['minimum_gain']==0
        assert b'CustodyRepresentatives' not in subprocess.check_output(['git','show',pin+':Core/Contracts/HostageCustody.cs'],cwd=R)
        assert 'CustodyAction.Reconfirm' in (R/'Game/Scripts/NewGameRuntime.Custody.cs').read_text(encoding='utf-8')
        assert 'CompanionAction.VisitCustodySite' in (R/'Game/Scripts/NewGameRuntime.Custody.cs').read_text(encoding='utf-8')
    elif version==40:
        from verify_hostage_custody import check as verify
        folder="HostageCustodyV1"
        assert b.get('automatic_main') is True and b['minimum_gain']==0
        assert subprocess.run(['git','cat-file','-e',pin+':Core/Contracts/HostageCustody.cs'],cwd=R,capture_output=True).returncode!=0
        assert 'HostageCustody.Policy' in (R/'Game/Scripts/NewGameFrontEnd.cs').read_text(encoding='utf-8')
        assert 'HostageCustody.View' in (R/'Game/Scripts/NewGameRuntime.Custody.cs').read_text(encoding='utf-8')
    elif version==39:
        from verify_adult_marriage import check as verify
        folder="AdultMarriageV1"
        assert b.get('automatic_main') is True and b['minimum_gain']==0
        assert subprocess.run(['git','cat-file','-e',pin+':Core/Contracts/AdultMarriage.cs'],cwd=R,capture_output=True).returncode!=0
        assert 'AdultMarriage.Policy' in (R/'Game/Scripts/NewGameFrontEnd.cs').read_text(encoding='utf-8')
        assert 'AdultMarriage.View' in (R/'Game/Scripts/NewGameRuntime.Marriage.cs').read_text(encoding='utf-8')
    elif version==38:
        from verify_adult_adoption import check as verify
        folder="AdultAdoptionV1"
        assert b.get('automatic_main') is True and b['minimum_gain']==0
        assert subprocess.run(['git','cat-file','-e',pin+':Core/Contracts/AdultAdoption.cs'],cwd=R,capture_output=True).returncode!=0
        assert 'AdultAdoption.Policy' in (R/'Game/Scripts/NewGameFrontEnd.cs').read_text(encoding='utf-8')
        assert 'AdultAdoption.View' in (R/'Game/Scripts/NewGameRuntime.Adoption.cs').read_text(encoding='utf-8')
    elif version==37:
        from verify_character_relations import check as verify
        folder="CharacterRelationsV1"
        assert b.get('automatic_main') is True and b['minimum_gain']==0
        assert subprocess.run(['git','cat-file','-e',pin+':Core/Contracts/CharacterRelations.cs'],cwd=R,capture_output=True).returncode!=0
        assert 'CharacterRelations.Policy' in (R/'Game/Scripts/NewGameFrontEnd.cs').read_text(encoding='utf-8')
        assert 'CharacterRelations.View' in (R/'Game/Scripts/NewGameRuntime.CharacterDevelopment.cs').read_text(encoding='utf-8')
    elif version==36:
        from verify_character_development import check as verify
        folder="CharacterDevelopmentV1"
        assert b.get('automatic_main') is True and b['minimum_gain']==0
        assert subprocess.run(['git','cat-file','-e',pin+':Core/Contracts/CharacterDevelopment.cs'],cwd=R,capture_output=True).returncode!=0
        assert 'CharacterDevelopment.Policy' in (R/'Game/Scripts/NewGameFrontEnd.cs').read_text(encoding='utf-8')
        assert 'ShowCharacterDevelopment' in (R/'Game/Scripts/NewGameRuntime.CharacterDevelopment.cs').read_text(encoding='utf-8')
    elif version==35:
        from verify_treaty_defense import check as verify
        folder="TreatyDefenseV1"
        assert b.get('automatic_main') is True and b['minimum_gain']==0
        assert subprocess.run(['git','cat-file','-e',pin+':Core/Contracts/TreatyDefense.cs'],cwd=R,capture_output=True).returncode!=0,'Defense scope not frozen before code'
        assert 'ShowTreatyDefense' in (R/'Game/Scripts/NewGameRuntime.TreatyDefense.cs').read_text(encoding='utf-8')
    elif version==34:
        from verify_treaty_supply import check as verify
        folder="TreatySupplyV1"
        assert b.get('automatic_main') is True and b['minimum_gain']==0
        assert subprocess.run(['git','cat-file','-e',pin+':Core/Contracts/TreatySupply.cs'],cwd=R,capture_output=True).returncode!=0,'Supply scope not frozen before code'
        assert 'ShowTreatySupply' in (R/'Game/Scripts/NewGameRuntime.TreatySupply.cs').read_text(encoding='utf-8')
    elif version==33:
        from verify_treaty import check as verify
        folder="TreatyV1"
        assert subprocess.run(['git','cat-file','-e',pin+':Core/Contracts/TreatyNegotiations.cs'],cwd=R,capture_output=True).returncode!=0,'Treaty scope not frozen before code'
        assert 'ShowTreatyNegotiation' in (R/'Game/Scripts/NewGameRuntime.Truce.cs').read_text(encoding='utf-8')
        assert 'ShowTreatyLetter' in (R/'Game/Scripts/NewGameRuntime.Mail.cs').read_text(encoding='utf-8')
    elif version==32:
        from verify_remote_military_succession import check as verify
        folder="RemoteMilitarySuccessionV1"
        assert subprocess.run(['git','cat-file','-e',pin+':Core/Contracts/NewGameMilitary.RemoteSuccession.cs'],cwd=R,capture_output=True).returncode!=0,'Remote military scope not frozen before implementation'
        assert 'AppendRemoteMilitary' in (R/'Game/Scripts/NewGameRuntime.HouseSuccession.cs').read_text(encoding='utf-8')
        assert 'ShowRemoteMilitaryLetter' in (R/'Game/Scripts/NewGameRuntime.Mail.cs').read_text(encoding='utf-8')
        assert 'SendRemoteMilitary' in (R/'Game/Scripts/NewGameRuntime.RemoteMilitarySuccession.cs').read_text(encoding='utf-8')
    elif version==31:
        from verify_active_military_succession import check as verify
        folder="ActiveMilitarySuccessionV1"
        assert subprocess.run(['git','cat-file','-e',pin+':Core/Contracts/NewGameMilitary.ActiveSuccession.cs'],cwd=R,capture_output=True).returncode!=0,'Active army scope not frozen before implementation'
        assert subprocess.run(['git','cat-file','-e',pin+':Core/Contracts/NewGameMilitary.StoredSuccession.cs'],cwd=R,capture_output=True).returncode!=0,'Stored garrison scope not frozen before implementation'
        ui=(R/'Game/Scripts/NewGameRuntime.HouseSuccession.cs').read_text(encoding='utf-8')
        assert 'AgreeActiveMilitary' in ui and 'StoredSuccessionRequest' in ui
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
    return dict(baseline_commit=pin,baseline=float(old),current=float(new),earned_gain=float(gain),excluded_reassessment=(0 if version==39 and "reassessment_delta_excluded" not in b else b["reassessment_delta_excluded"]),minimum=b["minimum_gain"],status="PASSED")
if __name__=="__main__":print(json.dumps(check(),ensure_ascii=False,indent=2))
