"""Current V2 gate -> build/import -> real GPU tests -> independent restart. No native input."""
from pathlib import Path
import sys,json,subprocess
import officer3d_v2_pipeline as gate

def run(args,root):
 p=subprocess.run([str(a) for a in args],cwd=root,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,encoding='utf-8',errors='replace');print(p.stdout,flush=True)
 assert p.returncode==0 and not any(x in p.stdout for x in ['ERROR:','SCRIPT ERROR:','Shader compilation failed']),str(args[0])+' failed'
 return p.stdout

def dependencies(root):
 return ['Game/Data/prototype.json','Game/project.godot','Game/HakutaiKagami.Game.csproj','Game/Scripts/Officer3DModel.cs','Game/Shaders/Officer3D.gdshader','Game/Assets/Officer3D/TrialV1/normal.png','Game/Assets/Officer3D/TrialV1/orm.png',*[str(p.relative_to(root)) for p in sorted((root/'Core').rglob('*.cs')) if not any(x in p.parts for x in ['obj','bin'])],'Art/Officer3D/TrialV2/contract.json','Art/Officer3D/TrialV2/export-manifest.json','Scripts/officer3d_v2_pipeline.py','Scripts/validate_officer3d_v2.py','Game/Scripts/Officer3DActor.cs','Game/Scripts/Officer3DStage.cs','Game/Scripts/Officer3DV2Review.cs','Game/Scripts/Officer3DV2Gate.cs','Game/Scripts/Main.cs','Game/Scripts/TaxPanel.cs','Game/Shaders/Officer3DV2.gdshader','Game/Data/Officer3D/face-protection.json','Game/Data/Officer3D/presets-v2.json','Game/Scenes/Officer3DV2Review.tscn',*[f'Game/Assets/Officer3D/TrialV2/{n}' for n in ['near.glb','mid.glb','far.glb','materials.png']]]

def main():
 root=Path(sys.argv[1]);stage=root/'Art/Officer3D/TrialV2';binding=gate.require(root,'export');export=gate.read(stage/'export-manifest.json');assert binding==export['binding']
 if '--bind' in sys.argv:gate.bind(root,'runtime',dependencies(root))
 runtime=gate.require(root,'runtime');godot=root/'.tools/godot-4.5.2/Godot_v4.5.2-stable_mono_win64/Godot_v4.5.2-stable_mono_win64_console.exe'
 if '--show' in sys.argv:
  visual=gate.read(stage/'visual-review.json');assert visual['runtime_plan_sha256']==gate.sha(stage/'runtime-plan.json')
  for n,h in visual['evidence_sha256'].items():assert gate.sha(stage/'runtime-local'/n)==h
  subprocess.Popen([str(godot),'--path',str(root/'Game'),'res://Scenes/Officer3DV2Review.tscn'],cwd=root,creationflags=subprocess.CREATE_NO_WINDOW);return
 run(['dotnet','build','Game/HakutaiKagami.Game.csproj','--no-restore'],root)
 run([godot,'--headless','--path',root/'Game','--editor','--import'],root)
 assert 'V2_TEST_COMPLETE' in run([godot,'--path',root/'Game','--rendering-method','gl_compatibility','res://Scenes/Officer3DV2Review.tscn','--','--v2-test'],root)
 assert 'V2_RESTORE_COMPLETE' in run([godot,'--path',root/'Game','--rendering-method','gl_compatibility','res://Scenes/Officer3DV2Review.tscn','--','--v2-restore'],root)
 local=stage/'runtime-local';report=gate.read(local/'results.json')
 names=['game-world','review','restart']
 slots=['mask','weapon','underclothes','cloak','helmet','torso','neck_guard','shoulders','arms','feet','waist','thighs','shins']
 for level in ['near','mid','far']:
  names += [f'lod-{level}-{clip}' for clip in ['idle','walk','attack']]+[f'off-{level}-{slot}' for slot in slots]
  names += [f'veteran-{level}-{angle}-{clip}' for angle in [0,90,180,270] for clip in ['idle','walk','attack']]
  names += [f'silhouette-{level}-{angle}' for angle in [0,90,180,270]]
 for angle in [0,90,180,270]:
  names += [f'color-{angle}-{category}-{phase}' for category in ['cloth','lacquer','metal'] for phase in ['before','mask','after']]+[f'skin-{angle}-{phase}' for phase in ['before','mask','after']]+[f'silhouette-source-{angle}']
 names += [f'perf-{level}-{count}' for level in ['source','near','mid','far'] for count in [1,10,50]]+[f'delivery-frame-{i:02}' for i in range(24)]
 assert len(set(names))==len(names)==187
 report.update({'runtime_binding':runtime,'runtime_plan_sha256':gate.sha(stage/'runtime-plan.json'),'export_binding':binding,'restart_restore':'passed in separate process','evidence_sha256':{name+'.png':gate.sha(local/(name+'.png')) for name in names}})
 (stage/'runtime-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
 print('V2_VALIDATED',runtime)
if __name__=='__main__':main()
