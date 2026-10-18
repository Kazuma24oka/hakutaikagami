"""Gated packaging of unchanged native front motion sheets for Godot."""
import argparse,json
from pathlib import Path
import officer_art_pipeline as g
import numpy as np
from PIL import Image

def main():
    p=argparse.ArgumentParser();p.add_argument('--root',type=Path,default=Path(__file__).resolve().parents[1]);a=p.parse_args();r=a.root
    w=json.loads((r/'Art/Officer/work-record.json').read_text(encoding='utf-8'));plan=w['plans'][w['godot_motion_plan']];binding=g.require_plan(r,'motion',plan)
    manifest=json.loads((r/'Art/Officer/FrontMotionV4/manifest.json').read_text(encoding='utf-8'));baseline=json.loads((r/'Art/Officer/FrontMotionV4/combination-hashes.json').read_text(encoding='utf-8'))
    for name,value in manifest['sheets'].items():
        if g.sha(r/'Game/Assets/Trials/OfficerFrontMotionV4'/name)!=value:raise ValueError('Changed native sheet '+name)
    keys=baseline['keys'];definition=json.loads((r/'Art/Officer/ArmorSplitV3/part-definition.json').read_text(encoding='utf-8'))
    sheets={name[:-4]:{'path':'res://Assets/Trials/OfficerFrontMotionV4/'+name,'sha256':sha} for name,sha in manifest['sheets'].items() if not name.startswith('lookup-')}
    import hashlib
    for key,item in sheets.items():
        pixels=np.array(Image.open(r/'Game/Assets/Trials/OfficerFrontMotionV4'/(key+'.png')).convert('RGBA'));pixels[pixels[:,:,3]==0]=0
        item['frame_hashes']=[hashlib.sha256(pixels[:,64*i:64*(i+1)].tobytes()).hexdigest() for i in range(12)]
    data={'canvas':[64,96],'display_scale':1,'keys':keys,'labels':['顔・髭','髪']+[definition['parts'][key]['label'] for key in keys[2:]],'order':manifest['draw_order'],'idle_duration_ms':manifest['clips']['idle']['duration_ms'],'walk_duration_ms':manifest['clips']['walk']['duration_ms'],'sheets':sheets,'hashes':[baseline['hashes'][str(flags)] for flags in range(1024)],'binding':binding,'scope':'Front native trial; no world tick or other-view approval'}
    path=r/'Game/Data/officer_motion_trial.json';path.write_text(json.dumps(data,ensure_ascii=False,separators=(',',':'))+'\n',encoding='utf-8')
    report={'status':'PREPARED_RUNTIME_UNVERIFIED','binding':binding,'data':{'path':path.relative_to(r).as_posix(),'sha256':g.sha(path)},'native_sheets_unchanged':22,'new_generator_invocations':0}
    (r/'Art/Officer/GodotMotionV5/package-validation.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8');print('GODOT_OFFICER_PACKAGE_READY '+binding)
if __name__=='__main__':main()
