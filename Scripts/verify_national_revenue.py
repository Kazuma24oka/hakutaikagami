"""全国郡収入v116の実測と接続を検査する。"""
from pathlib import Path
R=Path(__file__).resolve().parents[1]
def check(record):
    assert record["scope"]=="national-district-revenue"
    for name in ("related_tests","full_suite","game_build","ui","legacy"):
        assert record[name]["status"]=="PASSED",name
    assert record["full_suite"]["passed"]==4535 and record["full_suite"]["failed"]==0
    assert record["game_build"]["errors"]==0
    assert record["ui"]["house_cash"]+4==record["ui"]["gross_income"]
    assert record["ui"]["private_cash"]==1000
    assert record["computer_use"]=="未使用"
    revenue=(R/"Core/Contracts/NationalDistrictRevenue.cs").read_text(encoding="utf-8")
    assert "AnnualKokuPerMon=10" in revenue and "NationalStrategy.Owner" in revenue
    assert "NationalDistrictRevenue.Income" in (R/"Core/Contracts/RegionalDomains.cs").read_text(encoding="utf-8")
    start=(R/"Core/Contracts/NationalStart.cs").read_text(encoding="utf-8")
    assert all(x in start for x in ("hakutai-1560-national-v1","hakutai-1560-national-v2","hakutai-1560-national-v3","hakutai-1560-national-v4","ScenarioFor"))
