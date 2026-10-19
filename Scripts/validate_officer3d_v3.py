"""Bound inputs -> GLB skin/material audit -> build/import -> real GPU -> separate-process restore."""
from pathlib import Path
import sys,json,subprocess,struct,numpy as np
import officer3d_v3_pipeline as gate

def run(args,root):
 p=subprocess.run([str(a) for a in args],cwd=root,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,encoding='utf-8',errors='replace');print(p.stdout,flush=True)
 assert p.returncode==0 and not any(x in p.stdout for x in ['ERROR:','SCRIPT ERROR:','Shader compilation failed']),str(args[0])+' failed'
 return p.stdout

def audit(path):
 raw=path.read_bytes();length=struct.unpack_from('<I',raw,12)[0];g=json.loads(raw[20:20+length]);start=20+length;size=struct.unpack_from('<I',raw,start)[0];data=raw[start+8:start+8+size]
 def acc(i):
  a=g['accessors'][i];v=g['bufferViews'][a['bufferView']];dtype={5126:'<f4',5123:'<u2',5121:'u1',5125:'<u4'}[a['componentType']];n={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4,'MAT4':16}[a['type']];dt=np.dtype(dtype);stride=v.get('byteStride',dt.itemsize*n)
  return np.ndarray((a['count'],n),dtype=dt,buffer=data,offset=v.get('byteOffset',0)+a.get('byteOffset',0),strides=(stride,dt.itemsize))
 assert len(g['skins'])==1 and len(g['skins'][0]['joints'])==73
 vertices=0;maximum=0
 for m in g['meshes']:
  for p in m['primitives']:
   pos=acc(p['attributes']['POSITION']);assert np.isfinite(pos).all();weights=acc(p['attributes']['WEIGHTS_0']);joints=acc(p['attributes']['JOINTS_0']);assert np.isfinite(weights).all() and weights.min()>=0 and joints.max()<73
   error=float(np.abs(weights.sum(axis=1)-1).max());maximum=max(maximum,error);assert error<.001;vertices+=len(pos)
 assert {a['name'] for a in g['animations']}=={'idle','walk','attack'}
 return {'vertices_including_seams':vertices,'max_weight_sum_error':maximum,'skin_count':1,'bones':73,'parts':len(g['meshes'])}

def dependencies(root):
 names=['Art/Officer3D/TrialV3/contract.json','Art/Officer3D/TrialV3/export-manifest.json','Scripts/officer3d_v3_pipeline.py','Scripts/validate_officer3d_v3.py','Game/Scripts/Officer3DCustomActor.cs','Game/Scripts/Officer3DCreator.cs','Game/Scripts/Officer3DV3Validation.cs','Game/Scripts/Officer3DV3Gate.cs','Game/Scripts/Officer3DModel.cs','Game/Shaders/Officer3D.gdshader','Game/Shaders/Officer3DV3.gdshader','Game/Scenes/Officer3DCreator.tscn','Game/Data/Officer3D/face-protection.json','Core/Common/SessionInfrastructure.cs','Game/Assets/Officer3D/TrialV1/normal.png','Game/Assets/Officer3D/TrialV1/orm.png','Game/Assets/Officer3D/TrialV2/materials.png']
 return names+[str(p.relative_to(root)) for p in sorted((root/'Core').rglob('*.cs')) if not any(x in p.parts for x in ['obj','bin']) and str(p.relative_to(root)) not in names]+[f'Game/Assets/Officer3D/TrialV3/{n}.glb' for n in ['near','mid','far']]

def main():
 root=Path(sys.argv[1]);e=root/'Art/Officer3D/TrialV3';export=gate.read(e/'export-manifest.json');assert gate.require(root,'export')==export['binding']
 if '--bind' in sys.argv:gate.bind(root,'runtime',dependencies(root))
 binding=gate.require(root,'runtime');godot=root/'.tools/godot-4.5.2/Godot_v4.5.2-stable_mono_win64/Godot_v4.5.2-stable_mono_win64_console.exe'
 if '--show' in sys.argv:
  visual=gate.read(e/'visual-review.json');assert visual['runtime_plan_sha256']==gate.sha(e/'runtime-plan.json')
  for n,h in visual['evidence_sha256'].items():assert gate.sha(e/'runtime-local'/n)==h
  subprocess.Popen([str(godot),'--path',str(root/'Game'),'res://Scenes/Officer3DCreator.tscn'],cwd=root,creationflags=subprocess.CREATE_NO_WINDOW);return
 other=subprocess.check_output(['powershell','-NoProfile','-Command',"@(Get-Process | Where-Object {$_.ProcessName -like 'Godot*'}).Count"],text=True).strip();assert other=='0','Close existing Godot previews before comparable GPU measurement';print('V3_NO_OTHER_GODOT_PREVIEWS')
 geometry={lod:audit(root/f'Game/Assets/Officer3D/TrialV3/{lod}.glb') for lod in ['near','mid','far']}
 run(['dotnet','build','Game/HakutaiKagami.Game.csproj','--no-restore'],root)
 run([godot,'--headless','--path',root/'Game','--editor','--import'],root)
 assert 'V3_TEST_COMPLETE' in run([godot,'--path',root/'Game','--rendering-method','gl_compatibility','res://Scenes/Officer3DCreator.tscn','--','--v3-test'],root)
 assert 'V3_RESTORE_COMPLETE' in run([godot,'--path',root/'Game','--rendering-method','gl_compatibility','res://Scenes/Officer3DCreator.tscn','--','--v3-restore'],root)
 assert gate.require(root,'runtime')==binding,'Inputs changed during validation'
 local=e/'runtime-local';report=gate.read(local/'results.json');names=report.pop('raw_capture_names')+['restart'];assert len(names)==len(set(names)) and len(names)>250
 report.update({'runtime_binding':binding,'runtime_plan_sha256':gate.sha(e/'runtime-plan.json'),'export_binding':export['binding'],'geometry_audit':geometry,'restart_restore':'passed','evidence_sha256':{n+'.png':gate.sha(local/(n+'.png')) for n in names}})
 (e/'runtime-report.json').write_bytes((json.dumps(report,ensure_ascii=False,indent=2)+'\n').encode());print('V3_VALIDATED',binding,len(names))
if __name__=='__main__':main()
