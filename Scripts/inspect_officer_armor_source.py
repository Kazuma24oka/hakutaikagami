from pathlib import Path
import sys,json
from PIL import Image,ImageDraw,ImageFont
r=Path(sys.argv[1]);sys.path.insert(0,str(r/'Scripts'));import officer_art_pipeline as g
w=json.loads((r/'Art/Officer/work-record.json').read_text(encoding='utf-8'));p=w['plans']['armor-split-source-grid'];g.require_plan(r,p['stage'],p)
s=Image.open(r/'Game/Assets/Trials/OfficerContextV1/front-64x96.png').convert('RGBA');font=ImageFont.truetype('C:/Windows/Fonts/consola.ttf',12)
for index,(lo,hi) in enumerate([(29,61),(62,95)]):
    im=Image.new('RGB',(1130,920),(226,224,213));d=ImageDraw.Draw(im)
    for x in range(5,61):d.text((25+(x-5)*19,5),str(x),font=font,fill='black')
    for y in range(lo,hi+1):
        d.text((1,30+(y-lo)*25),str(y),font=font,fill='black')
        for x in range(5,61):
            px=s.getpixel((x,y));xx=25+(x-5)*19;yy=25+(y-lo)*25;d.rectangle((xx,yy,xx+18,yy+24),fill=px[:3] if px[3] else (210,211,202),outline=(153,153,145))
    im.save(r/('Art/Officer/ArmorSplitV3/source-grid-'+str(index)+'.png'))
print('ARMOR_SOURCE_GRID_READY')
