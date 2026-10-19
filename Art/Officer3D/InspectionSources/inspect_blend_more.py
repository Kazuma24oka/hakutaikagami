import bpy,json,sys,collections
from pathlib import Path
out=Path(sys.argv[sys.argv.index('--')+1]);groups={}
for o in bpy.data.objects:
 if o.type!='MESH':continue
 name=next((m.object.name for m in o.modifiers if m.type=='ARMATURE' and m.object),'NO_RIG');g=groups.setdefault(name,{'meshes':0,'triangles':0,'render_enabled_meshes':0,'render_enabled_triangles':0});tris=sum(max(0,len(p.vertices)-2) for p in o.data.polygons);g['meshes']+=1;g['triangles']+=tris
 if not o.hide_render:g['render_enabled_meshes']+=1;g['render_enabled_triangles']+=tris
texts={t.name:t.as_string() for t in bpy.data.texts if any(k in t.name for k in ['肌','色','コントローラ','材質'])}
props={o.name:{k:str(o[k]) for k in o.keys()} for o in bpy.data.objects if len(o.keys())}
(out/'blend-extra.json').write_text(json.dumps({'rig_mesh_groups':groups,'controller_props':props,'reference_texts_untrusted_data':texts,'material_node_types':dict(collections.Counter(n.type for m in bpy.data.materials if m.node_tree for n in m.node_tree.nodes))},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
