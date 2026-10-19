"""Normal game integration gate and GPU smoke runner. Uses only dedicated test saves."""
from pathlib import Path
import sys,json,hashlib,subprocess

def sha(path):return hashlib.sha256(Path(path).read_bytes()).hexdigest()
def read(path):return json.loads(Path(path).read_text(encoding='utf-8'))
def dependencies(root):
 names=['Game/project.godot','Game/HakutaiKagami.Game.csproj','Game/Data/prototype.json','Game/Scenes/Main.tscn','Art/Officer3D/WorldIntegrationV1/contract.json','Art/Officer3D/TrialV3/contract.json','Art/Officer3D/TrialV3/export-manifest.json','Scripts/validate_officer_world.py']
 names += [str(p.relative_to(root)).replace('\\','/') for p in sorted((root/'Core').rglob('*.cs')) if not any(n in p.parts for n in ['obj','bin'])]
 names += ['Game/Scripts/'+n+'.cs' for n in ['Main','TaxPanel','OfficerWorldStage','OfficerWorldGate','OfficerWorldValidation','Officer3DCustomActor','Officer3DCreator','Officer3DModel']]
 names += ['Game/Shaders/'+n+'.gdshader' for n in ['Officer3D','Officer3DV3']]
 names += ['Game/Data/Officer3D/face-protection.json','Game/Assets/Officer3D/TrialV1/normal.png','Game/Assets/Officer3D/TrialV1/orm.png','Game/Assets/Officer3D/TrialV2/materials.png']
 names += ['Game/Assets/Officer3D/TrialV3/'+n+'.glb' for n in ['near','mid','far']]
 return names
def require(root):
 p=read(root/'Art/Officer3D/WorldIntegrationV1/runtime-plan.json');assert p['schema']==1
 for d in p['dependencies']:assert sha(root/d['path'])==d['sha256'],'Stale world integration dependency: '+d['path']
 return sha(root/'Art/Officer3D/WorldIntegrationV1/runtime-plan.json')
def run(args,root):
 p=subprocess.run([str(x) for x in args],cwd=root,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,encoding='utf-8',errors='replace');print(p.stdout,flush=True)
 assert p.returncode==0 and not any(e in p.stdout for e in ['ERROR:','SCRIPT ERROR:','Shader compilation failed']),str(args[0])+' failed'
 return p.stdout
def main():
 root=Path(sys.argv[1]).resolve();base=root/'Art/Officer3D/WorldIntegrationV1';base.mkdir(exist_ok=True)
 if '--bind' in sys.argv:(base/'runtime-plan.json').write_bytes((json.dumps({'schema':1,'dependencies':[{'path':n,'sha256':sha(root/n)} for n in dependencies(root)]},ensure_ascii=False,indent=2)+'\n').encode())
 binding=require(root);godot=root/'.tools/godot-4.5.2/Godot_v4.5.2-stable_mono_win64/Godot_v4.5.2-stable_mono_win64_console.exe'
 if '--show' in sys.argv:
  v=read(base/'visual-review.json');assert v['runtime_plan_sha256']==binding
  for n,h in v['evidence_sha256'].items():assert sha(base/'runtime-local'/n)==h
  subprocess.Popen([str(godot),'--path',str(root/'Game'),'--','--officer-world-show'],cwd=root,creationflags=subprocess.CREATE_NO_WINDOW);return
 assert subprocess.check_output(['powershell','-NoProfile','-Command',"@(Get-Process | Where-Object {$_.ProcessName -like 'Godot*'}).Count"],text=True).strip()=='0','Close existing Godot previews before validation'
 tests=run(['dotnet','test','Tests/HakutaiKagami.Tests.csproj','--no-restore','--verbosity','minimal'],root)
 run(['dotnet','build','Game/HakutaiKagami.Game.csproj','--no-restore'],root)
 run([godot,'--headless','--path',root/'Game','--editor','--import'],root)
 assert 'OFFICER_WORLD_TEST_COMPLETE' in run([godot,'--path',root/'Game','--','--officer-world-test'],root)
 assert 'OFFICER_WORLD_RESTORE_COMPLETE' in run([godot,'--path',root/'Game','--','--officer-world-restore'],root)
 assert require(root)==binding,'Inputs changed during validation'
 report=read(base/'runtime-local/results.json');names=report.pop('captures')+['restart.png'];assert len(names)==len(set(names))
 report.update({'runtime_plan_sha256':binding,'evidence_sha256':{n:sha(base/'runtime-local'/n) for n in names},'restart_restore':'passed','core_test_output':tests})
 (base/'runtime-report.json').write_bytes((json.dumps(report,ensure_ascii=False,indent=2)+'\n').encode());print('OFFICER_WORLD_VALIDATED',binding,len(names))
if __name__=='__main__':main()
