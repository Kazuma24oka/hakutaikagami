from pathlib import Path
import subprocess,re
ROOT=Path(__file__).resolve().parents[1]
def main():
 exe=ROOT/'.tools/godot-4.5.2/Godot_v4.5.2-stable_mono_win64/Godot_v4.5.2-stable_mono_win64_console.exe'
 result=subprocess.run([str(exe),'--headless','--path',str(ROOT/'Game'),'--script','res://Tools/author_ground_material.gd'],cwd=ROOT,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,creationflags=subprocess.CREATE_NO_WINDOW,timeout=60)
 log=result.stdout.decode('utf8',errors='replace');assert result.returncode==0 and 'GROUND_MATERIAL_AUTHOR_PASSED' in log and 'ERROR:' not in log,log
 p=ROOT/'Game/Assets/Scenery/Materials/field-earth-grain.tres';text=p.read_bytes().decode();text=re.sub(r' uid="uid://[^"]+"','',text)
 for i,key in enumerate(re.findall(r' id="([^"]+)"',text)):
  text=text.replace('id="'+key+'"','id="Resource_'+str(i)+'"').replace('SubResource("'+key+'")','SubResource("Resource_'+str(i)+'")')
 p.write_bytes(text.encode());print('Generated deterministic grayscale ground grain with mipmaps')
if __name__=='__main__':main()
