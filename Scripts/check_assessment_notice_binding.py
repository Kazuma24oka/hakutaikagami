from pathlib import Path
import json
from verify_assessment_notice import ROOT,BASE,OUT,sha,source_files
j=json.loads((BASE/'verification.json').read_bytes())
assert j['status']=='PASSED'
assert j['sources']=={p.relative_to(ROOT).as_posix():sha(p) for p in source_files()}
for p,h in j['binaries'].items():assert sha(ROOT/p)==h,p
for p,h in j['images'].items():assert sha(OUT/p)==h,p
assert sha(OUT/'tests/assessment-notice.trx')==j['trx_sha256']
assert len(j['comparisons'])==32 and set(j['comparisons'].values())=={0}
assert j['visual_review'] and j['visual_review']['inspected']
for p,h in j['visual_review']['inspected'].items():assert sha(OUT/p)==h,p
print('PASS assessment notice source, binary, tests and actual image binding')
