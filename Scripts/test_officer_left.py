from pathlib import Path
import unittest,json,hashlib,itertools
import numpy as np
from PIL import Image
import build_officer_left as m
R=Path(__file__).resolve().parents[1];A=R/'Game/Assets/Trials/OfficerLeftViewV11';O=R/'Art/Officer/LeftViewV11'
class LeftStaticTests(unittest.TestCase):
 @classmethod
 def setUpClass(c):
  c.b=m.rgba(A/'body-left.png');c.layers={n:m.rgba(A/(n+'.png')) for n in m.ORDER};c.hides={n:np.array(Image.open(A/(n+'-occlusion.png')).convert('L'))>0 for n in m.ORDER};c.fg=m.rgba(A/'bare-arm-front.png');c.source=m.rgba(A/'source-left.png')
 def test_all_restore_and_off_exact(self):
  self.assertTrue(np.array_equal(m.compose(self.b,self.layers,self.hides,m.ORDER,self.fg),self.source));self.assertTrue(np.array_equal(m.compose(self.b,self.layers,self.hides,[],self.fg),self.b))
 def test_body_exact_existing_geometry_reflection(self):
  self.assertTrue(np.array_equal(self.b,m.rgba(R/'Game/Assets/Trials/OfficerRightViewV7/body-right.png')[:,::-1]));self.assertEqual(Image.open(A/'body-left.png').getbbox(),(21,13,39,92))
 def test_face_not_hair_owned(self):
  owner=np.array(Image.open(A/'ownership.png'));d=json.loads((O/'part-definition.json').read_text(encoding='utf-8'));self.assertTrue(np.all(owner[self.layers['face'][:,:,3]>0]==d['ids']['face']));self.assertGreater(int((self.layers['face'][:,:,3]>0).sum()),40)
 def test_1024_combinations_not_mutate_base(self):
  saved=self.b.copy();hashes=json.loads((O/'combination-hashes.json').read_text());slots=m.SLOTS+['face','hair']
  for bits in itertools.product([False,True],repeat=10):
   a=m.compose(self.b,self.layers,self.hides,[n for n,b in zip(slots,bits) if b],self.fg);self.assertEqual(hashlib.sha256(a.tobytes()).hexdigest(),hashes[''.join(str(int(v)) for v in bits)])
  self.assertTrue(np.array_equal(saved,self.b))
 def test_visible_ownership_full_coverage(self):
  o=np.array(Image.open(A/'ownership.png'));self.assertTrue(np.all(o[self.source[:,:,3]>0]>0));self.assertTrue(np.all(o[self.source[:,:,3]==0]==0))
 def test_fixed_native_binary_alpha_and_material_scope(self):
  for p in A.glob('*.png'):
   with Image.open(p) as im:
    self.assertEqual(im.size,(64,96))
    if im.mode=='RGBA':self.assertTrue(set(np.unique(np.array(im)[:,:,3]))<={0,255})
  mat=np.array(Image.open(A/'material.png'));owner=np.array(Image.open(A/'ownership.png'));self.assertFalse(np.any((mat==2)&np.isin(owner,[7,8,9,10])))
 def test_no_body_features_and_flat_pelvis(self):
  rgb=self.b[self.b[:,:,3]>0,:3];self.assertTrue(np.all(rgb[:,0]>rgb[:,2]));spec=json.loads((O/'body-definition.json').read_text());p=m.mask(spec['body_parts']['pelvis']['polygon']);self.assertLessEqual(len(np.unique(self.b[p,:3],axis=0)),2)
 def test_sole_and_source_not_mirrored_art(self):
  self.assertEqual(int(np.where(self.source[:,:,3]>0)[0].max()),91);right=m.rgba(R/'Game/Assets/Trials/OfficerRightViewV7/source-right.png');self.assertFalse(np.array_equal(self.source,right[:,::-1]))
if __name__=='__main__':unittest.main()
