from pathlib import Path
import json,math,subprocess,re
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'Game/Assets/Scenery/Models'
class Scene:
 def __init__(self,name):self.name=name;self.resources=[];self.nodes=[];self.count=0;self.mats={};self.external=[]
 def resource(self,kind,props):
  self.count+=1;key='Resource_'+str(self.count);self.resources.append('[sub_resource type="'+kind+'" id="'+key+'"]\n'+props+'\n');return key
 def material(self,color):
  if color not in self.mats:
   vals=[int(color[i:i+2],16)/255 for i in [0,2,4]];self.mats[color]=self.resource('StandardMaterial3D','albedo_color = Color('+', '.join(f'{v:.7f}' for v in vals)+', 1)\nroughness = 1.0')
  return self.mats[color]
 def add(self,name,kind,props,color,pos=(0,0,0),scale=(1,1,1),yaw=0):
  material=self.material(color);mesh=self.resource(kind,props);vec=lambda p:'Vector3('+', '.join(str(v) for v in p)+')'
  self.nodes.append('[node name="'+name+'" type="MeshInstance3D" parent="."]\nposition = '+vec(pos)+'\nscale = '+vec(scale)+'\nrotation = Vector3(0, '+str(yaw)+', 0)\nmesh = SubResource("'+mesh+'")\nmaterial_override = SubResource("'+material+'")\n')
 def add_external(self,name,path,color):
  material=self.material(color);key='External_'+str(len(self.external));self.external.append('[ext_resource type="ArrayMesh" path="'+path+'" id="'+key+'"]')
  self.nodes.append('[node name="'+name+'" type="MeshInstance3D" parent="."]\nmesh = ExtResource("'+key+'")\nmaterial_override = SubResource("'+material+'")\n')
 def save(self,path):
  text='[gd_scene load_steps='+str(self.count+len(self.external)+1)+' format=3]\n\n'+'\n'.join(self.external)+'\n'+'\n'.join(self.resources)+'\n[node name="'+self.name+'" type="Node3D"]\n\n'+'\n'.join(self.nodes);(OUT/path).write_bytes(text.encode())
def main():
 OUT.mkdir(parents=True,exist_ok=True);catalog=json.loads((ROOT/'Game/Data/Scenery/scenery-appearance-v1.json').read_bytes());colors={m['Id']:m['Color'] for m in catalog['Materials']};c=lambda x:colors['material:'+x]
 tree=Scene('FieldPine');tree.add('Trunk','CylinderMesh','top_radius = 0.18\nbottom_radius = 0.34\nheight = 7\nradial_segments = 8',c('tree'),(0,3.5,0))
 for i,(radius,height,y) in enumerate([(2.8,3.8,6.5),(2.25,3.2,8),(1.5,2.8,9)]):tree.add('Crown'+str(i),'CylinderMesh',f'top_radius = 0.12\nbottom_radius = {radius}\nheight = {height}\nradial_segments = 9',c(['pine-dark','pine-mid','pine-light'][i]),(0,y,0),yaw=i*.17)
 tree.save('field-pine-a.tscn')
 exe=ROOT/'.tools/godot-4.5.2/Godot_v4.5.2-stable_mono_win64/Godot_v4.5.2-stable_mono_win64_console.exe'
 run=subprocess.run([str(exe),'--headless','--path',str(ROOT/'Game'),'--script','res://Tools/author_field_rocks.gd'],cwd=ROOT,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,creationflags=subprocess.CREATE_NO_WINDOW,timeout=60)
 log=run.stdout.decode('utf8',errors='replace');assert run.returncode==0 and 'FIELD_ROCK_AUTHOR_PASSED' in log and 'ERROR:' not in log,log
 for variant in ['a','b']:
  mesh=OUT/('field-rock-mesh-'+variant+'.tres');text=mesh.read_bytes().decode();text=re.sub(r' uid="uid://[^"]+"','',text);mesh.write_bytes(text.encode())
  rock=Scene('FieldRock');rock.add_external('Stone','res://Assets/Scenery/Models/field-rock-mesh-'+variant+'.tres',c('rock'));rock.save('field-rock-'+variant+'.tscn')
 keep=Scene('FieldTrialKeep')
 def box(name,size,color,pos):keep.add(name,'BoxMesh','size = Vector3('+', '.join(map(str,size))+')',c(color),pos)
 box('Foundation',(16,2.4,12),'wall',(0,1.2,0));box('LowerHouse',(14,4.8,10),'plaster',(0,4.8,0))
 keep.add('LowerRoof','PrismMesh','size = Vector3(18.8, 2.4, 14.8)',c('roof-lower'),(0,8.4,0))
 box('UpperHouse',(8,2.2,6),'plaster',(0,9.3,0));keep.add('UpperRoof','PrismMesh','size = Vector3(10.4, 2, 8.4)',c('roof-upper'),(0,11.4,0))
 for x in [-6.85,6.85]:
  for z in [-4.85,4.85]:box(f'Pillar{x}_{z}',(.22,4.8,.22),'cut',(x,4.8,z))
 for z in [-5.01,5.01]:
  box('Beam'+str(z),(14.2,.22,.18),'cut',(0,4,z))
  for x in [-4,0,4]:box(f'LowerWindow{x}_{z}',(.7,.8,.05),'roof-upper',(x,5.4,z))
 for z in [-3.01,3.01]:
  for x in [-2,2]:box(f'UpperWindow{x}_{z}',(.7,.65,.05),'roof-upper',(x,9.6,z))
 keep.save('field-trial-keep-a.tscn');print('Generated four deterministic static field models')
if __name__=='__main__':main()
