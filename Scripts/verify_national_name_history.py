from pathlib import Path
import json

R=Path(__file__).resolve().parents[1]

def check(record):
    completion=json.loads((R/"Docs/Design/NationalNameHistoryV1/completion.json").read_bytes())
    verification=json.loads((R/"Docs/Design/NationalNameHistoryV1/verification.json").read_bytes())
    assert record["scope"]=="national-name-history"
    assert len(completion["criteria"])==4 and all(x["status"]=="PASSED" and x["evidence"] for x in completion["criteria"])
    assert verification["related_tests"]["status"]=="PASSED" and verification["related_tests"]["failed"]==0
    assert verification["short_suite"]["status"] in {"PENDING","PASSED"} and verification["short_suite"]["failed"]==0
    assert verification["game_build"]["status"]=="PASSED" and verification["game_build"]["errors"]==0
    assert verification["save_restore"]["status"]=="PASSED" and verification["legacy_world"]["status"]=="PASSED"
    assert verification["full_suite"]["status"] in {"PENDING","PASSED"}
    assert verification["player_display"]["status"]=="CODE_AND_CORE_TEST_PASSED" and verification["computer_use"]=="未使用"
