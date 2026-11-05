"""工程末尾に取得した全件結果・実保存・旧保存・Godot配置を一度だけ照合する。"""
from pathlib import Path
import argparse, hashlib, json, os, subprocess, xml.etree.ElementTree as ET
R=Path(__file__).resolve().parents[1]
D=R/'Docs/Design/NpcPurposeV1'; O=D/'runtime-local'
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def put(p,v): p.write_bytes((json.dumps(v,ensure_ascii=False,indent=2)+'\n').encode('utf-8'))
def sources():
    return {p.relative_to(R).as_posix():sha(p) for base in ['Core','Tests','Game'] for p in sorted((R/base).rglob('*.cs')) if not {'bin','obj','.godot'}.intersection(p.parts)}
def run(args,log,env=None):
    with log.open('wb') as f: subprocess.run(args,cwd=R,stdout=f,stderr=subprocess.STDOUT,env=env,check=True,creationflags=subprocess.CREATE_NO_WINDOW,timeout=10800)
def check(v):
    assert v['status']=='PASSED' and v['sources']==sources()
    for group in ['binaries','proofs']:
        for f,h in v[group].items(): assert sha(R/f)==h,f
    c=ET.parse(R/v['trx']).find('.//{*}Counters').attrib
    assert int(c['total'])==int(c['passed'])==4017 and int(c['failed'])==int(c['notExecuted'])==0
    assert v['legacy_probe']['files']==254 and v['recovery_phases']==7
    assert v['node3d']['pairs']==7 and v['full_core_suite']=='PASSED'
def main():
    ap=argparse.ArgumentParser();ap.add_argument('--legacy-root',type=Path);ap.add_argument('--godot',type=Path);ap.add_argument('--check-only',action='store_true');a=ap.parse_args()
    if a.check_only: check(json.loads((D/'verification.json').read_bytes()));print('検証記録の照合成功');return
    assert a.legacy_root and a.godot
    trx=O/'full/full.trx';c=ET.parse(trx).find('.//{*}Counters').attrib
    assert c['total']==c['passed']=='4017' and c['failed']==c['notExecuted']=='0',c
    before=sources(); proofs={trx.relative_to(R).as_posix():sha(trx)}
    frozen=json.loads((O/'run-inputs.json').read_bytes())
    assert {k:v for k,v in frozen.items() if k.startswith(('Core/','Tests/'))}=={k:v for k,v in before.items() if k.startswith(('Core/','Tests/'))}
    proofs[(O/'run-inputs.json').relative_to(R).as_posix()]=sha(O/'run-inputs.json')
    run([str(a.godot),'--headless','--path',str(R/'Game'),'res://Scenes/NpcPurposeEntryCheck.tscn'],O/'entry.log')
    assert 'NPC_PURPOSE_ENTRY_PREVIEW_PASS' in (O/'entry.log').read_text(encoding='utf-8')
    proofs[(O/'entry.log').relative_to(R).as_posix()]=sha(O/'entry.log')
    projection=O/'projection';projection.mkdir(exist_ok=True)
    for phase in range(7):
        raw=(O/f'full/npc-purpose-{phase}.before.json').read_bytes()
        assert raw==(O/f'full/npc-purpose-{phase}.restored.json').read_bytes()
        for suffix in ['before','restored']:
            p=projection/f'npc-purpose-{phase}.{suffix}.json';p.write_bytes(raw);proofs[p.relative_to(R).as_posix()]=sha(p)
    env=dict(os.environ,HAKUTAI_WORLD_CLASH_QA_DIR=str(projection),HAKUTAI_PROJECTION_EXPECTED_PAIRS='7')
    run([str(a.godot),'--headless','--path',str(R/'Game'),'res://Scenes/WorldClashProjectionCheck.tscn'],O/'node3d.log',env)
    result=json.loads((projection/'projection-result.json').read_bytes());assert result['Status']=='PASSED' and result['Pairs']==7
    for phase in range(7):
        row=json.loads(result['Results'][f'npc-purpose-{phase}.before.json'])
        w=json.loads((projection/f'npc-purpose-{phase}.before.json').read_bytes())
        assert {x['Id'] for x in row['Actors']}=={w['PlayerControl']}|{d['Actor'] for d in w['NpcWork']['Decisions']}
    config=json.loads((R/'Docs/Design/NpcPerformanceV1/legacy-sources.json').read_bytes());files=[]
    for row in config['sources']:
        m=a.legacy_root/row['manifest'];old=json.loads(m.read_bytes());proofs[str(m)]=sha(m)
        for n in range(row['count']):
            key=next(k for k in old['proofs'] if k.endswith('/'+row['prefix']+'-'+str(n)+'.before.json'))
            p=a.legacy_root/key;assert sha(p)==old['proofs'][key];files.append(str(p));proofs[str(p)]=sha(p)
    assert len(files)==len(set(files))==254
    probe=O/'legacy-probe';probe.mkdir(exist_ok=True)
    core=R/'Core/bin/Debug/net8.0/HakutaiKagami.Core.dll'
    (probe/'probe.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings></PropertyGroup><ItemGroup><Reference Include="HakutaiKagami.Core"><HintPath>'+core.as_posix()+'</HintPath></Reference></ItemGroup></Project>',encoding='utf-8')
    (probe/'Program.cs').write_text('''using HakutaiKagami.Core.Contracts;
using System.Text.Json;
var files=JsonSerializer.Deserialize<string[]>(File.ReadAllBytes(args[0]))!;
foreach(var p in files){var raw=File.ReadAllBytes(p);var s=new WorldSession(ContractJson.Read<ContractWorld>(raw));if(!raw.SequenceEqual(ContractJson.Write(s.DiagnosticSnapshot())))throw new Exception("旧保存の内容が変化: "+p);}
Console.WriteLine("旧保存バイト一致: "+files.Length);
''',encoding='utf-8')
    put(probe/'inputs.json',files)
    run(['dotnet','build',str(probe/'probe.csproj'),'--nologo','--verbosity','quiet'],probe/'build.log')
    assert sha(probe/'bin/Debug/net8.0/HakutaiKagami.Core.dll')==sha(core)
    run(['dotnet',str(probe/'bin/Debug/net8.0/probe.dll'),str(probe/'inputs.json')],probe/'run.log')
    for p in [projection/'projection-result.json',O/'node3d.log',*probe.glob('*')]:
        if p.is_file():proofs[p.relative_to(R).as_posix()]=sha(p)
    assert before==sources()
    binaries=['Core/bin/Debug/net8.0/HakutaiKagami.Core.dll','Tests/bin/Debug/net8.0/HakutaiKagami.Core.dll','Tests/bin/Debug/net8.0/HakutaiKagami.Tests.dll','Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Core.dll','Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Game.dll']
    assert len({sha(R/f) for f in binaries if f.endswith('/HakutaiKagami.Core.dll')})==1
    v=dict(status='PASSED',scope='headless-npc-duty-priority-idle-practice',sources=before,binaries={f:sha(R/f) for f in binaries},proofs=proofs,tests=dict(passed=4017,failed=0,skipped=0),trx=trx.relative_to(R).as_posix(),new_tests=23,recovery_phases=7,node3d=dict(pairs=7,files=14,nodes=result['Nodes']),legacy_probe=dict(status='PASSED',files=254),gui='NOT_RUN',full_core_suite='PASSED',note='全件実行で実保存と同じ未来を検査。旧保存原本は元の作業フォルダで保持し、検証済みCoreで読込・再書出し。外観は未確認。')
    put(D/'verification.json',v);check(v);print('全件・保存復旧・旧保存・Godot配置の照合成功')
if __name__=='__main__': main()
