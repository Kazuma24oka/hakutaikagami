"""Validate immutable export/runtime plans before build, import or GPU execution."""
from pathlib import Path
import argparse,hashlib,json,subprocess,sys
import officer3d_pipeline as gate

def run(args,cwd):
 p=subprocess.run([str(x) for x in args],cwd=cwd,encoding='utf-8',errors='replace',stdout=subprocess.PIPE,stderr=subprocess.STDOUT)
 print(p.stdout,flush=True)
 if p.returncode or any(t in p.stdout for t in ['SCRIPT ERROR:','ERROR:','Shader compilation failed']):raise RuntimeError('Execution failed: '+str(args[0]))
 return p.stdout

def main():
 parser=argparse.ArgumentParser();parser.add_argument('root');parser.add_argument('--show',action='store_true');a=parser.parse_args();root=Path(a.root)
 binding,c,p=gate.require(root,'runtime');gate.require(root,'export')
 manifest=gate.read(root/'Art/Officer3D/TrialV1/export-manifest.json');glb=root/'Game/Assets/Officer3D/TrialV1/officer.glb'
 assert gate.sha(glb)==manifest['glb']['sha256'];assert manifest['binding']==gate.require(root,'export')[0]
 assert sum(manifest['body_partition_faces'].values())==manifest['body_original_faces'];assert set(manifest['slots'])==set(c['slots'])
 print('OFFICER3D FIXED_INPUT_GATE '+binding,flush=True)
 if a.show:
  verified=gate.read(root/'Art/Officer3D/TrialV1/runtime-report.json');visual=gate.read(root/'Art/Officer3D/TrialV1/visual-review.json')
  assert verified['runtime_binding']==binding and verified['glb_sha256']==gate.sha(glb),'GPU results are stale'
  assert visual['runtime_binding']==binding and visual['status']=='SELF_REVIEWED_USER_NOT_YET_ACCEPTED','Visual review missing or stale'
  for name,digest in visual['evidence_sha256'].items():assert gate.sha(root/'Art/Officer3D/TrialV1/runtime-local'/name)==digest,'Reviewed image changed: '+name
 godot=root/'.tools/godot-4.5.2/Godot_v4.5.2-stable_mono_win64/Godot_v4.5.2-stable_mono_win64_console.exe'
 run(['dotnet','build','Game/HakutaiKagami.Game.csproj','--no-restore'],root)
 run([godot,'--headless','--path',root/'Game','--editor','--import'],root)
 if a.show:
  subprocess.Popen([str(godot),'--path',str(root/'Game'),'res://Scenes/Officer3DReview.tscn'],cwd=root,creationflags=subprocess.CREATE_NO_WINDOW)
  return
 log=run([godot,'--path',root/'Game','--rendering-method','gl_compatibility','res://Scenes/Officer3DReview.tscn','--','--officer3d-test'],root)
 assert 'OFFICER3D GPU_TEST_COMPLETE' in log
 restart=run([godot,'--path',root/'Game','--rendering-method','gl_compatibility','res://Scenes/Officer3DReview.tscn','--','--officer3d-restore-test'],root)
 assert 'OFFICER3D RESTART_RESTORE_COMPLETE' in restart
 local=root/'Art/Officer3D/TrialV1/runtime-local';report=gate.read(local/'results.json')
 report['runtime_binding']=binding;report['export_binding']=manifest['binding'];report['glb_sha256']=gate.sha(glb)
 report['restart_restore']='separate Godot process passed'
 names=['face-paint-before','face-paint-mask','face-paint-after','gear-before','gear-mask','gear-tinted','torso-mask','torso-tinted','review-default','restart-restored']
 for bare in [False,True]:
  for angle in [0,90,180,270]:names.extend([f'{"bare" if bare else "full"}-{angle}',f'skin-mask-{bare}-{angle}',f'skin-tan-{bare}-{angle}'])
 names.extend(f'off-{s}-{pose}' for s in c['slots'] for pose in ['idle','walk'])
 names.extend('motion-'+id for id in ['idle','walk','attack']);names.extend(f'walk-frame-{i:02}' for i in range(24));names.extend('actors-'+str(i) for i in [1,10,50])
 assert len(set(names))==90
 report['evidence_sha256']={name+'.png':gate.sha(local/(name+'.png')) for name in names}
 (root/'Art/Officer3D/TrialV1/runtime-report.json').write_bytes((json.dumps(report,ensure_ascii=False,indent=2)+'\n').encode())
if __name__=='__main__':main()
