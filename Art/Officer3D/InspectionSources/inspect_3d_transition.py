import bpy,json,sys
from pathlib import Path
out=Path(sys.argv[sys.argv.index('--')+1]);out.mkdir(exist_ok=True)
def vector(v):return [round(float(x),5) for x in v]
objects=[]
for o in bpy.data.objects:
 d={'name':o.name,'type':o.type,'parent':o.parent.name if o.parent else None,'location':vector(o.location),'scale':vector(o.scale),'dimensions':vector(o.dimensions),'hidden_render':o.hide_render,'modifiers':[{'name':m.name,'type':m.type,'object':getattr(getattr(m,'object',None),'name',None)} for m in o.modifiers]}
 if o.type=='MESH':d.update(vertices=len(o.data.vertices),polygons=len(o.data.polygons),triangles=sum(max(0,len(p.vertices)-2) for p in o.data.polygons),materials=[m.name if m else None for m in o.data.materials],vertex_groups=[g.name for g in o.vertex_groups],shape_keys=list(o.data.shape_keys.key_blocks.keys()) if o.data.shape_keys else [])
 if o.type=='ARMATURE':d['bones']=[{'name':b.name,'parent':b.parent.name if b.parent else None,'deform':b.use_deform,'head':vector(b.head_local),'tail':vector(b.tail_local)} for b in o.data.bones]
 if o.animation_data:d['animation']={'action':o.animation_data.action.name if o.animation_data.action else None,'nla':[{'name':t.name,'strips':[{'name':s.name,'action':s.action.name if s.action else None,'start':s.frame_start,'end':s.frame_end} for s in t.strips]} for t in o.animation_data.nla_tracks]}
 objects.append(d)
materials=[]
for m in bpy.data.materials:
 d={'name':m.name,'diffuse':vector(m.diffuse_color),'nodes':[]}
 if m.node_tree:
  for n in m.node_tree.nodes:
   item={'name':n.name,'type':n.type}
   if n.type=='BSDF_PRINCIPLED':item['base_color']=vector(n.inputs['Base Color'].default_value);item['metallic']=n.inputs['Metallic'].default_value;item['roughness']=n.inputs['Roughness'].default_value
   if n.type=='TEX_IMAGE':item['image']=n.image.name if n.image else None
   d['nodes'].append(item)
 materials.append(d)
data={'blender':bpy.app.version_string,'file':bpy.data.filepath,'scenes':[{'name':s.name,'engine':s.render.engine,'frames':[s.frame_start,s.frame_end],'fps':s.render.fps/s.render.fps_base,'resolution':[s.render.resolution_x,s.render.resolution_y,s.render.resolution_percentage],'camera':s.camera.name if s.camera else None,'unit_system':s.unit_settings.system,'unit_scale':s.unit_settings.scale_length} for s in bpy.data.scenes],'objects':objects,'materials':materials,'images':[{'name':i.name,'filepath':i.filepath,'packed':bool(i.packed_file)} for i in bpy.data.images],'actions':[{'name':a.name,'frame_range':vector(a.frame_range),'slots':[s.identifier for s in a.slots]} for a in bpy.data.actions],'embedded_text_names':list(bpy.data.texts.keys())}
(out/'blend-inspection.json').write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8');print('BLEND_INSPECTED',len(objects),'objects',len(materials),'materials')
