"""Run the actual default Godot entrance; no desktop input tools are used."""
from pathlib import Path
import argparse,json,uuid,hashlib
from verify_service_entry import ROOT,EXE,run,pairs,sha,write

def sources():
    files=[Path(__file__),ROOT/'Scripts/verify_service_entry.py',ROOT/'Game/project.godot']
    for folder,extensions in [('Core',{'.cs','.csproj'}),('Tests',{'.cs','.csproj','.json'}),('Game/Scripts',{'.cs'}),('Game/Scenes',{'.tscn'}),('Game/Data',{'.json'}),('Game/Assets',{'.glb','.png','.gdshader'})]:
        files.extend(p for p in (ROOT/folder).rglob('*') if p.is_file() and p.suffix in extensions and not {'bin','obj','.godot'}.intersection(p.parts))
    return {p.relative_to(ROOT).as_posix():sha(p) for p in sorted(set(files))}

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--case',choices=['housearmy-normal','housearmy-loss'],required=True);parser.add_argument('--check-only',action='store_true');parser.add_argument('--restore-only',action='store_true');a=parser.parse_args()
    folder=ROOT/'Docs/Design/HouseholdMilitaryV1/runtime-local'/a.case;folder.mkdir(parents=True,exist_ok=True)
    manifest=folder/'execution.json';before=sources();dll=ROOT/'Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Core.dll'
    if a.restore_only:
        prior=json.loads(manifest.read_bytes());assert prior['sources']==before and prior['core_dll']==sha(dll),'sources or Core DLL changed since check';key=prior['key']
    else:key=uuid.uuid4().hex
    args=[EXE,'--path',ROOT/'Game','--resolution','1280x900','--','--new-game-test-key='+key,'--new-game-case='+a.case]
    if not a.restore_only:
        run(args,folder/'check.log','HOUSEHOLD_MILITARY_PASSED',timeout=1200)
        write(manifest,{'case':a.case,'key':key,'sources':before,'core_dll':sha(dll),'status':'CHECK_PASSED'})
    if a.check_only:return
    run(args+['--new-game-restore'],folder/'restore.log','HOUSEHOLD_MILITARY_PASSED',timeout=1800)
    assert sources()==before,'inputs changed during actual game run'
    count=pairs(folder,7)
    for n in range(7):
        for left,right in [(f's{n}',f'restored-s{n}'),('final',f'continued-s{n}')]:
            assert (folder/(left+'-pose.json')).read_bytes()==(folder/(right+'-pose.json')).read_bytes(),(left,right,'complete display pose')
    proof={p.relative_to(ROOT).as_posix():sha(p) for p in folder.iterdir() if p.is_file() and p.suffix in {'.png','.json','.sha256','.txt','.log'} and p.name!='execution.json'}
    write(manifest,{'case':a.case,'key':key,'sources':before,'core_dll':sha(dll),'status':'PASSED','processes':2,'savepoints':7,'comparisons':count,'scope':'World, information text, rendered 3D/information pixels, complete display pose; same future from every save. Existing other-house army journal unchanged.','proofs':proof})
    print('PASS',a.case,count,'world/pixel/pose comparisons',flush=True)
if __name__=='__main__':main()
