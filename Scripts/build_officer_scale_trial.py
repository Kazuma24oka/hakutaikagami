"""Deterministic size study only: no AI redraw, segmentation, or production asset adoption."""
import argparse
import hashlib
import json
import sys
from pathlib import Path
from PIL import Image, ImageChops, ImageDraw, ImageFont, __version__ as pillow_version

ROOT = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument('--root', type=Path, default=ROOT)
parser.add_argument('--verify-godot', action='store_true')
args = parser.parse_args()
root = args.root
folder = root / 'Art/Trials/OfficerScaleV1'
export = root / 'Game/Assets/Trials/OfficerScaleV1'
export.mkdir(parents=True, exist_ok=True)
source_path = folder / 'front-review-source.png'
source = Image.open(source_path).convert('RGB')
assert source.size == (295, 472)
assert hashlib.sha256(source_path.read_bytes()).hexdigest() == 'e80e43ceff78e07406814e7fd4edf78590049c9304898b804cd394f8030448ef', 'Source changed: create a new trial revision instead of silently re-fitting.'
# Manually chosen visible review anchors, NOT an anatomical skeleton or body-height measurement.
# Coordinates refer to the fixed source crop, not per-output foreground bounding boxes.
anchor = {'center_x': 145, 'visible_hair_top_y': 10, 'sole_y': 467}
variants = []
palette = source.quantize(colors=24, method=Image.Quantize.MEDIANCUT)
palette_values = palette.getpalette()[:72]
for w, h in [(32, 48), (48, 64), (64, 96)]:
    sole = h - 4
    scale = (h - 8) / (anchor['sole_y'] - anchor['visible_hair_top_y'])
    tx = w / 2 - anchor['center_x'] * scale
    ty = sole - anchor['sole_y'] * scale
    # A single isotropic affine transform preserves the relationship of every source point.
    transformed = source.transform((w, h), Image.Transform.AFFINE,
        (1 / scale, 0, -tx / scale, 0, 1 / scale, -ty / scale),
        Image.Resampling.BICUBIC, fillcolor=source.getpixel((0, 0)))
    # Fixed reference palette for this study only, shared across all size variants.
    image = transformed.quantize(palette=palette, dither=Image.Dither.NONE).convert('RGBA')
    name = f'front-{w}x{h}.png'
    image.save(export / name)
    assert image.size == (w, h)
    assert image.getextrema()[3] == (255, 255), 'Review background must remain explicitly opaque.'
    assert len(image.getcolors(w * h)) <= 24
    assert abs((anchor['sole_y'] * scale + ty) - sole) < 1e-9
    variants.append({'id': f'{w}x{h}', 'file': name, 'canvas': [w, h],
        'source_to_cell': {'uniform_scale': scale, 'translate': [tx, ty]},
        'review_anchor': {'center_x': w / 2, 'sole_y': sole},
        'rgba_sha256': hashlib.sha256(image.tobytes()).hexdigest(),
        'png_sha256': hashlib.sha256((export / name).read_bytes()).hexdigest(),
        'colors_used': len(image.getcolors(w * h))})

manifest = {'schema': 1, 'status': 'size-study-awaiting-user-selection',
    'source_sha256': hashlib.sha256(source_path.read_bytes()).hexdigest(),
    'source_size': list(source.size), 'source_crop_from_generated_sheet': [110, 80, 405, 552],
    'source_sheet_sha256': 'ab9cc8aa39f1bb2236ccd8000f5301a3c7343a469b368d2d6b390292e8c4e3d5',
    'source_anchors': anchor, 'python_version': sys.version,
    'pillow_version': pillow_version, 'resampling': 'one isotropic affine / BICUBIC',
    'palette': 'MEDIANCUT24 from fixed source, no dithering, shared across candidates',
    'palette_rgb': [palette_values[i:i + 3] for i in range(0, 72, 3)],
    'runtime_filter': 'nearest; integer display magnification only',
    'has_production_alpha': False, 'has_semantic_masks': False, 'has_rig': False,
    'anatomical_head_ratio_measured': False, 'adopted_size': None,
    'variants': variants}
(folder / 'scale-contract.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')

# Review board uses the actual exported files; it is not an AI approximation of their appearance.
board = Image.new('RGB', (1100, 950), '#19232b')
draw = ImageDraw.Draw(board)
font_path = 'C:/Windows/Fonts/meiryo.ttc'
font = ImageFont.truetype(font_path, 18)
small = ImageFont.truetype(font_path, 14)
draw.text((24, 15), '百代鏡：同じ原画からの原寸・整数拡大比較（サイズ未採用）', font=font, fill='white')
for col, v in enumerate(variants):
    x = 25 + col * 360
    draw.text((x, 55), v['id'] + ' / 24色以内・背景付き試料', font=font, fill='#e2c891')
    sprite = Image.open(export / v['file'])
    for row, zoom in enumerate([1, 2, 4]):
        y = [200, 440, 870][row]
        center = x + 150
        scaled = sprite.resize((sprite.width * zoom, sprite.height * zoom), Image.Resampling.NEAREST)
        top = y - int(v['review_anchor']['sole_y'] * zoom)
        draw.line((x, y, x + 315, y), fill='#e18d78')
        board.paste(scaled.convert('RGB'), (center - scaled.width // 2, top))
        draw.text((x, y + zoom * 4 + 6), f'{zoom}倍 / 表示枠 {sprite.width * zoom}×{sprite.height * zoom}', font=small, fill='#b8c8d2')
draw.text((25, 925), '背景除去・手直し・パーツ化前。同じセル寸法でも外見・動作の合格にはなりません。', font=small, fill='#c4cbd0')
evidence = root / 'Docs/Proposals/Evidence'
evidence.mkdir(parents=True, exist_ok=True)
board.save(evidence / 'officer-scale-comparison.png')
print('OFFICER_SCALE_EXPORT_PASS candidates=3 shared_palette=24 isotropic=1 fixed_sole=1 production_alpha=0')
if args.verify_godot:
    capture = Image.open(evidence / 'officer-scale-godot.png').convert('RGB')
    assert capture.size == (1100, 720)
    verification = []
    for i, v in enumerate(variants):
        w, h = v['canvas']
        x = 180 + 360 * i - w * 2
        y = 615 - (h - 4) * 4
        actual = capture.crop((x, y, x + w * 4, y + h * 4))
        expected = Image.open(export / v['file']).convert('RGB').resize((w * 4, h * 4), Image.Resampling.NEAREST)
        diff = ImageChops.difference(actual, expected)
        # The scene intentionally draws a baseline over the image, exclude only those documented rows.
        ImageDraw.Draw(diff).rectangle((0, 614 - y, w * 4 - 1, 616 - y), fill=(0, 0, 0))
        maximum = max(e[1] for e in diff.getextrema())
        assert maximum == 0, f'GPU pixels differ: {v["id"]}, max={maximum}'
        verification.append({'id': v['id'], 'zoom': 4, 'maximum_rgb_difference': maximum,
            'excluded_screen_rows_inclusive': [614, 616]})
    (folder / 'godot-pixel-verification.json').write_text(json.dumps(verification, indent=2) + '\n', encoding='utf-8')
    print('OFFICER_SCALE_GPU_PIXEL_PASS variants=3 nearest_blocks=4x4 difference=0 excluded_baseline_rows=614..616')
