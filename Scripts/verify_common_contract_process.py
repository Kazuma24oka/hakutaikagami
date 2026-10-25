"""Foreign-process save/restore, abrupt exit and synthetic peace-loop verification."""
from pathlib import Path
import hashlib,json,subprocess,uuid

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'Docs/Design/CommonContractV1/runtime-local'
def main():
 OUT.mkdir(parents=True,exist_ok=True)
 run=OUT/('run-'+uuid.uuid4().hex);run.mkdir()
 dll=ROOT/'Tools/CommonContractProbe/bin/Debug/net8.0/CommonContractProbe.dll'
 def call(mode,expected=0):
  p=subprocess.run(['dotnet',str(dll),mode,str(run/'saves')],cwd=ROOT,capture_output=True,text=True,encoding='utf-8')
  (run/(mode+('.interrupt' if expected else '')+'.txt')).write_text(p.stdout+p.stderr,encoding='utf-8')
  assert p.returncode==expected,(mode,p.returncode,p.stdout,p.stderr)
  return json.loads(p.stdout) if expected==0 else None
 a=call('exercise');b=call('restore')
 assert a['SavedSeconds']==b['RestoredSeconds']==4
 assert a['SavedDigest']==b['RestoredDigest']
 assert b['InitiallyPaused'] is True
 assert a['Expected']==b['Actual'],(a['Expected'],b['Actual'])
 assert b['Actual']['Transfers']==1 and b['Actual']['Treasury']==40 and b['Actual']['Assessment']=='Confirmed'
 call('interrupt',73)
 c=call('restore')
 assert c['RestoredSeconds']==4 and c['RestoredDigest']==a['SavedDigest']
 assert c['Actual']==a['Expected']
 peace=call('peace');assert peace['Treasury']==40 and peace['Completed']==peace['Assessed']==3
 assert len(peace['Timeline'])==5
 assert any(x['Kind']=='Traveling' and x['ProgressMm']>0 for x in peace['Timeline'][2]['Locations'])
 result={'schema':1,'date':'2026-10-20','status':'PASSED','cross_process':{'saved_seconds':4,'restored_seconds':4,'next_random_identical':a['Expected']['NextRandom']==b['Actual']['NextRandom'],'semantic_digest_identical':a['Expected']['Digest']==b['Actual']['Digest'],'single_transfer':True,'restored_paused':True},'abrupt_process_exit':{'exit_code':73,'stage':'GenerationVerified before current reference publication','old_generation_still_current':True,'scope':'Process exit tested; physical power-loss durability not certified.'},'peace_fixture':peace,'limitations':['Synthetic seconds-domain region; not historical geography or production balancing.','Semantic digest excludes control pause and revision metadata only.','Not all 48 design acceptance cases or national-scale performance are complete.'],'run_evidence':str(run.relative_to(ROOT)).replace('\\','/'),'sources':{str(p.relative_to(ROOT)).replace('\\','/'):hashlib.sha256(p.read_bytes()).hexdigest() for folder in ['Core/Contracts','Tools/CommonContractProbe'] for p in (ROOT/folder).glob('*.cs')}}
 path=ROOT/'Docs/Design/CommonContractV1/process-verification.json';path.write_bytes((json.dumps(result,ensure_ascii=False,indent=2)+'\n').encode())
 print('PASS: foreign-process continuation, abrupt exit recovery, 3-task synthetic peace loop')
if __name__=='__main__':main()
