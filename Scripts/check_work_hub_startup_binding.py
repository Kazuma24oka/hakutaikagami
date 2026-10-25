import json
from verify_work_hub_startup import ROOT,BASE,source_files,sha
j=json.loads((BASE/'verification.json').read_bytes());assert j['status']=='PASSED'
assert j['sources']=={p.relative_to(ROOT).as_posix():sha(p) for p in source_files()}
for p,h in j['binaries'].items():assert sha(ROOT/p)==h,p
for p,h in j['images'].items():assert sha(ROOT/p)==h,p
proof=j['prior_test_proof'];assert sha(ROOT/proof['report'])==proof['report_sha256']
assert sha(ROOT/proof['trx_path'])==proof['trx_sha256']
assert len(j['comparisons'])==36 and set(j['comparisons'].values())=={0}
assert j['visual_review'] and j['visual_review']['inspected']
for p,h in j['visual_review']['inspected'].items():assert sha(ROOT/p)==h,p
print('PASS work hub current source, tested Core, binary and actual GPU image binding; approved default applied')
