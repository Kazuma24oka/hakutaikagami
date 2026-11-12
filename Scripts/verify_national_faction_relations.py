from pathlib import Path
import json
R=Path(__file__).resolve().parents[1]
def check(record):
    completion=json.loads((R/"Docs/Design/NationalFactionRelationsV1/completion.json").read_bytes())
    v=json.loads((R/"Docs/Design/NationalFactionRelationsV1/verification.json").read_bytes())
    assert record["scope"]=="national-faction-relations"
    assert len(completion["criteria"])==4 and all(x["status"]=="PASSED" and x["evidence"] for x in completion["criteria"])
    assert v["related_tests"]["status"]=="PASSED" and v["related_tests"]["passed"]==2 and v["related_tests"]["failed"]==0
    assert v["short_suite"]["status"]=="PASSED" and v["short_suite"]["failed"]==0
    assert v["game_build"]["status"]=="PASSED" and v["game_build"]["errors"]==0
    assert v["save_restore"]["status"]=="PASSED" and v["legacy_profile"]["status"]=="PASSED"
    assert v["full_suite"]["status"] in {"PENDING","PASSED"}
    assert v["player_display"]["status"]=="CODE_AND_CORE_TEST_PASSED" and v["computer_use"]=="未使用"
