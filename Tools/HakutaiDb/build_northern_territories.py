"""百代鏡：地図に足す北方領土（択捉島・国後島・色丹島・歯舞群島）の島の形を作る（2026-11-12、表示だけに使う）。

  python Tools/HakutaiDb/build_northern_territories.py [jpn-atlas の japan.json]
    既定：D:/01_資料/論文/戦国時代/戦国DB/MAPDATA/jpn-atlas-1.0.2/japan.json
    → Game/Data/Geography/northern-territories-v1.json

材料：jpn-atlas 1.0.2（BSD-3-Clause。国土地理院「地球地図日本」をもとにした TopoJSON、正積方位図法で投影済み）。
同じフォルダの projection_fit.json（中心 138.5°E・36.1°N、倍率・平行移動）で緯度経度へ戻す。
今のゲームの海岸線（Natural Earth の日本）には北方領土が入っていないので、その島だけを足す。
世界の計算（陸地の判定）には使わない。
"""
from __future__ import annotations

import json
import math
import sys
from pathlib import Path

SRC = Path(sys.argv[1]) if len(sys.argv) > 1 else Path("D:/01_資料/論文/戦国時代/戦国DB/MAPDATA/jpn-atlas-1.0.2/japan.json")
OUT = Path(__file__).resolve().parents[2] / "Game" / "Data" / "Geography" / "northern-territories-v1.json"


def main():
    topo = json.loads(SRC.read_text(encoding="utf-8"))
    fit = json.loads((SRC.parent / "projection_fit.json").read_text(encoding="utf-8"))
    sx, sy = topo["transform"]["scale"]
    tx_, ty_ = topo["transform"]["translate"]
    arcs = []
    for arc in topo["arcs"]:
        x = y = 0
        pts = []
        for dx, dy in arc:
            x += dx
            y += dy
            pts.append((x * sx + tx_, y * sy + ty_))
        arcs.append(pts)
    lam0, phi0 = math.radians(fit["lon0"]), math.radians(fit["lat0"])
    s, tx, ty = fit["s"], fit["tx"], fit["ty"]

    def inverse(px, py):  # d3.geoAzimuthalEqualArea の逆
        x, y = (px - tx) / s, (ty - py) / s
        rho = math.hypot(x, y)
        if rho == 0:
            return math.degrees(phi0), math.degrees(lam0)
        c = 2 * math.asin(rho / 2)
        phi = math.asin(math.cos(c) * math.sin(phi0) + y * math.sin(c) * math.cos(phi0) / rho)
        lam = lam0 + math.atan2(x * math.sin(c), rho * math.cos(phi0) * math.cos(c) - y * math.sin(phi0) * math.sin(c))
        return math.degrees(phi), math.degrees(lam)

    def forward(lat, lon):
        phi, lam = math.radians(lat), math.radians(lon) - lam0
        k = math.sqrt(2 / (1 + math.sin(phi0) * math.sin(phi) + math.cos(phi0) * math.cos(phi) * math.cos(lam)))
        return tx + s * k * math.cos(phi) * math.sin(lam), ty - s * k * (math.cos(phi0) * math.sin(phi) - math.sin(phi0) * math.cos(phi) * math.cos(lam))
    # 投影の向きの確かめ：根室（43.33N 145.58E）を往復させる
    la, lo = inverse(*forward(43.33, 145.58))
    assert abs(la - 43.33) < 1e-6 and abs(lo - 145.58) < 1e-6

    def ring(ids):
        out = []
        for i in ids:
            pts = arcs[i] if i >= 0 else list(reversed(arcs[~i]))
            out += pts if not out else pts[1:]
        if len(out) > 1 and out[0] == out[-1]:
            out = out[:-1]  # 閉じた輪の最後の点（始点と同じ）は持たない（ゲームの海岸線と同じ形）
        return [inverse(*p) for p in out]
    geoms = topo["objects"]["country"]["geometries"]
    polys = []
    for g in geoms:
        if g["type"] == "Polygon":
            polys.append(g["arcs"])
        elif g["type"] == "MultiPolygon":
            polys += g["arcs"]
    out = []
    for p in polys:
        outer = ring(p[0])
        lat = sum(q[0] for q in outer) / len(outer)
        lon = sum(q[1] for q in outer) / len(outer)
        # 北方領土：根室半島（145.8°E）より東・北の島（知床半島を除くため 145.4°E より東、43.2°N より北、北海道本島の外）
        if lon > 145.6 and lat > 43.2 and len(outer) >= 4 and not (lon < 145.9 and lat < 43.5 and len(outer) > 500):
            out.append({"Outer": [{"LatitudeE7": round(a * 1e7), "LongitudeE7": round(b * 1e7)} for a, b in outer],
                        "Holes": [[{"LatitudeE7": round(a * 1e7), "LongitudeE7": round(b * 1e7)} for a, b in ring(h)] for h in p[1:]]})
    doc = {"Schema": 1, "Id": "northern-territories-v1", "Use": "地図の表示だけ（世界の陸地の判定には使わない）",
           "Source": {"Name": "jpn-atlas 1.0.2（国土地理院「地球地図日本」をもとにした TopoJSON）", "License": "BSD-3-Clause（データの出典：国土地理院 地球地図日本）",
                      "File": "japan.json の country", "Projection": "projection_fit.json の正積方位図法を逆に計算"},
           "Polygons": out}
    OUT.write_text(json.dumps(doc, ensure_ascii=False, separators=(",", ":")) + "\n", encoding="utf-8", newline="\n")
    big = sorted(((len(p["Outer"]), sum(q["LatitudeE7"] for q in p["Outer"]) / len(p["Outer"]) / 1e7, sum(q["LongitudeE7"] for q in p["Outer"]) / len(p["Outer"]) / 1e7) for p in out), reverse=True)[:6]
    print(f"{OUT.name}: 島 {len(out)}（大きい順 {[(n, round(a, 2), round(b, 2)) for n, a, b in big]}）")


if __name__ == "__main__":
    main()
