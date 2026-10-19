from pathlib import Path
import json,hashlib,unittest
import numpy as np
import officer_left_motion as m
R=Path(__file__).resolve().parents[1]
class LeftMotionTests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  cls.rig=json.loads((R/'Art/Officer/LeftMotionV12/rig.json').read_text(encoding='utf-8'));cls.t,cls.ops=m.load(R);cls.frames=m.frames(cls.rig);cls.animated=[m.animate(cls.t,f,cls.rig) for _,_,f in cls.frames]
 def test_neutral_full_and_bare_restore(self):
  for selected,name in [(set(m.KEYS),'source-left.png'),(set(),'body-left.png')]:self.assertTrue(np.array_equal(m.compose(self.animated[0],self.ops,selected),m.rgba(R/'Game/Assets/Trials/OfficerLeftViewV11'/name)))
 def test_opposite_limb_phase(self):
  f=self.rig['clips']['walk']['frames'][1];x=np.array(30.);y=np.array(61.);n=m.field(x,y,f,self.rig,'near_arm');far=m.field(x,y,f,self.rig,'far_arm');self.assertAlmostEqual(float(n[0]),-float(far[0]));self.assertGreater(float(n[0]),0)
 def test_planted_soles_and_alternating_lift(self):
  for f in self.rig['clips']['walk']['frames']:
   lifted=[]
   for kind in ['near_leg','far_leg']:
    _,dy=m.field(np.array(35.),np.array(91.),f,self.rig,kind);lifted.append(float(dy))
   self.assertAlmostEqual(max(lifted),0);self.assertLessEqual(min(lifted),0)
  self.assertLess(float(m.field(np.array(35.),np.array(91.),self.rig['clips']['walk']['frames'][0],self.rig,'near_leg')[1]),0)
  self.assertLess(float(m.field(np.array(35.),np.array(91.),self.rig['clips']['walk']['frames'][4],self.rig,'far_leg')[1]),0)
 def test_shared_roots(self):
  for _,_,f in self.frames:
   for kind,y in [('near_leg',63),('far_leg',63),('near_arm',38),('far_arm',38)]:
    dx,dy=m.field(np.array(31.),np.array(float(y)),f,self.rig,kind);self.assertAlmostEqual(float(dx),0);self.assertAlmostEqual(float(dy),f['bob'])
 def test_inverse_and_no_surface_fold(self):
  y,x=np.mgrid[:96,:64].astype(float)
  for _,_,f in self.frames:
   for kind in ['core','near_arm','far_arm','near_leg','far_leg','coat']:
    sx=x.copy();sy=y.copy()
    for _ in range(50):dx,dy=m.field(sx,sy,f,self.rig,kind);sx=x-dx;sy=y-dy
    dx,dy=m.field(sx,sy,f,self.rig,kind);self.assertLess(float(np.max(abs(sx+dx-x)+abs(sy+dy-y))),1e-5)
    _,dy1=m.field(x,y+.001,f,self.rig,kind);_,dy0=m.field(x,y,f,self.rig,kind);self.assertGreater(float((1+(dy1-dy0)/.001).min()),.6)
 def test_rigid_head_sword(self):
  for a in self.animated:
   for n in ['face','hair','sword']:
    source=self.t[n][0];self.assertEqual(int((a[n][:,:,3]>0).sum()),int((source[:,:,3]>0).sum()))
 def test_same_chain_material_contacts(self):
  self.assertEqual(self.t['near_legs'][1],self.t['near_boot'][1]);self.assertEqual(self.t['far_legs'][1],self.t['far_boot'][1]);self.assertEqual(self.t['near_arm_skin'][1],self.t['near_arm'][1]);self.assertNotEqual(self.t['near_arm'][1],self.t['far_arm'][1])
 def test_far_material_is_actually_visible_in_walk(self):
  for group in [['far_arm','far_shoulder'],['far_legs','far_boot']]:
   without=[o for o in self.ops if o['sheet'] not in group]
   differences=[int(np.any(m.compose(a,self.ops,set(m.KEYS))!=m.compose(a,without,set(m.KEYS)),axis=2).sum()) for a in self.animated[4:]]
   self.assertGreater(max(differences),5,'Far chain was erased or never drawn: '+str(group))
 def test_luminance_occlusion_not_full_alpha(self):
  from PIL import Image
  for slot in ['torso','waist','coat','arms','legs','footwear','shoulders','face','hair']:
   expected=np.array(Image.open(R/'Game/Assets/Trials/OfficerLeftViewV11'/(slot+'-occlusion.png')).convert('L'));self.assertTrue(np.array_equal(self.t['core_hide_'+slot][0][:,:,3],expected))
 def test_far_katana_order_and_side_identity(self):
  order=[o['sheet'] for o in self.ops];self.assertLess(order.index('sword'),order.index('core'));self.assertLess(order.index('sword'),order.index('near_arm'))
  self.assertEqual(self.rig['near_character_side'],'right');self.assertEqual(self.rig['far_character_side'],'left')
  # With body and katana only, sword may not overwrite any opaque base pixel.
  for a in self.animated:
   bare=m.compose(a,self.ops,set());with_sword=m.compose(a,self.ops,{'sword'});live=bare[:,:,3]>0;self.assertTrue(np.array_equal(bare[live],with_sword[live]))
 def test_sources_immutable(self):
  original={n:hashlib.sha256(a.tobytes()).hexdigest() for n,(a,_) in self.t.items()}
  for a in self.animated:
   for selected in [set(m.KEYS),set(),set(m.KEYS)-{'arms','coat'}]:m.compose(a,self.ops,selected)
  self.assertEqual(original,{n:hashlib.sha256(a.tobytes()).hexdigest() for n,(a,_) in self.t.items()})
 def test_full_walk_distinct_and_unclipped(self):
  images=[m.compose(a,self.ops,set(m.KEYS)) for a in self.animated[4:]];self.assertEqual(len({hashlib.sha256(a.tobytes()).hexdigest() for a in images}),8)
  for a in images:self.assertFalse(a[0,:,3].any() or a[-1,:,3].any() or a[:,0,3].any() or a[:,-1,3].any())
 def test_bare_body_connected(self):
  for a in self.animated:
   alpha=m.compose(a,self.ops,set())[:,:,3]>0;ys,xs=np.where(alpha);seen=set();stack=[(int(ys[0]),int(xs[0]))]
   while stack:
    y,x=stack.pop()
    if (y,x) in seen:continue
    seen.add((y,x))
    for dy in [-1,0,1]:
     for dx in [-1,0,1]:
      yy,xx=y+dy,x+dx
      if 0<=yy<96 and 0<=xx<64 and alpha[yy,xx] and (yy,xx) not in seen:stack.append((yy,xx))
   self.assertEqual(len(seen),int(alpha.sum()),'Disconnected bare joint')
if __name__=='__main__':unittest.main()
