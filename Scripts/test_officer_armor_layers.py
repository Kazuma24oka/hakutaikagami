"""Actual eight-part artifacts and cumulative restoration regressions."""
from pathlib import Path
import json,unittest,hashlib,copy
import numpy as np
from PIL import Image
import officer_armor_layers as shared
from art_shared.part_ownership import audit_texture
R=Path(__file__).resolve().parents[1];O=R/'Game/Assets/Trials/OfficerArmorSplitV3'
def rgba(p):return np.array(Image.open(p).convert('RGBA'))
class ArmorSplitTests(unittest.TestCase):
    @classmethod
    def setUpClass(c):
        c.spec=json.loads((R/'Art/Officer/ArmorSplitV3/part-definition.json').read_text(encoding='utf-8'));c.base=rgba(R/'Game/Assets/Trials/OfficerBodyBaseV2/body-front.png');c.kit=rgba(R/'Game/Assets/Trials/OfficerEquipmentV2/armor.png');c.bodyids=np.array(Image.open(R/'Game/Assets/Trials/OfficerBodyBaseV2/ownership.png'));c.ids=np.array(Image.open(O/'source-owners.png'));c.layers={k:rgba(O/(k+'.png')) for k in c.spec['parts']};c.hidden={k:np.array(Image.open(O/(k+'-hidden.png')))>0 for k in c.layers};c.head={k:rgba(R/('Game/Assets/Trials/OfficerEquipmentV2/'+k+'.png')) for k in ['face','hair']};c.expected=json.loads((R/'Art/Officer/ArmorSplitV3/combination-hashes.json').read_text(encoding='utf-8'))
    def test_eight_native_binary_layers_and_exact_source_ownership(self):
        self.assertEqual(len(self.layers),8);self.assertTrue(np.array_equal(self.ids>0,self.kit[:,:,3]>0));self.assertEqual(set(np.unique(self.ids)),set(range(9)))
        for name,im in self.layers.items():self.assertEqual(im.shape,(96,64,4));self.assertTrue(set(np.unique(im[:,:,3])).issubset({0,255}))
    def test_all_1024_cases_match_frozen_native_hashes_and_no_mutation(self):
        before=self.base.copy();layers={k:v.copy() for k,v in self.layers.items()};keys=self.expected['keys'];self.assertEqual(len(self.expected['hashes']),1024)
        for flags in range(1024):
            selected={k for i,k in enumerate(keys) if flags&(1<<i)};out=shared.compose(self.base,self.head,self.layers,self.bodyids,self.spec,selected);self.assertEqual(shared.rgba_sha(out),self.expected['hashes'][str(flags)],str(flags))
        self.assertTrue(np.array_equal(self.base,before))
        for k in layers:self.assertTrue(np.array_equal(layers[k],self.layers[k]))
    def test_cumulative_previous_eight_states_retained(self):
        for flags in range(8):
            previous={k for i,k in enumerate(['face','hair','armor']) if flags&(1<<i)};selected=previous-{'armor'}
            if 'armor' in previous:selected |= set(self.layers)
            key='-'.join(sorted(previous)) or 'base';old=rgba(R/('Game/Assets/Trials/OfficerEquipmentV2/composite-'+key+'.png'));new=shared.compose(self.base,self.head,self.layers,self.bodyids,self.spec,selected);self.assertTrue(np.array_equal(old,new),key)
    def test_visible_pixels_hidden_material_and_component_limits(self):
        for name,art in self.layers.items():audit_texture(art,self.kit,self.ids,[self.spec['parts'][name]['id']],construction_mask=self.hidden[name],max_components=self.spec['parts'][name]['max_components'])
    def test_hidden_completion_only_beneath_declared_later_occluders(self):
        order=self.spec['draw_order']
        for name,m in self.hidden.items():
            rule=self.spec['completion_policy'].get(name)
            if not rule:self.assertFalse(m.any());continue
            allowed=np.isin(self.ids,[self.spec['parts'][k]['id'] for k in rule['occluders']]);self.assertFalse(np.any(m&~allowed));self.assertFalse(np.any(m&(self.ids==self.spec['parts'][name]['id'])))
            for k in rule['occluders']:self.assertGreater(order.index(k),order.index(name))
    def test_body_visibility_is_per_item_and_union_matches_previous(self):
        union=np.zeros((96,64),bool)
        for name,owners in self.spec['body_occlusion'].items():
            mask=np.array(Image.open(O/(name+'-occlusion.png')))>0;self.assertTrue(np.array_equal(mask,np.isin(self.bodyids,owners)));union|=mask
        prior=np.array(Image.open(R/'Game/Assets/Trials/OfficerEquipmentV2/armor-body-occlusion.png'))>0;self.assertTrue(np.array_equal(union,prior))
    def test_build_is_deterministic_against_exported_parts(self):
        ids,layers,hidden=shared.build(self.kit,self.bodyids,self.spec);self.assertTrue(np.array_equal(ids,self.ids))
        for name in layers:self.assertTrue(np.array_equal(layers[name],self.layers[name]));self.assertTrue(np.array_equal(hidden[name],self.hidden[name]))
    def test_all_removed_is_original_base_and_base_sha_unchanged(self):
        self.assertTrue(np.array_equal(shared.compose(self.base,self.head,self.layers,self.bodyids,self.spec,set()),self.base))
        b=json.loads((R/'Art/Officer/BodyBaseV2/candidate-bundle.json').read_text(encoding='utf-8'));self.assertEqual(hashlib.sha256((R/b['source']['path']).read_bytes()).hexdigest(),b['source']['sha256'])
    def test_undeclared_foreign_pixel_and_completion_cycle_rejected(self):
        art=self.layers['arms'].copy();art[0,0]=[20,20,20,255]
        with self.assertRaises(ValueError):audit_texture(art,self.kit,self.ids,[3],construction_mask=self.hidden['arms'],max_components=2)
        spec=copy.deepcopy(self.spec);spec['completion_policy']['torso']['occluders']=['footwear']
        with self.assertRaises(ValueError):shared.build(self.kit,self.bodyids,spec)
    def test_removed_gauntlets_restore_bare_hands_in_front_of_scabbard(self):
        mask=np.isin(self.bodyids,[8,11]);front=rgba(O/'bare-hands-front.png')
        self.assertTrue(np.array_equal(front[mask],self.base[mask]));self.assertFalse(np.any(front[~mask]))
        enabled={'face','hair',*self.layers}-{'arms'};result=shared.compose(self.base,self.head,self.layers,self.bodyids,self.spec,enabled)
        self.assertTrue(np.array_equal(result[mask],self.base[mask]))
    def test_semantic_override_outside_source_rejected(self):
        spec=copy.deepcopy(self.spec);spec['ownership_overrides'].append({'xy':[0,0],'part':'torso'})
        with self.assertRaises(ValueError):shared.ownership(self.kit,spec)
if __name__=='__main__':unittest.main(verbosity=2)
