from pathlib import Path
import json,hashlib
from PIL import Image,ImageChops
from verify_map_cache import source_files as parent_sources
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'Docs/Design/LandIndexV1';PERF=ROOT/'Docs/Design/TerrainPerformanceV1/runtime-local'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def source_files():return sorted(set(parent_sources()+[Path(__file__),ROOT/'Scripts/verify_pre_index_save.py',ROOT/'Scripts/check_land_index_binding.py',BASE/'README.md']))
def main():
 old=json.loads((PERF/'breakdown-summary.json').read_bytes());new=json.loads((PERF/'land-index-summary.json').read_bytes());assert all(sha(ROOT/p)==h for p,h in new['sources'].items());raw=json.loads((PERF/'land-index.json').read_bytes());assert len(raw['Rows'])==216 and all(r['Triangles']==2048 for r in raw['Rows'])
 assert new['cpu_refresh_ms_p50']<old['cpu_refresh_ms_p50'] and new['working_bytes_max']<1024**3 and new['godot_video_bytes_max']<512*1024**2
 x,y,w,h=map(int,raw['ViewportRect']);box=(x,y,x+w,y+h);same={};images={}
 for group in range(2):
  for n in [0,54,107]:
   a=PERF/f'coverage-breakdown-{group}-{n}.png';b=PERF/f'coverage-land-index-{group}-{n}.png';ia=Image.open(a).convert('RGB');ib=Image.open(b).convert('RGB');assert ia.size==ib.size;delta=sum(p!=(0,0,0) for p in ImageChops.difference(ia.crop(box),ib.crop(box)).get_flattened_data());assert delta==0,(group,n,delta);same[f'{group}/{n}']=delta
   for p in [a,b]:images[p.relative_to(ROOT).as_posix()]=sha(p)
 binaries={p.relative_to(ROOT).as_posix():sha(p) for p in (ROOT/'Game/.godot/mono/temp/bin/Debug').glob('HakutaiKagami.*.dll')};assert new['game_dll_sha256']==binaries['Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Game.dll'];proofs=[PERF/'land-index-summary.json',PERF/'land-index.json',BASE/'runtime-local/pre-index-final-compatibility.json',ROOT/'Scripts/current_test_contract.json',ROOT/'Docs/Design/WorkHubStartupV1/runtime-local/tests/work-hub.trx']
 for stage in ['VisualAcceptanceV1','CameraPanV1','GroundMaterialV1','FieldModelsV1','ForceLodV1','CameraContinuityV1','LodAppearanceV1','MidTerrainV1','WorkHubStartupV1']:
  p=ROOT/'Docs/Design'/stage/'verification.json';j=json.loads(p.read_bytes());assert j['binaries']==binaries;proofs.append(p)
 expected=json.loads((ROOT/'Scripts/current_test_contract.json').read_bytes())['expected_passed'];startup=json.loads((ROOT/'Docs/Design/WorkHubStartupV1/verification.json').read_bytes());assert startup['summary']['tests']==expected==945
 compat=json.loads((BASE/'runtime-local/pre-index-final-compatibility.json').read_bytes());assert len(compat['comparisons'])==6 and set(compat['comparisons'].values())=={0}
 report={'status':'PASSED','sources':{p.relative_to(ROOT).as_posix():sha(p) for p in source_files()},'binaries':binaries,'comparisons':same,'images':images,'tests':expected,'before':old,'after':new,'median_reduction_percent':(1-new['cpu_refresh_ms_p50']/old['cpu_refresh_ms_p50'])*100,'proofs':{p.relative_to(ROOT).as_posix():sha(p) for p in proofs},'visual_review':None}
 (BASE/'verification.json').write_bytes((json.dumps(report,ensure_ascii=False,indent=2)+'\n').encode());print('PASS exact land predicates, 945 tests, old saves, six equal nation views and CPU reduction',report['median_reduction_percent'])
if __name__=='__main__':main()
