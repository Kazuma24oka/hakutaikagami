from pathlib import Path
import json,uuid,sys,shutil,xml.etree.ElementTree as ET
from PIL import Image,ImageChops
from verify_service_entry import ROOT,EXE,sha,write,run as parent_run,pairs,sources as parent_sources
BASE=ROOT/'Docs/Design/RecognizedMilitaryV1';OUT=BASE/'runtime-local'
def sources():
 j=parent_sources()
 for p in [Path(__file__),ROOT/'Scripts/check_recognized_military_binding.py',BASE/'README.md',BASE/'contract.json',BASE/'PLAN.md',BASE/'implementation-notes.md',ROOT/'.gitattributes',ROOT/'.gitignore',ROOT/'Docs/Design/PersonalWithdrawalV1/README.md',ROOT/'Docs/Design/PersonalWithdrawalV1/contract.json',ROOT/'Docs/Design/UnitHeldDeliveryV1/PLAN.md',ROOT/'Docs/Design/UnitPostInquiryV1/README.md',ROOT/'Docs/Design/UnitPostInquiryV1/contract.json',ROOT/'Docs/Design/UnitPostRecoveryV1/README.md',ROOT/'Docs/Design/UnitPostRecoveryV1/contract.json',ROOT/'Docs/Design/RoutRegroupV1/README.md',ROOT/'Docs/Design/RoutRegroupV1/contract.json']:
  j[p.relative_to(ROOT).as_posix()]=sha(p)
 return j
def run(args,log,marker=None):
 parent_run(args,log,marker,timeout=1200)
 assert b'RUNTIME_REJECT' not in log.read_bytes(),str(log)
 if any(str(a).startswith('--new-game-test-key=') for a in args):assert log.read_bytes().count(b'GAME_PROCESS_EXIT_RELEASED')==1,str(log)
def pose_pairs(folder,count):
 for n in range(count):
  for a,b in [(f's{n}',f'restored-s{n}'),('final',f'continued-s{n}')]:assert json.loads((folder/(a+'-pose.json')).read_bytes())==json.loads((folder/(b+'-pose.json')).read_bytes()),(folder,a,b,'numeric pose')
 return count*2

def collect(folder,proofs):
 for p in folder.glob('*'):
  if p.is_file() and p.suffix in ['.png','.json','.sha256','.txt','.log']:proofs[p.relative_to(ROOT).as_posix()]=sha(p)
def main():
 OUT.mkdir(parents=True,exist_ok=True);before=sources();proofs={};comparisons={};numeric_poses={};dimensions={};terrain_coverage={}
 run(['dotnet','build','Game/HakutaiKagami.Game.csproj','--no-restore'],OUT/'build.log')
 core_inputs={p:h for p,h in before.items() if p.startswith(('Core/','Tests/')) or p=='Scripts/current_test_contract.json'}
 trx=OUT/'tests/all.trx'
 if '--reuse-tests' in sys.argv:
  prior=json.loads((OUT/'test-manifest.json').read_bytes());assert prior['sources']==core_inputs and prior['trx']==sha(trx) and prior['core']==sha(ROOT/'Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Core.dll')
 else:
  run(['dotnet','test','Tests/HakutaiKagami.Tests.csproj','--logger','trx;LogFileName=all.trx','--results-directory',OUT/'tests'],OUT/'test.log')
 tree=ET.parse(trx);c=tree.find('.//{*}Counters').attrib;assert c['passed']=='1759' and c['failed']=='0' and c['notExecuted']=='0',c
 for name,n in [('MilitaryRecognitionTests.',20),('RepeatedExerciseTests.',21),('MilitaryRenewalTests.',23),('UnitCancellationRecoveryTests.',8),('UnitCancellationTests.',24),('UnitSenderEvidenceTests.',19),('UnitOrderDeadlineTests.',17),('UnitCommandHoldingTests.',31),('UnitHeldDeliveryTests.',18),('UnitReplyRecoveryTests.',21),('UnitInquiryTests.',18),('UnitRetryTests.',17),('RoutRegroupTests.',26),('PersonalWithdrawalTests.',42),('ContactCombatTests.',27),('NewGameCombatTests.',22)]:
  tests=[t for t in tree.findall('.//{*}UnitTestResult') if name in t.attrib['testName']];assert len(tests)==n and all(t.attrib['outcome']=='Passed' for t in tests)
 write(OUT/'test-manifest.json',dict(sources=core_inputs,trx=sha(trx),core=sha(ROOT/'Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Core.dll')))
 cases=['known-withdraw','known-spread','repeat-renewal','renewal-oldmail','cancelnotice-recovery','cancel-moving','evidence-unit','deadline-closed','withdrawal-kofu','held-open']
 for case in cases:
  recognizing=case.startswith('known-');repeating=case.startswith('repeat-');renewing=case.startswith('renewal-');recovering=case.startswith('cancelnotice-');cancelling=case.startswith('cancel-');evidence=case.startswith('evidence-');deadline=case.startswith('deadline-');commanding=case.startswith('command-');holding=case.startswith('held-');replying=case.startswith('reply-');inquiring=case.startswith('inquiry-');retrying=case.startswith('retry-');routing=case.startswith('rout-');personal=case.startswith('withdrawal-');combat=case.startswith('combat-');military=case.startswith('military-');service=case.startswith('service-')
  folder=(OUT if recognizing else ROOT/'Docs/Design/RepeatedExerciseV1/runtime-local' if repeating else ROOT/'Docs/Design/MilitaryRenewalV1/runtime-local' if renewing else ROOT/'Docs/Design/UnitCancellationRecoveryV1/runtime-local' if recovering else ROOT/'Docs/Design/UnitCancellationV1/runtime-local' if cancelling else ROOT/'Docs/Design/UnitSenderEvidenceV1/runtime-local' if evidence else ROOT/'Docs/Design/UnitOrderDeadlineV1/runtime-local' if deadline else ROOT/'Docs/Design/UnitCommandHoldingV1/runtime-local' if commanding else ROOT/'Docs/Design/UnitHeldDeliveryV1/runtime-local' if holding else ROOT/'Docs/Design/UnitReplyRecoveryV1/runtime-local' if replying else ROOT/'Docs/Design/UnitPostInquiryV1/runtime-local' if inquiring else ROOT/'Docs/Design/UnitPostRecoveryV1/runtime-local' if retrying else ROOT/'Docs/Design/RoutRegroupV1/runtime-local' if routing else ROOT/'Docs/Design/PersonalWithdrawalV1/runtime-local' if personal else ROOT/'Docs/Design/ContactCombatV1/runtime-local' if combat else ROOT/'Docs/Design/NewGameMilitaryV1/runtime-local' if military else ROOT/'Docs/Design/ServiceEntryV1/runtime-local' if service else ROOT/'Docs/Design/NewGameFlowV1/runtime-local')/case
  folder.mkdir(parents=True,exist_ok=True);key=uuid.uuid4().hex
  if case=='corrupt':p=folder/key/'library';p.mkdir(parents=True);(p/'officers.json').write_bytes(b'broken')
  for mode in ['check','restore']:
   film=case=='known-withdraw' and mode=='check'
   args=[EXE,'--path',ROOT/'Game','--resolution','1280x900']+(['--fixed-fps','60'] if film else [])+['--','--new-game-test-key='+key,'--new-game-case='+case]+(['--new-game-restore'] if mode=='restore' else [])+(['--recognition-film'] if film else [])
   run(args,folder/(mode+'.log'),'MILITARY_RECOGNITION_PASSED' if recognizing else 'REPEATED_EXERCISE_PASSED' if repeating else 'MILITARY_RENEWAL_PASSED' if renewing else 'UNIT_CANCEL_RECOVERY_PASSED' if recovering else 'UNIT_CANCEL_PASSED' if cancelling else 'UNIT_EVIDENCE_PASSED' if evidence else 'UNIT_DEADLINE_PASSED' if deadline else 'UNIT_COMMAND_PASSED' if commanding else 'UNIT_HELD_PASSED' if holding else 'UNIT_REPLY_PASSED' if replying else 'UNIT_INQUIRY_PASSED' if inquiring else 'UNIT_RETRY_PASSED' if retrying else 'ROUT_REGROUP_PASSED' if routing else 'PERSONAL_WITHDRAWAL_PASSED' if personal else 'CONTACT_COMBAT_PASSED' if combat else 'NEW_MILITARY_PASSED' if military else 'SERVICE_ENTRY_PASSED' if service else 'NEW_GAME_FLOW_PASSED')
  if recognizing:
   for n in range(16):
    rect=json.loads((folder/f's{n}-rect.json').read_bytes())['Surface'];x,y,w,h=map(int,rect);part=Image.open(folder/f's{n}.png').convert('RGB').crop((x,y,x+w,y+h));diff=ImageChops.difference(part,Image.new('RGB',part.size,(54,79,92))).convert('L');fraction=diff.point(lambda value:255 if value>12 else 0).histogram()[255]/(w*h);assert fraction>.15,(case,n,'blank terrain',fraction);terrain_coverage[f'{case}/s{n}']=fraction
  comparisons[case]=pairs(folder,16 if recognizing else 25 if repeating else 21 if renewing else 16 if recovering or cancelling else 15 if evidence else 12 if deadline else 15 if commanding else 14 if holding else 12 if replying or inquiring or retrying or routing or personal else 9 if combat else 17 if military else 6 if service else 4);numeric_poses[case]=pose_pairs(folder,comparisons[case]//2);collect(folder,proofs);dimensions[case]=list(Image.open(folder/'final.png').size);print('PASS exact world/person/3D pairs',case,comparisons[case],flush=True)
 for case,folder,args,marker,n in [
  ('old-military',ROOT/'Docs/Design/CareerMilitaryV1/runtime-local',['--start-selection-military-'],'MILITARY_PASSED',17),
  ('old-combat',ROOT/'Docs/Design/CreatedCampaignV1/runtime-local/combat',['--start-selection-campaign-','--campaign-case=combat'],'CREATED_CAMPAIGN_PASSED',5)]:
  key=uuid.uuid4().hex
  for mode in ['check','restore']:run([EXE,'--path',ROOT/'Game','--resolution','1100x720','--',args[0]+mode]+args[1:]+['--save-key='+key],folder/(mode+'.log'),marker)
  comparisons[case]=pairs(folder,n,False);collect(folder,proofs)
 # Shared-origin changes also require actual mesh/material UV and frame-crossing regression.
 ground=ROOT/'Docs/Design/GroundMaterialV1/runtime-local';key=uuid.uuid4().hex
 for mode in ['check','restore']:run([EXE,'--path',ROOT/'Game','--resolution','1100x720','res://Scenes/ContinuousWorldTrial.tscn','--','--continuous-'+mode,'--ground-material','--case=coverage','--save-key='+key],ground/(mode+'.log'),'GROUND_MATERIAL_PASSED')
 bounds=json.loads((ground/'check.json').read_bytes())['ViewportRect'];x,y,w,h=map(int,bounds);box=(x,y,x+w,y+h);ground_comparisons={}
 for a,b in [(f'coverage-stage-{i}',f'coverage-restored-{i}') for i in range(4)]+[('coverage-stage-0','coverage-back-0')]:
  aa=Image.open(ground/(a+'.png')).convert('RGB');bb=Image.open(ground/(b+'.png')).convert('RGB');assert aa.size==bb.size and ImageChops.difference(aa.crop(box),bb.crop(box)).getbbox() is None,(a,b)
  ground_comparisons[a+'/'+b]=0
 for mode in ['check','restore']:assert json.loads((ground/(mode+'.json')).read_bytes())['UvSamples']>100000
 collect(ground,proofs)
 preserved={}
 for flag,marker,file in [('--smoke-test','SCENE_SMOKE_PASS','godot-foundation.png'),('--tax-smoke-test','TAX_SCENE_PASS','tax-vertical.png'),('--shared-protocol-smoke-test','SHARED_PROTOCOL_GUI_PASS','shared-protocol.png')]:
  p=ROOT/'Docs/Proposals/Evidence'/file;original=p.read_bytes();preserved[file]=sha(p)
  try:
   log=OUT/(flag[2:]+'.log');run([EXE,'--path',ROOT/'Game','--resolution','1100x720','--',flag],log,marker);target=OUT/('legacy-'+file);target.write_bytes(p.read_bytes());proofs[target.relative_to(ROOT).as_posix()]=sha(target);proofs[log.relative_to(ROOT).as_posix()]=sha(log)
  finally:p.write_bytes(original)
  assert sha(p)==preserved[file]
 folder=OUT/'known-withdraw';frames=sorted((folder/'film').glob('observed-*.png'));assert len(frames)==180 and len({sha(p) for p in frames})>=150
 sequence=[]
 def stills(numbers):
  for n in numbers:sequence.extend([folder/(f's{n}.png')]*60)
 stills([0,1]);sequence+=frames;stills([3,4,5,6,7,8,9,10,11,12,13,14,15])
 video_frames=OUT/'video-frames';video_frames.mkdir(exist_ok=True)
 for n,p in enumerate(sequence):shutil.copyfile(p,video_frames/('frame-'+str(n).zfill(4)+'.png'))
 ff=ROOT/'.tools/video-tools/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe';movie=OUT/'recognized-military.mp4'
 run([ff,'-y','-framerate','60','-i',video_frames/'frame-%04d.png','-frames:v',str(len(sequence)),'-vf','pad=ceil(iw/2)*2:ceil(ih/2)*2','-c:v','libx264','-crf','20','-pix_fmt','yuv420p','-movflags','+faststart',movie],OUT/'encode.log')
 run([ff,'-v','error','-i',movie,'-f','null','-'],OUT/'decode.log')
 for p in [movie,trx,OUT/'test-manifest.json',OUT/'build.log',OUT/'test.log',OUT/'encode.log',OUT/'decode.log']+frames:proofs[p.relative_to(ROOT).as_posix()]=sha(p)
 for p in (OUT/'corrections').rglob('*'):
  if p.is_file():proofs[p.relative_to(ROOT).as_posix()]=sha(p)
 binaries={p.relative_to(ROOT).as_posix():sha(p) for p in (ROOT/'Game/.godot/mono/temp/bin/Debug').glob('HakutaiKagami*.dll')};assert sha(ROOT/'Tests/bin/Debug/net8.0/HakutaiKagami.Core.dll')==sha(ROOT/'Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Core.dll')
 assert sources()==before and sum(comparisons.values())==370 and sum(numeric_poses.values())==326
 write(OUT/'run-evidence.json',dict(sources=before,binaries=binaries,proofs=proofs,comparisons=comparisons,numeric_pose_comparisons=numeric_poses,normal_shutdown_release_markers=len(cases)*2,actual_window_dimensions=dimensions,ground_material_comparisons=ground_comparisons,terrain_surface_coverage=terrain_coverage,tests=1759,added=20,core_tests_reused_exact="--reuse-tests" in sys.argv,gpu_processes=29,new_evidence_checkpoints=32,new_evidence_full_world_person_image_pairs=64,preserved_legacy_evidence=preserved,video=movie.relative_to(ROOT).as_posix(),video_frames=len(sequence),video_fps=60,video_seconds=len(sequence)/60,unique_motion_frames=len({sha(p) for p in frames}),limits=['80m is the existing regional engineering coefficient; final forest/terrain occlusion is not accepted.','Exact full-army local reports/formation/hold/contact start require current observation of all three units. Uncertain reports and remote per-unit formation UI remain future work.','Legacy observation-free saves remain explicitly labelled technical display until a versioned observation action or next request opts in. No past observations are invented.','Only the stated29 GPU checks were rerun: two new knowledge cases, representative repeat/renewal/cancel result/moving cancellation/evidence/deadline/withdrawal/held recovery, legacy military/combat, ground material and three smokes. Other older cases were not rerun in this slice.','Observation history storage and nationwide load are not accepted by these local trials.','18second fixed60fps recording contains actual movement and chronological saved-state cuts; it is not a realtime performance benchmark.']))
 print('PASS automatic checks; inspect actual images before --finalize',flush=True)
def finalize():
 j=json.loads((OUT/'run-evidence.json').read_bytes());assert j['sources']==sources()
 for kind in ['binaries','proofs']:
  for p,h in j[kind].items():assert sha(ROOT/p)==h,p
 review=json.loads((OUT/'visual-review.json').read_bytes());assert review['method'] and review['inspected']
 for p,h in review['inspected'].items():assert j['proofs'][p]==h and sha(ROOT/p)==h,p
 j.update(status='PASSED',completion='5/5',visual_review=review);j['proofs'][(OUT/'visual-review.json').relative_to(ROOT).as_posix()]=sha(OUT/'visual-review.json');write(BASE/'verification.json',j);print('PASS5/5 actor knowledge display current source/binary/test/image binding')
if __name__=='__main__':
 if '--finalize' in sys.argv:finalize()
 else:main()
