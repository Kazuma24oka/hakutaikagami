"""百代鏡：地図に描く国と郡の境界の線と名前を作る（2026-11-12 ユーザーの指示）。

  python Tools/HakutaiDb/build_borders.py [郡地図の zip]
    既定：D:/01_資料/論文/戦国時代/戦国DB/MAPDATA/郡地図_Ver_1_1_11.zip
    → Game/Data/Geography/borders-v1.json

材料：郡地図 Ver 1.1（郡地図研究会、クリエイティブ・コモンズ 表示 4.0 国際）の 国地図Ver1_1_11・郡地図Ver1_1_11（シェープファイル、日本測地系の緯度経度）。
「郡地図 Ver 1.1」を加工して作成：
  - 国は明治の区分のまま（陸奥・陸中・陸前・磐城・岩代、羽前・羽後など）。北海道（渡島国〜千島国）は国「蝦夷」にまとめる。
  - 郡は明治の郡。明治の市・区（仙台市・函館区など）は郡にしない（線も名前も出さない）。渡島国の郡はまとめて郡「渡島」、ほかの北海道の郡は出さない。
  - 線は隣の国・郡と接する境目だけ（海岸は描かない。地図の海岸線は別）。約200m の誤差で間引く。
  - 日本測地系から世界測地系へ、近似の式で直す。
  - 名前の位置は、その国・郡の一番大きい形の重心。琉球・小笠原など無人や操作しない所の国名は出さない。
"""
from __future__ import annotations

import io
import json
import struct
import sys
import zipfile
from collections import defaultdict
from pathlib import Path

SRC = Path(sys.argv[1]) if len(sys.argv) > 1 else Path("D:/01_資料/論文/戦国時代/戦国DB/MAPDATA/郡地図_Ver_1_1_11.zip")
OUT = Path(__file__).resolve().parents[2] / "Game" / "Data" / "Geography" / "borders-v1.json"
Q = 100000  # 1e-5 度で頂点をそろえる
TOL = 0.002  # 間引きの許容（度、約200m）


def read_zip(name):
    z = zipfile.ZipFile(SRC)
    base = next(n for n in z.namelist() if n.endswith(name + ".shp"))[:-4]
    return z.read(base + ".shp"), z.read(base + ".dbf")


def dbf_rows(b):
    n = struct.unpack("<I", b[4:8])[0]
    hl, rl = struct.unpack("<HH", b[8:12])
    fields, i = [], 32
    while b[i] != 0x0D:
        fields.append((b[i:i + 11].split(b"\0")[0].decode(), b[i + 16]))
        i += 32
    rows = []
    for r in range(n):
        off, row = hl + r * rl + 1, {}
        for name, ln in fields:
            row[name] = b[off:off + ln].decode("utf-8", "replace").strip()
            off += ln
        rows.append(row)
    return rows


def shp_polygons(b):
    out, i = [], 100
    while i < len(b):
        _, length = struct.unpack(">II", b[i:i + 8])
        rec = io.BytesIO(b[i + 8:i + 8 + length * 2])
        i += 8 + length * 2
        st = struct.unpack("<i", rec.read(4))[0]
        if st == 0:
            out.append([])
            continue
        rec.read(32)
        np_, npt = struct.unpack("<ii", rec.read(8))
        parts = list(struct.unpack(f"<{np_}i", rec.read(4 * np_))) + [npt]
        pts = struct.unpack(f"<{2 * npt}d", rec.read(16 * npt))
        rings = []
        for a, c in zip(parts, parts[1:]):
            rings.append([(round(pts[2 * k + 1] * Q), round(pts[2 * k] * Q)) for k in range(a, c)])  # (緯度, 経度)
        out.append(rings)
    return out


def tokyo_to_wgs(lat, lon):  # 日本測地系→世界測地系の近似（国土地理院の簡易式）
    return (lat - 0.00010695 * lat + 0.000017464 * lon + 0.0046017,
            lon - 0.000046038 * lat - 0.000083043 * lon + 0.010040)


def simplify(pts, tol):
    if len(pts) < 3:
        return pts
    keep = [False] * len(pts)
    keep[0] = keep[-1] = True
    stack = [(0, len(pts) - 1)]
    while stack:
        a, c = stack.pop()
        (y1, x1), (y2, x2) = pts[a], pts[c]
        dy, dx = y2 - y1, x2 - x1
        norm = (dx * dx + dy * dy) ** 0.5 or 1e-12
        best, idx = 0.0, -1
        for k in range(a + 1, c):
            y, x = pts[k]
            d = abs(dy * (x - x1) - dx * (y - y1)) / norm if (dx or dy) else ((x - x1) ** 2 + (y - y1) ** 2) ** 0.5
            if d > best:
                best, idx = d, k
        if best > tol and idx > 0:
            keep[idx] = True
            stack += [(a, idx), (idx, c)]
    return [p for p, k in zip(pts, keep) if k]


def lines(polys, keys):
    """隣と接する辺（2つの違う鍵の形に出てくる辺）を、つながった線にまとめる。"""
    owners = defaultdict(set)
    for rings, key in zip(polys, keys):
        if key is None:
            continue
        for ring in rings:
            for a, b in zip(ring, ring[1:] + ring[:1]):
                if a != b:
                    owners[(a, b) if a < b else (b, a)].add(key)
    shared = {e for e, ks in owners.items() if len(ks) >= 2}
    adj = defaultdict(list)
    for a, b in shared:
        adj[a].append(b)
        adj[b].append(a)
    seen, out = set(), []
    for a, b in shared:
        if (a, b) in seen:
            continue
        chain = [a, b]
        seen.add((a, b))
        for forward in (True, False):
            while True:
                end = chain[-1] if forward else chain[0]
                prev = chain[-2] if forward else chain[1]
                nxt = [n for n in adj[end] if n != prev and ((end, n) if end < n else (n, end)) not in seen]
                if len(adj[end]) != 2 or not nxt:
                    break
                n = nxt[0]
                seen.add((end, n) if end < n else (n, end))
                if forward:
                    chain.append(n)
                else:
                    chain.insert(0, n)
        pts = [tokyo_to_wgs(y / Q, x / Q) for y, x in chain]
        out.append([[round(y * 1e7), round(x * 1e7)] for y, x in simplify(pts, TOL)])
    return out


def centroid(ring):
    a = cy = cx = 0.0
    for (y1, x1), (y2, x2) in zip(ring, ring[1:] + ring[:1]):
        f = x1 * y2 - x2 * y1
        a += f
        cx += (x1 + x2) * f
        cy += (y1 + y2) * f
    if abs(a) < 1e-9:
        return ring[0]
    return cy / (3 * a), cx / (3 * a)


def area(ring):
    return abs(sum(x1 * y2 - x2 * y1 for (y1, x1), (y2, x2) in zip(ring, ring[1:] + ring[:1]))) / 2


def province(k):
    code = None
    return "蝦夷" if k in HOKKAIDO else k[:-1] if k.endswith("国") else k


HOKKAIDO = {"渡島国", "後志国", "石狩国", "天塩国", "北見国", "胆振国", "日高国", "十勝国", "釧路国", "根室国", "千島国"}
NO_LABEL = {"琉球", "沖縄", "小笠原"}


def main():
    pshp, pdbf = read_zip("国地図Ver1_1_11")
    gshp, gdbf = read_zip("郡地図Ver1_1_11")
    prows, prings = dbf_rows(pdbf), shp_polygons(pshp)
    grows, grings = dbf_rows(gdbf), shp_polygons(gshp)
    pkeys = [province(r["KUNI"]) for r in prows]

    def gkey(r):
        k, g = r["KUNI"], r["GUN"]
        if g.endswith(("市", "区")):
            return None
        if k in HOKKAIDO:
            return ("蝦夷", "渡島") if k == "渡島国" else ("蝦夷", "")
        return (province(k), g)
    gkeys = [gkey(r) for r in grows]
    plines = lines(prings, pkeys)
    # 郡の線：同じ国の中の境目だけ（国の境目は国の線で描く）。郡の名前のない北海道の郡どうしは描かない
    inner = [k if k and k[1] != "" else (k and (k[0], "")) for k in gkeys]
    owners_prov = {}
    glines_all = lines(grings, [None if k is None else k for k in inner])
    pset = set()
    for ln in plines:
        for p in ln:
            pset.add(tuple(p))
    glines = [ln for ln in glines_all if not all(tuple(p) in pset for p in ln)]

    def label_of(rings_list):
        best = max((r for rings in rings_list for r in rings), key=area)
        y, x = centroid(best)
        y, x = tokyo_to_wgs(y / Q, x / Q)
        return [round(y * 1e7), round(x * 1e7)]
    pgroups, ggroups = defaultdict(list), defaultdict(list)
    for k, r in zip(pkeys, prings):
        if r:
            pgroups[k].append(r)
    for k, r in zip(gkeys, grings):
        if k and k[1] and r:
            ggroups[k].append(r)
    provinces = [{"Name": k, "Label": label_of(v)} for k, v in sorted(pgroups.items()) if not any(n in k for n in NO_LABEL)]
    districts = [{"Province": k[0], "Name": k[1], "Label": label_of(v)} for k, v in sorted(ggroups.items()) if k[0] not in ("琉球", "沖縄")]
    doc = {"Schema": 1, "Id": "borders-v1",
           "Source": {"Name": "郡地図 Ver 1.1（郡地図研究会）", "License": "CC BY 4.0", "Credit": "「郡地図 Ver 1.1」を加工して作成",
                      "Edits": "明治の国（北海道は蝦夷にまとめ、渡島国は郡「渡島」）、明治の市・区を除く、隣と接する境目だけ、約200mで間引き、日本測地系から世界測地系へ近似で変換"},
           "Provinces": provinces, "Districts": districts, "ProvinceLines": plines, "DistrictLines": glines}
    OUT.write_text(json.dumps(doc, ensure_ascii=False, separators=(",", ":")) + "\n", encoding="utf-8", newline="\n")
    print(f"{OUT.name}: 国 {len(provinces)}・郡 {len(districts)}・国の線 {len(plines)}（点 {sum(map(len, plines))}）・郡の線 {len(glines)}（点 {sum(map(len, glines))}）・{OUT.stat().st_size // 1024}KB")


if __name__ == "__main__":
    main()
