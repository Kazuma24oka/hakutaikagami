"""Preview-only background removal. Not semantic part masks or production alpha approval."""
from collections import deque
import hashlib
import json
from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parents[1]
folder = root / 'Art/Trials/OfficerContextV1'
output = root / 'Game/Assets/Trials/OfficerContextV1'
output.mkdir(parents=True, exist_ok=True)
source = Image.open(root / 'Art/Trials/OfficerScaleV1/front-review-source.png').convert('RGB')
w, h = source.size
pixels = list(source.get_flattened_data())
# A restricted low-chroma, bright background candidate; only edge-connected candidates are removed.
# This is deliberately documented as an approximate context mask, not anatomical segmentation.
allowed = [max(p) - min(p) < 24 and abs(p[0] - p[1]) < 12 and abs(p[1] - p[2]) < 18 and sum(p) > 315 for p in pixels]
removed = bytearray(w * h)
queue = deque([(x, 0) for x in range(w)] + [(x, h - 1) for x in range(w)] + [(0, y) for y in range(h)] + [(w - 1, y) for y in range(h)])
while queue:
    x, y = queue.popleft()
    index = y * w + x
    if removed[index] or not allowed[index]:
        continue
    removed[index] = 1
    for xx, yy in [(x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)]:
        if 0 <= xx < w and 0 <= yy < h and not removed[yy * w + xx]:
            queue.append((xx, yy))
mask = Image.new('L', source.size)
mask.putdata([0 if n else 255 for n in removed])
mask.save(folder / 'source-context-alpha.png')
foreground = source.convert('RGBA')
foreground.putalpha(mask)
audit = Image.new('RGB', (w * 3, h), 'white')
audit.paste(source, (0, 0))
for i, bg in enumerate(['white', 'black'], 1):
    tile = Image.new('RGB', source.size, bg)
    tile.paste(foreground, (0, 0), foreground)
    audit.paste(tile, (i * w, 0))
evidence = root / 'Docs/Proposals/Evidence'
evidence.mkdir(parents=True, exist_ok=True)
audit.save(evidence / 'officer-context-alpha-audit.png')
contract = json.loads((root / 'Art/Trials/OfficerScaleV1/scale-contract.json').read_text(encoding='utf-8'))
assert hashlib.sha256((root / 'Art/Trials/OfficerScaleV1/front-review-source.png').read_bytes()).hexdigest() == contract['source_sha256']
records = []
for variant in contract['variants']:
    width, height = variant['canvas']
    transform = variant['source_to_cell']
    scale = transform['uniform_scale']
    tx, ty = transform['translate']
    cell_mask = mask.transform((width, height), Image.Transform.AFFINE,
        (1 / scale, 0, -tx / scale, 0, 1 / scale, -ty / scale), Image.Resampling.NEAREST, fillcolor=0)
    color = Image.open(root / 'Game/Assets/Trials/OfficerScaleV1' / variant['file']).convert('RGBA')
    assert hashlib.sha256((root / 'Game/Assets/Trials/OfficerScaleV1' / variant['file']).read_bytes()).hexdigest() == variant['png_sha256']
    rgb_before = color.convert('RGB').tobytes()
    color.putalpha(cell_mask)
    assert color.convert('RGB').tobytes() == rgb_before
    assert set(cell_mask.get_flattened_data()) == {0, 255}
    color.save(output / variant['file'])
    records.append({'id': variant['id'], 'source_rgb_unchanged': True, 'binary_alpha': True,
        'png_sha256': hashlib.sha256((output / variant['file']).read_bytes()).hexdigest()})
portrait = Image.open(folder / 'dialogue-face-reference.png')
assert portrait.size == (130, 140)
portrait.save(output / 'dialogue-face-reference.png')
manifest = {'status': 'context-preview-only-not-adopted', 'source_scale_contract': '../OfficerScaleV1/scale-contract.json',
    'alpha': 'edge-connected low-chroma bright candidate, visually inspected on white/black; not semantic part masks',
    'mask_l8_sha256': hashlib.sha256(mask.tobytes()).hexdigest(),
    'portrait': {'origin': 'user-provided character sheet, crop [655,75,785,215]',
        'role': 'fixed-size dialogue-face reference, not final six-head standing art',
        'file_sha256': hashlib.sha256((folder / 'dialogue-face-reference.png').read_bytes()).hexdigest()},
    'variants': records}
(folder / 'context-contract.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print('OFFICER_CONTEXT_EXPORT_PASS candidates=3 rgb_unchanged=1 preview_alpha_only=1 portrait_reference=1')
