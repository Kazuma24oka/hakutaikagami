"""Minimum implementation gain from immutable pre-code baselines, without re-rating credit."""
from pathlib import Path
from fractions import Fraction
import json,subprocess
R=Path(__file__).resolve().parents[1]
BASELINES={1:("e304cb0018cbeb870a31b82b208ee1c3c18932a7","Docs/Development/ProgressV1/approval-baseline-v1.json"),2:("c67b815170681d8453cd36a8d5e8aa1411be54ab","Docs/Development/ProgressV1/approval-baseline-v2.json"),3:("09806b77d0f372b7edb5e72031ddfca0d6d533b7","Docs/Development/ProgressV1/approval-baseline-v3.json")}
def calculate(a,d,leaves):
    groups={g["id"]:g for g in d["groups"]};total=Fraction(0)
    for domain in a["domains"]:
        ids=[i["id"] for gid in domain["groups"] for i in groups[gid]["items"] if i["id"] not in a["excluded_from_rollup"]]
        total+=sum(Fraction(leaves[i]["percent"]) for i in ids)/len(ids)*Fraction(domain["weight"],100)
    return total
def check():
    a=json.loads((R/"Docs/Development/ProgressV1/estimate.json").read_bytes());version=a.get("approval_batch",{}).get("version",1);pin,base=BASELINES[version]
    frozen=subprocess.check_output(["git","show",pin+":"+base],cwd=R);assert (R/base).read_bytes()==frozen,"Frozen baseline changed"
    b=json.loads(frozen);p=json.loads((R/"Docs/Development/ProgressV1/progress.json").read_bytes());assert a["domains"]==b["domains"] and a["excluded_from_rollup"]==b["excluded_from_rollup"],"Weights/exclusions changed"
    design=p["design_source"];assert (R/design).read_bytes()==subprocess.check_output(["git","show",pin+":"+design],cwd=R),"Design denominator changed"
    d=json.loads((R/design).read_bytes());expected={k:v["percent"] for k,v in b["baseline_leaves"].items()};expected.update(b["targets"])
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
