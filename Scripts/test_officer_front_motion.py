"""Actual native motion artifacts, contact, masks, immutability and gate tests."""
import unittest,json,copy,hashlib
from pathlib import Path
import numpy as np
from PIL import Image
import officer_front_motion as m
import officer_art_pipeline as g
from art_shared.part_ownership import component_sizes
R=Path(__file__).resolve().parents[1];A=R/'Art/Officer/FrontMotionV4';O=R/'Game/Assets/Trials/OfficerFrontMotionV4'
class FrontMotionTests(unittest.TestCase):
    @classmethod
    def setUpClass(c):
        c.rig=json.loads((A/'rig.json').read_text(encoding='utf-8'));c.manifest,c.spec,c.t=m.load(R);c.frames=m.frames(c.rig);c.anim=[m.animate(c.t,f,c.rig) for _,_,f in c.frames];c.keys=['face','hair',*c.spec['parts']]
    def test_actual_exported_independent_sheets_match_fixed_rig(self):
        for k in self.t:
            sheet=m.rgba(O/(k+'.png'));self.assertEqual(sheet.shape,(96,768,4))
            for i,t in enumerate(self.anim):self.assertTrue(np.array_equal(sheet[:,64*i:64*(i+1)],t[k]),(k,i))
    def test_neutral_restores_source_and_off_restores_immutable_base(self):
        self.assertTrue(np.array_equal(m.compose(self.anim[0],self.spec,set(self.keys)),m.rgba(R/self.manifest['source']['path'])))
        self.assertTrue(np.array_equal(m.compose(self.anim[0],self.spec,set()),self.t['body']))
        for e in [self.manifest['source'],self.manifest['base'],*self.manifest['face_hair'].values()]:self.assertEqual(g.sha(R/e['path']),e['sha256'])
    def test_planted_foot_never_slides_or_lifts_and_other_foot_lifts(self):
        for _,_,f in self.frames:
            if f['lift']>=0:planted,lifted='sole_left','sole_right'
            else:planted,lifted='sole_right','sole_left'
            x,y=self.rig['joints'][planted];dx,dy=m.displacement(np.array(float(x)),np.array(float(y)),f,self.rig);self.assertAlmostEqual(float(dx),0);self.assertAlmostEqual(float(dy),0)
            x,y=self.rig['joints'][lifted];_,dy=m.displacement(np.array(float(x)),np.array(float(y)),f,self.rig)
            if abs(f['lift'])>0.5:self.assertLess(float(dy),-2)
    def test_skin_joint_surface_remains_connected_at_all_phases(self):
        for i,t in enumerate(self.anim):self.assertEqual(len(component_sizes(t['body'][:,:,3]>0)),1,str(i))
    def test_masks_share_exact_hand_and_foot_lookup(self):
        for f,t in zip(self.frames,self.anim):
            mapping=m.lookup(f[2],self.rig)
            for slot in self.spec['parts']:self.assertTrue(np.array_equal(t['hide_'+slot],m.warp(self.t['hide_'+slot],mapping)))
            self.assertTrue(np.array_equal(t['hands'],m.warp(self.t['hands'],mapping)))
            out=m.compose(t,self.spec,set(self.keys)-{'arms'});hand=t['hands'][:,:,3]>0;self.assertTrue(np.array_equal(out[hand],t['hands'][hand]))
    def test_head_face_hair_are_rigid_common_translation(self):
        for (_,_,f),t in zip(self.frames,self.anim):
            # All visible face pixels lie above shoulder transition; no rescale.
            for name in ['face','hair']:
                src=self.t[name];shift=-int(np.floor(-f['bob']+.5));im=Image.new('RGBA',(64,96));im.paste(Image.fromarray(src),(0,shift));self.assertTrue(np.array_equal(t[name],np.array(im)),name)
    def test_sword_remains_rigid_and_no_mutation(self):
        before={k:v.copy() for k,v in self.t.items()}
        for (_,_,f),t in zip(self.frames,self.anim):
            shift=int(np.floor(f['bob']*self.rig['sword_bob_weight']+.5));im=Image.new('RGBA',(64,96));im.paste(Image.fromarray(self.t['sword']),(0,shift));self.assertTrue(np.array_equal(t['sword'],np.array(im)))
            m.compose(t,self.spec,set())
        for k,v in before.items():self.assertTrue(np.array_equal(v,self.t[k]))
    def test_binary_palette_unchanged_no_clipping_and_real_motion(self):
        for k,src in self.t.items():
            colors={tuple(v) for v in src.reshape(-1,4)}
            for t in self.anim:
                a=t[k];self.assertTrue(set(np.unique(a[:,:,3])).issubset({0,255}));self.assertTrue({tuple(v) for v in a.reshape(-1,4)}.issubset(colors|{(0,0,0,0)}));self.assertFalse(a[0,:,3].any() or a[-1,:,3].any() or a[:,0,3].any() or a[:,-1,3].any())
        self.assertGreater(len({hashlib.sha256(v['body'].tobytes()).hexdigest() for v in self.anim[4:]}),3)
    def test_warp_field_has_no_fold(self):
        yy,xx=np.mgrid[:96,:64].astype(float)
        for _,_,f in self.frames:
            dx,dy=m.displacement(xx,yy,f,self.rig);xdx=np.gradient(xx+dx,axis=1);xdy=np.gradient(xx+dx,axis=0);ydx=np.gradient(yy+dy,axis=1);ydy=np.gradient(yy+dy,axis=0);self.assertGreater(float((xdx*ydy-xdy*ydx).min()),.5)
    def test_gear_remains_connected_within_authored_component_limits(self):
        for t in self.anim:
            for name in self.spec['parts']:self.assertLessEqual(len(component_sizes(t[name][:,:,3]>0)),self.manifest['parts'][name]['max_components'],name)
    def test_gate_rejects_unrecorded_or_changed_motion_rig(self):
        w=json.loads((R/'Art/Officer/work-record.json').read_text(encoding='utf-8'));p=w['plans'][w['front_motion_plan']];self.assertTrue(g.require_plan(R,'motion',p));bad=copy.deepcopy(p);bad['registration']['per_frame_fit']=True
        with self.assertRaises(ValueError):g.require_plan(R,'motion',bad)
        wrong={'path':'Art/Officer/FrontMotionV4/rig.json','sha256':'0'*64}
        with self.assertRaises(ValueError):g.evidence(R,wrong)
if __name__=='__main__':unittest.main(verbosity=2)
