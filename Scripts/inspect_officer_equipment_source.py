from pathlib import Path
import json,sys
from PIL import Image,ImageDraw,ImageFont
r=Path(sys.argv[1]);sys.path.insert(0,str(r/'Scripts'));import officer_art_pipeline as g
w=json.loads((r/'Art/Officer/work-record.json').read_text(encoding='utf-8'));g.require_plan(r,'front_authoring',w['plans']['equipment-source-inspection'])
src=Image.open(r/'Game/Assets/Trials/OfficerContextV1/front-64x96.png').convert('RGBA');font=ImageFont.truetype('C:/Windows/Fonts/consola.ttf',13);im=Image.new('RGB',(1100,1150),(225,224,213));d=ImageDraw.Draw(im)
for y in range(3,34):
    d.text((5,45+(y-3)*32),str(y),fill='black',font=font)
    for x in range(17,46):
        sx=40+(x-17)*32;sy=40+(y-3)*32;rgb=src.getpixel((x,y));d.rectangle((sx,sy,sx+31,sy+31),fill=rgb[:3] if rgb[3] else (216,216,208),outline=(138,138,130))
for x in range(17,46):d.text((40+(x-17)*32,15),str(x),font=font,fill='black')
f=r/'Art/Officer/EquipmentPartsV2';f.mkdir(exist_ok=True);im.save(f/'source-head-grid.png')
print('SOURCE_GRID_ONLY')
