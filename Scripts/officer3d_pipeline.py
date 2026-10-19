"""3D production gate: source, decisions, inputs/tools and semantic contract."""
from pathlib import Path
import hashlib,json
def sha(path):
 h=hashlib.sha256()
 with Path(path).open('rb') as f:
  for b in iter(lambda:f.read(1048576),b''):h.update(b)
 return h.hexdigest()
def read(path):return json.loads(Path(path).read_text(encoding='utf-8'))
def require(root,stage):
 root=Path(root);p=read(root/f'Art/Officer3D/TrialV1/plans/{stage}.json');c=read(root/'Art/Officer3D/TrialV1/contract.json');s=read(root/'Art/Officer3D/production-status.json')
 assert s['options_status']=='USER_SELECTED_ALL_A' and s['selected_options']=={str(i):'A' for i in range(1,11)},'Unselected recommendations'
 assert p['contract_sha256']==sha(root/'Art/Officer3D/TrialV1/contract.json'),'Contract changed'
 assert stage in p['allowed_stages']
 for v in [p['source'],*p['inputs'],*p['tools']]:
  path=Path(v['path']);path=path if path.is_absolute() else root/path
  assert sha(path)==v['sha256'],'Changed dependency: '+str(path)
 assert len(c['slots'])==13 and c['skeleton']['source']=='Armature'
 assert set(c['clips'])=={'idle','walk','attack'}
 assert set(c['skin']['materials'])=={'N00_000_00_Body_00_SKIN (Instance)','N00_000_00_Face_00_SKIN (Instance)'}
 assert c['skin']['exclude_features']==['eyes','brows','mouth','eyeline','highlight','equipment']
 assert c['camera']['projection']=='orthographic' and c['portrait']=='2D illustration'
 assert c['source_preservation']['save_blend'] is False
 payload=json.dumps({'plan':p,'contract':c},sort_keys=True,separators=(',',':')).encode();return hashlib.sha256(payload).hexdigest(),c,p
