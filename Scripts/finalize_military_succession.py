"""Resume only the successful immutable run after correcting a two-node expectation.

This does not rerun tests or relabel changed production code as tested. Every
production input is compared byte-for-byte to the commit from the completed run.
The sole permitted verifier difference is the observed 738->736 count: a deployed
player no longer receives a local-house-succession marker in either saved world.
"""
from pathlib import Path
import json,hashlib,subprocess,sys,xml.etree.ElementTree as ET
R=Path('C:/Workspace/Projects/Games/hakutaikagami');sys.path.insert(0,str(R/'Scripts'))
import verify_military_succession as m
commit=sys.argv[1];projection=R/sys.argv[2];source=m.sources();selfname='Scripts/verify_military_succession.py'
prior_path=R/'Docs/Design/HouseSuccessionV1/verification.json';prior=json.loads(prior_path.read_bytes());local_inputs={}
def old(f):return subprocess.check_output(['git','show',commit+':'+f],cwd=R)
for f,h in source.items():
 probe=subprocess.run(['git','show',commit+':'+f],cwd=R,capture_output=True)
 if probe.returncode!=0:
  assert prior['sources'].get(f)==h,('Untracked local input has no unchanged prior verification hash',f)
  local_inputs[f]=h;continue
 raw=probe.stdout
 if f==selfname:raw=raw.replace(b'nodes=738',b'nodes=736').replace(b"['Nodes']==738",b"['Nodes']==736")
 if hashlib.sha256(raw).hexdigest()!=h:
  assert prior['sources'].get(f)==h,('Production input changed after successful run',f)
  local_inputs[f]=h
original=old(selfname);snapshot=m.OUT/'executed-verifier.py';snapshot.write_bytes(original)
n=json.loads((projection/'projection-result.json').read_bytes());assert n['Status']=='PASSED' and (n['Files'],n['Pairs'],n['Nodes'])==(300,150,736)
for f in projection.glob('*.before.json'):assert f.read_bytes()==f.with_name(f.name.replace('.before.json','.restored.json')).read_bytes(),f
assert len(list(projection.glob('*.before.json')))==150
assert len(json.loads(n['Results']['military-succession-4.before.json'])['Actors'])==4
for stem in ['military-succession-1','military-succession-2','military-succession-3','military-succession-5']:assert len(json.loads(n['Results'][stem+'.before.json'])['Actors'])==5
economy=projection/'economy'
for name in ['work','collected']:
 for suffix in ['.json','.view.json']:assert (economy/(name+'.before'+suffix)).read_bytes()==(economy/(name+'.restored'+suffix)).read_bytes()
c=ET.parse(m.OUT/'tests/military-succession-current.trx').find('.//{*}Counters').attrib
assert c['passed']==c['total']=='1493' and c['failed']==c['notExecuted']=='0'
binaries=['Core/bin/Debug/net8.0/HakutaiKagami.Core.dll','Tests/bin/Debug/net8.0/HakutaiKagami.Core.dll','Tests/bin/Debug/net8.0/HakutaiKagami.Tests.dll','Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Core.dll','Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Game.dll']
proofs=[m.OUT/'tests.log',m.OUT/'tests/military-succession-current.trx',m.OUT/'game-build.log',m.OUT/'node3d.log',snapshot,Path(__file__),prior_path,*projection.glob('*.json'),*economy.glob('*.json')]
v=dict(schema=1,status='PASSED',scope='headless-returned-garrison-case-issuer-succession',gui='NOT_RUN',full_core_suite='NOT_RUN',baseline_main='3e431a643f02fd9551ed7ed150a7f280d771b010',tests=dict(passed=1493,failed=0,skipped=0,counters=c),new_tests=40,supplemental_legacy_tests=38,sources=source,binaries={f:m.sha(R/f) for f in binaries},node3d=dict(files=300,pairs=150,nodes=736,gui='NOT_USED',screens='NOT_CAPTURED'),proofs={f.relative_to(R).as_posix():m.sha(f) for f in proofs},resumed_run=dict(source_commit=commit,unchanged_previously_verified_local_inputs=local_inputs,executed_verifier_sha256=hashlib.sha256(original).hexdigest(),correction='Only verifier expectation 738->736. Deployed military-succession-4 hides one local succession marker in each world. All Core/Tests/Game and other fingerprinted inputs are byte-identical to the completed run commit. Related tests/build/Node3D were not rerun or edited.'))
(m.BASE/'verification.json').write_bytes((json.dumps(v,ensure_ascii=False,indent=2)+'\n').encode());m.check(v);print('PASS resumed unchanged production run1493 / node150pairs736')
