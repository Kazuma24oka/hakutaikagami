"""Exact-source whole-Core regression evidence for the household ledger integration."""
from pathlib import Path
import json,xml.etree.ElementTree as ET
from verify_household_military import sources
from verify_service_entry import ROOT,run,sha,write

def main():
    out=ROOT/'Docs/Design/HouseholdMilitaryV1/runtime-local/all-tests';out.mkdir(parents=True,exist_ok=True)
    files=sources();files[Path(__file__).relative_to(ROOT).as_posix()]=sha(Path(__file__));files['Scripts/current_test_contract.json']=sha(ROOT/'Scripts/current_test_contract.json')
    binaries={p:sha(ROOT/p) for p in ['Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Core.dll','Tests/bin/Debug/net8.0/HakutaiKagami.Core.dll','Tests/bin/Debug/net8.0/HakutaiKagami.Tests.dll']}
    assert binaries['Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Core.dll']==binaries['Tests/bin/Debug/net8.0/HakutaiKagami.Core.dll'],'game/test Core differ'
    write(out/'inputs.json',{'sources':files,'binaries':binaries,'status':'RUNNING'})
    run(['dotnet','test','Tests/HakutaiKagami.Tests.csproj','--no-build','--no-restore','--logger','trx;LogFileName=all-current.trx','--results-directory',out,'--nologo','--verbosity','minimal'],out/'test.log',timeout=10800)
    counters=ET.parse(out/'all-current.trx').find('.//{*}Counters').attrib;expected=json.loads((ROOT/'Scripts/current_test_contract.json').read_bytes())['expected_passed']
    assert int(counters['passed'])==expected and counters['failed']=='0' and counters['notExecuted']=='0',counters
    for p,h in files.items():assert sha(ROOT/p)==h,('input changed',p)
    for p,h in binaries.items():assert sha(ROOT/p)==h,('binary changed',p)
    write(out/'execution.json',{'status':'PASSED','sources':files,'binaries':binaries,'counters':counters,'proofs':{p.relative_to(ROOT).as_posix():sha(p) for p in [out/'test.log',out/'all-current.trx']}})
    print('PASS exact current Core',expected,'tests; game/test Core DLL equal',flush=True)
if __name__=='__main__':main()
