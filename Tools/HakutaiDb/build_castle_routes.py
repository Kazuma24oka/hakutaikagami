"""百代鏡：百代鏡DBの城を拠点にした全国の移動網（推定の連絡路）を作る（2026-11-12、設計 Docs/Proposals/NATIONAL_PLACEMENT_DESIGN_V1.md の第4節1）。

  python Tools/HakutaiDb/build_castle_routes.py   → Core/Data/castle-routes-1560-v1.json

材料：Core/Data/hakutai-db-1560-v2.json（城の位置）、Core/Data/national-routes-v1.json（今の全国の拠点と道）、
      Core/Data/japan-terrain-mask-v1.json（陸地の形）。
決まり（試作。川・道・山の工程で置き換える）：
  - 城ごとに、近い拠点（今の全国の拠点・ほかの城）のうち、まっすぐ結んだ線が陸地の上を通るもの（1km ごとに確かめる）を、近い順に2つまで（40km 以内）結ぶ。
  - 今の全国の移動網につながらない城のまとまりは、陸地の上を通る一番近い結び方で足していく（80km まで）。
  - それでもつながらない島の城は、一番近い今の拠点か城へ海路で結ぶ。
  - 同じ位置の城（史料で座標が同じもの）は、後の城を北へ約33mずつずらす（位置から拠点を1つに決めるため）。
  - 長さは今の移動網と同じ：陸路は大円距離×1.25、海路は×1.10。速さも同じ（陸路 徒歩1000・使者2000、海路 6000・8000）。
今の全国の拠点はそのまま使う。全国の拠点どうしをまっすぐ結んだ陸の推定の連絡路は使わず（湖や海を横切るため）、全国の拠点も近い城と結ぶ。尾張の細かい道と海路は残す。湖は道を通さない。
"""
from __future__ import annotations

import hashlib
import json
import math
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DATA = ROOT / "Core" / "Data"
OUT = DATA / "castle-routes-1560-v1.json"
STEP_KM = 1.0
NEAR_KM = 40.0
BRIDGE_KM = 80.0
SHIFT_E7 = 300


def km(a, b):
    la1, lo1, la2, lo2 = map(math.radians, (a[0], a[1], b[0], b[1]))
    h = math.sin((la2 - la1) / 2) ** 2 + math.cos(la1) * math.cos(la2) * math.sin((lo2 - lo1) / 2) ** 2
    return 2 * 6371.0 * math.asin(math.sqrt(h))


def tdiv(x, n):  # C# の整数の割り算（0に向けて切り捨て）
    q = abs(x) // n
    return q if x >= 0 else -q


class Land:
    """Core/World/LandMaskIndex.cs と同じ判定（E7 の整数・外周の中で穴の外なら陸）。"""

    def __init__(self, polys):
        self.polys = []
        for p in polys:
            rings = [[(q["LatitudeE7"], q["LongitudeE7"]) for q in r] for r in [p["Outer"]] + p["Holes"]]
            self.polys.append([(r, min(x[0] for x in r), max(x[0] for x in r)) for r in rings])

    @staticmethod
    def ring(r, lat, lon):
        pts, lo, hi = r
        if lat < lo or lat > hi:
            return False
        inside = False
        n = len(pts)
        j = n - 1
        for i in range(n):
            a, b = pts[i], pts[j]
            j = i
            if a[0] == b[0]:
                continue
            if (a[0] > lat) != (b[0] > lat) and lon < (b[1] - a[1]) * (lat - a[0]) / (b[0] - a[0]) + a[1]:
                inside = not inside
        return inside

    def add_lakes(self, rings):  # 湖・広い川面（E7 の [緯度, 経度] の輪）を水にする
        self.lakes = getattr(self, "lakes", [])
        for r in rings:
            pts = [tuple(q) for q in r]
            self.lakes.append((pts, min(x[0] for x in pts), max(x[0] for x in pts), min(x[1] for x in pts), max(x[1] for x in pts)))

    def contains(self, lat, lon):
        for pts, a, b, c, d in getattr(self, "lakes", ()):
            if a <= lat <= b and c <= lon <= d and self.ring((pts, a, b), lat, lon):
                return False
        for rings in self.polys:
            if self.ring(rings[0], lat, lon) and not any(self.ring(h, lat, lon) for h in rings[1:]):
                return True
        return False

    def segment(self, a, b):
        a = (round(a[0] * 1e7), round(a[1] * 1e7))
        b = (round(b[0] * 1e7), round(b[1] * 1e7))
        n = max(2, int(km((a[0] / 1e7, a[1] / 1e7), (b[0] / 1e7, b[1] / 1e7)) / STEP_KM))
        checks = [(a[0] + tdiv((b[0] - a[0]) * i, n), a[1] + tdiv((b[1] - a[1]) * i, n)) for i in range(1, n)]
        checks += [(a[0] + tdiv((b[0] - a[0]) * i, 10), a[1] + tdiv((b[1] - a[1]) * i, 10)) for i in range(1, 10)]  # 検査と同じ点
        return all(self.contains(*q) for q in checks)


def main():
    db = json.loads((DATA / "hakutai-db-1560-v2.json").read_text(encoding="utf-8"))
    nat = json.loads((DATA / "national-routes-v1.json").read_text(encoding="utf-8"))
    land = Land(json.loads((DATA / "japan-terrain-mask-v1.json").read_text(encoding="utf-8"))["Polygons"])
    # 湖は道を通さない（国の形の穴の湖と、地球地図日本の湖・広い川面。2026-11-12 ユーザーの指摘：琵琶湖・宍道湖を横切る道）
    geo = ROOT / "Game" / "Data" / "Geography"
    land.add_lakes(json.loads((geo / "borders-v2.json").read_text(encoding="utf-8"))["Lakes"])
    land.add_lakes(json.loads((geo / "terrain-v1.json").read_text(encoding="utf-8"))["Lakes"])
    pts = {}
    outside = {n["Id"] for n in nat["Nodes"] if n["LatitudeE7"] < 289000000 and n["LongitudeE7"] < 1310000000}  # 琉球の拠点は城の移動網に入れない
    for n in nat["Nodes"]:
        if n["Id"] not in outside:
            pts[n["Id"]] = (n["LatitudeE7"] / 1e7, n["LongitudeE7"] / 1e7)
    national = set(pts)
    castles = []
    used = {(n["LatitudeE7"], n["LongitudeE7"]) for n in nat["Nodes"]}
    for c in db["Castles"]:
        if c.get("Region") == "琉球":
            continue
        nid = "c-" + c["Id"].replace("-", "").lower()
        lat = c["LatitudeE7"]
        # 同じ位置の拠点があれば、北へ約33m（緯度 E7 で 300）ずつずらす（位置から拠点を1つに決めるため。2026-11-12）
        while (lat, c["LongitudeE7"]) in used:
            lat += SHIFT_E7
        used.add((lat, c["LongitudeE7"]))
        c = dict(c, LatitudeE7=lat)
        pts[nid] = (lat / 1e7, c["LongitudeE7"] / 1e7)
        castles.append((nid, c))
    edges = {}

    def add(a, b, mode, why):
        key = tuple(sorted((a, b)))
        if key in edges:
            return
        d = km(pts[a], pts[b])
        edges[key] = {"Id": "castle-" + key[0] + "-" + key[1], "From": key[0], "To": key[1],
                      "LengthMm": str(max(1000, round(d * 1e6 * (1.25 if mode == "land" else 1.10)))),  # 同じ位置の城どうしは1m
                      "FootSpeed": 1000 if mode == "land" else 6000, "CourierSpeed": 2000 if mode == "land" else 8000,
                      "Mode": mode, "Basis": why}
    ids = list(pts)
    cache = {}

    def on_land(a, b):
        k = tuple(sorted((a, b)))
        if k not in cache:
            cache[k] = land.segment(pts[a], pts[b])
        return cache[k]
    # 1. 近い拠点・城へ2つまで
    for nid, _ in castles:
        near = sorted((km(pts[nid], pts[o]), o) for o in ids if o != nid)
        got = 0
        for d, o in near:
            if d > NEAR_KM or got >= 2:
                break
            if on_land(nid, o):
                add(nid, o, "land", "推定の連絡路：近い拠点・城を陸地の上で結ぶ（試作）")
                got += 1
    # 2. 全国の移動網につながるまで、まとまりを足す
    nat_adj = {}
    catalog_nodes = {n["Id"] for n in nat["Nodes"]}
    for e in nat["Edges"]:
        if e["From"] in outside or e["To"] in outside:
            continue
        if e["Mode"] == "land" and e["From"] in catalog_nodes and e["To"] in catalog_nodes:
            continue  # 全国の拠点どうしをまっすぐ結んだ推定の連絡路は使わない（城の道でつなぐ）
        nat_adj.setdefault(e["From"], set()).add(e["To"])
        nat_adj.setdefault(e["To"], set()).add(e["From"])

    def components():
        adj = {i: set(v) for i, v in nat_adj.items()}
        for i in ids:
            adj.setdefault(i, set())
        for a, b in edges:
            adj[a].add(b)
            adj[b].add(a)
        seen, comps = set(), []
        for i in list(adj):
            if i in seen:
                continue
            stack, comp = [i], set()
            while stack:
                x = stack.pop()
                if x in seen:
                    continue
                seen.add(x)
                comp.add(x)
                stack += adj[x] - seen
            comps.append(comp)
        return comps
    for limit, mode in ((BRIDGE_KM, "land"), (10000.0, "sea")):
        while True:
            comps = components()
            main_c = next(c for c in comps if "castle" in c or national & c)
            rest = [c for c in comps if c is not main_c]
            if not rest:
                break
            added = False
            for comp in sorted(rest, key=lambda c: min(c)):
                best = None
                for a in comp:
                    if a not in pts:
                        continue  # 尾張の細かい道の拠点（位置は別の表）
                    for b in ids:
                        if b in comp:
                            continue
                        d = km(pts[a], pts[b])
                        if d <= limit and (best is None or d < best[0]) and (mode == "sea" or on_land(a, b)):
                            best = (d, a, b)
                if best:
                    add(best[1], best[2], mode, "推定の連絡路：離れた城のまとまりを、一番近い拠点・城へ結ぶ（試作）" if mode == "land" else "推定の海路：陸でつながらない島などの城を、一番近い拠点・城へ結ぶ（試作）")
                    added = True
                    break
            if not added:
                break
    comps = components()
    assert len(comps) == 1, f"つながらないまとまり {len(comps)}"
    doc = {
        "Schema": 1, "Id": "castle-routes-1560-v1", "WorldYear": 1560, "BasedOn": "national-inferred-routes-v1",
        "Database": db["Id"], "DatabaseSource": db["Source"]["Commit"],
        "Policy": "百代鏡DBの城を拠点として足し、近い拠点・城と結ぶ推定の連絡路（試作）。今の全国の拠点と道はそのまま使う。川・道・山の工程で置き換える。史実の道筋の主張ではない。",
        "Nodes": [{"Id": nid, "Name": c["Name"], "LatitudeE7": c["LatitudeE7"], "LongitudeE7": c["LongitudeE7"], "Province": c["Province"], "Castle": c["Id"]} for nid, c in castles],
        "Edges": sorted(edges.values(), key=lambda e: e["Id"]),
    }
    lines = ["{"]
    keys = list(doc)
    for k in keys:
        v = doc[k]
        end = "," if k != keys[-1] else ""
        if isinstance(v, list):
            lines.append(f'"{k}":[')
            lines.append(",\n".join(json.dumps(x, ensure_ascii=False, separators=(",", ":")) for x in v))
            lines.append("]" + end)
        else:
            lines.append(f'"{k}":' + json.dumps(v, ensure_ascii=False, separators=(",", ":")) + end)
    lines.append("}")
    OUT.write_text("\n".join(lines) + "\n", encoding="utf-8", newline="\n")
    modes = {}
    for e in edges.values():
        modes[e["Mode"]] = modes.get(e["Mode"], 0) + 1
    print(f"{OUT.name}: 城の拠点 {len(castles)}・道 {len(edges)} {modes}")


if __name__ == "__main__":
    main()
