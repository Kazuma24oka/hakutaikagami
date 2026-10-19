"""Frontal assembly candidate. Ownership and inferred surfaces are not anatomical/motion approval."""
import hashlib
import json
from pathlib import Path
from officer_art_pipeline import Blocked
raise Blocked('OfficerPartsV1 was rejected. Retired exporter cannot recreate it; use the recorded production plan and shared gate.')
import numpy as np
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parents[1]
folder = root / 'Art/Trials/OfficerPartsV1'
export = root / 'Game/Assets/Trials/OfficerPartsV1'
export.mkdir(parents=True, exist_ok=True)
(export / 'layers').mkdir(exist_ok=True)
(export / 'previews').mkdir(exist_ok=True)
spec = json.loads((folder / 'parts-authoring.json').read_text(encoding='utf-8'))
source_file = root / 'Game/Assets/Trials/OfficerContextV1/front-64x96.png'
source = np.array(Image.open(source_file).convert('RGBA'))
visible = source[:, :, 3] > 0
source[~visible] = 0  # Canonicalize invisible RGB only; preserve alpha and every visible RGBA pixel.
assert source.shape == (96, 64, 4)

def polygon(points):
    im = Image.new('L', (64, 96))
    ImageDraw.Draw(im).polygon([tuple(p) for p in points], fill=255)
    return np.array(im) > 0

regions = spec['regions_in_paint_order'] + [
    {'id': 'face_features', 'label': '目眉・鼻の表現', 'group': 'face'},
    {'id': 'beard', 'label': '口髭・顎髭', 'group': 'beard'}]
ids = {r['id']: i + 1 for i, r in enumerate(regions)}
owners = np.zeros((96, 64), np.uint8)
weapon_under = None
for r in spec['regions_in_paint_order']:
    if r['id'] == 'weapon':
        weapon_under = owners.copy()
    owners[polygon(r['polygon']) & visible] = ids[r['id']]
for key, rois in [('face_features', spec['face_feature_rois']), ('beard', spec['beard_rois'])]:
    for x0, y0, x1, y1 in rois:
        roi = np.zeros_like(visible); roi[y0:y1, x0:x1] = True
        dark = source[:, :, :3].astype(int).sum(2) < 400
        eligible = (owners == ids['head_skin']) if key == 'face_features' else np.isin(owners, [ids['head_skin'],ids['collar'],ids['hair']])
        owners[roi & dark & eligible] = ids[key]
# The head's visible skin is not a rectangle: dark temple strands remain owned by hair.
yy, xx = np.mgrid[:96,:64]
temple = (owners == ids['head_skin']) & (source[:, :, :3].astype(int).sum(2) < 400) & (yy < 29)
owners[temple] = ids['hair']
# Review the broad starting assignment explicitly at the outer coat, greaves and foot edges.
fallback = owners == ids['underclothes']
owners[fallback & (yy >= 88) & (xx < 32)] = ids['foot_right']
owners[fallback & (yy >= 88) & (xx >= 32)] = ids['foot_left']
owners[fallback & (yy >= 80) & (yy < 88) & (xx < 32)] = ids['armor_shin_right']
owners[fallback & (yy >= 80) & (yy < 88) & (xx >= 32)] = ids['armor_shin_left']
owners[fallback & (yy < 80) & (xx < 22)] = ids['coat_right']
owners[fallback & (yy < 80) & (xx > 38)] = ids['coat_left']
owners[fallback & (yy < 54) & (xx >= 22) & (xx <= 38)] = ids['armor_torso']
owners[fallback & (yy >= 54) & (yy < 68) & (xx >= 22) & (xx <= 38)] = ids['armor_waist']
assert np.all(owners[visible] > 0)
assert np.all(owners[~visible] == 0)

# The generated unseen-material canvas is placed by ONE global translation, never per-part fit.
generated = Image.open(folder / 'generated-underlayer-material.png').convert('RGB')
assert generated.size == (64, 96)
material = Image.new('RGB', (64, 96), (220, 220, 216))
material.paste(generated, (2, 0))
palette_colors = json.loads((root / 'Art/Trials/OfficerScaleV1/scale-contract.json').read_text(encoding='utf-8'))['palette_rgb']
palette_colors += [[22, 28, 43], [35, 43, 62], [50, 58, 77], [67, 74, 89]]
palette = Image.new('P', (1, 1))
flat = [c for rgb in palette_colors for c in rgb]
palette.putpalette(flat + flat[-3:] * ((768 - len(flat)) // 3))
rgb = np.array(material.quantize(palette=palette, dither=Image.Dither.NONE).convert('RGB'))
raw = np.array(material).astype(int)
is_material = (raw.max(2) - raw.min(2) >= 28) | (raw.mean(2) < 120)
base = np.zeros_like(source)
base[:, :, :3] = rgb
base[:, :, 3] = np.where(visible & is_material, 255, 0)
base[base[:, :, 3] == 0] = 0

layers = {}
observed_masks = {}
inferred_masks = {}
for r in regions:
    key = r['id']; mask = owners == ids[key]
    layer = np.zeros_like(source); layer[mask] = source[mask]
    layers[key] = layer; observed_masks[key] = mask
    inferred_masks[key] = np.zeros_like(visible)

# Sword removal must reveal the coat/armor UNDER the sword, rather than a bare stripe of base cloth.
# Infer only the hidden weapon footprint, from nearest visible material of its author-defined owner.
weapon_pixels = owners == ids['weapon']
for r in regions:
    key = r['id']
    if key == 'weapon': continue
    hidden = weapon_pixels & (weapon_under == ids[key])
    yy, xx = np.where(observed_masks[key])
    if len(xx) == 0: continue
    for y, x in zip(*np.where(hidden)):
        nearest = np.argmin((xx - x) ** 2 + (yy - y) ** 2)
        layers[key][y, x] = source[yy[nearest], xx[nearest]]
        inferred_masks[key][y, x] = True

short_hair = layers['hair'].copy()
short_hair[~polygon(spec['short_hair_polygon'])] = 0
layers['hair_short'] = short_hair

def compose(enabled, hair='hair', blue=False):
    im = Image.fromarray(base)
    for r in regions:
        if r['group'] not in enabled: continue
        key = hair if r['id'] == 'hair' else r['id']
        layer = layers[key].copy()
        if blue and r['group'] == 'armor':
            rgb16 = layer[:, :, :3].astype(int)
            lacquer = (rgb16[:, :, 0] > rgb16[:, :, 1] * 1.6) & (rgb16[:, :, 1] < rgb16[:, :, 2] * 1.35) & (layer[:, :, 3] > 0)
            layer[lacquer, :3] = layer[lacquer, :3][:, ::-1]
        im.alpha_composite(Image.fromarray(layer))
    return im

groups = sorted({r['group'] for r in regions})
cases = [
    {'id':'equipped', 'label':'元の装備・再合成', 'groups':groups},
    {'id':'underlayer', 'label':'下地のみ（推定候補）', 'groups':[]},
    {'id':'no_hair', 'label':'髪を外す', 'groups':[g for g in groups if g != 'hair']},
    {'id':'no_beard', 'label':'髭を外す', 'groups':[g for g in groups if g != 'beard']},
    {'id':'no_weapon', 'label':'刀・鞘を外す', 'groups':[g for g in groups if g != 'weapon']},
    {'id':'underwear', 'label':'下衣一式へ交換（候補）', 'groups':['head','hair','face','beard']},
    {'id':'short_hair', 'label':'短髪形状へ交換（候補）', 'groups':groups, 'hair':'hair_short'},
    {'id':'blue_armor', 'label':'漆だけ青へ変更', 'groups':groups, 'blue':True}]

full = compose(groups)
assert np.array_equal(np.array(full), source), 'Full reconstruction differs from canonicalized source.'
Image.fromarray(source).save(export / 'reference.png')
Image.fromarray(base).save(export / 'underlayer.png')
Image.fromarray(owners).save(export / 'ownership-ids.png')
skin_mask = np.zeros_like(visible)
for key in ('head_skin', 'hand_right', 'hand_left'):
    m = observed_masks[key]
    skin_mask |= m & (source[:, :, 0] > 150) & (source[:, :, 0] > source[:, :, 2] * 1.2)
Image.fromarray(skin_mask.astype(np.uint8) * 255).save(export / 'skin-color-mask.png')
lacquer_mask = np.zeros_like(visible)
for r in regions:
    if r['group'] != 'armor': continue
    layer = layers[r['id']]; colors = layer[:, :, :3].astype(int)
    lacquer_mask |= (colors[:, :, 0] > colors[:, :, 1] * 1.6) & (colors[:, :, 1] < colors[:, :, 2] * 1.35) & (layer[:, :, 3] > 0)
Image.fromarray(lacquer_mask.astype(np.uint8) * 255).save(export / 'lacquer-color-mask.png')

for key, layer in layers.items():
    assert layer.shape == (96, 64, 4)
    Image.fromarray(layer).save(export / 'layers' / (key + '.png'))
    if any(r['id'] == key and r['group'] == 'armor' for r in regions):
        variant = layer.copy(); c = variant[:, :, :3].astype(int)
        paint = (c[:, :, 0] > c[:, :, 1] * 1.6) & (c[:, :, 1] < c[:, :, 2] * 1.35) & (variant[:, :, 3] > 0)
        variant[paint, :3] = variant[paint, :3][:, ::-1]
        Image.fromarray(variant).save(export / 'layers' / (key + '-blue.png'))
for case in cases:
    image = compose(case['groups'], case.get('hair','hair'), case.get('blue',False))
    image.save(export / 'previews' / (case['id'] + '.png'))
    case['rgba_sha256'] = hashlib.sha256(image.tobytes()).hexdigest()
blue = np.array(compose(groups, blue=True))
assert np.array_equal(blue[~lacquer_mask], source[~lacquer_mask])
assert np.array_equal(blue[:, :, 3], source[:, :, 3])
assert np.any(blue != source)
for case in cases[1:]:
    assert not np.array_equal(np.array(compose(case['groups'], case.get('hair','hair'), case.get('blue',False))), source)

colors = [(0,0,0)] + [tuple(int(v) for v in (np.array([67,113,151]) + i * np.array([71,47,89])) % 190 + 45) for i in range(len(regions))]
ownership_rgb = np.zeros_like(source)
for i, color in enumerate(colors):
    if i: ownership_rgb[owners == i] = (*color,255)
Image.fromarray(ownership_rgb).save(export / 'ownership-view.png')
manifest = {'schema':1,'status':'front-parts-candidate-not-user-approved','canvas':[64,96], 'display_scale':1,
    'source_sha256':hashlib.sha256(source_file.read_bytes()).hexdigest(),
    'invisible_rgb_canonicalized':True,'visible_rgba_preserved':True,'reconstruction_rgba_difference':0,
    'semantic_quality':'author-defined coarse ownership; inspect individual parts and contacts, not approved by reconstruction equality',
    'underlayer':'generated material, translated globally +2x; clipped to reference silhouette; hidden shape inferred, not anatomical reconstruction',
    'weapon_underpaint':'nearest observed material within corresponding author-defined underlying owner; inferred patches only',
    'landmarks_status':'static provisional landmarks, not an animated rig', 'groups':groups,
    'layers':[dict(r, pixels=int(observed_masks[r['id']].sum()),inferred_pixels=int(inferred_masks[r['id']].sum())) for r in regions],
    'cases':cases,'pending':['semantic boundary user review','underlayer appearance','all directional views','6-head parts','rigging and motion','part compatibility beyond this candidate']}
(folder / 'parts-contract.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2)+'\n',encoding='utf-8')
(export / 'parts-contract.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2)+'\n',encoding='utf-8')
(export / 'landmarks.json').write_text(json.dumps(spec['landmarks'],indent=2)+'\n',encoding='utf-8')
data = {'contract':manifest, 'landmarks':spec['landmarks']}
(root / 'Docs/Proposals/officer-parts-data.js').write_text('window.OFFICER_PARTS='+json.dumps(data,ensure_ascii=False)+';\n',encoding='utf-8')
evidence = root / 'Docs/Proposals/Evidence'; evidence.mkdir(parents=True,exist_ok=True)
board = Image.new('RGB',(1120,890),'#1d2932'); draw=ImageDraw.Draw(board)
font=ImageFont.truetype('C:/Windows/Fonts/meiryo.ttc',17)
draw.text((20,10),'百代鏡｜64×96 正面パーツ候補・4倍表示（下地と境界は未承認）',font=font,fill='white')
for i, case in enumerate(cases):
    x=20+(i%4)*275; y=60+(i//4)*410
    draw.text((x,y),case['label'],font=font,fill='#e0c28c')
    im=Image.open(export/'previews'/(case['id']+'.png')).resize((256,384),Image.Resampling.NEAREST)
    tile=Image.new('RGB',(256,384),'#b8b9b2'); tile.paste(im,(0,0),im)
    board.paste(tile,(x,y+26))
board.save(evidence/'officer-parts-candidates.png')
print(f'OFFICER_PARTS_EXPORT_PASS layers={len(layers)} visible_pixels={visible.sum()} reconstruction_rgba=0 recolor_outside=0 candidates={len(cases)} anatomy_approved=0')
