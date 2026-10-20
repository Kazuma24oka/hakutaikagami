from pathlib import Path
import json,hashlib
from verify_officer_loop import source_files
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'Docs/Design/OfficerLoopV1';OUT=BASE/'runtime-local'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 j=json.loads((BASE/'verification.json').read_bytes());assert j['status']=='PASSED'
 assert set(j['sources'])=={p.relative_to(ROOT).as_posix() for p in source_files()},'Source inventory changed'
 for section in ['sources','binaries']:
  for name,h in j[section].items():assert sha(ROOT/name)==h,'Stale '+name
 for name,h in j['images'].items():assert sha(OUT/name)==h,'Stale image '+name
 assert sha(OUT/'tests/officer-loop.trx')==j['trx_sha256']
 assert sha(BASE/j['video']['file'])==j['video']['sha256']
 assert sha(OUT/'video-decode.log')==j['video']['decode_sha256']
 assert j['summary']['equal_comparisons']==40 and all(v==0 for v in j['comparisons'].values())
 review=j.get('visual_review');assert review and review['passed'] and review['video_sha256']==j['video']['sha256'],'Fresh visual inspection required'
 for name,h in review['images'].items():assert sha(OUT/name)==h
 print('PASS officer loop sources/binaries/40 GPU comparisons/visual review binding')
if __name__=='__main__':main()
