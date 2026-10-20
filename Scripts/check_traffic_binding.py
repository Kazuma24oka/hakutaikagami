from pathlib import Path
import json,hashlib
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'Docs/Design/TrafficWorldV1'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 j=json.loads((BASE/'verification.json').read_bytes());assert j['status']=='PASSED' and isinstance(j.get('visual_review'),dict),'Actual image/video review required'
 for group in ['sources','binaries','assets','entry_tools']:
  for name,digest in j[group].items():assert sha(ROOT/name)==digest,'Changed '+name
 for name,digest in j['images'].items():assert sha(BASE/'runtime-local'/name)==digest,'Changed image '+name
 assert sha(BASE/'runtime-local/tests/traffic.trx')==j['trx_sha256'],'Changed test results'
 assert sha(BASE/'runtime-local/video-decode-check.log')==j['video']['decode_log_sha256'],'Changed video decode evidence'
 assert sha(BASE/j['video']['file'])==j['video']['sha256'],'Changed video'
 assert j['visual_review']['video_sha256']==j['video']['sha256'],'Video review not bound'
 print('PASS: current source, DLL, assets, screenshots and reviewed video')
if __name__=='__main__':main()
