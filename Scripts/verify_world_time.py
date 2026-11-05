"""0aの工程末尾に、全件・実保存・旧保存・ゲーム内部操作を照合する。"""
from pathlib import Path
import argparse, hashlib, json, os, subprocess, xml.etree.ElementTree as ET
R=Path(__file__).resolve().parents[1];D=R/'Docs/Design/WorldTimeV1';O=D/'runtime-local'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def put(p,v):p.write_bytes((json.dumps(v,ensure_ascii=False,indent=2)+'\n').encode('utf-8'))
def sources():return {p.relative_to(R).as_posix():sha(p) for base in ['Core','Tests','Game'] for p in sorted((R/base).rglob('*.cs')) if not {'bin','obj','.godot'}.intersection(p.parts)}
def run(args,log,env=None):
    with log.open('wb') as f:subprocess.run(args,cwd=R,stdout=f,stderr=subprocess.STDOUT,env=env,check=True,creationflags=subprocess.CREATE_NO_WINDOW,timeout=10800)
def check(v):
    assert v['status']=='PASSED' and v['sources']==sources()
    for group in ['proofs','binaries']:
        for p,h in v[group].items():assert sha(R/p)==h,p
    c=ET.parse(R/v['trx']).find('.//{*}Counters').attrib
    assert int(c['total'])==int(c['passed'])==4052 and int(c['failed'])==int(c['notExecuted'])==0
    assert v['legacy_count']==261 and v['recovery_phases']==9 and v['projection']['Pairs']==3
    assert v['game_internal_input']=='PASSED' and v['self_render']=='PASSED'
def main():
    ap=argparse.ArgumentParser();ap.add_argument('--legacy-root',type=Path);ap.add_argument('--godot',type=Path);ap.add_argument('--check-only',action='store_true');a=ap.parse_args()
    if a.check_only:check(json.loads((D/'verification.json').read_bytes()));print('0a検証記録の照合成功');return
    trx=O/'full/full.trx';c=ET.parse(trx).find('.//{*}Counters').attrib
    assert c['total']==c['passed']=='4052' and c['failed']==c['notExecuted']=='0',c
    before=sources();frozen=json.loads((O/'full/run-inputs.json').read_bytes());assert frozen=={k:v for k,v in before.items() if k.startswith(('Core/','Tests/'))}
    proofs={}
    def proof(p):proofs[str(p.relative_to(R)) if p.is_relative_to(R) else str(p)]=sha(p)
    proof(trx);proof(O/'full/run-inputs.json')
    for name,count in [('time-control',6),('time-travel',3)]:
        for n in range(count):
            p=O/f'full/{name}-{n}.before.json';q=O/f'full/{name}-{n}.restored.json';assert p.read_bytes()==q.read_bytes();proof(p);proof(q)
    projection=O/'projection';projection.mkdir(exist_ok=True)
    for n in range(3):
        for suffix in ['before','restored']:
            p=projection/f'time-travel-{n}.{suffix}.json';p.write_bytes((O/f'full/time-travel-{n}.{suffix}.json').read_bytes());proof(p)
    env=dict(os.environ,HAKUTAI_WORLD_CLASH_QA_DIR=str(projection),HAKUTAI_PROJECTION_EXPECTED_PAIRS='3')
    run([str(a.godot),'--headless','--path',str(R/'Game'),'res://Scenes/WorldClashProjectionCheck.tscn'],O/'projection.log',env)
    projection_result=json.loads((projection/'projection-result.json').read_bytes());assert projection_result['Status']=='PASSED' and projection_result['Pairs']==3
    proof(projection/'projection-result.json');proof(O/'projection.log')
    files=[];config=json.loads((R/'Docs/Design/NpcPerformanceV1/legacy-sources.json').read_bytes())
    for row in config['sources']:
        manifest=a.legacy_root/row['manifest'];old=json.loads(manifest.read_bytes());proof(manifest)
        for n in range(row['count']):
            key=next(k for k in old['proofs'] if k.endswith('/'+row['prefix']+'-'+str(n)+'.before.json'))
            p=a.legacy_root/key;assert sha(p)==old['proofs'][key];files.append(str(p));proof(p)
    old=json.loads((R/'Docs/Design/NpcPurposeV1/verification.json').read_bytes())
    for n in range(7):
        key=f'Docs/Design/NpcPurposeV1/runtime-local/projection/npc-purpose-{n}.before.json';p=R/key;assert sha(p)==old['proofs'][key];files.append(str(p));proof(p)
    assert len(files)==len(set(files))==261
    probe=O/'legacy-probe';probe.mkdir(exist_ok=True);core=R/'Core/bin/Debug/net8.0/HakutaiKagami.Core.dll'
    (probe/'probe.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings></PropertyGroup><ItemGroup><Reference Include="HakutaiKagami.Core"><HintPath>'+core.as_posix()+'</HintPath></Reference></ItemGroup></Project>',encoding='utf-8')
    (probe/'Program.cs').write_text('''using HakutaiKagami.Core.Contracts;
using System.Text.Json;
using System.Diagnostics;
var files=JsonSerializer.Deserialize<string[]>(File.ReadAllBytes(args[0]))!;
foreach(var p in files){var raw=File.ReadAllBytes(p);var w=new WorldSession(ContractJson.Read<ContractWorld>(raw));if(!raw.SequenceEqual(ContractJson.Write(w.DiagnosticSnapshot())))throw new Exception("旧保存の不一致: "+p);}
var fixture=ContractFixture.Create();fixture.TimeControl=WorldTimeControl.Initial();var s=new WorldSession(fixture);var watch=Stopwatch.StartNew();var r=s.AdvanceTo(365L*86400);watch.Stop();
Console.WriteLine(JsonSerializer.Serialize(new{Legacy=files.Length,QuietFixtureYearMilliseconds=watch.Elapsed.TotalMilliseconds,r.Processed,Scope="NPC・出来事のない検査用世界。全国NPCの1年性能ではありません"}));
''',encoding='utf-8')
    put(probe/'inputs.json',files);run(['dotnet','build',str(probe/'probe.csproj'),'--nologo','--verbosity','quiet'],probe/'build.log');assert sha(probe/'bin/Debug/net8.0/HakutaiKagami.Core.dll')==sha(core)
    run(['dotnet',str(probe/'bin/Debug/net8.0/probe.dll'),str(probe/'inputs.json')],probe/'run.log')
    for p in probe.glob('*'):
        if p.is_file():proof(p)
    ui=json.loads((O/'ui/result.json').read_bytes());assert ui['Status']=='PASSED'
    log=(O/'ui-render-verified.log').read_text(encoding='utf-8-sig');assert 'WORLD_TIME_UI_PASS' in log and 'ERROR:' not in log
    for p in [O/'ui/result.json',O/'ui/arrival.png',O/'ui-render-verified.log']:proof(p)
    assert before==sources()
    binary_paths=['Core/bin/Debug/net8.0/HakutaiKagami.Core.dll','Tests/bin/Debug/net8.0/HakutaiKagami.Core.dll','Tests/bin/Debug/net8.0/HakutaiKagami.Tests.dll','Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Core.dll','Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Game.dll']
    assert len({sha(R/p) for p in binary_paths if p.endswith('/HakutaiKagami.Core.dll')})==1
    v=dict(status='PASSED',sources=before,proofs=proofs,binaries={p:sha(R/p) for p in binary_paths},trx=trx.relative_to(R).as_posix(),tests=dict(passed=4052,failed=0,skipped=0),new_tests=35,recovery_phases=9,legacy_count=261,projection=projection_result,game_internal_input='PASSED',self_render='PASSED',computer_use='未使用',user_playtest='未依頼（第1版の完成時）',normal_entry_elapsed_ms=ui['ElapsedMilliseconds'],normal_entry_game_seconds=ui['Seconds'],note='1年約1時間は遊び方の目標。全国NPCの90年連続負荷・未実装の評定/訃報/襲撃発生源は本工程の達成に含めない。')
    put(D/'verification.json',v);check(v);print('0aの全件・保存・内部操作照合成功')
if __name__=='__main__':main()
