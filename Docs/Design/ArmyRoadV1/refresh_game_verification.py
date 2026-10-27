"""Reuse proven Core tests only after exact source/binary checks; rebuild changed UI. No GUI."""
from pathlib import Path
import hashlib,json,os,sys
R=Path(__file__).resolve().parents[3];sys.path.insert(0,str(R/'Scripts'))
from verify_army_road import sources,run,check
D=R/'Docs/Design/ArmyRoadV1';O=D/'runtime-local'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def put(p,v):p.write_bytes((json.dumps(v,ensure_ascii=False,indent=2)+'\n').encode())
v=json.loads((D/'verification.json').read_bytes());assert v['status']=='PASSED' and v['tests']['passed']==1217
current=sources();changed={k for k in current.keys()|v['sources'].keys() if current.get(k)!=v['sources'].get(k)}
if not changed:check(v);print('PASS already current verification');sys.exit(0)
assert changed=={'Game/Scripts/NewGameRuntime.ArmyRoad.cs'},changed
for group in ['proofs','binaries']:
 for path,h in v[group].items():assert sha(R/path)==h,path
core={p:h for p,h in v['sources'].items() if p.startswith(('Core/','Tests/','Game/Data/')) or p=='Scripts/current_test_contract.json'}
assert all(current[p]==h for p,h in core.items())
cache=O/'core-regression.json'
frozen=dict(schema=1,status='PASSED',scope='Actual Core test run retained before UI-only refresh',sources=core,binaries={p:h for p,h in v['binaries'].items() if not p.startswith('Game/')},tests=v['tests'],proofs={p:h for p,h in v['proofs'].items() if p.endswith('.trx') or p.endswith('/tests.log')})
if cache.exists():assert json.loads(cache.read_bytes())==frozen,'Core regression binding changed'
else:put(cache,frozen)
result_path=next(R/p for p in v['proofs'] if p.endswith('/projection-result.json'));projection=result_path.parent
os.environ['HAKUTAI_WORLD_CLASH_QA_DIR']=str(projection);os.environ['HAKUTAI_PROJECTION_EXPECTED_PAIRS']='97'
run(['dotnet','build','Game/HakutaiKagami.Game.csproj','--no-restore','--nologo','--verbosity','quiet'],O/'game-refresh-build.log')
exe=R/'.tools/godot-4.5.2/Godot_v4.5.2-stable_mono_win64/Godot_v4.5.2-stable_mono_win64_console.exe'
run([str(exe),'--headless','--path',str(R/'Game'),'res://Scenes/WorldClashProjectionCheck.tscn'],O/'game-refresh-node3d.log')
a=json.loads(result_path.read_bytes());assert a['Status']=='PASSED' and a['Files']==194 and a['Pairs']==97 and a['Nodes']==408
assert current==sources(),'Source changed during UI refresh'
for p,h in v['binaries'].items():
 if not p.endswith('HakutaiKagami.Game.dll'):assert sha(R/p)==h,p
v['sources']=current;v['binaries']={p:sha(R/p) for p in v['binaries']}
for p in [cache,Path(__file__),O/'game-refresh-build.log',O/'game-refresh-node3d.log',result_path]:v['proofs'][p.relative_to(R).as_posix()]=sha(p)
v['game_refresh']=dict(status='PASSED',changed=sorted(changed),core_regression=cache.relative_to(R).as_posix(),scope='Core/Tests/Game data/contract sources and test binaries unchanged; UI rebuilt and actual headless Node3D checked again. GUI NOT_RUN.')
put(D/'verification.json',v);check(v);print('PASS unchanged1217 Core results + rebuilt UI/headless97pairs; no GUI')
