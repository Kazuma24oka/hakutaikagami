"""Copy exact V1 animation samplers into LOD GLB, remapping bone names; avoid Blender FPS resampling."""
from pathlib import Path
import json,struct,copy,sys
import numpy as np

def read(p):
 b=Path(p).read_bytes();length=struct.unpack_from('<I',b,12)[0];g=json.loads(b[20:20+length]);start=20+length;size=struct.unpack_from('<I',b,start)[0];return g,b[start+8:start+8+size]

def preserve(source,target):
 src,sbin=read(source);out,obin=read(target);data=bytearray(obin);node_indices={n['name']:i for i,n in enumerate(out['nodes'])};accessors={};views={}
 def acc(index):
  if index in accessors:return accessors[index]
  a=copy.deepcopy(src['accessors'][index]);vi=a['bufferView']
  if vi not in views:
   v=copy.deepcopy(src['bufferViews'][vi]);start=v.get('byteOffset',0);payload=sbin[start:start+v['byteLength']];data.extend(b'\0'*((-len(data))%4));v['byteOffset']=len(data);v['buffer']=0;data.extend(payload);views[vi]=len(out['bufferViews']);out['bufferViews'].append(v)
  a['bufferView']=views[vi];accessors[index]=len(out['accessors']);out['accessors'].append(a);return accessors[index]
 animations=copy.deepcopy(src['animations'])
 for animation in animations:
  for sampler in animation['samplers']:
   sampler['input']=acc(sampler['input']);sampler['output']=acc(sampler['output'])
  for channel in animation['channels']:
   original=src['nodes'][channel['target']['node']];name=original['name'];index=node_indices[name];new=out['nodes'][index]
   for key,default in [('translation',[0,0,0]),('rotation',[0,0,0,1]),('scale',[1,1,1])]:assert np.allclose(original.get(key,default),new.get(key,default),atol=2e-5,rtol=0),(name,key)
   channel['target']['node']=index
 out['animations']=animations;out['buffers'][0]['byteLength']=len(data);data.extend(b'\0'*((-len(data))%4));j=json.dumps(out,separators=(',',':')).encode();j+=b' '*((-len(j))%4)
 Path(target).write_bytes(struct.pack('<III',0x46546c67,2,12+8+len(j)+8+len(data))+struct.pack('<II',len(j),0x4e4f534a)+j+struct.pack('<II',len(data),0x004e4942)+data)
 print('V2_EXACT_CLIPS',Path(target).name,len(accessors),'original sampler accessors',flush=True)
if __name__=='__main__':preserve(sys.argv[1],sys.argv[2])
