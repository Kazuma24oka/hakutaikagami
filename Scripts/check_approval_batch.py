"""Enforce implementation gain against immutable pre-implementation commit; no re-rating credit."""
from pathlib import Path
from fractions import Fraction
import json,subprocess
R=Path(__file__).resolve().parents[1]
PIN="e304cb0018cbeb870a31b82b208ee1c3c18932a7"
BASE="Docs/Development/ProgressV1/approval-baseline-v1.json"
def calculate(a,d,leaves):
    groups={g["id"]:g for g in d["groups"]};total=Fraction(0)
    for domain in a["domains"]:
        ids=[i["id"] for gid in domain["groups"] for i in groups[gid]["items"] if i["id"] not in a["excluded_from_rollup"]]
        total+=sum(Fraction(leaves[i]["percent"]) for i in ids)/len(ids)*Fraction(domain["weight"],100)
    return total
def check():
    pinned=subprocess.check_output(["git","show",PIN+":"+BASE],cwd=R)
    assert (R/BASE).read_bytes()==pinned,"Frozen baseline changed"
    b=json.loads(pinned);a=json.loads((R/"Docs/Development/ProgressV1/estimate.json").read_bytes());p=json.loads((R/"Docs/Development/ProgressV1/progress.json").read_bytes())
    assert a["domains"]==b["domains"] and a["excluded_from_rollup"]==b["excluded_from_rollup"],"Weights or exclusions changed"
    design=p["design_source"];assert (R/design).read_bytes()==subprocess.check_output(["git","show",PIN+":"+design],cwd=R),"Design denominator changed"
    d=json.loads((R/design).read_bytes());expected={k:v["percent"] for k,v in b["baseline_leaves"].items()};expected.update(b["targets"])
    assert {k:v["percent"] for k,v in a["leaves"].items()}==expected,"Only predeclared target gains are eligible"
    old=calculate(b,d,b["baseline_leaves"]);new=calculate(b,d,a["leaves"]);gain=new-old
    assert gain>=Fraction(str(b["minimum_gain"])),"Less than one earned implementation point"
    assert "CommunityEconomy.Policy" in (R/"Game/Scripts/NewGameFrontEnd.cs").read_text(encoding="utf-8")
    assert "Session.CommunityEconomyView()!=null" in (R/"Game/Scripts/NewGameRuntime.WorkExperience.cs").read_text(encoding="utf-8")
    from verify_community_economy import check as verify
    v=json.loads((R/"Docs/Design/CommunityEconomyV2/verification.json").read_bytes());verify(v)
    from probe_community_reports import check as probe
    probe()
    c=json.loads((R/"Docs/Design/CommunityEconomyV2/completion.json").read_bytes())
    assert len(c["criteria"])==len(b["criteria"])==4
    assert all(x["status"]=="PASSED" and x["evidence"] for x in c["criteria"])
    assert [x["contract"] for x in c["criteria"]]==b["criteria"]
    return dict(baseline_commit=PIN,baseline=float(old),current=float(new),earned_gain=float(gain),excluded_reassessment=b["reassessment_delta_excluded"],minimum=1,status="PASSED")
if __name__=="__main__":print(json.dumps(check(),ensure_ascii=False,indent=2))
