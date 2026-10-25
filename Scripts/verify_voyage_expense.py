import json,uuid
from pathlib import Path
from verify_service_entry import ROOT,EXE,sha,write,run,pairs
from verify_batched_advance import pose_pairs
from verify_transport_work import sources as parent_sources
BASE=ROOT/'Docs/Design/TravelExpenseV1';OUT=BASE/'runtime-local'
def sources():
 d=parent_sources()
 for p in [Path(__file__),ROOT/'Scripts/check_voyage_expense_binding.py',BASE/'README.md']:d[p.relative_to(ROOT).as_posix()]=sha(p)
 return d
def main():
 OUT.mkdir(exist_ok=True,parents=True);before=sources();proofs={};comparisons={}
 run(['dotnet','build','Game/HakutaiKagami.Game.csproj','--no-restore'],OUT/'build.log')
 cases=[('voyage-normal','TravelExpenseV1',10,'VOYAGE_EXPENSE_PASSED','1280x900'),('voyage-small','TravelExpenseV1',10,'VOYAGE_EXPENSE_PASSED','1100x720'),('voyage-poor','TravelExpenseV1',10,'VOYAGE_EXPENSE_PASSED','1280x900'),('voyage-cancel','TravelExpenseV1',4,'VOYAGE_EXPENSE_PASSED','1280x900'),('transport-normal','TransportWorkV1',7,'TRANSPORT_WORK_PASSED','1280x900'),('transport-military','TransportWorkV1',7,'TRANSPORT_WORK_PASSED','1280x900'),('supply-normal','SupplyWorkV1',6,'SUPPLY_WORK_PASSED','1280x900'),('military-retainer','NewGameMilitaryV1',17,'NEW_MILITARY_PASSED','1280x900')]
 for case,stage,count,marker,res in cases:
  key=uuid.uuid4().hex;folder=ROOT/'Docs/Design'/stage/'runtime-local'/case
  for restore in [False,True]:
   log=folder/('voyage-restore.log' if restore else 'voyage-check.log');run([EXE,'--path',ROOT/'Game','--resolution',res,'--','--new-game-test-key='+key,'--new-game-case='+case]+(['--new-game-restore'] if restore else []),log,marker,timeout=1400)
   data=log.read_bytes();assert b'RUNTIME_REJECT' not in data and b'UI_FAILURE' not in data and data.count(b'GAME_PROCESS_EXIT_RELEASED')==1
  comparisons[case]=dict(world_and_pixels=pairs(folder,count),full_pose=pose_pairs(folder,count))
  for name in ['final']+[prefix+str(n) for n in range(count) for prefix in ['s','restored-s','continued-s']]:
   for suffix in ['.png','-rect.json','-pose.json','-world.sha256','-info.txt']:
    p=folder/(name+suffix);assert p.is_file();proofs[p.relative_to(ROOT).as_posix()]=sha(p)
  for n in ['voyage-check.log','voyage-restore.log','check-checks.json','restore-checks.json']:
   p=folder/n
   if p.exists():proofs[p.relative_to(ROOT).as_posix()]=sha(p)
 assert sources()==before,'inputs changed during GPU verification'
 write(OUT/'gpu-evidence.json',dict(status='PASSED',sources=before,proofs=proofs,processes=16,comparisons=comparisons,binaries={p.relative_to(ROOT).as_posix():sha(p) for p in (ROOT/'Game/.godot/mono/temp/bin/Debug').glob('HakutaiKagami*.dll')}))
if __name__=='__main__':main()
