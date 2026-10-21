from pathlib import Path
import json,hashlib,statistics
from xml.etree import ElementTree as ET
from PIL import Image,ImageChops
from verify_map_cache import source_files as parent_sources
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'Docs/Design/TerrainBreakdownV1';PERF=ROOT/'Docs/Design/TerrainPerformanceV1/runtime-local'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def source_files():return sorted(set(parent_sources()+[Path(__file__),ROOT/'Scripts/check_terrain_breakdown_binding.py',BASE/'README.md']))
def main():
 summary=json.loads((PERF/'breakdown-summary.json').read_bytes());assert all(sha(ROOT/p)==h for p,h in summary['sources'].items())
 raw=json.loads((PERF/'breakdown.json').read_bytes());rows=[r for r in raw['Rows'] if r['NewBuild']];assert len(raw['Rows'])==216 and len(rows)>200
 for r in rows:assert r['VertexHeightMs']+r['KindAtMs']+r['MeshCommitMs']<=r['BuildMs']+.1 and r['BuildMs']<=r['RefreshMs']+1
 counters=ET.parse(BASE/'runtime-local/tests/breakdown.trx').find('.//{*}Counters').attrib;assert counters['passed']=='933' and counters['failed']=='0' and counters['notExecuted']=='0'
 binaries={p.relative_to(ROOT).as_posix():sha(p) for p in (ROOT/'Game/.godot/mono/temp/bin/Debug').glob('HakutaiKagami.*.dll')};assert binaries['Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Core.dll']==sha(ROOT/'Tests/bin/Debug/net8.0/HakutaiKagami.Core.dll');assert summary['game_dll_sha256']==binaries['Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Game.dll']
 x,y,w,h=map(int,raw['ViewportRect']);box=(x,y,x+w,y+h);same={};images={}
 for group in range(2):
  for n in [0,54,107]:
   a=PERF/f'coverage-map-cache-{group}-{n}.png';b=PERF/f'coverage-breakdown-{group}-{n}.png';ia=Image.open(a).convert('RGB');ib=Image.open(b).convert('RGB');assert ia.size==ib.size
   delta=sum(p!=(0,0,0) for p in ImageChops.difference(ia.crop(box),ib.crop(box)).get_flattened_data());assert delta==0,(group,n,delta);same[f'{group}/{n}']=delta
   for p in [a,b]:images[p.relative_to(ROOT).as_posix()]=sha(p)
 medians={key:statistics.median(r[key] for r in rows) for key in ['RefreshMs','VertexHeightMs','KindAtMs','MeshCommitMs','BuildMs']};proofs=[PERF/'breakdown-summary.json',PERF/'breakdown.json',BASE/'runtime-local/tests/breakdown.trx']
 report={'status':'PASSED','scope':'profiling-only instrumentation; 110 prior comparisons belong to 804fe65, not rerun for this instrumentation','sources':{p.relative_to(ROOT).as_posix():sha(p) for p in source_files()},'binaries':binaries,'tests':counters,'build_samples':len(rows),'medians':medians,'comparisons':same,'images':images,'proofs':{p.relative_to(ROOT).as_posix():sha(p) for p in proofs},'next':'optimize exact land-mask queries after preserving an independent reference oracle','visual_review':None}
 (BASE/'verification.json').write_bytes((json.dumps(report,ensure_ascii=False,indent=2)+'\n').encode());print('PASS profiling-only, 933 tests, six equal images',medians)
if __name__=='__main__':main()
