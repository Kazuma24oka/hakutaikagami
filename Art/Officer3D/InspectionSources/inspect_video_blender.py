import bpy,json,sys
from pathlib import Path
out=Path(sys.argv[sys.argv.index('--')+1]);video=sys.argv[sys.argv.index('--')+2];out.mkdir(exist_ok=True)
scene=bpy.context.scene;scene.sequence_editor_create();strips=scene.sequence_editor.strips
strip=strips.new_movie('Review video',video,channel=1,frame_start=1)
width=strip.elements[0].orig_width;height=strip.elements[0].orig_height;frames=strip.frame_duration
scene.render.resolution_x=width;scene.render.resolution_y=height;scene.render.resolution_percentage=100;scene.render.image_settings.file_format='PNG';scene.render.film_transparent=False;scene.view_settings.view_transform='Standard';scene.view_settings.look='None';scene.render.use_sequencer=True
data={'width':width,'height':height,'frames':frames,'source_fps':getattr(strip,'fps',None),'samples':[]}
for fraction in [0.02,0.22,0.44,0.65,0.85]:
 frame=1+int((frames-1)*fraction);scene.frame_set(frame);p=out/('video-frame-'+str(frame)+'.png');scene.render.filepath=str(p);bpy.ops.render.render(write_still=True);data['samples'].append({'frame':frame,'path':str(p)})
(out/'video-inspection.json').write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8');print('VIDEO_REVIEW_READY',width,height,frames)
