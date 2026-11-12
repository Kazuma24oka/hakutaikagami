"""v112の郡支配・地図・途中保存の作業記録を検査する。"""
from pathlib import Path
import json
R=Path(__file__).resolve().parents[1]

def check(record=None):
    if record is None: record=json.loads((R/"Docs/Design/NationalDistrictControlV1/verification.json").read_bytes())
    assert record["scope"]=="national-district-control" and record["baseline"]=="approval-baseline-v112"
    assert record["related_tests"]["status"]=="PASSED" and record["related_tests"]["passed"]==15 and record["related_tests"]["failed"]==0
    assert record["short_suite"]["status"]=="PASSED" and record["short_suite"]["passed"]==2820 and record["short_suite"]["failed"]==0
    assert record["game_build"]["status"]=="PASSED" and record["game_build"]["warnings"]==record["game_build"]["errors"]==0
    assert record["setup_map"]["status"]==record["world_map"]["status"]==record["legacy_profile"]["status"]=="PASSED"
    assert len(record["world_map"]["checks"])==8 and record["world_map"]["districts"]==637
    assert record["world_map"]["world_hash"]=="b5db5a3c10bd9c7d01ec9750a161b77f2eb1e78647d2c19f69994453fc53ff57"
    c=json.loads((R/"Docs/Design/NationalDistrictControlV1/completion.json").read_bytes())
    b=json.loads((R/"Docs/Development/ProgressV1/approval-baseline-v112.json").read_bytes())
    assert [x["contract"] for x in c["criteria"]]==b["criteria"]
    assert len(c["criteria"])==4 and all(x["status"]=="PASSED" and x["evidence"] for x in c["criteria"])
    return {"criteria":4,"status":"PASSED"}
