"""百代鏡DB：戦国DBの特定の版から、1560年の開始に使う人物・家・城を一方向に書き出す（2026-11-12）。

  python Tools/HakutaiDb/export_1560.py [戦国DBのフォルダ] [出力]
    既定：C:/Workspace/Projects/Data/SengokuDB → Core/Data/hakutai-db-1560-v1.json

戦国DB の決まり（docs/DESIGN.md 第6節）：百代鏡側から戦国DBを書き換えない。誤りは戦国DBで直して書き出し直す。
人物・家・城の ID は戦国DBのものをそのまま使う。

材料（どれも戦国DBの中）：
  build/sengoku.sqlite の「人物一覧」と father の記録（名前・読み・生没年・確度・登録状態）
  progress/external/person_year_status.json（1560年に活動中の武将の所属・置き場所の城・直属の上司。推定の決め方つき）
  progress/external/scenario_map_data.json（1560年の勢力・関係・城の持ち主）
  progress/external/castle_coords.json（城の位置）
どれも「機械で見積もったゲームの初期配置の下書き」で、史実の判定ではない。決め方（Basis）を値ごとに残す。
能力・性格・見た目・採用する説は百代鏡側にだけ置く（ここでは書き出さない）。
"""
from __future__ import annotations

import json
import sqlite3
import subprocess
import sys
from collections import Counter
from pathlib import Path

YEAR = 1560
SRC = Path(sys.argv[1]) if len(sys.argv) > 1 else Path("C:/Workspace/Projects/Data/SengokuDB")
OUT = Path(sys.argv[2]) if len(sys.argv) > 2 else Path(__file__).resolve().parents[2] / "Core" / "Data" / "hakutai-db-1560-v1.json"

CASTLE_BASIS = {0: "本人の城主の記録（時期つき）", 1: "本人の城主の記録（時期なし）", 2: "同じ苗字の城主の城（推定）", 3: "国の当主の城（推定）", 4: "国の家の城（推定）", 5: "家の本拠（推定）"}
POSITION_BASIS = {"参考サイト": "参考サイトの位置", "シナリオ": "シナリオの位置（目安）", "記事": "記事の位置", "手の表": "手で確かめた位置", "国": "国の中の目安", "郡": "郡の中の目安"}


def git(*args):
    return subprocess.run(["git", "-C", str(SRC), *args], capture_output=True, text=True, check=True).stdout.strip()


def main():
    if git("status", "--porcelain", "--untracked-files=no"):
        raise SystemExit("戦国DBに未コミットの変更がある。版を固定できないので書き出さない")
    commit = git("rev-parse", "HEAD")
    tags = git("tag", "--points-at", "HEAD").split()
    db = sqlite3.connect(SRC / "build" / "sengoku.sqlite")
    built = dict(db.execute("select key, value from build_info").fetchall())
    ext = SRC / "progress" / "external"
    pys = json.loads((ext / "person_year_status.json").read_text(encoding="utf-8"))
    mp = json.loads((ext / "scenario_map_data.json").read_text(encoding="utf-8"))
    coords = json.loads((ext / "castle_coords.json").read_text(encoding="utf-8"))
    year = mp["years"][str(YEAR)]
    status = pys["years"][str(YEAR)]

    # 家（勢力）
    fl = year["factions"]
    houses = []
    for f in fl:
        houses.append({
            "Id": f["gid"], "Name": f["name"],
            "Head": None, "Home": None,
            "Religious": bool(f["religious"]),
            "Overlord": fl[f["lord"]]["gid"] if f.get("lord") is not None else None,
            "Relations": sorted({(r[0], fl[r[1]]["gid"]) for r in f["relations"] if fl[r[1]]["gid"] != f["gid"]}, key=lambda t: (t[1], t[0])),
            "HeadBasis": (f.get("cite_head") or ["記録から機械で選んだ当主の候補"])[0],
            "_head_name": f.get("head"), "_home_name": f.get("home"),
        })
    hid = {h["Id"]: h for h in houses}

    # 城
    castles = {}
    gone = set(year["absent"])
    hidden = set(year["hidden"])
    for i, c in enumerate(mp["castles"]):
        if i in gone:
            continue
        xy = coords.get(c["id"])
        if not xy:
            continue
        castles[c["id"]] = {"Id": c["id"], "Name": c["name"], "Province": (c.get("gun") or "").split("::")[0] or None,
                            "District": (c.get("gun") or "").split("::")[1] if "::" in (c.get("gun") or "") else None,
                            "LatitudeE7": round(xy["lat"] * 1e7), "LongitudeE7": round(xy["lon"] * 1e7),
                            "PositionBasis": POSITION_BASIS.get(c.get("how"), c.get("how") or "不明"),
                            "Owner": None, "Holder": None, "OwnerBasis": None, "_i": i}
    by_index = {c["_i"]: c for c in castles.values()}
    for o in year["owners"]:
        c = by_index.get(o[0])
        if c is None or o[0] in hidden:
            continue
        c["Owner"] = fl[o[1]]["gid"]
        c["Holder"] = o[2] or None
        c["OwnerBasis"] = o[3]
    by_name = {}
    for c in castles.values():
        by_name.setdefault(c["Name"], []).append(c)

    def castle_id(name, house, province):
        cands = by_name.get(name or "", [])
        if len(cands) == 1:
            return cands[0]["Id"]
        own = [c for c in cands if c["Owner"] == house]
        if len(own) == 1:
            return own[0]["Id"]
        near = [c for c in cands if province and (c["Province"] or "").rstrip("国") == province]
        if len(near) == 1:
            return near[0]["Id"]
        return None

    # 人物
    ids = sorted(p for p, v in status.items() if v[0] not in ("所属不明", "浪人") and v[1] in hid)
    person_row = {r[0]: r for r in db.execute(
        "select 人物ID, 表示名, 代表の読み, 苗字, 諱, 通称, 官途名, 性別, 実在の確度, 生年, 没年, 役割, 登録状態 from 人物一覧")}
    fathers = {}
    for s, ref in db.execute("select subject, value_ref from assertions where property='father' and state='value' and value_ref is not null"):
        fathers.setdefault(s, set()).add(ref)
    people = []
    missing = Counter()
    for p in ids:
        v = status[p]
        r = person_row.get(p)
        if r is None:
            missing["人物一覧にない"] += 1
            continue
        name, born, died, province = pys["persons"].get(p, [None] * 4)

        def year_of(x):
            try:
                return int(x)
            except (TypeError, ValueError):
                return None
        father = sorted(fathers.get(p, ()))
        house = v[1]
        cname = v[2] if len(v) > 2 else None
        people.append({
            "Id": p, "Name": r[1], "Reading": r[2], "Surname": r[3], "Imina": r[4], "Tsusho": r[5], "Kanto": r[6],
            "Sex": r[7], "Birth": year_of(r[9]), "Death": year_of(r[10]), "Father": father[0] if len(father) == 1 else None,
            "Existence": r[8], "Registration": r[12], "Role": r[11], "Province": province,
            "House": house, "HouseBasis": v[0],
            "Castle": castle_id(cname, house, province) if cname else None, "CastleName": cname,
            "CastleBasis": CASTLE_BASIS.get(v[3]) if len(v) > 3 and v[3] is not None else None,
            "Superior": v[4] if len(v) > 4 else None, "SuperiorBasis": v[5] if len(v) > 5 else None,
        })
    pid = {x["Id"] for x in people}
    for x in people:
        if x["Superior"] not in pid:
            x["Superior"], x["SuperiorBasis"] = None, None
        if x["Father"] not in pid:
            x["Father"] = None  # 1560年の名簿の外の父は v1 では指さない（戦国DBには残る）
        if x["Castle"] is None and x["CastleName"]:
            missing["城の名前を一つに決められない"] += 1
    for p, v in status.items():
        if v[0].startswith("当主") and v[1] in hid and p in pid:
            hid[v[1]]["Head"] = p
    for h in houses:
        if h["_home_name"]:
            h["Home"] = castle_id(h["_home_name"], h["Id"], None)
        del h["_head_name"], h["_home_name"]
        h["Relations"] = [{"House": g, "Kind": k} for k, g in h["Relations"]]
    # 百代鏡の採用：当主は家の本拠に置く（戦国DBの見積もりは本人の城の記録を先に使うので、今川義元が清洲城などになる）。
    P = {x["Id"]: x for x in people}
    moved = []
    for h in houses:
        x = P.get(h["Head"])
        if x and h["Home"] and x["Castle"] != h["Home"]:
            moved.append((x["Name"], x["CastleName"], castles[h["Home"]]["Name"]))
            x["Castle"], x["CastleName"], x["CastleBasis"] = h["Home"], castles[h["Home"]]["Name"], "家の本拠（当主・百代鏡の採用）"
    print("当主を本拠に置き直した", len(moved), moved[:8])
    used_castles = {x["Castle"] for x in people} | {h["Home"] for h in houses} | {c["Id"] for c in castles.values() if c["Owner"]}
    out_castles = []
    for c in sorted(castles.values(), key=lambda c: c["Id"]):
        if c["Id"] in used_castles:
            del c["_i"]
            out_castles.append(c)
    doc = {
        "Schema": 1, "Id": "hakutai-db-1560-v1", "WorldYear": YEAR,
        "Source": {"Repository": "SengokuDB", "Commit": commit, "Tags": tags, "SqliteBuiltAt": built.get("built_at"), "CatalogVersion": built.get("catalog_version")},
        "Policy": "戦国DBの版から一方向に書き出した1560年の開始用の下書き。所属・置き場所の城・直属の上司・城の持ち主は機械の見積もりで、史実の判定ではない（決め方を値ごとに残す）。城の位置は公知の地理、所属と城の持ち主はその年の情勢（人が報告で知るもの）として扱う。能力・性格・見た目は百代鏡側で別に持つ。",
        "Houses": sorted(houses, key=lambda h: h["Id"]),
        "People": people,
        "Castles": out_castles,
    }
    lines = ["{"]
    keys = list(doc)
    for k in keys:
        v = doc[k]
        if isinstance(v, list):
            lines.append(f'"{k}":[')
            lines.append(",\n".join(json.dumps(x, ensure_ascii=False, separators=(",", ":")) for x in v))
            lines.append("]" + ("," if k != keys[-1] else ""))
        else:
            lines.append(f'"{k}":' + json.dumps(v, ensure_ascii=False, separators=(",", ":")) + ("," if k != keys[-1] else ""))
    lines.append("}")
    OUT.write_text("\n".join(lines) + "\n", encoding="utf-8", newline="\n")
    print(f"{OUT}: 家 {len(houses)}・人物 {len(people)}・城 {len(out_castles)}（戦国DB {commit[:8]}）")
    print("当主のいる家", sum(1 for h in houses if h["Head"]), "本拠の決まった家", sum(1 for h in houses if h["Home"]))
    print("城の決まった人", sum(1 for x in people if x["Castle"]), "上司のいる人", sum(1 for x in people if x["Superior"]), dict(missing))


if __name__ == "__main__":
    main()
