from pathlib import Path
import unittest,json,hashlib
import numpy as np
from PIL import Image
import build_officer_back as s
import officer_back_motion as m
R=Path(__file__).resolve().parents[1];A=R/'Game/Assets/Trials/OfficerBackViewV9'
def connected(mask):
 yy,xx=np.where(mask)
 if not len(yy):return 0
 seen=set();stack=[(int(yy[0]),int(xx[0]))]
 while stack:
  y,x=stack.pop()
  if (y,x) in seen:continue
  seen.add((y,x))
  for dy in [-1,0,1]:
   for dx in [-1,0,1]:
    ny,nx=y+dy,x+dx
    if 0<=ny<96 and 0<=nx<64 and mask[ny,nx] and (ny,nx) not in seen:stack.append((ny,nx))
 return len(seen)
class BackTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  cls.t,cls.ops=m.load(R);cls.rig=json.loads((R/'Art/Officer/BackMotionV10/rig.json').read_text(encoding='utf-8'));cls.anim=[m.animate(cls.t,f,cls.rig) for _,_,f in m.frames(cls.rig)]
 def test_original_sources_unchanged(self):
  for p,h in [('Game/Assets/Trials/OfficerContextV1/front-64x96.png','1065fc8cbdca434a683d97a355262d366287c4b8ea924bd7499f6ed1e48275d3'),('Game/Assets/Trials/OfficerBodyBaseV2/body-front.png','f2e62a97092eabc7fa99e12ac665ad74c74f1d8a837bad359feb17d947633036')]:self.assertEqual(hashlib.sha256((R/p).read_bytes()).hexdigest(),h)
 def test_neutral_restoration(self):
  self.assertTrue(np.array_equal(m.compose(self.anim[0],self.ops,set(m.KEYS)),s.rgba(A/'source-back.png')));self.assertTrue(np.array_equal(m.compose(self.anim[0],self.ops,set()),s.rgba(A/'body.png')))
 def test_face_is_noop_all_frames(self):
  self.assertFalse(self.t['face'][0][:,:,3].any())
  for a in self.anim:self.assertTrue(np.array_equal(m.compose(a,self.ops,set(m.KEYS)),m.compose(a,self.ops,set(m.KEYS)-{'face'})))
 def test_body_connected_all_frames(self):
  for a in self.anim:
   alpha=m.compose(a,self.ops,set())[:,:,3]>0;self.assertEqual(connected(alpha),int(alpha.sum()),'Detached body/ear joint')
 def test_sword_character_left_and_connected(self):
  a=self.t['sword'][0];mask=a[:,:,3]>0;yy,xx=np.where(mask);self.assertLess(int(xx.max()),32);self.assertEqual(connected(mask),int(mask.sum()))
 def test_alpha_dimensions_and_body_not_global_cropped(self):
  for p in A.glob('*.png'):
   a=s.rgba(p);self.assertEqual(a.shape,(96,64,4));self.assertTrue(set(np.unique(a[:,:,3]))<={0,255})
  self.assertTrue(np.any((self.t['body'][0][:,:,3]>0)&(s.rgba(A/'source-back.png')[:,:,3]==0)))
 def test_real_slot_contribution(self):
  for n in m.KEYS[1:]:
   a=m.compose(self.anim[0],self.ops,{n});bare=m.compose(self.anim[0],self.ops,set());self.assertGreater(int(np.any(a!=bare,axis=2).sum()),0,n)
 def test_observed_skin_not_hair_owned(self):
  d=json.loads((R/'Art/Officer/BackViewV9/parts.json').read_text());hair=self.t['hair'][0]
  for x,y in d['observed_skin_pixels']:self.assertEqual(int(hair[y,x,3]),0)
 def test_support_sole_height(self):
  for f in self.rig['clips']['walk']['frames']:
   # Back character-left maps to original front's right-hand screen side.
   support_x=18 if f['lift']>=0 else 45;dx,dy=m.field(np.array(float(support_x)),np.array(91.),f,self.rig,'surface');self.assertAlmostEqual(float(dy),0,places=5)
 def test_rigid_hair_sword_and_no_clipping(self):
  for a in self.anim:
   for n in ['hair','sword']:self.assertEqual(int((a[n][:,:,3]>0).sum()),int((self.t[n][0][:,:,3]>0).sum()))
   q=m.compose(a,self.ops,set(m.KEYS));self.assertFalse(q[0,:,3].any() or q[-1,:,3].any() or q[:,0,3].any() or q[:,-1,3].any())
 def test_inverse_no_fold(self):
  y,x=np.mgrid[:96,:64].astype(float)
  for _,_,f in m.frames(self.rig):
   sx=x.copy();sy=y.copy()
   for _ in range(50):dx,dy=m.field(sx,sy,f,self.rig,'surface');sx=x-dx;sy=y-dy
   dx,dy=m.field(sx,sy,f,self.rig,'surface');self.assertLess(float(np.max(abs(sx+dx-x)+abs(sy+dy-y))),.0001)
 def test_boundary_alpha_is_mask_not_opaque_canvas(self):
  for n in m.KEYS[2:]:self.assertLess(int((self.t['hide_'+n][0][:,:,3]>0).sum()),500)
if __name__=='__main__':unittest.main()
