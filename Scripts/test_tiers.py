"""百代鏡：検査を2段に分ける（2026-11-12 ユーザー決定、PROCESS_V2 第2節）。

  python Scripts/test_tiers.py <全件の TRX>   → Docs/Development/TEST_DURATIONS.md を書き、重い組に [Trait("Tier","Heavy")] を付ける

  短い段：dotnet test Tests -c Release --filter "Tier!=Heavy"
  全件  ：dotnet test Tests -c Release
重い組：組（検査のクラス）の合計の所要時間が 300 秒を超えるもの。ファイルは印を足すだけで、改行などほかは変えない。
"""
from __future__ import annotations
import collections, re, sys, xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
N = "{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}"
LIMIT = 300.0
MARK = b'[Trait("Tier","Heavy")]'


def sec(d: str) -> float:
    h, m, s = d.split(":")
    return int(h) * 3600 + int(m) * 60 + float(s)


def main(trx: str) -> None:
    res = [(r.get("testName"), sec(r.get("duration", "0:0:0"))) for r in ET.parse(trx).getroot().iter(N + "UnitTestResult")]
    cls: dict[str, list[float]] = collections.defaultdict(list)
    for name, d in res:
        cls[name.split("(")[0].rsplit(".", 1)[0].rsplit(".", 1)[-1]].append(d)
    total = sum(d for _, d in res)
    heavy = sorted((c for c, v in cls.items() if sum(v) > LIMIT), key=lambda c: -sum(cls[c]))
    marked = 0
    for f in sorted((ROOT / "Tests").glob("*.cs")):
        raw = f.read_bytes()
        out = raw
        for c in heavy:
            pattern = re.compile(rb"(?m)^([ \t]*)((?:public |internal )?(?:sealed )?(?:partial )?class " + re.escape(c.encode()) + rb"\b)")
            def add(m: re.Match[bytes]) -> bytes:
                before = out[max(0, m.start() - len(MARK) - 2):m.start()]
                return m.group(0) if MARK in before else m.group(1) + MARK + m.group(2)
            out = pattern.sub(add, out)
        if out != raw:
            f.write_bytes(out)
            marked += 1
    keep = sum(sum(v) for c, v in cls.items() if c not in heavy)
    lines = ["# 検査ごとの所要時間（2026-11-12）", "",
             f"Scripts/test_tiers.py が全件の記録（TRX）から書く。全 {len(res)} 件・合計 {total/60:.0f} 分（並列で実時間 約1時間10分）。",
             f"重い組（合計 {LIMIT:.0f} 秒を超える組）{len(heavy)} 組に `Tier=Heavy` を付けた。短い段は残りの {sum(len(v) for c, v in cls.items() if c not in heavy)} 件・合計 {keep/60:.0f} 分（全体の {keep/total*100:.0f}%）。", "",
             "- 短い段：`dotnet test Tests -c Release --filter \"Tier!=Heavy\"`（版の途中と統合の前）",
             "- 全件：`dotnet test Tests -c Release`（遊べる版をまとめるとき、別の作業用フォルダで裏で流す）", "",
             "| 組 | 件数 | 合計（分） | 一番重い1件（秒） |", "|---|---|---|---|"]
    for c in heavy:
        v = cls[c]
        lines.append(f"| {c} | {len(v)} | {sum(v)/60:.1f} | {max(v):.0f} |")
    (ROOT / "Docs" / "Development" / "TEST_DURATIONS.md").write_text("\n".join(lines) + "\n", encoding="utf-8", newline="\n")
    print(f"重い組 {len(heavy)}、印を付けたファイル {marked}")


if __name__ == "__main__":
    main(sys.argv[1])
