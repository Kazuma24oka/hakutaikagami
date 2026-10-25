"""Fail closed when inputs, binary, assets, evidence or image review changed."""
from pathlib import Path
import hashlib,json
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'Docs/Design/JapanMapV1';OUT=BASE/'runtime-local'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 r=json.loads((BASE/'verification.json').read_bytes());assert r['status']=='PASSED' and r['visual_review']['status']=='SELF_REVIEWED_NOT_USER_APPROVAL'
 for group in ['source_sha256','binary_sha256','asset_sha256']:
  for name,value in r[group].items():assert sha(ROOT/name)==value,name+' changed; reverify'
 for name,value in r['images'].items():assert sha(OUT/name)==value,name+' changed; reverify'
 assert r['visual_review']['images']==r['images']
 assert sha(OUT/'tests/japan.trx')==r['trx_sha256'] and sha(OUT/'checks.json')==r['checks_sha256']
 print('PASS: current source, binary, assets, actual GPU images and self-review match')
if __name__=='__main__':main()
