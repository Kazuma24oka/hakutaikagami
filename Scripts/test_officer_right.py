from pathlib import Path
import unittest,json,hashlib
import numpy as np
from PIL import Image
import build_officer_right as m
R=Path(__file__).resolve().parents[1];A=R/'Game/Assets/Trials/OfficerRightViewV7';O=R/'Art/Officer/RightViewV7'
class RightViewTests(unittest.TestCase):
 def test_fixed_native_alpha(self):
  for p in A.glob('*.png'):
   im=Image.open(p);self.assertEqual(im.size,(64,96));a=np.array(im.convert('RGBA'));self.assertTrue(set(np.unique(a[:,:,3]))<={0,255})
 def test_original_front_untouched(self):
  for p,h in [('Game/Assets/Trials/OfficerContextV1/front-64x96.png','1065fc8cbdca434a683d97a355262d366287c4b8ea924bd7499f6ed1e48275d3'),('Game/Assets/Trials/OfficerBodyBaseV2/body-front.png','f2e62a97092eabc7fa99e12ac665ad74c74f1d8a837bad359feb17d947633036')]:self.assertEqual(hashlib.sha256((R/p).read_bytes()).hexdigest(),h)
 def test_restoration_and_coverage(self):
  a=m.rgba(A/'source-right.png');b=m.rgba(A/'assembled-right.png');owner=np.array(Image.open(A/'ownership.png'));self.assertTrue(np.array_equal(a,b));self.assertTrue(np.all(owner[a[:,:,3]>0]>0));self.assertEqual(len(json.loads((O/'combination-hashes.json').read_text())),1024)
 def test_body_geometry_is_authored_mask(self):
  body=m.rgba(A/'body-right.png');sil=np.array(Image.open(O/'body-silhouette-mask.png'));self.assertTrue(np.array_equal(body[:,:,3],sil));self.assertEqual(Image.open(A/'body-right.png').getbbox(),(25,13,43,92))
 def test_no_baked_features_or_clothing_on_body(self):
  b=m.rgba(A/'body-right.png');rgb=b[b[:,:,3]>0,:3];self.assertTrue(np.all(rgb[:,0]>rgb[:,2]));self.assertGreater(int(rgb[:,0].min()),90)
  self.assertTrue(np.all(b[:13,:,3]==0));spec=json.loads((O/'body-definition.json').read_text());pelvis=m.mask(spec['body_parts']['pelvis']['polygon']);self.assertLessEqual(len(np.unique(b[pelvis,:3],axis=0)),2)
 def test_joint_probe_preserves_head_feet(self):
  for name in ['body-right','assembled-right']:
   a=m.rgba(A/(name+'.png'))
   for n in [-1,1]:
    b=m.warp(a,n);self.assertTrue(np.array_equal(a[:36],b[:36]));self.assertTrue(np.array_equal(a[70:],b[70:]));self.assertFalse(np.array_equal(a,b))
 def test_shared_surface_warp_commutes_with_layering(self):
  base=m.rgba(A/'body-right.png');fg=m.rgba(A/'bare-arm-front.png');layers={n:m.rgba(A/(n+'.png')) for n in m.ORDER};hides={n:np.array(Image.open(A/(n+'-occlusion.png')))>0 for n in m.ORDER}
  for omitted in [None,*m.ORDER]:
   selected=[n for n in m.ORDER if n!=omitted];a=m.compose(base,layers,hides,selected,fg)
   for n in [-1,1]:
    warped_hides={k:m.warp(np.dstack([v*0,v*0,v*0,v*255]).astype('uint8'),n)[:,:,3]>0 for k,v in hides.items()};b=m.compose(m.warp(base,n),{k:m.warp(v,n) for k,v in layers.items()},warped_hides,selected,m.warp(fg,n));self.assertTrue(np.array_equal(m.warp(a,n),b))
 def test_all_off_not_cropped(self):
  base=m.rgba(A/'body-right.png');layers={n:m.rgba(A/(n+'.png')) for n in m.ORDER};hides={n:np.array(Image.open(A/(n+'-occlusion.png')))>0 for n in m.ORDER};self.assertTrue(np.array_equal(m.compose(base,layers,hides,[],m.rgba(A/'bare-arm-front.png')),base))
if __name__=='__main__':unittest.main()
