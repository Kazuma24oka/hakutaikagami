"""家督争い・後見・他家の養子工程末尾の関連5件・保存・旧保存・通常入口を一度に照合する。"""
from pathlib import Path
import argparse,hashlib,json,os,re,subprocess,xml.etree.ElementTree as ET
R=Path(__file__).resolve().parents[1];D=R/'Docs/Design/GuardianshipV1';O=D/'runtime-local'
G=Path('C:/Workspace/Projects/Games/hakutaikagami/.tools/godot-4.5.2/Godot_v4.5.2-stable_mono_win64/Godot_v4.5.2-stable_mono_win64_console.exe')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def put(p,v):p.write_bytes((json.dumps(v,ensure_ascii=False,indent=2)+'\n').encode('utf-8'))
def sources():return {p.relative_to(R).as_posix():sha(p) for base in ['Core','Tests','Game'] for p in sorted((R/base).rglob('*.cs')) if not {'bin','obj','.godot'}.intersection(p.parts)}
def run(args,log,env=None):
 with log.open('wb') as f:subprocess.run(args,cwd=R,stdout=f,stderr=subprocess.STDOUT,env=env,check=True,creationflags=subprocess.CREATE_NO_WINDOW,timeout=14400)
def check(v):
 assert v['status']=='PASSED' and v['sources']==sources()
 for group in ['proofs','binaries']:
  for p,h in v[group].items():assert sha(R/p)==h,p
 c=ET.parse(R/v['trx']).find('.//{*}Counters').attrib
 assert int(c['total'])==int(c['passed'])==5 and int(c['failed'])==int(c['notExecuted'])==0
 assert v['legacy_count']==3 and v['recovery_phases']==2 and v['game_internal_input']=='PASSED' and v['self_render']=='PASSED'
def main():
 ap=argparse.ArgumentParser();ap.add_argument('--collect-only',action='store_true');ap.add_argument('--check-only',action='store_true');ap.add_argument('--run-id',default='critical');a=ap.parse_args()
 assert re.fullmatch(r'[a-zA-Z0-9_-]+',a.run_id),'実行先は単一のフォルダ名にしてください'
 if a.check_only:check(json.loads((D/'verification.json').read_bytes()));print('家督争い・後見・他家の養子検証記録の照合成功');return
 O.mkdir(exist_ok=True);full=O/a.run_id;full.mkdir(exist_ok=True)
 if not a.collect_only:
  assert not any(full.iterdir()),'既存の証跡は上書きしません。新しい --run-id を指定してください'
  run(['dotnet','build','Tests/HakutaiKagami.Tests.csproj','--no-restore','--verbosity','quiet'],O/'build-tests.log')
  run(['dotnet','build','Game/HakutaiKagami.Game.csproj','--no-restore','--verbosity','quiet'],O/'build-game.log')
  put(full/'run-inputs.json',{k:v for k,v in sources().items() if k.startswith(('Core/','Tests/'))})
  env=dict(os.environ,HAKUTAI_WORLD_CLASH_QA_DIR=str(full))
  print('家督争い・後見・他家の養子の関連5件テストを開始（代表復旧は通常入口で確認）',flush=True)
  run(['dotnet','test','Tests/HakutaiKagami.Tests.csproj','--no-build','--no-restore','--filter','FullyQualifiedName~GuardianshipTests|FullyQualifiedName~DistantChildLearns|FullyQualifiedName~AdultAdoptedFromAnotherHouse','--logger','trx;LogFileName=full.trx','--results-directory',str(full),'--verbosity','quiet'],full/'run.log',env)
 before=sources();assert json.loads((full/'run-inputs.json').read_bytes())=={k:v for k,v in before.items() if k.startswith(('Core/','Tests/'))}
 trx=full/'full.trx';c=ET.parse(trx).find('.//{*}Counters').attrib;assert c['total']==c['passed']=='5' and c['failed']==c['notExecuted']=='0',c
 proofs={}
 def proof(p):proofs[p.relative_to(R).as_posix() if p.is_relative_to(R) else str(p)]=sha(p)
 for p in [trx,full/'run.log',full/'run-inputs.json',O/'build-tests.log',O/'build-game.log']:proof(p)
 previous=R/'Docs/Design/WorldTimeV1';old=json.loads((previous/'verification.json').read_bytes());old_paths={str((R/k).resolve()):h for k,h in old['proofs'].items()}
 files=json.loads((previous/'runtime-local/legacy-probe/inputs.json').read_bytes())
 for prefix,count in [('time-control',6),('time-travel',3)]:files.extend(str(previous/f'runtime-local/full-fixed/{prefix}-{n}.before.json') for n in range(count))
 files=files[:3];assert len(files)==len(set(files))==3
 for f in files:p=Path(f);assert sha(p)==old_paths[str(p.resolve())];proof(p)
 probe=O/'legacy-probe';probe.mkdir(exist_ok=True);core=R/'Core/bin/Debug/net8.0/HakutaiKagami.Core.dll'
 (probe/'probe.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings></PropertyGroup><ItemGroup><Reference Include="HakutaiKagami.Core"><HintPath>'+core.as_posix()+'</HintPath></Reference></ItemGroup></Project>',encoding='utf-8')
 (probe/'Program.cs').write_text('''using HakutaiKagami.Core.Contracts;
using System.Text.Json;
var files=JsonSerializer.Deserialize<string[]>(File.ReadAllBytes(args[0]))!;
foreach(var p in files){var raw=File.ReadAllBytes(p);var s=new WorldSession(ContractJson.Read<ContractWorld>(raw));if(!raw.SequenceEqual(ContractJson.Write(s.DiagnosticSnapshot())))throw new Exception("旧保存の不一致: "+p);}
Console.WriteLine(JsonSerializer.Serialize(new{Status="PASSED",Legacy=files.Length}));
''',encoding='utf-8')
 put(probe/'inputs.json',files);run(['dotnet','build',str(probe/'probe.csproj'),'--verbosity','quiet'],probe/'build.log');assert sha(probe/'bin/Debug/net8.0/HakutaiKagami.Core.dll')==sha(core)
 run(['dotnet',str(probe/'bin/Debug/net8.0/probe.dll'),str(probe/'inputs.json')],probe/'run.log')
 for p in probe.glob('*'):
  if p.is_file():proof(p)
 print('関連5件・旧保存成功。通常入口の内部操作・自前描画を最終確認',flush=True)
 if not a.collect_only:
  run([str(G),'--path','Game','--rendering-method','gl_compatibility','--audio-driver','Dummy','res://Scenes/GuardianshipCheck.tscn'],O/'ui-final.log')
 assert (O/'ui-final.log').is_file()  # 通常画面は直前に実行した証跡を収集し、理由なく再実行しない
 ui=json.loads((O/'ui/result.json').read_bytes());assert ui['Status']=='PASSED';assert 'GUARDIANSHIP_UI_PASS' in (O/'ui-final.log').read_text(encoding='utf-8-sig') and 'ERROR:' not in (O/'ui-final.log').read_text(encoding='utf-8-sig')
 assert (O/'ui/before-pose.json').read_bytes()==(O/'ui/restored-pose.json').read_bytes()
 for p in [O/'ui-final.log',*sorted((O/'ui').glob('*'))]:
  if p.is_file():proof(p)
 assert before==sources()
 binaries=['Core/bin/Debug/net8.0/HakutaiKagami.Core.dll','Tests/bin/Debug/net8.0/HakutaiKagami.Core.dll','Tests/bin/Debug/net8.0/HakutaiKagami.Tests.dll','Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Core.dll','Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Game.dll']
 assert len({sha(R/p) for p in binaries if p.endswith('/HakutaiKagami.Core.dll')})==1
 v=dict(status='PASSED',scope='guardianship',sources=before,proofs=proofs,binaries={p:sha(R/p) for p in binaries},trx=trx.relative_to(R).as_posix(),tests=dict(passed=5,failed=0,skipped=0),new_tests=5,recovery_phases=2,legacy_count=3,game_internal_input='PASSED',self_render='PASSED',ui_pose_pairs=1,computer_use='未使用',user_playtest='未依頼（第4版がまとまった時点で案内）',limits='後見の実務は帳簿保管。成人候補の異議と競合は係争として保持し、自動の多数決や武力決着は行わない。養子は現地で両家が承諾した成人を扱う。全国長期試遊、全件検査、ユーザーの外観評価は未実施。')
 put(D/'verification.json',v);check(v);print('家督争い・後見・他家の養子の工程末尾の検査成功',flush=True)
if __name__=='__main__':main()
