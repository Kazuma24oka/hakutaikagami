"""V3 dependency gate. V1 remains an immutable comparison baseline."""
from pathlib import Path
import json,hashlib

def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def read(p):return json.loads(Path(p).read_text(encoding='utf-8'))
def require(root,stage):
 root=Path(root);plan=read(root/f'Art/Officer3D/TrialV3/{stage}-plan.json')
 assert plan['stage']==stage
 for item in plan['dependencies']:
  p=Path(item['path']);p=p if p.is_absolute() else root/p
  assert sha(p)==item['sha256'],'Stale V3 dependency: '+str(p)
 return hashlib.sha256(json.dumps(plan,sort_keys=True,separators=(',',':')).encode()).hexdigest()
def bind(root,stage,paths):
 root=Path(root);plan={'schema':1,'stage':stage,'dependencies':[{'path':str(p),'sha256':sha(Path(p) if Path(p).is_absolute() else root/p)} for p in paths]}
 (root/f'Art/Officer3D/TrialV3/{stage}-plan.json').write_text(json.dumps(plan,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
 return require(root,stage)
