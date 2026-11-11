"""百代鏡：地図に描く地形（山の陰影・川・湖）を作る（2026-11-12 ユーザーの指示。表示だけに使う）。

  python Tools/HakutaiDb/build_terrain.py [地球地図日本のフォルダ]
    既定：D:/01_資料/論文/戦国時代/戦国DB/MAPDATA/gm-japan
    → Game/Data/Geography/terrain-v1.json（川・湖）と terrain-shade-v1.png（山の陰影）

材料：国土地理院「地球地図日本」（地球地図日本 第2.2版 ベクタ（gm-jpn-all_u_2_2）・第1.1版 標高（gm-jpn-el_u_1_1））。
国土地理院コンテンツ利用規約に従い、出典を記載する。「地球地図日本」（国土地理院）を加工して作成：
  - 川は riverl（線）、湖・広い川面は inwatera（面）。約100m で間引く。
  - 山は標高（30秒、約1km）から北西の光の陰影を作り、海は透明。北緯24〜46度・東経122〜154度の範囲の画像。
"""
from __future__ import annotations

import io
import json
import math
import struct
import sys
import zipfile
from pathlib import Path

from PIL import Image

SRC = Path(sys.argv[1]) if len(sys.argv) > 1 else Path("D:/01_資料/論文/戦国時代/戦国DB/MAPDATA/gm-japan")
OUT = Path(__file__).resolve().parents[2] / "Game" / "Data" / "Geography"
sys.path.insert(0, str(Path(__file__).resolve().parent))
from build_borders import simplify  # noqa: E402


def read_shp(z, name):
    b = z.read(f"gm-jpn-all_u_2_2/{name}.shp")
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
        out.append([[(pts[2 * k + 1], pts[2 * k]) for k in range(a, c)] for a, c in zip(parts, parts[1:])])
    return out


def e7(ln, tol=0.001):
    return [[round(a * 1e7), round(b * 1e7)] for a, b in simplify(ln, tol)]


def main():
    z = zipfile.ZipFile(SRC / "gm-jpn-all_u_2_2.zip")
    rivers = [e7(part) for feat in read_shp(z, "riverl_jpn") for part in feat if len(part) >= 2]
    rivers = [r for r in rivers if len(r) >= 2]
    lakes = []
    for feat in read_shp(z, "inwatera_jpn"):
        for ring in feat:
            if len(ring) >= 4:
                r = e7(ring[:-1] if ring[0] == ring[-1] else ring, 0.0007)
                if len(r) >= 4:
                    lakes.append(r)
    # 山：標高（パレット画像の値が約25m 刻みの段階）から陰影
    Image.MAX_IMAGE_PIXELS = None
    el = Image.open(SRC / "gm-jpn-el_u_1_1" / "jpn" / "el.tif") if (SRC / "gm-jpn-el_u_1_1" / "jpn" / "el.tif").exists() else \
        Image.open(io.BytesIO(zipfile.ZipFile(SRC / "gm-jpn-el_u_1_1.zip").read("gm-jpn-el_u_1_1/jpn/el.tif")))
    step = 1 / 120  # 30秒
    west, north = 120.0, 50.0  # el.tfw の左上の画素の端
    S, N, W, E = 24.0, 46.0, 122.0, 154.0
    x0, x1 = round((W - west) / step), round((E - west) / step)
    y0, y1 = round((north - N) / step), round((north - S) / step)
    px = el.load()
    w, h = x1 - x0, y1 - y0
    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    op = out.load()
    az, alt = math.radians(315), math.radians(45)
    for y in range(h):
        lat = N - (y + 0.5) * step
        dx_m = 926.6 * math.cos(math.radians(lat))
        dy_m = 926.6
        for x in range(w):
            sx, sy = x0 + x, y0 + y
            v = px[sx, sy]
            if v == 0:
                continue
            a = px[sx - 1, sy] * 25.0 if sx > 0 else v * 25.0
            b = px[sx + 1, sy] * 25.0 if sx + 1 < el.size[0] else v * 25.0
            c = px[sx, sy - 1] * 25.0 if sy > 0 else v * 25.0
            d = px[sx, sy + 1] * 25.0 if sy + 1 < el.size[1] else v * 25.0
            gx = (b - a) / (2 * dx_m)
            gy = (d - c) / (2 * dy_m)
            slope = math.atan(math.hypot(gx, gy) * 1.6)
            aspect = math.atan2(gy, -gx)
            shade = math.sin(alt) * math.cos(slope) + math.cos(alt) * math.sin(slope) * math.cos(az - aspect)
            shade = max(0.0, min(1.0, shade))
            dark = max(0.0, 0.78 - shade)
            light = max(0.0, shade - 0.85)
            high = min(1.0, v * 25.0 / 2500.0)
            if dark >= light:
                op[x, y] = (38, 30, 22, min(200, int(dark * 300 + high * 40)))
            else:
                op[x, y] = (255, 250, 235, min(120, int(light * 500)))
    out.save(OUT / "terrain-shade-v1.png", optimize=True)
    doc = {"Schema": 1, "Id": "terrain-v1",
           "Source": {"Name": "地球地図日本（国土地理院）", "Credit": "「地球地図日本」（国土地理院）を加工して作成", "Terms": "国土地理院コンテンツ利用規約",
                      "Edits": "川（riverl）と湖・広い川面（inwatera）を約100mで間引き、標高（30秒）から陰影の画像を作った"},
           "Shade": {"File": "terrain-shade-v1.png", "South": round(S * 1e7), "North": round(N * 1e7), "West": round(W * 1e7), "East": round(E * 1e7)},
           "Rivers": rivers, "Lakes": lakes}
    (OUT / "terrain-v1.json").write_text(json.dumps(doc, ensure_ascii=False, separators=(",", ":")) + "\n", encoding="utf-8", newline="\n")
    print(f"川 {len(rivers)}（点 {sum(map(len, rivers))}）・湖 {len(lakes)}・陰影 {w}x{h} {(OUT / 'terrain-shade-v1.png').stat().st_size // 1024}KB・{(OUT / 'terrain-v1.json').stat().st_size // 1024}KB")


if __name__ == "__main__":
    main()
