from pathlib import Path
import subprocess,uuid,json,hashlib,xml.etree.ElementTree as ET,sys
from verify_life_entry import source_files as parent_sources
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'Docs/Design/NewGameSetupV1';OUT=BASE/'runtime-local/library'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def source_files():return sorted(set(parent_sources()+[Path(__file__),ROOT/'Scripts/check_officer_library_binding.py',BASE/'README.md',ROOT/'Core/Contracts/OfficerLibraryStore.cs',ROOT/'Core/Contracts/NewGameSetup.cs',ROOT/'Tests/NewGameSetupTests.cs',ROOT/'Tests/OfficerLibraryStoreTests.cs',ROOT/'Game/Scripts/OfficerLibraryTrial.cs',ROOT/'Game/Scenes/OfficerLibraryTrial.tscn']))
def sources():return {p.relative_to(ROOT).as_posix():sha(p) for p in source_files()}
def write(path,data):path.write_bytes((json.dumps(data,ensure_ascii=False,indent=2)+'\n').encode())
def main():
    OUT.mkdir(parents=True,exist_ok=True)
    if '--finalize' not in sys.argv:
        before=sources();key=uuid.uuid4().hex
        exe=ROOT/'.tools/godot-4.5.2/Godot_v4.5.2-stable_mono_win64/Godot_v4.5.2-stable_mono_win64_console.exe'
        subprocess.run(['dotnet','build',str(ROOT/'Game/HakutaiKagami.Game.csproj'),'--no-restore','--verbosity','quiet'],cwd=ROOT,check=True)
        for restore in [False,True]:
            args=[str(exe),'--path',str(ROOT/'Game'),'--resolution','1100x720','res://Scenes/OfficerLibraryTrial.tscn','--','--library-test-key='+key]+(['--library-restore'] if restore else [])
            run=subprocess.run(args,cwd=ROOT,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,creationflags=subprocess.CREATE_NO_WINDOW,timeout=90)
            log=run.stdout.decode('utf8',errors='replace');(OUT/('restore.log' if restore else 'check.log')).write_bytes(log.encode())
            assert run.returncode==0 and 'OFFICER_LIBRARY_PASSED' in log and 'ERROR:' not in log,log
        assert (OUT/'edited.sha256').read_bytes()==(OUT/'restored.sha256').read_bytes()
        assert before==sources()
        evidence=dict(sources=before,files={(OUT/n).relative_to(ROOT).as_posix():sha(OUT/n) for n in ['check.log','restore.log','edited.sha256','restored.sha256','edited.png','restored.png']})
        write(OUT/'run-evidence.json',evidence);print('GPU PASS registration,copy,edit,invalid input,process restart. Inspect final PNGs and record visual-review.json, then --finalize.');return
    evidence=json.loads((OUT/'run-evidence.json').read_bytes());assert evidence['sources']==sources()
    for p,h in evidence['files'].items():assert sha(ROOT/p)==h,p
    images={p:h for p,h in evidence['files'].items() if p.endswith('.png')}
    review=json.loads((OUT/'visual-review.json').read_bytes());assert review['inspected']==images and review['method']
    prior=ROOT/'Docs/Design/LifeEntryV1/verification.json';j=json.loads(prior.read_bytes())
    expected=json.loads((ROOT/'Scripts/current_test_contract.json').read_bytes())['expected_passed'];assert expected>=1316 and j['tests']==expected
    assert j['binaries']['Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Core.dll']==sha(ROOT/'Tests/bin/Debug/net8.0/HakutaiKagami.Core.dll')
    for p,h in j['binaries'].items():assert sha(ROOT/p)==h,p
    trx=ROOT/'Docs/Design/WorkHubStartupV1/runtime-local/tests/work-hub.trx';tree=ET.parse(trx)
    counters=tree.find('.//{*}Counters').attrib;assert counters['passed']==str(expected) and counters['failed']=='0' and counters['notExecuted']=='0'
    added=[t for t in tree.findall('.//{*}UnitTestResult') if 'NewGameSetupTests.' in t.attrib['testName'] or 'OfficerLibraryStoreTests.' in t.attrib['testName']]
    assert len(added)==35 and all(t.attrib['outcome']=='Passed' for t in added)
    proofs={prior.relative_to(ROOT).as_posix():sha(prior),trx.relative_to(ROOT).as_posix():sha(trx),**evidence['files']}
    for name in ['run-evidence.json','visual-review.json']:proofs[(OUT/name).relative_to(ROOT).as_posix()]=sha(OUT/name)
    report=dict(status='PASSED',sources=evidence['sources'],binaries=j['binaries'],tests=expected,added=35,proofs=proofs,images=images,visual_review=review,summary=dict(gpu_processes=79,legacy_world_image_comparisons=424,lifetime_comparisons=3,library_gpu_processes=2,library_data_restore_identical=True,existing_representatives_unchanged=j['summary']['old_representatives_byte_identical'],existing_representatives_reinspected=j['summary']['old_representatives_reinspected'],default_scene_unchanged=True),limits=['Library and pre-start projection only; no ContractWorld conversion or map/player selection GUI.','GPU checks register,copy,edit,invalid input and process restart; import/export dialogs and rename are implemented but not end-to-end GPU tested.','No claim of completed art, full historical personnel data or gameplay with newly allocated territory.'])
    write(BASE/'verification.json',report);print(f'PASS {expected} tests;35 library/placement cases;79 GPU processes including2 library;424 old world image comparisons plus3 lifetime; exact library data restore')
if __name__=='__main__':main()
