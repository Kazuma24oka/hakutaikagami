from pathlib import Path
import json,hashlib
from PIL import Image,ImageChops
from verify_visual_acceptance import source_files as parent_sources
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'Docs/Design/MapCacheV1';PERF=ROOT/'Docs/Design/TerrainPerformanceV1/runtime-local'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def source_files():return sorted(set(parent_sources()+[Path(__file__),ROOT/'Scripts/check_map_cache_binding.py',ROOT/'Scripts/check_visual_acceptance_binding.py',BASE/'README.md']))
def main():
 old=json.loads((PERF/'acceptance-summary.json').read_bytes());new=json.loads((PERF/'map-cache-summary.json').read_bytes());raw=json.loads((PERF/'map-cache.json').read_bytes());assert len(raw['Rows'])==216 and all(r['Triangles']==2048 for r in raw['Rows'])
 assert new['cpu_refresh_ms_p50']<old['cpu_refresh_ms_p50'];assert new['working_bytes_max']<1024**3 and new['godot_video_bytes_max']<512*1024**2
 x,y,w,h=map(int,raw['ViewportRect']);box=(x,y,x+w,y+h);same={};images={}
 for group in range(2):
  for n in [0,54,107]:
   a=PERF/f'coverage-acceptance-{group}-{n}.png';b=PERF/f'coverage-map-cache-{group}-{n}.png';ia=Image.open(a).convert('RGB');ib=Image.open(b).convert('RGB');assert ia.size==ib.size
   delta=sum(p!=(0,0,0) for p in ImageChops.difference(ia.crop(box),ib.crop(box)).get_flattened_data());assert delta==0,(group,n,delta);same[f'{group}/{n}']=delta
   for p in [a,b]:images[p.relative_to(ROOT).as_posix()]=sha(p)
 binaries={p.relative_to(ROOT).as_posix():sha(p) for p in (ROOT/'Game/.godot/mono/temp/bin/Debug').glob('HakutaiKagami.*.dll')};assert new['game_dll_sha256']==binaries['Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Game.dll']
 proofs=[PERF/'map-cache-summary.json',PERF/'map-cache.json',PERF/'acceptance-summary.json']
 for stage in ['CameraPanV1','GroundMaterialV1','FieldModelsV1','ForceLodV1','CameraContinuityV1','LodAppearanceV1','MidTerrainV1','WorkHubStartupV1']:
  p=ROOT/'Docs/Design'/stage/'verification.json';assert json.loads(p.read_bytes())['binaries']==binaries;proofs.append(p)
 j={'status':'PASSED','sources':{p.relative_to(ROOT).as_posix():sha(p) for p in source_files()},'binaries':binaries,'images':images,'comparisons':same,'before':old,'after':new,'median_reduction_percent':(1-new['cpu_refresh_ms_p50']/old['cpu_refresh_ms_p50'])*100,'proofs':{p.relative_to(ROOT).as_posix():sha(p) for p in proofs},'visual_review':None}
 (BASE/'verification.json').write_bytes((json.dumps(j,ensure_ascii=False,indent=2)+'\n').encode());print('PASS six unchanged mid views and nationwide CPU reduction',j['median_reduction_percent'])
if __name__=='__main__':main()
