"""Fail closed after source, asset, binary or inspected-image changes."""
from pathlib import Path
import json
from verify_regional_route import ROOT,BASE,OUT,sha,sources,assets
def main():
 r=json.loads((BASE/'verification.json').read_text(encoding='utf-8'))
 assert r['status']=='PASSED' and isinstance(r['visual_review'],dict),'Inspect fresh GPU images first'
 assert r['visual_review']['inspected_image_sha256']==r['images'],'Visual review does not match images'
 assert len(r['pixel_checks'])==12 and all(c['changed_pixels']==0 for c in r['pixel_checks'].values()),'Missing or failed pixel comparisons'
 current={p.relative_to(ROOT).as_posix() for p in sources() if 'obj' not in p.parts and 'bin' not in p.parts}
 assert current==set(r['source_sha256']),'Source input set changed'
 assert {p.relative_to(ROOT).as_posix() for p in assets()}==set(r['asset_sha256']),'Asset input set changed'
 for section in ['source_sha256','binary_sha256','asset_sha256']:
  for name,digest in r[section].items():assert sha(ROOT/name)==digest,'Changed '+name
 for name,digest in r['images'].items():assert sha(OUT/name)==digest,'Changed image '+name
 assert sha(OUT/'tests/route.trx')==r['trx_sha256'],'Test evidence changed'
 print('REGIONAL_BINDING_VERIFIED')
if __name__=='__main__':main()
