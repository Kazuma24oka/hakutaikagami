from pathlib import Path
import json,hashlib
from verify_historical_chronicle import sources
R=Path(__file__).resolve().parents[1];D=R/'Docs/Design/VerbNotebookV1'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def check(v):
 assert v['status']=='PASSED' and v['sources']==sources()
 for group in ['proofs','binaries']:
  for p,h in v[group].items():assert sha(R/p)==h,p
 assert v['game_internal_input']=='PASSED' and v['self_render']=='PASSED' and v['recovery_phases']==1
if __name__=='__main__':
 old=json.loads((R/'Docs/Design/HistoricalChronicleV1/verification.json').read_bytes());now=sources()
 assert {k:v for k,v in now.items() if k.startswith(('Core/','Tests/'))}=={k:v for k,v in old['sources'].items() if k.startswith(('Core/','Tests/'))},'Coreの再検査が必要'
 for p,h in old['proofs'].items():assert sha(R/p)==h,p
 for p,h in old['binaries'].items():
  if p.startswith(('Core/','Tests/')):assert sha(R/p)==h,p
 out=D/'runtime-local';log=out/'ui-final.log';text=log.read_text(encoding='utf-8-sig');assert 'VERB_NOTEBOOK_UI_PASS' in text and 'ERROR:' not in text
 assert (out/'ui/before-pose.json').read_bytes()==(out/'ui/restored-pose.json').read_bytes()
 proofs=dict(old['proofs']);proofs.update({p.relative_to(R).as_posix():sha(p) for p in [log,*sorted((out/'ui').glob('*'))] if p.is_file()})
 v=dict(old,status='PASSED',scope='verb-notebook-normal-entry-ui',sources=now,proofs=proofs,binaries={p:sha(R/p) for p in old['binaries']},core_tests_reused='CoreとTestsは前段から不変。P13により9件と旧保存270件の証跡を再利用し、ゲーム内部の一巡と実保存復旧だけ再実施。',limits='評定・方針・陣触れは指定された後続工程。全件・全国長期性能・ユーザー試し遊びは未実施。')
 (D/'verification.json').write_bytes((json.dumps(v,ensure_ascii=False,indent=2)+'\n').encode());check(v);print('動詞・手帳の内部操作と実保存復旧が成功。Core不変の検査を再実行せず再利用。')
