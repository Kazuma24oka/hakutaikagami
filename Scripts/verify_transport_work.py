from pathlib import Path
import json,uuid,sys
from verify_service_entry import ROOT,EXE,sha,write,run,pairs
from verify_batched_advance import pose_pairs
BASE=ROOT/'Docs/Design/TransportWorkV1';OUT=BASE/'runtime-local'
def sources():
 files=[]
 for folder,extensions in [('Core',{'.cs','.csproj','.json'}),('Tests',{'.cs','.csproj','.json'}),('Game/Scripts',{'.cs'}),('Game/Scenes',{'.tscn'}),('Game/Data',None),('Game/Assets',None)]:
  files.extend(p for p in (ROOT/folder).rglob('*') if p.is_file() and not any(s in p.parts for s in ['obj','bin']) and p.suffix not in ['.uid','.import'] and (extensions is None or p.suffix in extensions))
 files += [ROOT/'Game/project.godot',Path(__file__),ROOT/'Scripts/check_transport_work_binding.py',ROOT/'Scripts/current_test_contract.json',BASE/'README.md']
 return {p.relative_to(ROOT).as_posix():sha(p) for p in sorted(set(files))}
def main():
 OUT.mkdir(exist_ok=True,parents=True);before=sources();comparisons={};proofs={}
 run(['dotnet','build','Game/HakutaiKagami.Game.csproj','--no-restore'],OUT/'build.log')
 for case,stage,count,marker,res in [('transport-normal','TransportWorkV1',7,'TRANSPORT_WORK_PASSED','1280x900'),('transport-small','TransportWorkV1',7,'TRANSPORT_WORK_PASSED','1100x720'),('transport-military','TransportWorkV1',7,'TRANSPORT_WORK_PASSED','1280x900'),('transport-cancel','TransportWorkV1',7,'TRANSPORT_WORK_PASSED','1280x900'),('supply-normal','SupplyWorkV1',6,'SUPPLY_WORK_PASSED','1280x900'),('military-retainer','NewGameMilitaryV1',17,'NEW_MILITARY_PASSED','1280x900')]:
  key=uuid.uuid4().hex;folder=ROOT/'Docs/Design'/stage/'runtime-local'/case
  for restore in [False,True]:
   log=folder/('transport-restore.log' if restore else 'transport-check.log');run([EXE,'--path',ROOT/'Game','--resolution',res,'--','--new-game-test-key='+key,'--new-game-case='+case]+(['--new-game-restore'] if restore else []),log,marker,timeout=1000)
   text=log.read_bytes();assert b'RUNTIME_REJECT' not in text and b'UI_FAILURE' not in text and text.count(b'GAME_PROCESS_EXIT_RELEASED')==1
  comparisons[case]=dict(world_and_pixels=pairs(folder,count),full_pose=pose_pairs(folder,count))
  for p in folder.glob('*'):
   if p.is_file() and p.suffix in ['.png','.json','.sha256','.txt','.log']:proofs[p.relative_to(ROOT).as_posix()]=sha(p)
 assert sources()==before,'runtime inputs changed during verification'
 write(OUT/'gpu-evidence.json',dict(status='PASSED',sources=before,proofs=proofs,comparisons=comparisons,processes=12,binaries={p.relative_to(ROOT).as_posix():sha(p) for p in (ROOT/'Game/.godot/mono/temp/bin/Debug').glob('HakutaiKagami*.dll')}))
if __name__=='__main__':main()
