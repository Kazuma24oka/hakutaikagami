from pathlib import Path
import hashlib,json,subprocess,xml.etree.ElementTree as ET
ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'Docs/Design/StartConfigurationV1'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def sources():
 return sorted([p for d in ['Core','Tests'] for p in (ROOT/d).rglob('*') if p.is_file() and not {'bin','obj'}.intersection(p.relative_to(ROOT/d).parts)]+[Path(__file__),ROOT/'Scripts/current_test_contract.json',BASE/'README.md'])
def main():
 before={p.relative_to(ROOT).as_posix():sha(p) for p in sources()};expected=json.loads((ROOT/'Scripts/current_test_contract.json').read_bytes())['expected_passed'];assert expected==970
 out=BASE/'runtime-local/tests';out.mkdir(parents=True,exist_ok=True)
 subprocess.run(['dotnet','test','Tests/HakutaiKagami.Tests.csproj','--no-restore','--verbosity','minimal','--logger','trx;LogFileName=start-all.trx','--results-directory',str(out)],cwd=ROOT,check=True)
 ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'};trx=out/'start-all.trx';tree=ET.parse(trx);counts=tree.find('.//t:Counters',ns).attrib
 assert int(counts['passed'])==expected and int(counts['failed'])==0 and int(counts['notExecuted'])==0
 targeted=[x for x in tree.findall('.//t:UnitTestResult',ns) if 'StartConfigurationTests.' in x.attrib['testName']];assert len(targeted)==25 and all(x.attrib['outcome']=='Passed' for x in targeted)
 assert before=={p.relative_to(ROOT).as_posix():sha(p) for p in sources()}
 dlls=[ROOT/'Core/bin/Debug/net8.0/HakutaiKagami.Core.dll',ROOT/'Tests/bin/Debug/net8.0/HakutaiKagami.Tests.dll']
 report=dict(status='PASSED',tests=expected,added=25,sources=before,binaries={p.relative_to(ROOT).as_posix():sha(p) for p in dlls},proofs={trx.relative_to(ROOT).as_posix():sha(trx)},ui_scope='No UI or existing session behavior changed; prior render reports are historical, not rerun.')
 (BASE/'verification.json').write_bytes((json.dumps(report,ensure_ascii=False,indent=2)+'\n').encode());print('PASS start configuration, full970 and25 role/budget/save cases bound to exact binaries')
if __name__=='__main__':main()
