"""Evidence checks for the v115 national person data stage."""
from pathlib import Path
import hashlib, json
R = Path(__file__).resolve().parents[1]

def check(record):
    table_path = R / "Core/Data/national-person-estimates-v1.json"
    table = json.loads(table_path.read_bytes())
    db_path = R / "Core/Data/hakutai-db-1560-v2.json"
    db = json.loads(db_path.read_bytes())
    assert table["Policy"] == "national-person-data-v1"
    assert table["SourceEdition"] == db["Id"]
    assert table["SourceCommit"] == db["Source"]["Commit"]
    assert table["SourceSha256"] == hashlib.sha256(db_path.read_bytes()).hexdigest()
    assert len(table["People"]) == len(db["People"]) == 4583
    rows = {row["Id"]: row for row in table["People"]}
    people = {row["Id"]: row for row in db["People"]}
    assert set(rows) == set(people)
    for person_id, row in rows.items():
        source = people[person_id]
        if source["Birth"] is not None:
            assert row["BirthYear"] == source["Birth"] and row["BirthKind"] == "explicit"
        else:
            assert row["BirthKind"].startswith("estimated-") and row["BirthBasis"] and row["BirthConfidence"].startswith("estimate-")
        assert all(1 <= row["Abilities"][key] <= 100 for key in ("Leadership", "Valor", "Intelligence", "Politics", "Charm"))
        assert row["AbilityConfidence"] == "estimate-very-low"
        assert row["Abilities"]["Charm"] == 50
        assert "交渉値は転用しない" in row["AbilityBasis"]["Charm"]
        father_id = source.get("Father")
        if father_id in rows:
            assert row["BirthParentAccepted"] == (rows[father_id]["BirthYear"] < row["BirthYear"])
    assert record["data_table"]["status"] == "PASSED"
    assert record["data_table"]["people"] == 4583
    assert record["related_tests"]["status"] == "PASSED" and record["related_tests"]["passed"] >= 4
    assert record["short_suite"]["status"] == "PASSED" and record["short_suite"]["failed"] == 0
    assert record["game_build"]["status"] == "PASSED" and record["game_build"]["errors"] == 0
    assert record["save_restore"]["status"] == "PASSED" and record["legacy"]["status"] == "PASSED"
    assert record["full_suite"]["status"] == "PASSED" and record["full_suite"]["failed"] == 0
