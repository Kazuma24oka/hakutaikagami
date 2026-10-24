from pathlib import Path
import json,sys,uuid,shutil,xml.etree.ElementTree as ET
from PIL import Image,ImageChops
from verify_batched_advance import ROOT,EXE,run,pairs,pose_pairs,sha,write,collect,sources as parent_sources
BASE=ROOT/'Docs/Design/TerrainCacheV1';OUT=BASE/'runtime-local'
def sources():
 j=parent_sources()
 for p in [Path(__file__),ROOT/'Scripts/check_terrain_cache_binding.py',BASE/'README.md',BASE/'contract.json']:j[p.relative_to(ROOT).as_posix()]=sha(p)
 return j

def main():
 before=sources();proofs={};comparison={};numeric={};baseline={};ground_cmp={};observations={}
 previous=ROOT/'Docs/Design/BatchedAdvanceV1/verification.json';old=json.loads(previous.read_bytes());allowed=set(json.loads((BASE/'contract.json').read_bytes())['allowed_source_changes']);delta={p for p in set(before)|set(old['sources']) if before.get(p)!=old['sources'].get(p)};assert delta<=allowed,sorted(delta-allowed)
 proofs[previous.relative_to(ROOT).as_posix()]=sha(previous)
 run(['dotnet','build','Game/HakutaiKagami.Game.csproj','--no-restore'],OUT/'build.log')
 core_inputs={p:h for p,h in before.items() if p.startswith(('Core/','Tests/','Game/Data/')) or p=='Scripts/current_test_contract.json'}
 manifest=ROOT/'Docs/Design/BatchedAdvanceV1/runtime-local/test-manifest.json';m=json.loads(manifest.read_bytes());trx=ROOT/'Docs/Design/ResponsiveClockV1/runtime-local/tests/all.trx'
 assert core_inputs==m['sources'] and len(core_inputs)==304 and sha(trx)==m['trx'] and sha(ROOT/'Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Core.dll')==m['core']==sha(ROOT/'Tests/bin/Debug/net8.0/HakutaiKagami.Core.dll')
 counters=ET.parse(trx).find('.//{*}Counters').attrib;assert counters['passed']=='2055' and counters['failed']=='0' and counters['notExecuted']=='0'
 write(OUT/'test-manifest.json',dict(sources=core_inputs,trx=sha(trx),core=m['core'],reused_from=manifest.relative_to(ROOT).as_posix()))
 for p in [manifest,trx,OUT/'test-manifest.json']:proofs[p.relative_to(ROOT).as_posix()]=sha(p)
 run([sys.executable,ROOT/'Scripts/check_session_generations.py'],OUT/'ownership.log','SESSION_GENERATION_OWNERSHIP_PASSED')
 # Same exact stored public inputs as before the cache change. Copy new outputs before fresh runs.
 for case in ['batch-10','batch-60','batch-360']:
  original=OUT/'baseline'/case;live=ROOT/'Docs/Design/BatchedAdvanceV1/runtime-local'/case;key=(original/'baseline-key.txt').read_bytes().decode()
  run([EXE,'--path',ROOT/'Game','--resolution','1280x900','--','--new-game-test-key='+key,'--new-game-case='+case,'--new-game-restore'],OUT/(case+'-baseline-restore.log'),'BATCHED_ADVANCE_PASSED')
  after=OUT/'baseline-restored'/case;after.mkdir(parents=True,exist_ok=True)
  for p in original.iterdir():
   if p.is_file():shutil.copyfile(p,after/p.name)
  for p in live.iterdir():
   if p.is_file() and p.suffix in ['.png','.json','.sha256','.txt','.log']:shutil.copyfile(p,after/p.name)
  # Forward s/final remain exactly the preserved pre-cache bytes.
  for prefix in ['s0','s1','final']:
   for suffix in ['.png','-world.sha256','-pose.json','-rect.json','-info.txt']:
    assert (after/(prefix+suffix)).read_bytes()==(original/(prefix+suffix)).read_bytes(),(case,prefix,suffix)
  baseline[case]=pairs(after,2);numeric['baseline/'+case]=pose_pairs(after,2)
  a=json.loads((original/'check-batch-observation.json').read_bytes());b=json.loads((after/'restore-batch-observation.json').read_bytes());assert a['Targets']==b['Targets']
  observations['baseline/'+case]=b;collect(original,proofs);collect(after,proofs)
  print('PASS identical pre-cache public inputs,world,pixels,numeric',case,flush=True)
 for case in ['batch-10','batch-60','batch-360','batch-switch','batch-save','batch-notice']:
  live=ROOT/'Docs/Design/BatchedAdvanceV1/runtime-local'/case;folder=OUT/'current'/case;folder.mkdir(parents=True,exist_ok=True);key=uuid.uuid4().hex
  for mode in ['check','restore']:
   run([EXE,'--path',ROOT/'Game','--resolution','1280x900','--','--new-game-test-key='+key,'--new-game-case='+case]+(['--new-game-restore'] if mode=='restore' else []),folder/(mode+'.log'),'BATCHED_ADVANCE_PASSED')
  for p in live.iterdir():
   if p.is_file() and p.suffix in ['.png','.json','.sha256','.txt'] and p.name not in ['baseline-key.txt']:shutil.copyfile(p,folder/p.name)
  comparison[case]=pairs(folder,2);numeric[case]=pose_pairs(folder,2)
  if case in ['batch-10','batch-60','batch-360']:
   a=json.loads((folder/'check-batch-observation.json').read_bytes());b=json.loads((folder/'restore-batch-observation.json').read_bytes());assert a['Targets']==b['Targets'];assert a['TerrainUpdatesMilliseconds'] and b['TerrainUpdatesMilliseconds'];observations[case]=a
  collect(folder,proofs);print('PASS current batch renderer',case,flush=True)
 for case,live,args,marker,n in [
  ('old-military',ROOT/'Docs/Design/CareerMilitaryV1/runtime-local',['--start-selection-military-'],'MILITARY_PASSED',17),
  ('old-combat',ROOT/'Docs/Design/CreatedCampaignV1/runtime-local/combat',['--start-selection-campaign-','--campaign-case=combat'],'CREATED_CAMPAIGN_PASSED',5)]:
  key=uuid.uuid4().hex;folder=OUT/case;folder.mkdir(parents=True,exist_ok=True)
  for mode in ['check','restore']:run([EXE,'--path',ROOT/'Game','--resolution','1100x720','--',args[0]+mode]+args[1:]+['--save-key='+key],folder/(mode+'.log'),marker)
  for p in live.iterdir():
   if p.is_file() and p.suffix in ['.png','.json','.sha256','.txt']:shutil.copyfile(p,folder/p.name)
  comparison[case]=pairs(folder,n,False);collect(folder,proofs)
 ground=ROOT/'Docs/Design/GroundMaterialV1/runtime-local';folder=OUT/'ground';folder.mkdir(parents=True,exist_ok=True);key=uuid.uuid4().hex
 for mode in ['check','restore']:run([EXE,'--path',ROOT/'Game','--resolution','1100x720','res://Scenes/ContinuousWorldTrial.tscn','--','--continuous-'+mode,'--ground-material','--case=coverage','--save-key='+key],folder/(mode+'.log'),'GROUND_MATERIAL_PASSED')
 for p in ground.iterdir():
  if p.is_file() and p.suffix in ['.png','.json','.sha256','.txt']:shutil.copyfile(p,folder/p.name)
 x,y,w,h=map(int,json.loads((folder/'check.json').read_bytes())['ViewportRect']);box=(x,y,x+w,y+h)
 for a,b in [(f'coverage-stage-{i}',f'coverage-restored-{i}') for i in range(4)]+[('coverage-stage-0','coverage-back-0')]:
  aa=Image.open(folder/(a+'.png')).convert('RGB');bb=Image.open(folder/(b+'.png')).convert('RGB');assert aa.size==bb.size and ImageChops.difference(aa.crop(box),bb.crop(box)).getbbox() is None;ground_cmp[a+'/'+b]=0
 for mode in ['check','restore']:assert json.loads((folder/(mode+'.json')).read_bytes())['UvSamples']>100000
 collect(folder,proofs)
 preserved={}
 for flag,marker,file in [('--smoke-test','SCENE_SMOKE_PASS','godot-foundation.png'),('--tax-smoke-test','TAX_SCENE_PASS','tax-vertical.png'),('--shared-protocol-smoke-test','SHARED_PROTOCOL_GUI_PASS','shared-protocol.png')]:
  p=ROOT/'Docs/Proposals/Evidence'/file;original=p.read_bytes();preserved[file]=sha(p)
  try:
   log=OUT/(flag[2:]+'.log');run([EXE,'--path',ROOT/'Game','--resolution','1100x720','--',flag],log,marker);target=OUT/('legacy-'+file);target.write_bytes(p.read_bytes());proofs[target.relative_to(ROOT).as_posix()]=sha(target)
  finally:p.write_bytes(original)
  assert sha(p)==preserved[file]
 for p in OUT.glob('*.log'):proofs[p.relative_to(ROOT).as_posix()]=sha(p)
 proofs[(OUT/'isolated-lab-evidence.json').relative_to(ROOT).as_posix()]=sha(OUT/'isolated-lab-evidence.json')
 binaries={p.relative_to(ROOT).as_posix():sha(p) for p in (ROOT/'Game/.godot/mono/temp/bin/Debug').glob('HakutaiKagami*.dll')}
 assert sources()==before and sum(comparison.values())==68 and sum(baseline.values())==12 and sum(numeric.values())==36
 write(OUT/'run-evidence.json',dict(sources=before,binaries=binaries,proofs=proofs,comparisons=comparison,baseline_comparisons=baseline,numeric_pose_comparisons=numeric,ground_material_comparisons=ground_cmp,observations=observations,tests=2055,added=0,core_tests_reused_exact=True,core_inputs=304,gpu_processes=24,normal_shutdown_release_markers=15,preserved_legacy_evidence=preserved,limits=['24 current GPU executions:3 exact pre-cache restores,12 fresh batch,4 old military/combat,2 ground/material,3 smokes. Historical62 not rerun.','Core2055 reused only exact304inputs/TRX/DLL. No Core/save schema changes.','1000 stationary synthetic people plus selected travel; not national simultaneous simulation. No guaranteed fps/rate.','Isolated lab before/after timing is diagnostic evidence, separate from actual current observations.','Identical catalog color/repeat cached by finite skin/kind; geometry,UV/picks/origin unchanged; nullable timer records duration without changing world.']))
 print('PASS automatic current24GPU;direct visual review required',flush=True)
def finalize():
 j=json.loads((OUT/'run-evidence.json').read_bytes());assert j['sources']==sources()
 for kind in ['binaries','proofs']:
  for p,h in j[kind].items():assert sha(ROOT/p)==h,p
 review=json.loads((OUT/'visual-review.json').read_bytes());assert review['method'] and review['inspected']
 for p,h in review['inspected'].items():assert j['proofs'][p]==h==sha(ROOT/p),p
 j.update(status='PASSED',completion='5/5',visual_review=review);j['proofs'][(OUT/'visual-review.json').relative_to(ROOT).as_posix()]=sha(OUT/'visual-review.json');write(BASE/'verification.json',j);print('PASS5/5 terrain cache current24GPU')
if __name__=='__main__':
 if '--finalize' in sys.argv:finalize()
 else:main()
