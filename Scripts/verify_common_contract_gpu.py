"""Isolated Godot UI callbacks + GPU images, without OS input or closing other previews."""
from pathlib import Path
import hashlib,json,shutil,subprocess,sys,time

ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'Docs/Design/CommonContractV1'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 if '--no-build' not in sys.argv:
  subprocess.run(['dotnet','build','Game/HakutaiKagami.Game.csproj','--no-restore','--verbosity','minimal'],cwd=ROOT,check=True)
 out=BASE/'runtime-local/godot';out.mkdir(parents=True,exist_ok=True)
 exe=ROOT/'.tools/godot-4.5.2/Godot_v4.5.2-stable_mono_win64/Godot_v4.5.2-stable_mono_win64_console.exe'
 startup=subprocess.STARTUPINFO();startup.dwFlags|=subprocess.STARTF_USESHOWWINDOW;startup.wShowWindow=0
 started=time.time()
 proc=subprocess.run([str(exe),'--path',str(ROOT/'Game'),'--resolution','1400x1000','res://Scenes/CommonContractTrial.tscn','--','--common-contract-check','--common-contract-capture'],cwd=ROOT,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,encoding='utf-8',errors='replace',startupinfo=startup,creationflags=subprocess.CREATE_NO_WINDOW,timeout=45)
 (out/'gpu-run.txt').write_text(proc.stdout,encoding='utf-8')
 assert proc.returncode==0 and 'COMMON_CONTRACT_UI_COMPLETE 9' in proc.stdout and 'ERROR:' not in proc.stdout,proc.stdout
 report=json.loads((out/'ui-checks.json').read_text(encoding='utf-8'));assert report['status']=='PASSED' and len(report['checks'])==9
 evidence=BASE/'Evidence';evidence.mkdir(exist_ok=True)
 names=['sealed','in-progress','restored','complete']
 for n in names:
  p=out/(n+'.png');assert p.exists() and p.stat().st_mtime>=started-1
  shutil.copyfile(p,evidence/(n+'.png'))
 sources=list((ROOT/'Core/Contracts').glob('*.cs'))+[ROOT/'Game/Scripts/CommonContractTrial.cs',ROOT/'Game/Scenes/CommonContractTrial.tscn']
 report.update(schema=1,date='2026-10-20',scope='Independent data-only Godot screen; normal 3D Main and legacy saves unchanged.',gpu_log=proc.stdout,images={n+'.png':sha(evidence/(n+'.png')) for n in names},code_sha256={str(p.relative_to(ROOT)).replace('\\','/'):sha(p) for p in sources},binary_sha256={str(p.relative_to(ROOT)).replace('\\','/'):sha(p) for p in [ROOT/'Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Core.dll',ROOT/'Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Game.dll']},visual_review='PENDING')
 (BASE/'godot-verification.json').write_bytes((json.dumps(report,ensure_ascii=False,indent=2)+'\n').encode())
 print('PASS: isolated Godot GPU run, 9 UI checks, 4 fresh captures; native input unused')
if __name__=='__main__':main()
