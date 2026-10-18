"""Regression checks on actual modular source assets; no visual quality claims."""
from pathlib import Path
import unittest,itertools,json,hashlib
import numpy as np
from PIL import Image
R=Path(__file__).resolve().parents[1];O=R/'Game/Assets/Trials/OfficerEquipmentV2'
def pixels(p):return np.array(Image.open(p).convert('RGBA'))
class EquipmentTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.source=pixels(R/'Game/Assets/Trials/OfficerContextV1/front-64x96.png');cls.base=pixels(R/'Game/Assets/Trials/OfficerBodyBaseV2/body-front.png');cls.parts={k:pixels(O/(k+'.png')) for k in ['face','hair','armor']};cls.owners=np.array(Image.open(O/'source-owners.png'));cls.protected=np.array(Image.open(O/'protected-features.png'))>0;cls.occlusion=np.array(Image.open(O/'armor-body-occlusion.png'))>0;cls.bodyids=np.array(Image.open(R/'Game/Assets/Trials/OfficerBodyBaseV2/ownership.png'))
    def test_shared_native_canvas_and_binary_alpha(self):
        for im in [self.base,*self.parts.values()]:self.assertEqual(im.shape,(96,64,4));self.assertTrue(set(np.unique(im[:,:,3])).issubset({0,255}))
    def test_original_visible_pixels_exactly_restored(self):
        reference=self.source.copy();reference[reference[:,:,3]==0]=0
        self.assertTrue(np.array_equal(pixels(O/'composite-armor-face-hair.png'),reference))
    def test_all_eight_compositions_and_immutable_base(self):
        frozen=self.base.copy()
        for mask in range(8):
            selected={name for i,name in enumerate(['face','hair','armor']) if mask&(1<<i)}
            b=self.base.copy()
            if 'armor' in selected:b[self.occlusion]=0
            im=Image.fromarray(b)
            for name in ['face','armor','hair']:
                if name in selected:im.alpha_composite(Image.fromarray(self.parts[name]))
            key='-'.join(sorted(selected)) or 'base';self.assertTrue(np.array_equal(np.array(im),pixels(O/('composite-'+key+'.png'))),key)
        self.assertTrue(np.array_equal(frozen,self.base))
    def test_all_removed_equals_frozen_bare_base(self):
        self.assertTrue(np.array_equal(pixels(O/'composite-base.png'),self.base))
        bundle=json.loads((R/'Art/Officer/BodyBaseV2/candidate-bundle.json').read_text(encoding='utf-8'))
        self.assertEqual(hashlib.sha256((R/bundle['source']['path']).read_bytes()).hexdigest(),bundle['source']['sha256'])
    def test_hair_removal_preserves_observed_face_features(self):
        hair_off=pixels(O/'composite-armor-face.png')
        self.assertTrue(np.array_equal(hair_off[self.protected],self.source[self.protected]))
        self.assertFalse(np.any(self.parts['hair'][self.protected,3]))
    def test_face_removal_exposes_blank_base_without_eye_remnants(self):
        noface=pixels(O/'composite-armor-hair.png')
        self.assertTrue(np.array_equal(noface[self.protected],self.base[self.protected]))
        self.assertFalse(np.any(self.parts['armor'][self.protected,3]))
    def test_source_ownership_and_forehead_shadow_are_explicit(self):
        fg=self.source[:,:,3]>0;self.assertTrue(np.array_equal(self.owners>0,fg))
        self.assertEqual(set(np.unique(self.owners)),{0,1,2,3,4})
        shadow=np.array(Image.open(O/'hair-cast-shadow.png'))>0
        self.assertEqual(int(shadow.sum()),6);self.assertTrue(np.array_equal(self.owners==4,shadow));self.assertFalse(np.any(shadow&self.protected))
        for name,ids in [('hair',[2,4]),('armor',[3])]:
            owned=np.isin(self.owners,ids);self.assertTrue(np.array_equal(self.parts[name][:,:,3]>0,owned));self.assertTrue(np.array_equal(self.parts[name][owned],self.source[owned]))
    def test_completion_only_in_current_head(self):
        owned=self.owners==1;self.assertTrue(np.array_equal(self.parts['face'][owned],self.source[owned]))
        completed=(self.parts['face'][:,:,3]>0)&~owned;self.assertFalse(np.any(completed&(self.bodyids!=1)))
    def test_armor_visibility_uses_anatomical_regions_not_reference_clip(self):
        self.assertTrue(np.array_equal(self.occlusion,np.isin(self.bodyids,[8,11,13,14,16,17])))
        outside=(self.base[:,:,3]>0)&(self.source[:,:,3]==0);self.assertEqual(int(outside.sum()),23);self.assertTrue(np.all(self.occlusion[outside]))
        # A garment visibility mask contains entire declared body regions, not a global source-alpha crop.
        self.assertGreater(int(self.occlusion.sum()),int(outside.sum()))
if __name__=='__main__':unittest.main(verbosity=2)
