"""Integration-scope reassessment consistency check (documentation only, no GUI or tests)."""
from pathlib import Path
import json,hashlib,sys
R=Path(__file__).resolve().parents[1];D=R/'Docs/Design/ProgressScopeV1'
sys.path.insert(0,str(R/'Scripts'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
FILES=['Docs/Development/ProgressV1/approval-baseline-v46-scope.json','Docs/Development/ProgressV1/approval-baseline-v47.json','Docs/Decisions/2026-10-31_GETENROKU_BD_CHANGES.md','Docs/Proposals/GETENROKU_INTEGRATION_V1.md']
def check(v):
 from progress_estimate import load,calculate
 assert v['status']=='PASSED' and v['scope']=='integration-scope-reassessment'
 for f,h in v['inputs'].items():assert sha(R/f)==h,f
 b=json.loads((R/FILES[0]).read_bytes());n=json.loads((R/FILES[1]).read_bytes());e=load()
 d=json.loads((R/'Docs/Design/basic-design-v1.json').read_bytes())
 assert b['targets']=={} and b['minimum_gain']==0 and b['reassessment_delta_excluded']<0
 assert all(c['new']<=c['old'] for c in b['changes'].values())
 assert {k:x['percent'] for k,x in e['leaves'].items()}=={k:x['percent'] for k,x in b['baseline_leaves'].items()}
 assert n['baseline_leaves']==b['baseline_leaves'] and n['targets']=={'BD-13':70,'BD-58':71}
 assert abs(calculate(e,d)['raw_total']-b['baseline_total'])<1e-9 and abs(b['baseline_total']-b['previous_total']-b['reassessment_delta_excluded'])<1e-9
 assert v['changed_items']==sum(c['new']!=c['old'] for c in b['changes'].values())
def main():
 b=json.loads((R/FILES[0]).read_bytes())
 v=dict(status='PASSED',scope='integration-scope-reassessment',inputs={f:sha(R/f) for f in FILES},previous_total=b['previous_total'],reassessed_total=b['baseline_total'],
  reassessment_delta_excluded=b['reassessment_delta_excluded'],reviewed_items=len(b['changes']),changed_items=sum(c['new']!=c['old'] for c in b['changes'].values()),tests='NOT_RUN (documentation only)',gui='NOT_RUN')
 (D/'verification.json').write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n');check(v);print('PASS')
if __name__=='__main__':main()
