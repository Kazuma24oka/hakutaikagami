from verify_lod_appearance import ROOT,BASE,source_files,sha
import json
j=json.loads((BASE/'verification.json').read_bytes());assert j['status']=='PASSED'
assert j['sources']=={p.relative_to(ROOT).as_posix():sha(p) for p in source_files()}
for group in ['binaries','images']:
 for p,h in j[group].items():assert sha(ROOT/p)==h,p
assert len(j['comparisons'])==8 and set(j['comparisons'].values())=={0}
assert len(j['visible_changes'])==4 and all(x>0 for x in j['visible_changes'].values())
assert j['visual_review'] and j['visual_review']['inspected']
for p,h in j['visual_review']['inspected'].items():assert sha(ROOT/p)==h,p
for stage in ['MidTerrainV1','WorkHubStartupV1']:
 prior=json.loads((ROOT/'Docs/Design'/stage/'verification.json').read_bytes());assert j['binaries']==prior['binaries']
print('PASS LOD appearance current sources, binaries, real restored images and regression')
