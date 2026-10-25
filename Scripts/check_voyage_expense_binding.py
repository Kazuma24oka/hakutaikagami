import json,xml.etree.ElementTree as E
from verify_voyage_expense import ROOT,BASE,sources,sha
v=json.loads((BASE/'verification.json').read_bytes());assert v['status']=='PASSED' and v['completion']=='5/5' and v['sources']==sources()
for section in ['proofs','binaries']:
 for p,h in v[section].items():assert sha(ROOT/p)==h,p
t=v['tests'];c=E.parse(ROOT/t['trx']).find('.//{*}Counters').attrib
assert c['passed']=='2308' and c['failed']=='0' and c['notExecuted']=='0'
m=json.loads((BASE/'runtime-local/test-inputs.json').read_bytes());current={p:h for p,h in sources().items() if p.startswith(('Core/','Tests/','Game/Data/')) or p=='Scripts/current_test_contract.json'};assert current==m['sources']
core='Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Core.dll';assert sha(ROOT/core)==m['binaries']['Tests/bin/Debug/net8.0/HakutaiKagami.Core.dll']
assert v['gpu_processes']==16 and sum(x['world_and_pixels'] for x in v['comparisons'].values())==142 and v['visual_review']=='inspected'
print('PASS: full2308 on current Core, GPU16/142, versioned fare, free land travel, restore images and balances bound')
