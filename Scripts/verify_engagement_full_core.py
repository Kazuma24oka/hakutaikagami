"""One full regression for shared save/enum changes, using the exact Godot-tested DLLs."""
from pathlib import Path
import json,subprocess,xml.etree.ElementTree as ET
from verify_engagement_delivery import ROOT,BASE,OUT,check,sources
from verify_service_entry import sha

def main():
    game=json.loads((BASE/'verification.json').read_bytes());check(game)
    initial=sources();initial[Path(__file__).relative_to(ROOT).as_posix()]=sha(Path(__file__))
    expected=json.loads((ROOT/'Scripts/current_test_contract.json').read_bytes())['expected_passed']
    folder=OUT/'full-core';folder.mkdir(parents=True,exist_ok=True)
    command=['dotnet','test','Tests/HakutaiKagami.Tests.csproj','--no-build','--no-restore','--logger',
             'trx;LogFileName=all-current.trx','--results-directory',str(folder),'--nologo','--verbosity','minimal']
    (folder/'inputs.json').write_bytes((json.dumps(dict(status='RUNNING',sources=initial,binaries=game['binaries']),indent=2)+'\n').encode())
    proc=subprocess.run(command,cwd=ROOT,capture_output=True,creationflags=subprocess.CREATE_NO_WINDOW,timeout=10800)
    (folder/'test.log').write_bytes(proc.stdout+proc.stderr)
    assert proc.returncode==0,(folder/'test.log')
    c=ET.parse(folder/'all-current.trx').find('.//{*}Counters').attrib
    assert int(c['passed'])==expected and c['failed']==c['notExecuted']=='0',c
    after=sources();after[Path(__file__).relative_to(ROOT).as_posix()]=sha(Path(__file__))
    assert initial==after,'sources changed during full regression'
    for p,h in game['binaries'].items():assert sha(ROOT/p)==h,p
    v=dict(schema=1,status='PASSED',scope='full-core-exact-godot-tested-binaries',
           passed=expected,failed=0,skipped=0,counters=c,sources=initial,binaries=game['binaries'],
           proofs={p.relative_to(ROOT).as_posix():sha(p) for p in [folder/'test.log',folder/'all-current.trx',folder/'inputs.json']})
    (BASE/'full-core.json').write_bytes((json.dumps(v,ensure_ascii=False,indent=2)+'\n').encode('utf-8'))
    print('PASS full Core',expected,'tests; same sources/DLLs as the actual Godot verification',flush=True)
if __name__=='__main__':main()
