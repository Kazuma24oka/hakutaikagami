import json,xml.etree.ElementTree as E
from verify_transport_work import ROOT,BASE,sources,sha
v=json.loads((BASE/'verification.json').read_bytes());assert v['status']=='PASSED' and v['completion']=='5/5'
assert v['sources']==sources(),'current inputs differ'
for section in ['proofs','binaries']:
 for p,h in v[section].items():assert sha(ROOT/p)==h,p
t=v['tests']
for key,n in [('trx',2273),('final_trx',358)]:
 c=E.parse(ROOT/t[key]).find('.//{*}Counters').attrib;assert c['passed']==str(n) and c['failed']=='0' and c['notExecuted']=='0'
old=json.loads((ROOT/t['baseline_inputs']).read_bytes());current={p:h for p,h in v['sources'].items() if p.startswith(('Core/','Tests/')) or p=='Scripts/current_test_contract.json'}
delta={p for p in set(current)|set(old['sources']) if current.get(p)!=old['sources'].get(p)}
assert delta==set(t['changed_after_full'])=={'Core/Contracts/TransportWork.cs','Core/Contracts/ContractModels.cs','Core/Contracts/TrafficWorld.cs','Core/Contracts/RegionalCampaign.cs','Tests/TransportWorkLocationTests.cs','Scripts/current_test_contract.json'}
gpu=json.loads((BASE/'runtime-local/gpu-evidence.json').read_bytes());runtime=lambda d:{p:h for p,h in d.items() if p.startswith(('Core/','Game/'))}
assert runtime(gpu['sources'])==runtime(v['sources']) and gpu['processes']==12
core='Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Core.dll';assert sha(ROOT/core)==sha(BASE/'runtime-local/final-test-bin/HakutaiKagami.Core.dll')
assert sum(x['world_and_pixels'] for x in v['comparisons'].values())==102 and v['visual_review']=='inspected'
print('PASS: full2273 baseline plus final-production358; GPU12/102; exact source delta, binaries, raw results and restore proofs')
