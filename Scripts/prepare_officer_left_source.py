"""Gated whole-canvas raw-to-native registration. Never fit individual parts."""
from pathlib import Path
import json
import numpy as np
from PIL import Image
import officer_art_pipeline as g
R=Path(__file__).resolve().parents[1];O=R/'Art/Officer/LeftViewV11';w=json.loads((R/'Art/Officer/work-record.json').read_text(encoding='utf-8'));p=w['plans']['left-source-v11-registration'];g.require_plan(R,'front_authoring',p)
im=Image.open(O/'armored-v2-raw.png').convert('RGBA');assert im.size==(1024,1536)
a=np.array(im.resize((64,96),Image.Resampling.NEAREST));visible=a[:,:,3]>=200
a[:,:,:3]=np.array(Image.fromarray(a[:,:,:3]).quantize(colors=48,method=Image.Quantize.MEDIANCUT,dither=Image.Dither.NONE).convert('RGB'));a[:,:,3]=visible*255;a[~visible]=0
b=np.zeros_like(a);b[1:]=a[:-1]
Image.fromarray(b).save(O/'source-left-candidate.png');print('LEFT_SOURCE_FIXED_UNIFORM_1_16_Y_PLUS_1')
