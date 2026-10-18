"""Read-only validation before launching/building the saved right motion package."""
from pathlib import Path
import json
import officer_art_pipeline as g
R=Path(__file__).resolve().parents[1];w=json.loads((R/'Art/Officer/work-record.json').read_text(encoding='utf-8'));p=w['plans'][w['back_motion_plan']];binding=g.require_plan(R,'motion',p)
result=p['results'];g.evidence(R,result['runtime_data']);path=g.evidence(R,result['manifest']);manifest=json.loads(path.read_text(encoding='utf-8'));assert manifest['binding']==binding
for value in manifest['sheets'].values():
 path=R/'Game'/value['path'].removeprefix('res://');assert g.sha(path)==value['sha256'],str(path)
print('BACK_NATIVE_PACKAGE_READY '+binding)
