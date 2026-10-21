from pathlib import Path
import json,hashlib
from PIL import Image,ImageChops
from verify_mid_terrain import source_files as parent_sources
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'Docs/Design/TerrainPerformanceV1';OUT=BASE/'runtime-local'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def source_files():return sorted(set(parent_sources()+[Path(__file__),ROOT/'Scripts/measure_terrain_performance.py',ROOT/'Scripts/check_terrain_performance_binding.py',BASE/'README.md']))
def main():
 a=json.loads((OUT/'baseline-summary.json').read_bytes());b=json.loads((OUT/'optimized-summary.json').read_bytes());j=json.loads((OUT/'optimized.json').read_bytes());rect=j['ViewportRect'];x,y,w,h=map(int,rect);box=(x,y,x+w,y+h);images={};diffs={}
 for passno in range(2):
  for i in [0,54,107]:
   pa=OUT/f'coverage-baseline-{passno}-{i}.png';pb=OUT/f'coverage-optimized-{passno}-{i}.png';ia=Image.open(pa).convert('RGB');ib=Image.open(pb).convert('RGB');assert ia.size==ib.size
   diff=sum(p!=(0,0,0) for p in ImageChops.difference(ia.crop(box),ib.crop(box)).get_flattened_data());assert diff==0,(passno,i,diff);diffs[f'{passno}/{i}']=diff
   for p in [pa,pb]:images[p.relative_to(ROOT).as_posix()]=sha(p)
 assert len(j['Rows'])==216 and all(r['Triangles']==2048 for r in j['Rows'])
 assert b['working_bytes_max']<1024**3 and b['godot_video_bytes_max']<512*1024**2
 assert b['cpu_refresh_ms_p50']<a['cpu_refresh_ms_p50'],'optimization did not reduce median'
 mid=json.loads((ROOT/'Docs/Design/MidTerrainV1/verification.json').read_bytes());startup=json.loads((ROOT/'Docs/Design/WorkHubStartupV1/verification.json').read_bytes())
 binaries={p.relative_to(ROOT).as_posix():sha(p) for p in (ROOT/'Game/.godot/mono/temp/bin/Debug').glob('HakutaiKagami.*.dll')};assert binaries==mid['binaries']==startup['binaries']
 proofs={str(p.relative_to(ROOT)).replace('\\','/'):sha(p) for p in [OUT/'baseline.json',OUT/'optimized.json',OUT/'baseline-summary.json',OUT/'optimized-summary.json',ROOT/'Docs/Design/MidTerrainV1/verification.json',ROOT/'Docs/Design/WorkHubStartupV1/verification.json']}
 report={'status':'PASSED','sources':{p.relative_to(ROOT).as_posix():sha(p) for p in source_files()},'binaries':binaries,'images':images,'comparisons':diffs,'baseline':a,'optimized':b,'median_reduction_percent':(1-b['cpu_refresh_ms_p50']/a['cpu_refresh_ms_p50'])*100,'proofs':proofs,'visual_review':None}
 (BASE/'verification.json').write_bytes((json.dumps(report,ensure_ascii=False,indent=2)+'\n').encode());print('PASS nationwide 216 views, fixed budgets, six exact before/after images',report['median_reduction_percent'])
if __name__=='__main__':main()
