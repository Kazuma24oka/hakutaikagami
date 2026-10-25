from pathlib import Path
import json,hashlib,subprocess
from PIL import Image,ImageChops
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'Docs/Design/LandIndexV1/runtime-local'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 proof=json.loads((OUT/'pre-index-save-compatibility.json').read_bytes());folder=ROOT/proof['slot_root'];before={p.relative_to(ROOT).as_posix():sha(p) for p in folder.rglob('*') if p.is_file()};assert before==proof['source_save_files']
 baseline_size=Image.open(OUT/'move-s0.png').size
 exe=ROOT/'.tools/godot-4.5.2/Godot_v4.5.2-stable_mono_win64/Godot_v4.5.2-stable_mono_win64_console.exe';scene=ROOT/'Docs/Design/ContinuousWorldV1/runtime-local'
 run=subprocess.run([str(exe),'--path',str(ROOT/'Game'),'--resolution',str(baseline_size[0])+'x'+str(baseline_size[1]),'res://Scenes/ContinuousWorldTrial.tscn','--','--continuous-restore','--case=move','--save-key='+folder.name.replace('-','')],cwd=ROOT,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,creationflags=subprocess.CREATE_NO_WINDOW,timeout=180)
 log=run.stdout.decode('utf8',errors='replace');(OUT/'pre-index-final-restore.log').write_bytes(log.encode());assert run.returncode==0 and 'CONTINUOUS_CHECK_PASSED move' in log and 'ERROR:' not in log,log;assert before=={p.relative_to(ROOT).as_posix():sha(p) for p in folder.rglob('*') if p.is_file()}
 x,y,w,h=map(int,json.loads((scene/'move-restore-checks.json').read_bytes())['ViewportRect']);box=(x,y,x+w,y+h);same={};images={}
 for i in range(3):
  for a,b in [('s'+str(i),'restored-s'+str(i)),('final','continued-s'+str(i))]:
   pa=OUT/('move-'+a+'.png');source=scene/('move-'+b+'.png');pb=OUT/('final-core-move-'+b+'.png');pb.write_bytes(source.read_bytes());ia=Image.open(pa).convert('RGB');ib=Image.open(pb).convert('RGB');assert ia.size==ib.size
   delta=sum(p!=(0,0,0) for p in ImageChops.difference(ia.crop(box),ib.crop(box)).get_flattened_data());assert delta==0,(a,b,delta);same[a+'/'+b]=delta
   for p in [pa,pb]:images[p.relative_to(ROOT).as_posix()]=sha(p)
 result={'status':'PASSED','source_save_files':before,'comparisons':same,'images':images,'slot_root':proof['slot_root'],'rules_and_save_version_changed':False};(OUT/'pre-index-final-compatibility.json').write_bytes((json.dumps(result,indent=2)+'\n').encode());print('PASS original save files unchanged and six old-to-new restored/continued images')
if __name__=='__main__':main()
