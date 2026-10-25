"""Refuse to show stale candidate evidence; local models remain local."""
from pathlib import Path
import json,hashlib
R=Path(__file__).resolve().parents[1]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
    p=R/'Docs/Design/HouseholdMilitaryV1/verification.json';v=json.loads(p.read_bytes())
    assert v['schema']==2 and v['status']=='PASSED' and v['completed_conditions']==[1,2,3,4],'milestone is not complete'
    assert v['core_tests']['failed']==0 and v['core_tests']['skipped']==0 and v['gpu']['comparisons']==60,'verification incomplete'
    for group in ['sources','binaries','proofs']:
        for name,h in v[group].items():assert sha(R/name)==h,(group,name)
    print('PASS current household candidate: exact sources, binaries and test/render evidence')
if __name__=='__main__':main()
