"""Behavior-preserving NPC performance acceptance, without GUI use."""
from pathlib import Path
import os,json,hashlib,subprocess,statistics,re,shutil,xml.etree.ElementTree as ET
R=Path(__file__).resolve().parents[1];D=R/'Docs/Design/NpcPerformanceV1';O=D/'runtime-local'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def sources():
 return {p.relative_to(R).as_posix():sha(p) for base in ['Core','Tests','Game'] for p in sorted((R/base).rglob('*.cs')) if not {'bin','obj','.godot'}.intersection(p.parts)}
def run(args,log,env=None):
 print('RUN',log.name,flush=True)
 with log.open('wb') as f:subprocess.run(args,cwd=R,stdout=f,stderr=subprocess.STDOUT,env=env,creationflags=subprocess.CREATE_NO_WINDOW,check=True,timeout=10800)
def check(v):
 assert v['status']=='PASSED' and v['sources']==sources()
 for group in ['proofs','binaries']:
  for f,h in v[group].items():assert sha(R/f)==h,f
 assert v['tests']==dict(passed=3994,failed=0,skipped=0)
 assert v['performance']['after_median_ms']<v['performance']['before_median_ms']
 assert v['node3d']==dict(pairs=301,files=602,nodes=1742)
 assert v['gui']=='NOT_RUN' and v['full_core_suite']=='PASSED'
def main():
 O.mkdir(exist_ok=True);source=sources();old=json.loads((R/'Docs/Design/NpcContinuingV1/verification.json').read_bytes());prior=R/old['projection_result'];rows=json.loads(prior.read_bytes())
 measure=[]
 for n in range(3):
  folder=O/f'after-{n}';folder.mkdir(exist_ok=True);env=dict(os.environ,HAKUTAI_WORLD_CLASH_QA_DIR=str(folder))
  run(['dotnet','test','Tests/HakutaiKagami.Tests.csproj','--no-build','--no-restore','--filter','FullyQualifiedName~NpcContinuingTests.ActualActivePopulationUsesMultipleNationalSites','--logger','trx;LogFileName=measurement.trx','--results-directory',str(folder),'--nologo'],folder/'test.log',env)
  measure.append(int(re.search(r'milliseconds=(\d+)',(folder/'npc-continuing-active-population-measurement.txt').read_text()).group(1)))
 before=json.loads((D/'before-measurement.json').read_bytes());perf=dict(before_median_ms=before['median_ms'],after_ms=measure,after_median_ms=statistics.median(measure),scope=before['scope']);print(perf,flush=True)
 full=O/'full';full.mkdir(exist_ok=True);env=dict(os.environ,HAKUTAI_WORLD_CLASH_QA_DIR=str(full))
 run(['dotnet','test','Tests/HakutaiKagami.Tests.csproj','--no-build','--no-restore','--logger','trx;LogFileName=full.trx','--results-directory',str(full),'--nologo'],O/'full.log',env)
 counts=ET.parse(full/'full.trx').find('.//{*}Counters').attrib;assert counts['passed']==counts['total']=='3994' and counts['failed']==counts['notExecuted']=='0',counts
 projection=O/'projection';projection.mkdir(exist_ok=True)
 for key in rows['Results']:
  oldfile=prior.parent/key;assert sha(oldfile)==old['proofs'][oldfile.relative_to(R).as_posix()]
  if key.startswith('npc-continuing-'):assert (full/key).read_bytes()==oldfile.read_bytes(),key
  shutil.copyfile(oldfile,projection/key)
 run(['dotnet','build','Game/HakutaiKagami.Game.csproj','--no-restore','--nologo','--verbosity','quiet'],O/'game-build.log')
 env=dict(os.environ,HAKUTAI_WORLD_CLASH_QA_DIR=str(projection),HAKUTAI_PROJECTION_EXPECTED_PAIRS='301')
 exe=R/'.tools/godot-4.5.2/Godot_v4.5.2-stable_mono_win64/Godot_v4.5.2-stable_mono_win64_console.exe'
 run([str(exe),'--headless','--path',str(R/'Game'),'res://Scenes/WorldClashProjectionCheck.tscn'],O/'node3d.log',env)
 result=json.loads((projection/'projection-result.json').read_bytes());assert result['Status']=='PASSED' and result['Results']==rows['Results'] and result['Nodes']==1742
 assert source==sources()
 binaries=['Core/bin/Debug/net8.0/HakutaiKagami.Core.dll','Tests/bin/Debug/net8.0/HakutaiKagami.Core.dll','Tests/bin/Debug/net8.0/HakutaiKagami.Tests.dll','Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Core.dll','Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Game.dll']
 assert len({sha(R/f) for f in binaries if f.endswith('/HakutaiKagami.Core.dll')})==1
 proofs=[p for p in O.rglob('*') if p.is_file() and p.suffix in {'.json','.trx','.txt','.log'}]+[D/'before-measurement.json']
 v=dict(status='PASSED',scope='headless-npc-performance-identical-behavior',sources=source,binaries={f:sha(R/f) for f in binaries},proofs={f.relative_to(R).as_posix():sha(f) for f in proofs},tests=dict(passed=3994,failed=0,skipped=0),performance=perf,node3d=dict(pairs=301,files=602,nodes=1742),gui='NOT_RUN',full_core_suite='PASSED',projection_result=(projection/'projection-result.json').relative_to(R).as_posix(),new_tests=0,recovery_phases=5)
 (D/'verification.json').write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf-8');check(v);print('PASS',flush=True)
if __name__=='__main__':main()
