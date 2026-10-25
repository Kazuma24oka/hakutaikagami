from pathlib import Path
import json,hashlib,collections
from build_progress_report import render
R=Path(__file__).resolve().parents[1]
p=json.loads((R/'Docs/Development/ProgressV1/progress.json').read_bytes());d=json.loads((R/p['design_source']).read_bytes());a=json.loads((R/p['acceptance_source']).read_bytes())
ids=[i['id'] for g in d['groups'] for i in g['items']]
assert len(d['groups'])==15 and len(ids)==60 and len(set(ids))==60
assert [i['id'] for i in p['items']]==ids
assert dict(collections.Counter(i['status'] for i in p['items']))==dict(normal=31,isolated=3,foundation=11,design_only=14,management=1)
assert len(a['cases'])==170
for i in p['items']:
 assert i['existing'] and i['remaining'] and i['evidence']
 assert i['planned_acceptance_cases']==sum(c['design_id']==i['id'] for c in a['cases'])
 for f in i['evidence']:assert (R/f).is_file(),f
def check(f,h):assert hashlib.sha256((R/f).read_bytes()).hexdigest()==h,f
for f,h in p['source_hashes'].items():check(f,h)
for f,h in p['evidence']['runtime_source_hashes'].items():check(f,h)
assert (R/'Docs/Development/ProgressV1/index.html').read_text(encoding='utf-8')==render()
print('PASS: 60 rows / 15 domains / 170 planned cases; evidence and runtime source hashes unchanged')

v=json.loads((R/p["evidence"]["latest_game_verification"]).read_bytes())
check(p["evidence"]["latest_game_verification"],p["evidence"]["latest_game_verification_sha256"])
for f,h in v["binaries"].items():check(f,h)
t=p["evidence"]["core_test"];check(t["trx"],t["sha256"])
print("PASS: bound binaries and recorded test-result provenance unchanged")

# Product-scope estimate is separate from test counts and preserves all adopted design items.
from progress_estimate import load as load_estimate,calculate
x=load_estimate();c=calculate(x,d)
assert sum(v['weight'] for v in x['domains'])==100
covered=[g for v in x['domains'] for g in v['groups']]
assert len(covered)==len(set(covered))==15 and set(covered)=={g['id'] for g in d['groups']}
assert set(x['leaves'])==set(ids)
assert all(isinstance(v['percent'],int) and 0<=v['percent']<=100 and v['basis'] for v in x['leaves'].values())
assert set(x['excluded_from_rollup'])=={'BD-01','BD-60'}
assert 0<=c['total']<=100 and c['total']%x['rounding_step']==0
assert x['overall_range'][0]<=c['total']<=x['overall_range'][1]
assert x['focus']['leaf'] in ids and 3<=len(x['focus']['exit'])<=5
assert len(x['roadmap'])==6 and all(v['finish'] and v['why'] and v['status'] for v in x['roadmap'])
print('PASS: weighted estimate / 6 large, 15 medium, 60 small entries / scope exits and roadmap')
