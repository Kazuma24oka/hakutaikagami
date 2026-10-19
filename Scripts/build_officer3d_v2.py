"""Blender background producer: source ownership mask, three authored LODs, reusable veteran parts. Never save source."""
import bpy,json,sys,math,struct,time
from pathlib import Path
import numpy as np
R=Path(sys.argv[sys.argv.index('--')+1]);sys.path.insert(0,str(R/'Scripts'))
import officer3d_v2_pipeline as gate
binding=gate.require(R,'export');C=gate.read(R/'Art/Officer3D/TrialV2/contract.json');O=R/'Game/Assets/Officer3D/TrialV2';O.mkdir(parents=True,exist_ok=True)
started=time.time();source=Path(bpy.data.filepath);assert gate.sha(source)==C['source_blend_sha256']
# Replay the exact V1 UV recipe, without executing its bake/output sections.
prefix=(R/'Scripts/export_officer3d_trial.py').read_text(encoding='utf-8').split('materials={m')[0]
scope={'__file__':str(R/'Scripts/export_officer3d_trial.py')};exec(compile(prefix,'v1_uv_recipe','exec'),scope)
gear=scope['gear'];size=C['material_mask']['size'];roles=C['source_material_roles']
# Rasterize evaluated source triangle material IDs, not texture colors. Same UV recipe as V1.
uvs=[];owners=[];deps=bpy.context.evaluated_depsgraph_get();role_ids={n:['cloth','lacquer','metal'].index(v) for n,v in roles.items()}
for obj in gear:
 for mod in obj.modifiers:
  mod.show_viewport=mod.show_render
  if mod.type=='SUBSURF':mod.levels=mod.render_levels
 evaluated=obj.evaluated_get(deps);mesh=evaluated.to_mesh(preserve_all_data_layers=True,depsgraph=deps);mesh.calc_loop_triangles();layer=mesh.uv_layers['GAMEUV']
 for tri in mesh.loop_triangles:
  uvs.append([tuple(layer.data[i].uv) for i in tri.loops]);owners.append(role_ids[obj.data.materials[tri.material_index].name])
 evaluated.to_mesh_clear()
np.savez_compressed(str(O/'source-material-uv.npz'),uv=np.array(uvs,dtype=np.float32),owner=np.array(owners,dtype=np.uint8))
import subprocess
python=Path(r'C:\Users\hizen\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe')
subprocess.run([str(python),str(R/'Scripts/rasterize_officer3d_materials.py'),str(O)],check=True)
mask_counts=json.loads((O/'mask-counts.json').read_text(encoding='utf-8'))
# Work from the fixed evaluated V1 export, leaving its source/atlas/animation intact.
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(R/'Game/Assets/Officer3D/TrialV1/officer.glb'))
rig=next(o for o in bpy.data.objects if o.type=='ARMATURE');rig.data.pose_position='REST';meshes=[o for o in bpy.data.objects if o.type=='MESH'];originals={o.name:o.data.copy() for o in meshes}
face=next(o for o in meshes if o.name=='face');print('V2 FACE_BOUNDS',[(min(v.co[i] for v in face.data.vertices),max(v.co[i] for v in face.data.vertices)) for i in range(3)],flush=True)
# Global glTF Y-up is Blender Z-up. The head bounds come from the skin_face surface only.
face_mat={i for i,m in enumerate(face.data.materials) if m.name=='skin_face'};head_indices={v for p in face.data.polygons if p.material_index in face_mat for v in p.vertices};points=[face.data.vertices[i].co for i in head_indices]
lo=np.min([tuple(v) for v in points],axis=0);hi=np.max([tuple(v) for v in points],axis=0);center=(lo+hi)/2;span=hi-lo
veteran=face.copy();veteran.data=face.data.copy();bpy.context.scene.collection.objects.link(veteran);veteran.name='face_veteran'
for i in head_indices:
 v=veteran.data.vertices[i];z=(v.co.z-lo[2])/span[2]
 if z<.48:v.co.x=center[0]+(v.co.x-center[0])*(1.08 if z>.13 else .94)
originals[veteran.name]=veteran.data.copy();meshes.append(veteran)
head_bone=next(b.name for b in rig.data.bones if b.name.endswith('Head'));parts=[]
def part(name,location,scale,color):
 bpy.ops.mesh.primitive_uv_sphere_add(segments=16,ring_count=8,location=location);o=bpy.context.object;o.name=name;o.scale=scale;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 mat=bpy.data.materials.new('feature_'+name);mat.diffuse_color=(*color,1);mat.use_nodes=True;bs=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED');bs.inputs['Base Color'].default_value=(*color,1);bs.inputs['Roughness'].default_value=.9;o.data.materials.append(mat)
 group=o.vertex_groups.new(name=head_bone);group.add(list(range(len(o.data.vertices))),1,'REPLACE');mod=o.modifiers.new('SharedRig','ARMATURE');mod.object=rig;o.parent=rig;parts.append(o);originals[name]=o.data.copy();return o
# Rear hair follows the actual head, not a different actor scale. Front direction in Blender is -Y.
x,y,z=center;w,d,h=span
part('hair_tied',(x,y,hi[2]-.015),(w*.51,d*.63,h*.33),(.018,.012,.009))
part('hair_knot',(x,hi[1]+.045,hi[2]-.025),(w*.22,d*.35,h*.16),(.018,.012,.009))
part('beard_short',(x,lo[1]+.014,lo[2]+h*.115),(w*.26,d*.15,h*.12),(.035,.023,.014))
meshes+=parts
# Keep all original animation tracks; new parts share the same 73-bone rig.
manifest={'binding':binding,'source_sha256':gate.sha(source),'source_glb_sha256':C['source_v1_glb_sha256'],'material_mask_counts':mask_counts,'lod':{},'parts':['face_veteran','hair_tied','hair_knot','beard_short'],'head_bone':head_bone,'head_bounds':[lo.tolist(),hi.tolist()]}
for level,setting in C['lod'].items():
 repairs=[]
 for o in meshes:
  o.data=originals[o.name].copy()
  ratio=setting['gear_ratio'] if o.name.startswith('slot_') else setting['skin_ratio']
  if o.name.startswith('hair_') or o.name=='beard_short':ratio=1
  if ratio<1:
   bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
   m=o.modifiers.new('Authored_'+level,'DECIMATE');m.ratio=ratio;m.use_collapse_triangulate=True
   bpy.ops.object.modifier_apply(modifier=m.name)
  if o.data.validate(clean_customdata=True):repairs.append(o.name)
  o.data.update()
  assert len(o.data.polygons)>0,o.name
 bpy.ops.object.select_all(action='DESELECT')
 for o in [rig,*meshes]:o.select_set(True)
 rig.data.pose_position='POSE'
 path=O/(level+'.glb')
 bpy.ops.export_scene.gltf(filepath=str(path),export_format='GLB',use_selection=True,export_animations=True,export_animation_mode='NLA_TRACKS',export_force_sampling=True,export_apply=False,export_morph=False,export_yup=True,export_influence_nb=4,export_vertex_color='NONE',will_save_settings=False)
 subprocess.run([str(python),str(R/"Scripts/preserve_officer3d_clips.py"),str(R/"Game/Assets/Officer3D/TrialV1/officer.glb"),str(path)],check=True)
 raw=path.read_bytes();length=struct.unpack_from('<I',raw,12)[0];g=json.loads(raw[20:20+length]);tri=sum(g['accessors'][p['indices']]['count']//3 for m in g['meshes'] for p in m['primitives'])
 assert len(g['skins'])==1 and len(g['skins'][0]['joints'])==73
 assert {a['name'] for a in g.get('animations',[])}=={'idle','walk','attack'}
 manifest['lod'][level]={'sha256':gate.sha(path),'bytes':len(raw),'triangles_including_hidden_variants':tri,'meshes':len(g['meshes']),'bones':73,'settings':setting,'validated_mesh_repairs':repairs}
 print('V2 LOD',level,tri,flush=True)
 rig.data.pose_position='REST'
assert gate.sha(source)==C['source_blend_sha256'];manifest['material_mask_sha256']=gate.sha(O/'materials.png');manifest['elapsed_seconds']=time.time()-started
(R/'Art/Officer3D/TrialV2/export-manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('V2_EXPORT_COMPLETE',flush=True)
