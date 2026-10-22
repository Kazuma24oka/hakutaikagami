from pathlib import Path
import json,uuid,xml.etree.ElementTree as ET,sys,shutil
from PIL import Image
from verify_service_entry import ROOT,EXE,sha,write,run as parent_run,pairs,sources as parent_sources
BASE=ROOT/'Docs/Design/NewGameMilitaryV1';OUT=BASE/'runtime-local'
def sources():
 j=parent_sources()
 for p in [Path(__file__),ROOT/'Scripts/check_new_game_military_binding.py',BASE/'README.md',BASE/'contract.json',ROOT/'.gitattributes']:j[p.relative_to(ROOT).as_posix()]=sha(p)
 return j
def run(args,log,marker=None):
 parent_run(args,log,marker,timeout=1200)
 assert b'RUNTIME_REJECT' not in log.read_bytes(),str(log)
def collect(folder,proofs):
 for p in folder.glob('*'):
  if p.is_file() and p.suffix in ['.png','.json','.sha256','.txt','.log']:proofs[p.relative_to(ROOT).as_posix()]=sha(p)
def main():
 OUT.mkdir(parents=True,exist_ok=True);before=sources();proofs={};comparisons={}
 run(['dotnet','build','Game/HakutaiKagami.Game.csproj','--no-restore'],OUT/'build.log')
 if '--reuse-core' in sys.argv:
  cached=json.loads((OUT/'core-test-manifest.json').read_bytes());current={p:h for p,h in before.items() if p.startswith(('Core/','Tests/')) or p=='Scripts/current_test_contract.json'}
  assert current==cached['sources'] and sha(ROOT/'Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Core.dll')==cached['core_sha256'] and sha(OUT/'tests/military.trx')==cached['trx_sha256']
  print('PASS exact unchanged Core/Tests source,Core DLL and full1405-test TRX',flush=True)
 else:
  run(['dotnet','test','Tests/HakutaiKagami.Tests.csproj','--logger','trx;LogFileName=military.trx','--results-directory',OUT/'tests'],OUT/'test.log')
 trx=OUT/'tests/military.trx';tree=ET.parse(trx);c=tree.find('.//{*}Counters').attrib;assert c['passed']=='1405' and c['failed']=='0' and c['notExecuted']=='0',c
 added=[t for t in tree.findall('.//{*}UnitTestResult') if 'NewGameMilitaryTests.' in t.attrib['testName']];assert len(added)==28 and all(t.attrib['outcome']=='Passed' for t in added)
 for case in ['group','historical','individual','absorb','collision','minor','corrupt','service-ronin','service-retainer','service-odawara','military-kofu','military-odawara','military-ronin']:
  military=case.startswith('military-');service=case.startswith('service-');folder=(OUT if military else ROOT/'Docs/Design/ServiceEntryV1/runtime-local' if service else ROOT/'Docs/Design/NewGameFlowV1/runtime-local')/case;folder.mkdir(parents=True,exist_ok=True);key=uuid.uuid4().hex
  if case=='corrupt':p=folder/key/'library';p.mkdir(parents=True);(p/'officers.json').write_bytes(b'broken')
  for mode in ['check','restore']:
   film=case=='military-kofu' and mode=='check'
   args=[EXE,'--path',ROOT/'Game','--resolution','1280x900']+(['--fixed-fps','60'] if film else [])+['--','--new-game-test-key='+key,'--new-game-case='+case]+(['--new-game-restore'] if mode=='restore' else [])+(['--new-military-film'] if film else [])
   run(args,folder/(mode+'.log'),'NEW_MILITARY_PASSED' if military else 'SERVICE_ENTRY_PASSED' if service else 'NEW_GAME_FLOW_PASSED')
  comparisons[case]=pairs(folder,17 if military else 6 if service else 4);collect(folder,proofs);print('PASS full world/person/3D pairs',case,comparisons[case],flush=True)
 folder=ROOT/'Docs/Design/CareerMilitaryV1/runtime-local';key=uuid.uuid4().hex
 for mode in ['check','restore']:run([EXE,'--path',ROOT/'Game','--resolution','1100x720','--','--start-selection-military-'+mode,'--save-key='+key],folder/(mode+'.log'),'MILITARY_PASSED')
 comparisons['old-military']=pairs(folder,17,False);collect(folder,proofs)
 preserved={}
 for flag,marker,file in [('--smoke-test','SCENE_SMOKE_PASS','godot-foundation.png'),('--tax-smoke-test','TAX_SCENE_PASS','tax-vertical.png'),('--shared-protocol-smoke-test','SHARED_PROTOCOL_GUI_PASS','shared-protocol.png')]:
  p=ROOT/'Docs/Proposals/Evidence'/file;original=p.read_bytes();preserved[file]=sha(p)
  try:
   log=OUT/(flag[2:]+'.log');run([EXE,'--path',ROOT/'Game','--resolution','1100x720','--',flag],log,marker);target=OUT/('legacy-'+file);target.write_bytes(p.read_bytes());proofs[target.relative_to(ROOT).as_posix()]=sha(target);proofs[log.relative_to(ROOT).as_posix()]=sha(log)
  finally:p.write_bytes(original)
  assert sha(p)==preserved[file]
 folder=OUT/'military-kofu';frames=sorted((folder/'film').glob('walk-*.png'));assert len(frames)==180 and len({sha(p) for p in frames})==180
 sequence=[]
 for name,n in [('menu',60),('s0',60),('s4',90),('s5',60),('s6',60)]:sequence.extend([folder/(name+'.png')]*n)
 sequence+=frames
 for name,n in [('s9',90),('s10',90),('s12',60),('s13',60),('s14',60),('s15',90),('s16',90)]:sequence.extend([folder/(name+'.png')]*n)
 video_frames=OUT/'video-frames';video_frames.mkdir(exist_ok=True)
 for n,p in enumerate(sequence):shutil.copyfile(p,video_frames/('frame-'+str(n).zfill(4)+'.png'))
 ff=ROOT/'.tools/video-tools/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe';movie=OUT/'new-game-military.mp4'
 run([ff,'-y','-framerate','60','-i',video_frames/'frame-%04d.png','-frames:v',str(len(sequence)),'-vf','pad=ceil(iw/2)*2:ceil(ih/2)*2','-c:v','libx264','-crf','20','-pix_fmt','yuv420p','-movflags','+faststart',movie],OUT/'encode.log')
 run([ff,'-v','error','-i',movie,'-f','null','-'],OUT/'decode.log')
 for p in [movie,trx,OUT/'build.log',OUT/'test.log',OUT/'encode.log',OUT/'decode.log',OUT/'core-test-manifest.json',OUT/'image-first-failure.log',OUT/'first-full-test.log']+frames:
  if p.exists():proofs[p.relative_to(ROOT).as_posix()]=sha(p)
 binaries={p.relative_to(ROOT).as_posix():sha(p) for p in (ROOT/'Game/.godot/mono/temp/bin/Debug').glob('HakutaiKagami*.dll')};assert sha(ROOT/'Tests/bin/Debug/net8.0/HakutaiKagami.Core.dll')==sha(ROOT/'Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Core.dll')
 assert sources()==before and sum(comparisons.values())==228
 write(OUT/'run-evidence.json',dict(sources=before,binaries=binaries,proofs=proofs,comparisons=comparisons,tests=1405,added=28,gpu_processes=31,new_military_checkpoints=51,new_military_full_world_person_image_pairs=102,preserved_legacy_evidence=preserved,video=movie.relative_to(ROOT).as_posix(),video_frames=len(sequence),video_fps=60,video_seconds=len(sequence)/60,limits=['Bounded noncombat placement loan:100 trial soldiers and120m around one home; not historical strength or nationwide military AI.','One loan per life in this iteration; reappointment,combat,losses and wages remain next.','Fixed60fps capture is not a realtime performance benchmark.','Only the stated old frontend/service/military/protocol GUI checks were rerun; other old reports are historical.']))
 print('PASS automatic checks; inspect current images/video keyframes before --finalize',flush=True)
def finalize():
 j=json.loads((OUT/'run-evidence.json').read_bytes());assert j['sources']==sources()
 for kind in ['binaries','proofs']:
  for p,h in j[kind].items():assert sha(ROOT/p)==h,p
 review=json.loads((OUT/'visual-review.json').read_bytes());assert review['method'] and review['inspected']
 for p,h in review['inspected'].items():assert j['proofs'][p]==h and sha(ROOT/p)==h,p
 j.update(status='PASSED',completion='5/5',visual_review=review);j['proofs'][(OUT/'visual-review.json').relative_to(ROOT).as_posix()]=sha(OUT/'visual-review.json');write(BASE/'verification.json',j);print('PASS5/5 new-game military current source/binary/test/image binding')
if __name__=='__main__':
 if '--finalize' in sys.argv:finalize()
 else:main()
