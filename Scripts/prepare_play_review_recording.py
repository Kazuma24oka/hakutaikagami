from pathlib import Path
import shutil,re,json,hashlib
ROOT=Path('C:/Workspace/Projects/Games/hakutaikagami')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def prepare(root,out,key):
 assert re.fullmatch('[0-9a-f]{32}',key)
 record=out/('record-project-'+key);assert not record.exists();record.mkdir(parents=True)
 shutil.copytree(root/'Game',record/'Game',ignore=shutil.ignore_patterns('.godot','bin','obj'))
 for rel in ['.godot/imported','.godot/mono/temp/bin/Debug']:
  shutil.copytree(root/'Game'/rel,record/'Game'/rel)
 uid=root/'Game/.godot/uid_cache.bin'
 if uid.exists():shutil.copy2(uid,record/'Game/.godot/uid_cache.bin')
 p=record/'Game/project.godot';source=p.read_bytes();text=source.decode('utf-8-sig')
 for name,value in [('viewport_width',1280),('viewport_height',900)]:
  text,n=re.subn(r'(?m)^window/size/'+name+r'=\d+$','window/size/'+name+'='+str(value),text);assert n==1
 p.write_bytes(text.encode());checks={}
 for folder,pattern in [('Scripts','*.cs'),('Scenes','*.tscn'),('Data','*.json')]:
  for f in (root/'Game'/folder).rglob(pattern):
   rel=f.relative_to(root/'Game');assert sha(f)==sha(record/'Game'/rel),str(rel);checks[rel.as_posix()]=sha(f)
 for name in ['HakutaiKagami.Game.dll','HakutaiKagami.Core.dll']:
  rel=Path('.godot/mono/temp/bin/Debug')/name;assert sha(root/'Game'/rel)==sha(record/'Game'/rel);checks[rel.as_posix()]=sha(record/'Game'/rel)
 (out/'recording-project.json').write_bytes(json.dumps(dict(record_project=str(record),actual_project_sha256=hashlib.sha256(source).hexdigest(),recording_project_sha256=sha(p),overrides={'viewport_width':1280,'viewport_height':900},copied_runtime_inputs=checks,scope='Isolated recording copy. Only movie viewport size overrides;actual project settings unchanged. Exact copied runtime code/scenes/data and Game/Core binaries verified.'),indent=2).encode());return record
