"""Blender CLI producer. Never saves the supplied .blend. Gate before any asset output."""
from pathlib import Path
import bpy,bmesh,json,sys,math,time,hashlib
R=Path(sys.argv[sys.argv.index('--')+1]);sys.path.insert(0,str(R/'Scripts'));import officer3d_pipeline as gate
binding,C,P=gate.require(R,'export');O=R/'Game/Assets/Officer3D/TrialV1';O.mkdir(parents=True,exist_ok=True);E=R/'Art/Officer3D/TrialV1';started=time.time()
def save_json(p,v):p.write_bytes((json.dumps(v,ensure_ascii=False,indent=2)+'\n').encode())
def log(*v):print('OFFICER3D',*v,flush=True)
rig=bpy.data.objects[C['skeleton']['source']];scene=bpy.context.scene;assert len(rig.data.bones)==73
source_sha=gate.sha(bpy.data.filepath);assert source_sha==P['source']['sha256']
all_names=[*C['body_objects'],*[n for v in C['slots'].values() for n in v['objects']]];assert len(all_names)==len(set(all_names))==274
for n in all_names:
 o=bpy.data.objects[n];assert o.type=='MESH' and any(m.type=='ARMATURE' and m.object==rig for m in o.modifiers),n
# Snapshot original visible semantic ownership; exclude all other source/reference objects.
for o in bpy.context.scene.objects:
 o.hide_render=o.name not in all_names and o!=rig;o.hide_set(False)
for layer in bpy.context.view_layer.layer_collection.children:layer.exclude=False
rig.data.pose_position='REST';rig.animation_data.action=None
for track in rig.animation_data.nla_tracks:track.mute=True
for n in all_names:
 o=bpy.data.objects[n]
 for m in list(o.modifiers):
  if m.type in ['NODES','MASK']:o.modifiers.remove(m)
 o.animation_data_clear()
# Bake the source procedural materials BEFORE joining. This preserves the
# object-local Generated/Object coordinates used by engraved metal and cloth.
gear=[bpy.data.objects[n] for v in C['slots'].values() for n in v['objects']]
bpy.ops.object.select_all(action='DESELECT')
for o in gear:
 o.select_set(True)
 if o.data.shape_keys:o.shape_key_clear()
 uv=o.data.uv_layers.new(name='GAMEUV');o.data.uv_layers.active=uv;uv.active_render=True
bpy.context.view_layer.objects.active=gear[0]
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=.002);bpy.ops.uv.pack_islands(margin=.002);bpy.ops.object.mode_set(mode='OBJECT')
materials={m for o in gear for m in o.data.materials if m};original_outputs={}
for m in materials:
 assert m.use_nodes
 bs=next((n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None);assert bs is not None,m.name
 out=next(n for n in m.node_tree.nodes if n.type=='OUTPUT_MATERIAL' and n.is_active_output)
 original_outputs[m]=[l.from_socket for l in out.inputs['Surface'].links]
 target=m.node_tree.nodes.new('ShaderNodeTexImage');target.name='GAME_BAKE_TARGET';m.node_tree.nodes.active=target
scene.render.engine='CYCLES';scene.cycles.samples=C['bake']['samples'];scene.cycles.device='CPU';scene.render.bake.margin=8;scene.render.bake.use_clear=False;scene.render.bake.use_selected_to_active=False
atlas_size=C['bake']['atlas_size'];images={};resume=gate.read(E/'failed-export-cache.json');reuse=False
assert resume['source_sha256']==source_sha
for item in resume['files'].values():assert gate.sha(item['path'])==item['sha256']
for kind in C['bake']['passes']:
 if kind!='albedo' and reuse:
  import shutil
  item=resume['files'][kind];shutil.copy2(item['path'],O/(kind+'.png'));images[kind]=bpy.data.images.load(str(O/(kind+'.png')),check_existing=False);images[kind].colorspace_settings.name='Non-Color';log('CACHE_REUSED_AFTER_EXACT_ALBEDO',kind);continue
 image=bpy.data.images.new('game_'+kind,width=atlas_size,height=atlas_size,alpha=False,float_buffer=False);image.colorspace_settings.name='sRGB' if kind=='albedo' else 'Non-Color';images[kind]=image
 for m in materials:
  nt=m.node_tree;bs=next(n for n in nt.nodes if n.type=='BSDF_PRINCIPLED');out=next(n for n in nt.nodes if n.type=='OUTPUT_MATERIAL' and n.is_active_output);target=nt.nodes['GAME_BAKE_TARGET'];target.image=image;nt.nodes.active=target
  for link in list(out.inputs['Surface'].links):nt.links.remove(link)
  if kind=='normal':nt.links.new(bs.outputs['BSDF'],out.inputs['Surface'])
  else:
   emit=nt.nodes.get('GAME_EMIT') or nt.nodes.new('ShaderNodeEmission');emit.name='GAME_EMIT';socket=bs.inputs[{'albedo':'Base Color','metallic':'Metallic','roughness':'Roughness'}[kind]]
   for link in list(emit.inputs['Color'].links):nt.links.remove(link)
   if socket.is_linked:nt.links.new(socket.links[0].from_socket,emit.inputs['Color'])
   else:
    v=socket.default_value;emit.inputs['Color'].default_value=tuple(v) if hasattr(v,'__len__') else (v,v,v,1)
   nt.links.new(emit.outputs['Emission'],out.inputs['Surface'])
 log('BAKE_START',kind,'objects',len(gear),'materials',len(materials));bpy.ops.object.bake(type='NORMAL' if kind=='normal' else 'EMIT',normal_space='TANGENT',use_clear=False)
 image.filepath_raw=str(O/(kind+'.png'));image.file_format='PNG';image.save();log('BAKE_DONE',kind)
 if kind=='albedo':reuse=gate.sha(O/'albedo.png')==resume['files']['albedo']['sha256'];log('CACHE_ALBEDO_EXACT',reuse)
# Pack scalar PBR channels in one ORM map; use linear/raw pixel values.
import numpy as np
metal=np.empty(atlas_size*atlas_size*4,dtype=np.float32);rough=metal.copy();images['metallic'].pixels.foreach_get(metal);images['roughness'].pixels.foreach_get(rough);orm=np.ones_like(metal).reshape(-1,4);orm[:,1]=rough.reshape(-1,4)[:,0];orm[:,2]=metal.reshape(-1,4)[:,0]
orm_image=bpy.data.images.new('game_orm',width=atlas_size,height=atlas_size,alpha=False);orm_image.colorspace_settings.name='Non-Color';orm_image.pixels.foreach_set(orm.ravel());orm_image.filepath_raw=str(O/'orm.png');orm_image.file_format='PNG';orm_image.save()
def principled(name):
 m=bpy.data.materials.new(name);m.use_nodes=True;nt=m.node_tree;bs=next(n for n in nt.nodes if n.type=='BSDF_PRINCIPLED');return m,nt,bs
def texture(nt,image,location):
 node=nt.nodes.new('ShaderNodeTexImage');node.image=image;node.location=location;return node
slot_materials={}
for slot in C['slots']:
 m,nt,bs=principled('gear_'+slot);a=texture(nt,images['albedo'],(-600,100));nt.links.new(a.outputs['Color'],bs.inputs['Base Color']);p=texture(nt,orm_image,(-600,-150));sep=nt.nodes.new('ShaderNodeSeparateColor');nt.links.new(p.outputs['Color'],sep.inputs[0]);nt.links.new(sep.outputs['Green'],bs.inputs['Roughness']);nt.links.new(sep.outputs['Blue'],bs.inputs['Metallic']);n=texture(nt,images['normal'],(-600,-400));normal=nt.nodes.new('ShaderNodeNormalMap');nt.links.new(n.outputs['Color'],normal.inputs['Color']);nt.links.new(normal.outputs[0],bs.inputs['Normal']);slot_materials[slot]=m
# Convert protected facial features and skin to portable texture-backed PBR.
portable={};material_roles={}
for objname in C['body_objects']:
 o=bpy.data.objects[objname]
 for i,src in enumerate(list(o.data.materials)):
  if src.name not in portable:
   skin=src.name in C['skin']['materials'];role='skin_body' if 'Body_' in src.name else 'skin_face' if skin else 'feature_'+('eyes' if '_EYE' in src.name else 'brows' if 'Brow' in src.name else 'eyeline' if 'Eyeline' in src.name else 'mouth')+'_'+str(len(portable))
   m,nt,bs=principled(role);image_node=next((n for n in src.node_tree.nodes if n.type=='TEX_IMAGE' and 'BaseColorTexture.Image' in n.name and n.image),None);assert image_node,src.name
   tex=texture(nt,image_node.image,(-500,0));nt.links.new(tex.outputs['Color'],bs.inputs['Base Color']);bs.inputs['Metallic'].default_value=0;bs.inputs['Roughness'].default_value=.85
   if not skin:nt.links.new(tex.outputs['Alpha'],bs.inputs['Alpha']);m.surface_render_method='DITHERED'
   portable[src.name]=m;material_roles[role]={'role':role if skin else 'protected_feature','source_material':src.name,'image':image_node.image.name}
  o.data.materials[i]=portable[src.name]
# Split the COMPLETE base into always-visible skin and equipment-covered regions.
# Source MASK only kept exposed skin. Do not export that amputated base.
body=bpy.data.objects['Body'];source_faces=len(body.data.polygons);bones=set(rig.data.bones.keys());exposed_index=body.vertex_groups.get('v008_露出する肌だけ').index
def owner(poly):
 if all(any(g.group==exposed_index and g.weight>.001 for g in body.data.vertices[v].groups) for v in poly.vertices):return 'exposed'
 counts={}
 for v in poly.vertices:
  for g in body.data.vertices[v].groups:
   name=body.vertex_groups[g.group].name
   if name in bones:counts[name]=counts.get(name,0)+g.weight
 assert counts,'Body polygon without skeleton weights'
 name=max(counts,key=counts.get)
 if 'UpperLeg' in name:return 'thighs'
 if 'LowerLeg' in name:return 'shins'
 if 'Foot' in name or 'Toe' in name:return 'feet'
 if any(n in name for n in ['Arm','Hand','Index','Thumb','Little','Middle','Ring','Shoulder']):return 'arms'
 if 'Hips' in name:return 'waist'
 if any(n in name for n in ['Head','Neck','FaceEye']):return 'exposed'
 return 'torso'
owners=[owner(p) for p in body.data.polygons];base_objects=[];body_stats={}
for region in sorted(set(owners)):
 o=body.copy();o.data=body.data.copy();bpy.context.scene.collection.objects.link(o);o.name='skin_'+region;o.animation_data_clear()
 if o.data.shape_keys:o.shape_key_clear()
 bm=bmesh.new();bm.from_mesh(o.data);bm.faces.ensure_lookup_table();discard=[f for f in bm.faces if owners[f.index]!=region];bmesh.ops.delete(bm,geom=discard,context='FACES');unused=[v for v in bm.verts if not v.link_faces]
 if unused:bmesh.ops.delete(bm,geom=unused,context='VERTS')
 bm.to_mesh(o.data);bm.free();base_objects.append(o);body_stats[region]=len(o.data.polygons)
assert sum(body_stats.values())==source_faces
body.hide_set(True);body.hide_render=True
face=bpy.data.objects['Face'];face.name='face';face.animation_data_clear();base_objects.append(face)
body_coverage={'exposed':[],'torso':['torso','underclothes'],'arms':['arms','underclothes'],'waist':['waist','underclothes'],'thighs':['thighs','underclothes'],'shins':['shins'],'feet':['feet']}
# Same pose/weights and source-generated atlas, merged only within each slot.
slot_objects={}
for slot,item in C['slots'].items():
 objs=[bpy.data.objects[n] for n in item['objects']];bpy.ops.object.select_all(action='DESELECT')
 for o in objs:
  o.hide_set(False);o.select_set(True);o.data.materials.clear();o.data.materials.append(slot_materials[slot]);
  for poly in o.data.polygons:poly.material_index=0
  for uv in list(o.data.uv_layers):
   if uv.name!='GAMEUV':o.data.uv_layers.remove(uv)
 bpy.context.view_layer.objects.active=objs[0];bpy.ops.object.join();joined=bpy.context.object;joined.name='slot_'+slot;slot_objects[slot]=joined
 log('MERGED',slot,len(objs),'->1')
# Export only the selected three primary-rig actions as independent NLA clips.
for track in list(rig.animation_data.nla_tracks):rig.animation_data.nla_tracks.remove(track)
rig.animation_data.action=None;clips={}
for name,clip in C['clips'].items():
 action=bpy.data.actions[clip['source']].copy();action.name='game_'+name
 for layer in action.layers:
  for strip in layer.strips:
   for bag in strip.channelbags:
    for curve in bag.fcurves:
     if curve.data_path=='pose.bones["Root"].location' and curve.array_index in [0,1]:
      for key in curve.keyframe_points:key.co[1]=0;key.handle_left[1]=0;key.handle_right[1]=0
 track=rig.animation_data.nla_tracks.new();track.name=name;strip=track.strips.new(name,1,action);strip.action_frame_start=clip['range'][0];strip.action_frame_end=clip['range'][1];strip.frame_start=1;strip.frame_end=clip['range'][1];strip.blend_type='REPLACE';clips[name]={'source':clip['source'],'frames':clip['range'],'seconds':(clip['range'][1]-clip['range'][0])/30,'loop':clip['loop']}
rig.data.pose_position='POSE';scene.render.fps=30;scene.frame_set(1)
selected=[rig,*base_objects,*slot_objects.values()];bpy.ops.object.select_all(action='DESELECT')
for o in selected:o.hide_set(False);o.select_set(True);o.hide_render=False
bpy.context.view_layer.objects.active=rig
glb=O/'officer.glb';log('EXPORT_START',len(selected))
bpy.ops.export_scene.gltf(filepath=str(glb),export_format='GLB',use_selection=True,use_visible=False,use_renderable=False,export_cameras=False,export_lights=False,export_extras=False,export_animations=True,export_animation_mode='NLA_TRACKS',export_nla_strips=True,export_force_sampling=True,export_frame_range=False,export_frame_step=1,export_optimize_animation_size=False,export_reset_pose_bones=True,export_apply=False,export_morph=False,export_def_bones=False,export_leaf_bone=False,export_yup=True,export_influence_nb=4,export_all_influences=False,export_rest_position_armature=True,export_vertex_color='NONE',will_save_settings=False)
# Read GLB structure independently of Blender's reported success.
import struct
raw=glb.read_bytes();assert raw[:4]==b'glTF';length,kind=struct.unpack_from('<II',raw,12);gltf=json.loads(raw[20:20+length]);animations=[a['name'] for a in gltf.get('animations',[])];assert set(animations)=={'idle','walk','attack'},animations
assert len(gltf.get('skins',[]))==1
assert gate.sha(bpy.data.filepath)==source_sha
manifest={'schema':1,'binding':binding,'source_sha256':source_sha,'exporter_sha256':gate.sha(__file__),'glb':{'path':'res://Assets/Officer3D/TrialV1/officer.glb','sha256':gate.sha(glb),'bytes':glb.stat().st_size},'slots':{n:'slot_'+n for n in C['slots']},'body_coverage':{'skin_'+k:v for k,v in body_coverage.items() if k in body_stats},'body_original_faces':source_faces,'body_partition_faces':body_stats,'materials':material_roles,'clips':clips,'bone_count':len(gltf['skins'][0]['joints']),'mesh_count':len(gltf.get('meshes',[])),'triangle_count':sum(a['count']//3 for m in gltf.get('meshes',[]) for p in m['primitives'] for a in [gltf['accessors'][p['indices']]]),'bake_textures':{p.name:gate.sha(p) for p in O.glob('*.png')},'elapsed_seconds':round(time.time()-started,2),'status':'EXPORTED_RUNTIME_NOT_VALIDATED','source_file_unchanged':True,'cache_reused_after_exact_albedo':reuse,'cache_provenance':resume if reuse else None,'license_distribution':'local-only pending owner/source terms clarification'}
save_json(E/'export-manifest.json',manifest);log('EXPORT_COMPLETE',manifest['mesh_count'],manifest['triangle_count'],manifest['elapsed_seconds'])
