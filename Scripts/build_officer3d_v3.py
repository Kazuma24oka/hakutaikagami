"""Background Blender authoring of source-fitted armor and character parts. Source files are read-only."""
import bpy,bmesh,json,sys,math,time,subprocess
from pathlib import Path
from mathutils import Vector,Matrix
from mathutils.kdtree import KDTree
from mathutils.bvhtree import BVHTree
R=Path(sys.argv[sys.argv.index('--')+1]);sys.path.insert(0,str(R/'Scripts'));import officer3d_v3_pipeline as gate
binding=gate.require(R,'export');C=gate.read(R/'Art/Officer3D/TrialV3/contract.json');O=R/'Game/Assets/Officer3D/TrialV3';manifest_path=R/'Art/Officer3D/TrialV3/export-manifest.json'
if '--repro-out' in sys.argv:
 O=Path(sys.argv[sys.argv.index('--repro-out')+1]).resolve();assert O.is_relative_to((R/'.art-test-tmp').resolve()) and not O.exists();manifest_path=O/'export-manifest.json'
O.mkdir(parents=True,exist_ok=True);start=time.time()
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(R/'Game/Assets/Officer3D/TrialV2/near.glb'))
rig=next(o for o in bpy.data.objects if o.type=='ARMATURE');rig.data.pose_position='REST';rig.animation_data.action=None
for t in rig.animation_data.nla_tracks:t.mute=True
bpy.context.view_layer.update();bones=tuple(sorted(rig.data.bones.keys()));proofs={}
def palette_linear(name):
 h=C['palette'][name].lstrip('#');v=[int(h[i:i+2],16)/255 for i in [0,2,4]];return tuple(c/12.92 if c<=.04045 else ((c+.055)/1.055)**2.4 for c in v)
colors={key:palette_linear(key) for key in ['cloth','lacquer','metal']}
class Shape:
 def __init__(self):self.v=[];self.f=[];self.m=[]
 def face(self,coords,mat):
  n=len(self.v);self.v.extend([tuple(p) for p in coords]);self.f.append(tuple(range(n,n+len(coords))));self.m.append(mat)
 def box(self,center,size,mat):
  x,y,z=center;w,d,h=[s/2 for s in size];v=[(x+a*w,y+b*d,z+c*h) for a,b,c in [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]]
  for ids in [(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)]:self.face([v[i] for i in ids],mat)
 def tube(self,a,b,r,mat,n=8):
  a,b=Vector(a),Vector(b);axis=(b-a).normalized();u=Vector((0,-1,0));u=(u-axis*u.dot(axis)).normalized();v=axis.cross(u)
  for i in range(n):
   t=i*math.tau/n;t2=(i+1)*math.tau/n;p=u*math.cos(t)*r+v*math.sin(t)*r;q=u*math.cos(t2)*r+v*math.sin(t2)*r
   self.face([a+p,a+q,b+q,b+p],mat)
  self.face([a+u*math.cos(i*math.tau/n)*r+v*math.sin(i*math.tau/n)*r for i in reversed(range(n))],mat)
  self.face([b+u*math.cos(i*math.tau/n)*r+v*math.sin(i*math.tau/n)*r for i in range(n)],mat)
 def shell(self,rx,ry,z0,z1,t0=0,t1=math.tau,mat='lacquer',flare=0,n=32,cy=0):
  def p(t,z,inside=False):
   k=(z1-z)/(z1-z0);return ((rx+flare*k-(.003 if inside else 0))*math.sin(t),cy-(ry+flare*k-(.003 if inside else 0))*math.cos(t),z)
  for i in range(n):
   a=t0+(t1-t0)*i/n;b=t0+(t1-t0)*(i+1)/n
   self.face([p(a,z0),p(b,z0),p(b,z1),p(a,z1)],mat);self.face([p(a,z1,True),p(b,z1,True),p(b,z0,True),p(a,z0,True)],mat)
   self.face([p(a,z1),p(b,z1),p(b,z1,True),p(a,z1,True)],mat);self.face([p(a,z0,True),p(b,z0,True),p(b,z0),p(a,z0)],mat)
  for t in [t0,t1]:self.face([p(t,z0),p(t,z1),p(t,z1,True),p(t,z0,True)],mat)
 def ellipsoid(self,c,r,mat,n=20,rows=10,lower=-math.pi/2,upper=math.pi/2):
  def p(t,a):return (c[0]+r[0]*math.cos(a)*math.sin(t),c[1]-r[1]*math.cos(a)*math.cos(t),c[2]+r[2]*math.sin(a))
  for j in range(rows):
   a=lower+(upper-lower)*j/rows;b=lower+(upper-lower)*(j+1)/rows
   for i in range(n):self.face([p(i*math.tau/n,a),p((i+1)*math.tau/n,a),p((i+1)*math.tau/n,b),p(i*math.tau/n,b)],mat)

def make(slot,shape):
 name='alt_'+slot;mesh=bpy.data.meshes.new(name);mesh.from_pydata(shape.v,[],shape.f);mesh.update();o=bpy.data.objects.new(name,mesh);bpy.context.scene.collection.objects.link(o);o.parent=rig
 for category in ['cloth','lacquer','metal']:
  m=bpy.data.materials.new('gear_red_'+slot+'_'+category);m.use_nodes=True;b=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED');b.inputs['Base Color'].default_value=(*colors[category],1);b.inputs['Metallic'].default_value=.8 if category=='metal' else .05;b.inputs['Roughness'].default_value=.36 if category=='lacquer' else .3 if category=='metal' else .9;mesh.materials.append(m)
 for p,key in zip(mesh.polygons,shape.m):p.material_index=['cloth','lacquer','metal'].index(key)
 # Source slot provides named deformation weights only; authored geometry is independent.
 ref=bpy.data.objects['slot_'+slot];kd=KDTree(len(ref.data.vertices))
 for i,v in enumerate(ref.data.vertices):kd.insert(ref.matrix_world@v.co,i)
 kd.balance();groups={b:o.vertex_groups.new(name=b) for b in bones};max_error=0
 for i,v in enumerate(mesh.vertices):
  weights={}
  for _,idx,dist in kd.find_n(v.co,4):
   for g in ref.data.vertices[idx].groups:
    n=ref.vertex_groups[g.group].name
    if n in bones:weights[n]=weights.get(n,0)+g.weight/max(dist,.002)**2
  total=sum(weights.values());assert total>0,(slot,i)
  values=sorted(weights.items(),key=lambda p:-p[1])[:4];total=sum(v for _,v in values)
  for b,w in values:groups[b].add([i],w/total,'REPLACE')
  max_error=max(max_error,abs(sum(w/total for _,w in values)-1))
 m=o.modifiers.new('SharedRig','ARMATURE');m.object=rig
 bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.000005);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free();mesh.validate(clean_customdata=True)
 bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
 bevel=o.modifiers.new('AuthoredPlateEdges','BEVEL');bevel.width=.0009;bevel.segments=1;bevel.limit_method='ANGLE';bevel.angle_limit=.45;bpy.ops.object.modifier_apply(modifier=bevel.name)
 proofs[slot]={'vertices':len(mesh.vertices),'faces':len(mesh.polygons),'max_weight_sum_error':max_error,'weights_reference':'slot_'+slot,'material_categories':['cloth','lacquer','metal']};return o

s=Shape()
for row in range(5):s.shell(.195+row*.005,.13+row*.003,.985+row*.065,1.057+row*.065,n=48,cy=.01)
s.shell(.18,.135,1.31,1.395,t0=-.85,t1=.85,n=16,cy=.01)
for x in [-.13,.13]:
 for a,b in zip([(x,-.13,1.32),(x,-.11,1.40),(x,0,1.435)],[(x,-.11,1.40),(x,0,1.435),(x,.12,1.35)]):s.tube(a,b,.013,'cloth')
for row in range(5):
 for t in [-2.3,-1.2,-.6,.6,1.2,2.3]:
  rx=.195+row*.005;ry=.13+row*.003;s.tube((rx*math.sin(t),.01-ry*math.cos(t),1.00+row*.065),(rx*math.sin(t),.01-(ry+.003)*math.cos(t),1.035+row*.065),.004,'cloth',6)
make('torso',s)
s=Shape()
for j in range(7):
 mid=math.pi/7+j*math.tau/7
 for row in range(5):
  z=.955-row*.042;r=.215+row*.012;s.shell(r,r*.84,z-.046,z+.009,mid-.40,mid+.40,flare=.009,n=10)
  for t in [mid-.24,mid+.24]:s.tube(((r+.005)*math.sin(t),-(r*.84+.005)*math.cos(t),z-.036),((r+.006)*math.sin(t),-(r*.84+.006)*math.cos(t),z+.008),.004,'cloth',6)
make('waist',s)
s=Shape();s.ellipsoid((0,.015,1.67),(.135,.15,.13),'lacquer',32,12,lower=0)
s.shell(.137,.151,1.665,1.683,t0=-1.0,t1=1.0,n=20,cy=.015,flare=.035)
for row in range(5):
 z=1.65-row*.033;s.shell(.138+row*.01,.152+row*.01,z-.034,z+.006,t0=.8,t1=math.tau-.8,n=32,cy=.015)
 for t in [1.05,1.7,2.4,math.pi,3.9,4.6,5.2]:s.tube(((.144+row*.01)*math.sin(t),.015-(.158+row*.01)*math.cos(t),z-.025),((.144+row*.01)*math.sin(t),.015-(.158+row*.01)*math.cos(t),z+.002),.003,'cloth',6)
for side in [-1,1]:
 points=[(side*.035,-.145,1.74),(side*.068,-.15,1.80),(side*.080,-.15,1.87)]
 for a,b in zip(points,points[1:]):s.tube(a,b,.008,'metal',8)
make('helmet',s)
s=Shape()
for row in range(3):s.shell(.087+row*.015,.09+row*.016,1.47-row*.028,1.505-row*.028,t0=-1.45,t1=1.45,n=24,cy=.01)
make('neck_guard',s)
s=Shape();s.shell(.084,.115,1.505,1.565,t0=-1.15,t1=1.15,n=18,cy=.005);s.ellipsoid((0,-.115,1.562),(.021,.019,.022),'lacquer',12,6);make('mask',s)
s=Shape()
for side in [-1,1]:
 for row in range(6):
  x=side*(.215+row*.030);z=1.492-row*.004;s.box((x,.028,z),(.042,.185,.016),'lacquer')
  # Front/back returns make the sleeve wrap around the arm rather than a flat shelf.
  s.box((x,-.064,z-.036),(.042,.012,.065),'lacquer');s.box((x,.119,z-.036),(.042,.012,.065),'lacquer')
  for y in [-.065,.12]:s.tube((x-.008,y,z-.045),(x+.008,y,z-.045),.004,'cloth',6)
make('shoulders',s)
s=Shape()
for side in ['L','R']:
 for bname,radius in [('UpperArm',.063),('LowerArm',.052)]:
  b=rig.data.bones['J_Bip_'+side+'_'+bname];a=b.head_local;z=b.tail_local;axis=(z-a).normalized();s.tube(a-axis*.012,z+axis*.012,radius,'cloth',16)
  if bname=='LowerArm':
   for f in [.15,.45,.75]:
    c=a.lerp(z,f);s.box((c.x,c.y-radius-.005,c.z),(.085,.012,.065),'lacquer')
 b=rig.data.bones['J_Bip_'+side+'_Hand'];s.box((b.head_local.x+(.026 if side=='L' else -.026),-.029,b.head_local.z),(.073,.014,.054),'lacquer')
make('arms',s)
s=Shape()
for side in [-1,1]:
 s.box((side*.125,-.136,.714),(.154,.014,.30),'cloth')
 for row in range(5):
  for col in range(4):s.box((side*.125+(col-1.5)*.036,-.15,.845-row*.057),(.034,.011,.051),'lacquer')
make('thighs',s)
s=Shape()
for side in ['L','R']:
 b=rig.data.bones['J_Bip_'+side+'_LowerLeg'];s.tube(b.head_local,b.tail_local,.065,'cloth',18)
 for f in [.18,.48,.77]:
  c=b.head_local.lerp(b.tail_local,f);s.box((c.x,c.y-.066,c.z),(.105,.014,.15),'lacquer')
 c=b.head_local;s.ellipsoid((c.x,c.y-.078,c.z),(.070,.017,.064),'metal',16,8)
make('shins',s)
s=Shape()
for side in [-1,1]:
 x=side*.123;s.ellipsoid((x,-.042,.052),(.055,.136,.042),'cloth',24,12)
 s.tube((x,.035,.06),(x,.035,.145),.049,'cloth',16)
 for z in [.048,.083]:s.tube((x-.053,-.087,z),(x+.053,-.087,z),.004,'lacquer',8)
make('feet',s)
# Mature face: shared topology/UV, broader lower jaw, restrained nose projection.
face=bpy.data.objects['face'];mature=bpy.data.objects['face_veteran'];mature.data=face.data.copy()
for v in mature.data.vertices:
 z=v.co.z
 if z<1.60:v.co.x*=1.12 if z>1.51 else 1.05
 if abs(v.co.x)<.022 and 1.58<z<1.64 and v.co.y<-.045:v.co.y-=.007*math.sin((z-1.58)/.06*math.pi)
# Replace placeholder balls with shaped scalp, bound strands, moustache and chin patch.
skin_indices={i for i,m in enumerate(mature.data.materials) if m.name=='skin_face'}
faces=[list(p.vertices) for p in mature.data.polygons if p.material_index in skin_indices];bvh=BVHTree.FromPolygons([v.co for v in mature.data.vertices],faces)
def front_y(x,z):
 hit=bvh.ray_cast(Vector((x,-.6,z)),Vector((0,1,0)))[0];return hit.y-.0018 if hit else -.06
# Locate painted mouth UV center in 3D to attach facial hair without covering the lips.
uv=mature.data.uv_layers.active;mouth=Vector((.5,1-775/1024));mouthpos=Vector((0,-.07,1.55));mature.data.calc_loop_triangles()
for tri in mature.data.loop_triangles:
 if tri.material_index not in skin_indices:continue
 u=[Vector(uv.data[i].uv) for i in tri.loops];a=u[1]-u[0];b=u[2]-u[0];q=mouth-u[0];det=a.x*b.y-a.y*b.x
 if abs(det)<1e-9:continue
 v=(q.x*b.y-q.y*b.x)/det;w=(a.x*q.y-a.y*q.x)/det
 if v>=0 and w>=0 and v+w<=1:
  ps=[mature.data.vertices[i].co for i in tri.vertices];mouthpos=ps[0]*(1-v-w)+ps[1]*v+ps[2]*w;break

def feature(name,shape):
 o=bpy.data.objects[name];mesh=bpy.data.meshes.new(name+'_v3');mesh.from_pydata(shape.v,[],shape.f);mesh.update();o.data=mesh;o.matrix_basis=Matrix.Identity(4);o.vertex_groups.clear();g=o.vertex_groups.new(name='J_Bip_C_Head');g.add(list(range(len(mesh.vertices))),1,'REPLACE')
 mat=bpy.data.materials.new('feature_'+name+'_v3');mat.use_nodes=True;b=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED');b.inputs['Base Color'].default_value=(*palette_linear('hair'),1);b.inputs['Roughness'].default_value=.9;mesh.materials.append(mat)
 for p in mesh.polygons:p.use_smooth=True
 return o
s=Shape()
def hairline(v):
 theta=math.atan2(v.x,-(v.y-.025));return 1.60+.045*math.cos(theta)-.020*abs(math.sin(theta))
def offset(v):
 n=Vector((v.x,(v.y-.025),max(0,v.z-1.62)*.8)).normalized();return v+n*.003
# Use actual head surfaces from BOTH the body skull and face; face bounds alone omit the back of the skull.
for source in [bpy.data.objects['skin_exposed'],mature]:
 for poly in source.data.polygons:
  if not source.data.materials[poly.material_index].name.startswith('skin_'):continue
  points=[source.matrix_world@source.data.vertices[i].co for i in poly.vertices]
  if max(v.z for v in points)<1.55:continue
  clipped=[]
  for p,q in zip(points,points[1:]+points[:1]):
   fp=p.z-hairline(p);fq=q.z-hairline(q)
   if fp>=0:clipped.append(p)
   if (fp>=0)!=(fq>=0):clipped.append(p.lerp(q,fp/(fp-fq)))
  if len(clipped)>=3:s.face([offset(v) for v in clipped],'hair')
# Uneven front locks break the scalp edge into hair, instead of a smooth cap silhouette.
for i in range(11):
 x=(i-5)*.014;z=1.646-.018*abs(x)/.075;w=.009;tipz=z-.009-(i%3)*.002
 s.face([(x-w,front_y(x-w,z+.022)-.001,z+.022),(x+w,front_y(x+w,z+.022)-.001,z+.022),(x+.004,front_y(x+.004,tipz)-.001,tipz)],'hair')
feature('hair_tied',s)
s=Shape();s.ellipsoid((0,.137,1.67),(.028,.034,.023),'hair',20,10);s.tube((0,.145,1.67),(0,.178,1.665),.018,'hair',10);feature('hair_knot',s)
s=Shape();mz=mouthpos.z
for side in [-1,1]:
 pts=[]
 for i in range(10):
  x=side*(.006+i*.004);z=mz+.009-.004*i/9;pts.append((x,front_y(x,z)-.0008,z))
 for a,b in zip(pts,pts[1:]):s.tube(a,b,.0032,'hair',6)
for row in range(10):
 z0=1.491+(mz-.012-1.491)*row/10;z1=1.491+(mz-.012-1.491)*(row+1)/10
 def point(col,z):
  width=.032+.015*min(1,(z-1.491)/.035);x=(col/16*2-1)*width;return (x,front_y(x,z)-.0007,z)
 for col in range(16):s.face([point(col,z0),point(col+1,z0),point(col+1,z1),point(col,z1)],'hair')
feature('beard_short',s)
# Mesh validation precedes export. New parts have no inherited subdivision stack.
objects=[o for o in bpy.data.objects if o.type=='MESH'];originals={o.name:o.data.copy() for o in objects};manifest={'binding':binding,'weights':proofs,'mouth_anchor':list(mouthpos),'lod':{},'source_preserved':True}
python=r'C:\Users\hizen\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
for level,ratio in C['lod'].items():
 legacy={}
 if level!='near':
  before=set(bpy.data.objects);bpy.ops.import_scene.gltf(filepath=str(R/'Game/Assets/Officer3D/TrialV2'/(level+'.glb')));temporary=set(bpy.data.objects)-before
  for temp in sorted(temporary,key=lambda o:o.name):
   if temp.type=='MESH':
    name=temp.name.rsplit('.',1)[0] if temp.name.rsplit('.',1)[-1].isdigit() else temp.name
    if name.startswith('slot_'):legacy[name]=(temp.data.copy(),[g.name for g in temp.vertex_groups])
  for temp in sorted(temporary,key=lambda o:o.name):bpy.data.objects.remove(temp,do_unlink=True)
 for o in objects:
  o.data=originals[o.name].copy()
  # Legacy armor uses its already validated V2 LOD; new armor uses authored simplification.
  is_legacy=o.name.startswith('slot_') or o.name.startswith('skin_') or o.name=='face'
  if o.name in legacy:
   mesh,groups=legacy[o.name];o.data=mesh;weights=[[(g.group,g.weight) for g in v.groups] for v in mesh.vertices];o.vertex_groups.clear()
   for name in groups:o.vertex_groups.new(name=name)
   for i,items in enumerate(weights):
    for index,weight in items:o.vertex_groups[index].add([i],weight,'REPLACE')
  # Contact surfaces must stay identical: independent decimation lets the skull
  # protrude through fitted hair. Preserve body/head/hair/beard at every LOD.
  contact=o.name.startswith(('hair_','beard_')) or o.name=='face_veteran'
  if ratio<1 and not is_legacy and not contact and len(o.data.polygons)>100:
   bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o;m=o.modifiers.new('V3_LOD','DECIMATE');m.ratio=ratio;m.use_collapse_triangulate=True;bpy.ops.object.modifier_apply(modifier=m.name)
  o.data.validate(clean_customdata=True);o.data.update();assert len(o.data.polygons)>0,o.name
 bpy.ops.object.select_all(action='DESELECT')
 for o in [rig,*objects]:o.select_set(True)
 rig.data.pose_position='POSE';path=O/(level+'.glb')
 bpy.ops.export_scene.gltf(filepath=str(path),export_format='GLB',use_selection=True,export_animations=False,export_apply=False,export_morph=False,export_yup=True,export_influence_nb=4,export_vertex_color='NONE',will_save_settings=False)
 subprocess.run([python,str(R/'Scripts/preserve_officer3d_clips.py'),str(R/'Game/Assets/Officer3D/TrialV1/officer.glb'),str(path)],check=True)
 import struct
 raw=path.read_bytes();length=struct.unpack_from('<I',raw,12)[0];gl=json.loads(raw[20:20+length]);assert len(gl['skins'])==1 and len(gl['skins'][0]['joints'])==73
 manifest['lod'][level]={'sha256':gate.sha(path),'bytes':len(raw),'triangles_all_parts':sum(gl['accessors'][p['indices']]['count']//3 for m in gl['meshes'] for p in m['primitives']),'mesh_count':len(gl['meshes'])};rig.data.pose_position='REST';print('V3_EXPORTED',level,flush=True)
manifest['elapsed_seconds']=time.time()-start;manifest_path.write_bytes((json.dumps(manifest,ensure_ascii=False,indent=2)+'\n').encode());print('V3_EXPORT_COMPLETE',flush=True)
