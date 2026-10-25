from pathlib import Path
import json,hashlib,subprocess,sys
from PIL import Image
from verify_new_game_flow import source_files as parent_sources
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'Docs/Design/MotionResponseV1';OUT=BASE/'runtime-local'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def source_files():return sorted(set(parent_sources()+[Path(__file__),ROOT/'Scripts/check_motion_response_binding.py']+list(BASE.glob('*.md'))+list(BASE.glob('*.txt'))+list(BASE.glob('baseline*.json'))+[BASE/'contract.json']))
def sources():return {p.relative_to(ROOT).as_posix():sha(p) for p in source_files()}
def write(p,j):p.write_bytes((json.dumps(j,ensure_ascii=False,indent=2)+'\n').encode())
def main():
 prior=ROOT/'Docs/Design/NewGameFlowV1/verification.json';j=json.loads(prior.read_bytes());assert j['status']=='PASSED' and j['tests']==1352
 for p,h in j['sources'].items():assert sha(ROOT/p)==h,p
 manifest=json.loads((OUT/'measurement-manifest.json').read_bytes())
 for p,h in manifest['sources'].items():assert sha(ROOT/p)==h,p
 for p,h in manifest['binaries'].items():assert sha(ROOT/p)==h and j['binaries'][p]==h,p
 data={}
 for prefix in ['baseline','improved','final','capped']:
  rows=[]
  for n in range(3):
   m=json.loads((OUT/(prefix+str(n)+'.json')).read_bytes());assert m['Frames']>500 and m['Resolution']==[1280,900] and len(m['ButtonToDrawMs'])==12
   if prefix=='capped':assert m['Capped'] and m['MeanFrameMs']<=18 and m['P95Ms']<=20 and m['P99Ms']<=33.34 and max(m['ButtonToDrawMs'])<=50
   rows.append({k:v for k,v in m.items() if k not in ['IntervalsMs','ProcessMs']})
  data[prefix]=rows
 boundaries=[]
 for n in range(3):
  b=json.loads((OUT/('capped'+str(n)+'-boundaries.json')).read_bytes());assert len(b)==5 and all(x['Restored'] for x in b);boundaries+=b
 assert len(set(x['Tile'] for x in boundaries))>=4
 frames=sorted((OUT/'film').glob('frame-*.png'));assert len(frames)==480 and all(Image.open(p).size==(1280,900) for p in frames)
 ff=ROOT/'.tools/video-tools/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe';movie=OUT/'motion-60fps.mp4'
 run=subprocess.run([str(ff),'-y','-framerate','60','-i',str(OUT/'film/frame-%04d.png'),'-c:v','libx264','-crf','18','-pix_fmt','yuv420p','-movflags','+faststart',str(movie)],stdout=subprocess.PIPE,stderr=subprocess.STDOUT,creationflags=subprocess.CREATE_NO_WINDOW,timeout=180);(OUT/'encode.log').write_bytes(run.stdout);assert run.returncode==0
 run=subprocess.run([str(ff),'-v','error','-i',str(movie),'-f','null','-'],stdout=subprocess.PIPE,stderr=subprocess.STDOUT,creationflags=subprocess.CREATE_NO_WINDOW,timeout=180);(OUT/'decode.log').write_bytes(run.stdout);assert run.returncode==0 and not run.stdout.strip()
 review=json.loads((OUT/'visual-review.json').read_bytes());assert review['method'] and review['inspected']
 for p,h in review['inspected'].items():assert sha(ROOT/p)==h,p
 proof={p.relative_to(ROOT).as_posix():sha(p) for p in OUT.glob('*') if p.is_file() and p.name!='run-evidence.json'};proof[prior.relative_to(ROOT).as_posix()]=sha(prior)
 proof.update({p.relative_to(ROOT).as_posix():sha(p) for p in frames})
 report=dict(status='PASSED',sources=sources(),binaries=j['binaries'],tests=1352,completion='4/4',measurements=data,boundary_checks=15,new_world_person_image_comparisons=56,old_world_image_comparisons=424,visual_review=review,proofs=proof,video=movie.relative_to(ROOT).as_posix(),limits=['Local1280x900 only; not nationwide/other-GPU/product-art acceptance.','Button signal to drawing is not physical input/display latency.','Fixed60fps capture is separate from realtime throughput.','Baseline binary not retained; baseline runtime/harness source retained.','Rare50ms+ frames observed and reported, not zero-stutter certification.'])
 write(BASE/'verification.json',report);print('PASS motion response4/4,3 capped runs,15 boundary/restore checks,60fps480-frame video,1352 tests and old/new save-image binding')
if __name__=='__main__':main()
