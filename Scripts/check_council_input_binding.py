from pathlib import Path
import hashlib,json
from verify_council_input import source_files
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'Docs/Design/CouncilInputV1';OUT=BASE/'runtime-local'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 j=json.loads((BASE/'verification.json').read_bytes());assert j['status']=='PASSED'
 assert set(j['sources'])=={p.relative_to(ROOT).as_posix() for p in source_files()},'Source inventory changed'
 for group in ['sources','binaries']:
  for n,h in j[group].items():assert sha(ROOT/n)==h,'Changed '+n
 for n,h in j['images'].items():assert sha(OUT/n)==h,'Changed image '+n
 assert sha(OUT/'tests/council-input.trx')==j['trx_sha256']
 assert j['summary']['equal_comparisons']==24 and all(v==0 for v in j['comparisons'].values())
 r=j['visual_review'];assert r and r['passed'],'Inspect actual images before release'
 for n,h in r['images'].items():assert sha(OUT/n)==h
 print('PASS council input sources, runtime, tests, restored lines and actual images')
if __name__=='__main__':main()
