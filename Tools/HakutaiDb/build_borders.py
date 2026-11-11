"""百代鏡：地図に描く国と郡の境界の線と名前を作る（2026-11-12 ユーザーの指示）。

  python Tools/HakutaiDb/build_borders.py [郡地図の zip]
    既定：D:/01_資料/論文/戦国時代/戦国DB/MAPDATA/郡地図_Ver_1_1_11.zip
    → Game/Data/Geography/borders-v1.json

材料：郡地図 Ver 1.1（郡地図研究会、クリエイティブ・コモンズ 表示 4.0 国際）の 国地図Ver1_1_11・郡地図Ver1_1_11（シェープファイル、日本測地系の緯度経度）。国を基準にし、郡は国に合わせる（国の外にはみ出した郡の部分は、はみ出した先の国の郡に取り込む）。
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


def lines(polys, keys, raw=False):
    """隣と接する辺（2つの違う鍵の形に出てくる辺）を、つながった線にまとめる。raw なら（両側の鍵, 点の列）を返す。"""
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
        if raw:
            out.append((owners[(a, b)], chain))
            continue
        pts = [tokyo_to_wgs(y / Q, x / Q) for y, x in chain]
        out.append([[round(y * 1e7), round(x * 1e7)] for y, x in simplify(pts, TOL)])
    return out


class Region:
    """国の形の内外の判定（緯度の帯ごとに辺を分けた ray 判定。日本測地系の Q 単位）。"""
    STRIP = 5000

    def __init__(self, rings):
        self.strips = defaultdict(list)
        for ring in rings:
            for a, b in zip(ring, ring[1:] + ring[:1]):
                if a[0] == b[0]:
                    continue
                for k in range(min(a[0], b[0]) // self.STRIP, max(a[0], b[0]) // self.STRIP + 1):
                    self.strips[k].append((a, b))

    def contains(self, y, x):
        c = False
        for (y1, x1), (y2, x2) in self.strips.get(int(y) // self.STRIP, ()):
            if (y1 > y) != (y2 > y) and x < (x2 - x1) * (y - y1) / (y2 - y1) + x1:
                c = not c
        return c


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


class PolyIndex:
    """たくさんの形（鍵つき）の内外の判定。形ごとに帯の索引を持ち、0.25度の格子で候補を絞る。"""
    CELL = 25000

    def __init__(self, items):  # items: [(鍵, [輪...])]
        self.items, self.grid = [], defaultdict(list)
        for key, rings in items:
            rings = [r for r in rings if len(r) > 3]
            if not rings:
                continue
            ys = [p[0] for r in rings for p in r]
            xs = [p[1] for r in rings for p in r]
            i = len(self.items)
            self.items.append((key, Region(rings), (min(ys), max(ys), min(xs), max(xs))))
            for gy in range(min(ys) // self.CELL, max(ys) // self.CELL + 1):
                for gx in range(min(xs) // self.CELL, max(xs) // self.CELL + 1):
                    self.grid[(gy, gx)].append(i)

    def keys_at(self, y, x):
        out = set()
        for i in self.grid.get((int(y) // self.CELL, int(x) // self.CELL), ()):
            key, reg, (a, b, c, d) = self.items[i]
            if a <= y <= b and c <= x <= d and reg.contains(y, x):
                out.add(key)
        return out


def border_runs(items, index, accept, step=60, off=150):
    """形の輪の辺ごとに、すぐ両側（off）の鍵を調べ、accept(自分, 相手, 中点) が真の辺をつないだ線にする。
    点がぴったり一致しなくても境目になる（国の地図と郡の地図の食い違い・同じファイル内の小さなずれ）。"""
    out = []
    for key, rings in items:
        if key is None:
            continue
        for ring in rings:
            if len(ring) < 4:
                continue
            pts = simplify([(float(y), float(x)) for y, x in ring] + [(float(ring[0][0]), float(ring[0][1]))], step)
            run = []
            for (y1, x1), (y2, x2) in zip(pts, pts[1:]):
                dy, dx = y2 - y1, x2 - x1
                n = (dy * dy + dx * dx) ** 0.5
                ok = False
                if n > 0:
                    my, mx = (y1 + y2) / 2, (x1 + x2) / 2
                    ny, nx = -dx / n * off, dy / n * off
                    sides = index.keys_at(my + ny, mx + nx) | index.keys_at(my - ny, mx - nx)
                    others = [k for k in sides if k != key and k is not None]
                    ok = key in sides and any(accept(key, k, (my, mx)) for k in others)
                if ok:
                    if not run:
                        run = [(y1, x1)]
                    run.append((y2, x2))
                else:
                    if len(run) >= 2:
                        out.append(run)
                    run = []
            if len(run) >= 2:
                out.append(run)
    return [[tokyo_to_wgs(y / Q, x / Q) for y, x in ln] for ln in out]  # 世界測地系の度（後で finish で整える）


class Land:
    """ゲームの地図の陸地（japan-terrain-mask-v1 と北方領土の島）。E7 の整数で、緯度の帯の索引つき ray 判定。"""
    STRIP = 500000

    def __init__(self):
        root = Path(__file__).resolve().parents[2]
        polys = json.loads((root / "Core" / "Data" / "japan-terrain-mask-v1.json").read_text(encoding="utf-8"))["Polygons"]
        polys += json.loads((root / "Game" / "Data" / "Geography" / "northern-territories-v1.json").read_text(encoding="utf-8"))["Polygons"]
        self.strips = defaultdict(list)
        for p in polys:
            for ring in [p["Outer"]] + p["Holes"]:
                pts = [(q["LatitudeE7"], q["LongitudeE7"]) for q in ring]
                for a, b in zip(pts, pts[1:] + pts[:1]):
                    if a[0] == b[0]:
                        continue
                    for k in range(min(a[0], b[0]) // self.STRIP, max(a[0], b[0]) // self.STRIP + 1):
                        self.strips[k].append((a, b))

    def add_water(self, rings):  # 描いた湖（E7 の輪）を水にする（偶奇で陸から抜ける）
        for ring in rings:
            pts = [tuple(q) for q in ring]
            for a, b in zip(pts, pts[1:] + pts[:1]):
                if a[0] == b[0]:
                    continue
                for k in range(min(a[0], b[0]) // self.STRIP, max(a[0], b[0]) // self.STRIP + 1):
                    self.strips[k].append((a, b))

    def contains(self, lat, lon):
        y, x = lat * 1e7, lon * 1e7
        c = False
        for (y1, x1), (y2, x2) in self.strips.get(int(y) // self.STRIP, ()):
            if (y1 > y) != (y2 > y) and x < (x2 - x1) * (y - y1) / (y2 - y1) + x1:
                c = not c
        return c


def meters(a, b):
    k = 111320.0
    return ((a[0] - b[0]) * k) ** 2 + ((a[1] - b[1]) * k * 0.81) ** 2


DANGLING = []


def finish(lines, land, targets=(), snap_m=900.0, extend_m=4000.0):
    """境界線を地図の陸地に合わせて整える（2026-11-12 ユーザーの指示：線がない・あいまいな所をなくす）。
    1. 海の上の部分を消し、海岸で切る。2. 海岸の手前で止まる端は海岸まで伸ばす。
    3. 途中で止まる端は、近くの線（targets も含む）の点につなぐ。4. 約200m で間引く。
    戻り値：(E7 の線, 海岸にもほかの線にも届かない端の数)。"""
    def coast(a, b):  # a は陸、b は海。海岸の点
        for _ in range(18):
            m = ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2)
            if land.contains(*m):
                a = m
            else:
                b = m
        return a
    # 1. 海で切る（点の間も 100m ごとに確かめる）
    clipped = []
    for ln in lines:
        dense = [ln[0]]
        for a, b in zip(ln, ln[1:]):
            n = max(1, int(meters(a, b) ** 0.5 / 100))
            dense += [(a[0] + (b[0] - a[0]) * i / n, a[1] + (b[1] - a[1]) * i / n) for i in range(1, n + 1)]
        run, prev, prev_in = [], None, False
        for p in dense:
            here = land.contains(*p)
            if here and prev is not None and not prev_in:
                run = [coast(p, prev)]
            if here:
                run.append(p)
            elif prev_in:
                run.append(coast(prev, p))
                if len(run) >= 2:
                    clipped.append(run)
                run = []
            prev, prev_in = p, here
        if len(run) >= 2:
            clipped.append(run)

    def at_coast(p):
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1), (0.7, 0.7), (-0.7, 0.7), (0.7, -0.7), (-0.7, -0.7)):
            if not land.contains(p[0] + dy * 0.0015, p[1] + dx * 0.0019):
                return True
        return False
    # 3 の準備：すべての線の点の格子（0.01度）
    grid = defaultdict(list)
    allines = clipped + [list(t) for t in targets]
    for li, ln in enumerate(allines):
        for pi, p in enumerate(ln):
            grid[(int(p[0] * 100), int(p[1] * 100))].append((li, pi))

    def nearest(p, li_self, end_index):
        best, bd = None, snap_m ** 2
        gy, gx = int(p[0] * 100), int(p[1] * 100)
        for yy in (gy - 1, gy, gy + 1):
            for xx in (gx - 1, gx, gx + 1):
                for li, pi in grid.get((yy, xx), ()):
                    if li == li_self and abs(pi - end_index) < 8:
                        continue
                    q = allines[li][pi]
                    d = meters(p, q)
                    if d < bd:
                        best, bd = q, d
        return best
    dangling = 0
    out, loose = [], []
    for li, ln in enumerate(clipped):
        ln = list(ln)
        for end in (0, -1):
            p = ln[end]
            if at_coast(p):
                continue
            q = nearest(p, li, 0 if end == 0 else len(ln) - 1)
            if q is not None:
                if meters(p, q) > 1:
                    if end == 0:
                        ln.insert(0, q)
                    else:
                        ln.append(q)
                continue
            # 2. 海岸まで伸ばす（最後の向きのまま）
            a, b = (ln[1], ln[0]) if end == 0 else (ln[-2], ln[-1])
            d = meters(a, b) ** 0.5 or 1
            dy, dx = (b[0] - a[0]) / d * 50, (b[1] - a[1]) / d * 50
            cur, ext = b, None
            for _ in range(int(extend_m / 50)):
                nxt = (cur[0] + dy, cur[1] + dx)
                if not land.contains(*nxt):
                    ext = coast(cur, nxt)
                    break
                cur = nxt
            if ext is not None:
                if end == 0:
                    ln.insert(0, ext)
                else:
                    ln.append(ext)
            else:
                # 向きを変えて一番近い海岸・水辺（1.5km まで）へつなぐ
                best = None
                for r in range(1, 31):
                    for k in range(0, 360, 10):
                        import math
                        q = (b[0] + r * 50 / 111320 * math.sin(math.radians(k)), b[1] + r * 50 / 90000 * math.cos(math.radians(k)))
                        if not land.contains(*q):
                            best = coast(b, q)
                            break
                    if best:
                        break
                if best:
                    if end == 0:
                        ln.insert(0, best)
                    else:
                        ln.append(best)
                else:
                    loose.append((len(out), end))
        out.append(ln)
    # 4. 湖や川で途切れた線は、対岸の続きの線の端とまっすぐつなぐ（近い組から、15km まで）
    pairs = sorted(((meters(out[a][ea], out[b][eb]), (a, ea), (b, eb)) for i, (a, ea) in enumerate(loose) for (b, eb) in loose[i + 1:]), key=lambda t: t[0])
    used = set()
    for d, (a, ea), (b, eb) in pairs:
        if d > 15000 ** 2 or (a, ea) in used or (b, eb) in used:
            continue
        used |= {(a, ea), (b, eb)}
        q = out[b][eb]
        if ea == 0:
            out[a].insert(0, q)
        else:
            out[a].append(q)
    drop = set()
    for a, ea in loose:
        if (a, ea) not in used:
            if sum(meters(p, q) ** 0.5 for p, q in zip(out[a], out[a][1:])) < 2000:
                drop.add(a)  # 形のわずかなずれから出た短い切れ端は消す
                continue
            dangling += 1
            DANGLING.append((round(out[a][ea][0], 4), round(out[a][ea][1], 4)))
    out = [ln for i, ln in enumerate(out) if i not in drop]
    tol = TOL  # 度
    return [[[round(a * 1e7), round(b * 1e7)] for a, b in simplify(ln, tol)] for ln in out if len(ln) >= 2], dangling


def main():
    # 国は「国地図」を基準にする。郡は国に合わせる：郡の線は、その郡の国の中にある部分だけを描き、
    # 隣の国にはみ出した部分は、はみ出した先の国の郡に取り込む（線を描かない）。郡名も自分の国の中に置く（2026-11-12 ユーザーの指示）
    # 境目は、辺のすぐ両側の国・郡が違う所（点の一致に頼らない。2026-11-12 途切れの修正）。国の形の穴は湖として描く。
    pshp, pdbf = read_zip("国地図Ver1_1_11")
    gshp, gdbf = read_zip("郡地図Ver1_1_11")
    prows, prings = dbf_rows(pdbf), shp_polygons(pshp)
    grows, grings = dbf_rows(gdbf), shp_polygons(gshp)
    pkeys = [province(r["KUNI"]) for r in prows]
    regions_raw = defaultdict(list)
    for k, rings in zip(pkeys, prings):
        regions_raw[k] += [r for r in rings if len(r) > 3]
    regions = {k: Region(v) for k, v in regions_raw.items()}

    def gkey(r):
        k, g = r["KUNI"], r["GUN"]
        if g.endswith(("市", "区")):
            return None
        if k in HOKKAIDO:
            return ("蝦夷", "渡島") if k == "渡島国" else ("蝦夷", "")
        return (province(k), g)
    gkeys = [gkey(r) for r in grows]
    # 明治の市・区は、接する長さが一番長い同じ国の郡に含める（市の所で郡境が止まらないように）
    nindex = PolyIndex([(k, rr) for k, rr in zip(gkeys, grings) if k is not None])
    for i, r in enumerate(grows):
        if gkeys[i] is not None:
            continue
        votes = defaultdict(float)
        for ring in grings[i]:
            pts = simplify([(float(y), float(x)) for y, x in ring], 60) if len(ring) > 3 else []
            for (y1, x1), (y2, x2) in zip(pts, pts[1:]):
                dy, dx = y2 - y1, x2 - x1
                n = (dy * dy + dx * dx) ** 0.5
                if n == 0:
                    continue
                my, mx = (y1 + y2) / 2, (x1 + x2) / 2
                for sgn in (1, -1):
                    for k in nindex.keys_at(my - sgn * dx / n * 150, mx + sgn * dy / n * 150):
                        if k[0] == province(r["KUNI"]):
                            votes[k] += n
        if votes:
            gkeys[i] = max(votes, key=votes.get)
    pitems = list(zip(pkeys, prings))
    pindex = PolyIndex(pitems)
    land = Land()
    # 湖：国の形の穴（琵琶湖・猪苗代湖・宍道湖・印旛沼など）。小さすぎる穴は描かない
    def signed(r):
        return sum(x1 * y2 - x2 * y1 for (y1, x1), (y2, x2) in zip(r, r[1:] + r[:1])) / 2
    lakes = []
    for rings in prings:
        for h in rings:
            if len(h) > 3 and signed(h) > 2e6:
                pts = simplify([(float(y), float(x)) for y, x in h], 50)
                if len(pts) >= 4:
                    lakes.append([[round(a * 1e7), round(b * 1e7)] for a, b in (tokyo_to_wgs(y / Q, x / Q) for y, x in pts[:-1] if True)])

    land.add_water(lakes)
    ryukyu = lambda ln: all(p[0] < 29.0 and p[1] < 131.0 for p in ln)  # 琉球の線は描かない
    praw = [ln for ln in border_runs(pitems, pindex, lambda a, b, m: a < b) if not ryukyu(ln)]
    plines, pdang = finish(praw, land)
    gitems = list(zip(gkeys, grings))
    gindex = PolyIndex(gitems)
    graw = border_runs(gitems, gindex, lambda a, b, m: a[0] == b[0] and (a[1] or b[1]) and a < b and a[0] in regions and regions[a[0]].contains(*m))
    graw = [ln for ln in graw if not ryukyu(ln)]
    glines, gdang = finish(graw, land, targets=praw)
    print(f"海岸にもほかの線にも届かない端：国 {pdang}・郡 {gdang}", DANGLING)
    cut = 0

    def label_of(rings_list, whole=False, inside=None):
        rings = [r for rr in rings_list for r in rr if len(r) > 3]
        if inside is not None:  # 郡の名前：自分の国の中の点（大きい形の重心から、なければ形の中の点）
            for r in sorted(rings, key=area, reverse=True):
                cy, cx = centroid(r)
                cands = [(cy, cx)] + [(p[0] * 0.6 + cy * 0.4, p[1] * 0.6 + cx * 0.4) for p in r[::max(1, len(r) // 24)]]
                hit = next((c for c in cands if inside.contains(*c)), None)
                if hit:
                    y, x = tokyo_to_wgs(hit[0] / Q, hit[1] / Q)
                    return [round(y * 1e7), round(x * 1e7)]
            return None
        if whole:  # 国の名前：形の重心を面積で重み付けした点
            w = sum(area(r) for r in rings) or 1
            y = sum(centroid(r)[0] * area(r) for r in rings) / w
            x = sum(centroid(r)[1] * area(r) for r in rings) / w
        else:
            y, x = centroid(max(rings, key=area))
        y, x = tokyo_to_wgs(y / Q, x / Q)
        return [round(y * 1e7), round(x * 1e7)]
    pgroups, ggroups = defaultdict(list), defaultdict(list)
    for k, r in zip(pkeys, prings):
        if r:
            pgroups[k].append(r)
    for k, r in zip(gkeys, grings):
        if k and k[1] and r:
            ggroups[k].append(r)
    provinces = [{"Name": k, "Label": label_of(v, True)} for k, v in sorted(pgroups.items()) if not any(n in k for n in NO_LABEL)]
    districts = [{"Province": k[0], "Name": k[1], "Label": lab} for k, v in sorted(ggroups.items()) if k[0] not in ("琉球", "沖縄") and k[0] in regions
                 for lab in [label_of(v, inside=regions[k[0]])] if lab]
    doc = {"Schema": 1, "Id": "borders-v1",
           "Source": {"Name": "郡地図 Ver 1.1（郡地図研究会）", "License": "CC BY 4.0", "Credit": "「郡地図 Ver 1.1」を加工して作成",
                      "Edits": "明治の国（北海道は蝦夷にまとめ、渡島国は郡「渡島」）、明治の市・区を除く、隣と接する境目だけ、約200mで間引き、日本測地系から世界測地系へ近似で変換"},
           "Provinces": provinces, "Districts": districts, "ProvinceLines": plines, "DistrictLines": glines, "Lakes": lakes, "Dangling": pdang + gdang}
    OUT.write_text(json.dumps(doc, ensure_ascii=False, separators=(",", ":")) + "\n", encoding="utf-8", newline="\n")
    print(f"湖 {len(lakes)}")
    print(f"{OUT.name}: 国 {len(provinces)}・郡 {len(districts)}・国の線 {len(plines)}（点 {sum(map(len, plines))}）・郡の線 {len(glines)}（点 {sum(map(len, glines))}）・{OUT.stat().st_size // 1024}KB")


if __name__ == "__main__":
    main()
