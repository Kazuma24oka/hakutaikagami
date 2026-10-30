"""Supplement: actual generation recovery before/after accepting during a road march, exact verified assemblies."""
from pathlib import Path
import json,hashlib,subprocess,os,xml.sax.saxutils
R=Path(__file__).resolve().parents[1];BASE=R/'Docs/Design/RemoteMilitarySuccessionV1';P=BASE/'runtime-local/road-probe';P.mkdir(parents=True,exist_ok=True)
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def run(args,log,env=None):
 with log.open('wb') as f:s=subprocess.run(args,cwd=R,stdout=f,stderr=subprocess.STDOUT,env=env,creationflags=subprocess.CREATE_NO_WINDOW,timeout=300)
 assert s.returncode==0,log
v=json.loads((BASE/'verification.json').read_bytes())
for f,h in v['binaries'].items():assert sha(R/f)==h
hint=xml.sax.saxutils.escape((R/'Core/bin/Debug/net8.0/HakutaiKagami.Core.dll').as_posix())
(P/'probe.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><Reference Include="HakutaiKagami.Core"><HintPath>'+hint+'</HintPath><Private>true</Private></Reference></ItemGroup></Project>',encoding='utf-8')
program=r'''using HakutaiKagami.Core.Contracts;
using System.Reflection;
using System.Runtime.Loader;
string tests=args[0],folder=args[1];
AssemblyLoadContext.Default.Resolving+=(ctx,name)=>File.Exists(Path.Combine(tests,name.Name+".dll"))?ctx.LoadFromAssemblyPath(Path.Combine(tests,name.Name+".dll")):null;
var type=Assembly.LoadFrom(Path.Combine(tests,"HakutaiKagami.Tests.dll")).GetType("HakutaiKagami.Tests.RemoteMilitarySuccessionTests")!;
object? Call(string method,params object[] inputs)=>type.GetMethod(method,BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,inputs);
void Need(bool ok){if(!ok)throw new Exception("Road probe mismatch");}
void Good(CommandResult r)=>Need(r.Code==ResultCode.Accepted);
var s=(WorldSession)Call("Holding",true)!;Call("Rice",s,100L);Call("Rice",s,100L);Call("Send",s,NewMilitaryAction.Load,180*86400L);Call("Succeed",s);
string paper=(string)Call("Issue",s,4L)!;Call("At",s,s.Seconds+5);Call("Open",s);
void Save(int phase){s.SetPaused(true);var w=s.DiagnosticSnapshot();var store=new GenerationSaveStore(Path.Combine(folder,"save-"+phase),w.Definitions);Need(store.Save(s,"road",w.WorldId).Published);var host=new WorldSessionHost((WorldSession)Call("Start",true)!);Need(host.CommitLoad(store.Prepare("road"),host.Current,host.Current.Revision)==ResultCode.Accepted);Need(ContractJson.Write(w).SequenceEqual(ContractJson.Write(host.Current.DiagnosticSnapshot())));File.WriteAllBytes(Path.Combine(folder,"road-"+phase+".before.json"),ContractJson.Write(w));File.WriteAllBytes(Path.Combine(folder,"road-"+phase+".restored.json"),ContractJson.Write(host.Current.DiagnosticSnapshot()));foreach(var x in new[]{s,host.Current})Call("At",x,x.Seconds+10);Need(ContractJson.Write(s.DiagnosticSnapshot()).SequenceEqual(ContractJson.Write(host.Current.DiagnosticSnapshot())));}
Save(0);Good(s.ManageArmyRoad(new(new("request:exact-road-probe"),ArmyRoadAction.March,new("site:suwa")),s.PlayerControl,s.Revision));Call("At",s,s.Seconds+10);Need(s.ArmyRoadView()!.Marching);Save(1);var before=s.DiagnosticSnapshot();Call("Remote",s,Call("Accept",s,paper)!);var after=s.DiagnosticSnapshot();Need(ContractJson.Write(before.ArmyRoad).SequenceEqual(ContractJson.Write(after.ArmyRoad)));Need(ContractJson.Write(before.Units).SequenceEqual(ContractJson.Write(after.Units)));Need(ContractJson.Write(before.ResourceLots).SequenceEqual(ContractJson.Write(after.ResourceLots)));Save(2);Console.WriteLine("PASS three actual generation recoveries and future equality; same-road acceptance retains route, units and rice");
'''
(P/'Program.cs').write_text(program,encoding='utf-8')
run(['dotnet','build',str(P/'probe.csproj'),'--nologo','--verbosity','quiet'],P/'build.log')
assert sha(P/'bin/Debug/net8.0/HakutaiKagami.Core.dll')==v['binaries']['Core/bin/Debug/net8.0/HakutaiKagami.Core.dll']
run(['dotnet',str(P/'bin/Debug/net8.0/probe.dll'),str(R/'Tests/bin/Debug/net8.0'),str(P)],P/'run.log')
env=os.environ.copy();env['HAKUTAI_WORLD_CLASH_QA_DIR']=str(P);env['HAKUTAI_PROJECTION_EXPECTED_PAIRS']='3'
exe=R/'.tools/godot-4.5.2/Godot_v4.5.2-stable_mono_win64/Godot_v4.5.2-stable_mono_win64_console.exe'
run([str(exe),'--headless','--path',str(R/'Game'),'res://Scenes/WorldClashProjectionCheck.tscn'],P/'node3d.log',env)
n=json.loads((P/'projection-result.json').read_bytes());assert n['Status']=='PASSED' and n['Files']==6 and n['Pairs']==3
for f in P.glob('*.before.json'):
 assert f.read_bytes()==f.with_name(f.name.replace('.before.json','.restored.json')).read_bytes()
 for mode in ['before','restored']:
  key=f.name.replace('.before.json','.'+mode+'.json');w=json.loads((P/key).read_bytes());actual=[a['Id'] for a in json.loads(n['Results'][key])['Actors']];assert w['PlayerControl'] in actual
  assert set(w['NewMilitary']['Binding']['Units']).issubset(actual)
for f,h in v['binaries'].items():assert sha(R/f)==h
proofs=[Path(__file__),P/'probe.csproj',P/'Program.cs',P/'build.log',P/'run.log',P/'node3d.log',P/'bin/Debug/net8.0/probe.dll',P/'bin/Debug/net8.0/HakutaiKagami.Core.dll',*P.glob('*.json')]
v['proofs'].update({f.relative_to(R).as_posix():sha(f) for f in proofs});v['road_recovery_probe']=dict(status='PASSED',generation_recoveries=3,future_equality=True,pairs=3,files=6,nodes=n['Nodes'],result=(P/'projection-result.json').relative_to(R).as_posix(),scope='Before departure, actual mid-road before acceptance, actual mid-road after acceptance; exact verified Core and test helper assembly; no production rebuild or GUI')
(BASE/'verification.json').write_bytes((json.dumps(v,ensure_ascii=False,indent=2)+'\n').encode());print('PASS exact-road recovery3 / headless3pairs / '+str(n['Nodes'])+'nodes')
