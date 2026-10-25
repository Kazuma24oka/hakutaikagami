"""Scoped physical unit IDs must also survive delivery, retries and cancellation."""
from pathlib import Path
import uuid,json
from verify_household_military import sources
from verify_service_entry import ROOT,EXE,run,pairs,sha,write

def main():
    before=sources();before[Path(__file__).relative_to(ROOT).as_posix()]=sha(Path(__file__));proofs={};results=[]
    for case,module,marker in [('directive-main-housearmy','UnitDirectiveV1','UNIT_DIRECTIVE_PASSED'),('dircancel-race-housearmy','UnitDirectiveCancellationV1','UNIT_DIRECTIVE_CANCELLATION_PASSED')]:
        folder=ROOT/'Docs/Design'/module/'runtime-local'/case;folder.mkdir(parents=True,exist_ok=True);key=uuid.uuid4().hex
        args=[EXE,'--path',ROOT/'Game','--resolution','1280x900','--','--new-game-test-key='+key,'--new-game-case='+case]
        for mode in ['check','restore']:
            log=folder/(mode+'.log');run(args+(['--new-game-restore'] if mode=='restore' else []),log,marker,timeout=1800);assert b'RUNTIME_REJECT' not in log.read_bytes(),log
        count=pairs(folder,8)
        for n in range(8):
            for a,b in [(f's{n}',f'restored-s{n}'),('final',f'continued-s{n}')]:assert (folder/(a+'-pose.json')).read_bytes()==(folder/(b+'-pose.json')).read_bytes(),(case,a,b,'pose')
        pose=json.loads((folder/'s0-pose.json').read_bytes());assert all(u['Id'].startswith('unit:military/') for u in pose['Military']['Units']),'legacy unscoped IDs were used'
        proofs.update({p.relative_to(ROOT).as_posix():sha(p) for p in folder.iterdir() if p.is_file() and p.suffix in {'.json','.png','.sha256','.txt','.log'}})
        results.append({'case':case,'module':module,'processes':2,'savepoints':8,'comparisons':count,'key':key});print('PASS scoped unit regression',case,count,flush=True)
    for p,h in before.items():assert sha(ROOT/p)==h,p
    write(ROOT/'Docs/Design/HouseholdMilitaryV1/runtime-local/scoped-unit-regression.json',{'status':'PASSED','sources':before,'core_dll':sha(ROOT/'Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Core.dll'),'cases':results,'proofs':proofs})
if __name__=='__main__':main()
