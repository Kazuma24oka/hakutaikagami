"""Gated displacement probes, then gated front idle/walk exports and review."""
import argparse,base64,hashlib,json
from pathlib import Path
import numpy as np
from PIL import Image,ImageDraw,ImageFont
import officer_front_motion as m
import officer_art_pipeline as g

def writej(path,value):path.write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def png(a,path):Image.fromarray(a).save(path)
def board(rows,path,labels):
    scale=3;w=8*204+170;h=len(rows)*320+30;im=Image.new('RGB',(w,h),'#1c2b33');d=ImageDraw.Draw(im)
    font=ImageFont.truetype('C:/Windows/Fonts/meiryo.ttc',14)
    for j,row in enumerate(rows):
        d.text((8,j*320+16),labels[j],font=font,fill='#e6d2ab')
        for i,a in enumerate(row):
            b=Image.new('RGBA',(64,96),'#bcbfb5');b.alpha_composite(Image.fromarray(a));im.paste(b.resize((192,288),Image.Resampling.NEAREST),(166+i*204,j*320+26));d.text((166+i*204,j*320+6),str(i+1),font=font,fill='white')
    im.save(path)

def main():
    p=argparse.ArgumentParser();p.add_argument('--root',type=Path,required=True);p.add_argument('--plan',required=True);p.add_argument('--probe',action='store_true');a=p.parse_args();r=a.root
    work=json.loads((r/'Art/Officer/work-record.json').read_text(encoding='utf-8'));plan=work['plans'][a.plan];binding=g.require_plan(r,'parts' if a.probe else 'motion',plan)
    rig=json.loads((r/'Art/Officer/FrontMotionV4/rig.json').read_text(encoding='utf-8'));manifest,spec,t=m.load(r);allkeys=['face','hair',*spec['parts']];selected=set(allkeys)
    art=r/'Art/Officer/FrontMotionV4';out=r/'Game/Assets/Trials/OfficerFrontMotionV4';ev=r/'Docs/Proposals/Evidence';out.mkdir(parents=True,exist_ok=True)
    animated=[m.animate(t,f,rig) for _,_,f in m.frames(rig)];walk=animated[4:]
    cases=[('全装備',selected),('素体',set()),*[(spec['parts'][k]['label']+'を外す',selected-{k}) for k in spec['parts']]]
    if a.probe:
        board([[m.compose(x,spec,sel) for x in walk] for _,sel in cases],art/'displacement-probes.png',[name for name,_ in cases])
        board([[x[k] for x in walk] for k in spec['parts']],art/'isolated-motion-probes.png',[spec['parts'][k]['label'] for k in spec['parts']])
        joint=[]
        yy,xx=np.mgrid[:96,:64].astype(float)
        for _,_,f in m.frames(rig)[4:]:
            pixels=m.compose(m.animate(t,f,rig),spec,set());im=Image.fromarray(pixels);d=ImageDraw.Draw(im)
            for k,(x,y) in rig['joints'].items():
                dx,dy=m.displacement(np.array(float(x)),np.array(float(y)),f,rig);x=int(np.floor(x+dx+.5));y=int(np.floor(y+dy+.5));d.rectangle((x-1,y-1,x+1,y+1),outline='red' if 'right' in k else 'cyan')
            joint.append(np.array(im))
        board([joint],art/'joint-probes.png',['関節追従'])
        writej(art/'probe-binding.json',{'plan':a.plan,'binding':binding,'scope':'front-only displacement probes; not completed motion','rig_sha256':g.sha(art/'rig.json')})
        print('PROBES_READY_FOR_ACTUAL_INSPECTION');return
    # Export each item, visibility mask and underlay independently at native scale.
    for k in t:
        sheet=Image.new('RGBA',(64*len(animated),96))
        for i,v in enumerate(animated):sheet.paste(Image.fromarray(v[k]),(64*i,0))
        sheet.save(out/(k+'.png'))
    for kind in ['surface','sword']:
        sheet=Image.new('RGBA',(64*len(animated),96))
        for i,(_,_,f) in enumerate(m.frames(rig)):
            x,y,v=m.lookup(f,rig,kind);a=np.zeros((96,64,4),np.uint8);a[:,:,0]=x;a[:,:,1]=y;a[:,:,2]=v*255;a[:,:,3]=255;sheet.paste(Image.fromarray(a),(64*i,0))
        sheet.save(out/('lookup-'+kind+'.png'))
    # Every selectable combination, every frame, including hidden/mask interaction.
    hashes={};uncovered=[]
    for flags in range(1024):
        sel={k for i,k in enumerate(allkeys) if flags&(1<<i)};hashes[str(flags)]=[]
        for j,v in enumerate(animated):
            image=m.compose(v,spec,sel);hashes[str(flags)].append(hashlib.sha256(image.tobytes()).hexdigest())
            if set(np.unique(image[:,:,3]))-{0,255}:raise ValueError('Nonbinary animated alpha')
            if image[0,:,3].any() or image[-1,:,3].any() or image[:,0,3].any() or image[:,-1,3].any():raise ValueError('Animation clipping')
    board([[m.compose(x,spec,sel) for x in walk] for _,sel in cases],ev/'officer-motion-frames-v4.png',[name for name,_ in cases])
    board([[m.compose(x,spec,selected) for x in animated[:4]]],ev/'officer-idle-frames-v4.png',['待機'])
    # Side by side small native / enlarged animation. No GIF resampling of source.
    for clip,offset,count in [('idle',0,4),('walk',4,8)]:
        imgs=[]
        for v in animated[offset:offset+count]:
            im=Image.new('RGB',(896,420),'#1c2b33');d=ImageDraw.Draw(im);font=ImageFont.truetype('C:/Windows/Fonts/meiryo.ttc',15)
            for i,sel in enumerate([selected,selected-{'arms','shoulders'},selected-{'coat'},set()]):
                a=Image.fromarray(m.compose(v,spec,sel));bg=Image.new('RGBA',(64,96),'#bcbfb5');bg.alpha_composite(a);im.paste(bg,(i*224+6,30));im.paste(bg.resize((192,288),Image.Resampling.NEAREST),(i*224+8,130));d.text((i*224+76,55),['全装備','腕・肩なし','陣羽織なし','素体'][i],font=font,fill='white')
            imgs.append(im)
        imgs[0].save(ev/('officer-'+clip+'-v4.gif'),save_all=True,append_images=imgs[1:],duration=rig['clips'][clip]['duration_ms'],loop=0,optimize=False,disposal=2)
    writej(art/'combination-hashes.json',{'keys':allkeys,'frames':m.frames(rig),'hashes':hashes})
    files={p.name:g.sha(p) for p in sorted(out.glob('*.png'))}
    writej(art/'manifest.json',{'canvas':[64,96],'display_scale':1,'filter':'nearest','clips':rig['clips'],'frame_order':[[n,i] for n,i,_ in m.frames(rig)],'slots':allkeys,'draw_order':spec['draw_order'],'sheets':files,'source_manifest':manifest,'rig':{'path':'Art/Officer/FrontMotionV4/rig.json','sha256':g.sha(art/'rig.json')},'binding':binding,'scope':'front-only small in-place controlled motion; no other views or portrait approval'})
    writej(art/'validation.json',{'status':'TECHNICAL_PASS_PENDING_ACTUAL_RUNTIME_INSPECTION','combinations':1024,'frames':12,'native_cases':12288,'binary_alpha':True,'clipped_cases':0,'base_source_unchanged':g.sha(r/manifest['base']['path'])==manifest['base']['sha256'],'source_neutral_equal':np.array_equal(m.compose(animated[0],spec,selected),m.rgba(r/manifest['source']['path']))})
    make_page(r,rig,spec,t)
    print('FRONT_MOTION_EXPORTED_12288_CASES')

def make_page(r,rig,spec,textures):
    folder=r/'Game/Assets/Trials/OfficerFrontMotionV4';urls={k:'data:image/png;base64,'+base64.b64encode((folder/(k+'.png')).read_bytes()).decode() for k in textures}
    html=(Path(__file__).with_name('officer_front_motion_page.html')).read_text(encoding='utf-8')
    data={'images':urls,'rig':rig,'order':spec['draw_order'],'parts':{k:v['label'] for k,v in spec['parts'].items()}}
    (r/'Docs/Proposals/officer-motion-review.html').write_text(html.replace('__DATA__',json.dumps(data,ensure_ascii=False)),encoding='utf-8')
if __name__=='__main__':main()
