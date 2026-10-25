from pathlib import Path
import json,hashlib
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'Docs/Design/ContinuousWorldV1'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 j=json.loads((BASE/'verification.json').read_bytes());assert j['status']=='PASSED' and isinstance(j['visual_review'],dict)
 for group in ['sources','binaries']:
  for n,h in j[group].items():assert sha(ROOT/n)==h,'Changed '+n
 for n,h in j['images'].items():assert sha(BASE/'runtime-local'/n)==h,'Changed '+n
 assert sha(BASE/'runtime-local/tests/continuous.trx')==j['trx_sha256']
 assert sha(BASE/j['video']['file'])==j['video']['sha256']==j['visual_review']['video_sha256']
 assert sha(BASE/'runtime-local/video-decode.log')==j['video']['decode_sha256']
 print('PASS current source/assets/DLL/images/video and recorded visual inspection')
if __name__=='__main__':main()
