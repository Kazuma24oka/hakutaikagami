"""旧家再興の工程末の実測記録を照合する。"""
import hashlib
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]


def check(record):
    for name, digest in record["proofs"].items():
        assert hashlib.sha256((ROOT / name).read_bytes()).hexdigest() == digest, name
    trx = ROOT / "Docs/Design/HouseRestorationV1/runtime-local/critical/critical.trx"
    counters = ET.parse(trx).find(".//{*}Counters").attrib
    assert int(counters["passed"]) == int(counters["total"]) == record["passed"] == 90
    assert int(counters["failed"]) == int(counters["notExecuted"]) == 0
