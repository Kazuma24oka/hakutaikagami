"""Validate real GPU captures against native art, then export a viewing GIF."""
from pathlib import Path
import json
import numpy as np
from PIL import Image,ImageDraw
import officer_art_pipeline as g
import officer_back_motion as m
R=Path(__file__).resolve().parents[1];O=R/'Art/Officer/BackMotionV10';w=json.loads((R/'Art/Officer/work-record.json').read_text(encoding='utf-8'));p=w['plans'][w['back_motion_plan']];binding=g.require_plan(R,'motion',p)
runtime=json.loads((O/'runtime-validation.json').read_text(encoding='utf-8'));assert runtime['binding']==binding and runtime['status']=='PASS'
t,ops=m.load(R);rig=json.loads((O/'rig.json').read_text(encoding='utf-8'));frames=[];board=Image.new('RGB',(4*350,2*430),'#26343b');d=ImageDraw.Draw(board);verified=[]
for i,f in enumerate(rig['clips']['walk']['frames']):
 path=O/f'godot-walk-{i}.png';im=Image.open(path).convert('RGB');assert im.size==(1100,720)
 art=m.compose(m.animate(t,f,rig),ops,set(m.KEYS));rgb=np.full((96,64,3),[38,52,59],np.uint8);mask=art[:,:,3]>0;rgb[mask]=art[mask,:3]
 actual=np.array(im.crop((200,260,264,356)));large=np.array(im.crop((650,175,906,559)));assert np.array_equal(actual,rgb),f'Native capture mismatch {i}';assert np.array_equal(large,np.repeat(np.repeat(rgb,4,axis=0),4,axis=1)),f'Large capture mismatch {i}'
 x=(i%4)*350;y=(i//4)*430;d.text((x+8,y+8),str(i+1),fill='white');board.paste(im.crop((200,260,264,356)),(x+8,y+35));board.paste(im.crop((650,175,906,559)),(x+84,y+35));frames.append(im);verified.append({'frame':i,'png_sha256':g.sha(path),'native_difference_pixels':0,'large_difference_pixels':0})
board.save(O/'godot-walk-frames.png');frames[0].save(O/'godot-walk.gif',save_all=True,append_images=frames[1:],duration=rig['clips']['walk']['duration_ms'],loop=0,disposal=2,optimize=False)
(O/'capture-validation.json').write_bytes((json.dumps({'binding':binding,'source':'real Godot GPU viewport PNGs','frames':verified,'gif_note':'Viewing copy with GIF palette conversion; PNGs remain exact source evidence.'},indent=2)+'\n').encode());print('GODOT_CAPTURE_NATIVE_AND_4X_8_FRAMES_EXACT')
