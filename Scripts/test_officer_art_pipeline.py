import copy
import json
from pathlib import Path
import tempfile
import unittest
import numpy as np
from PIL import Image
import officer_art_pipeline as p
from art_shared.part_ownership import build_texture, audit_texture
from art_shared.regression_runner import RegressionCache

class PipelineTests(unittest.TestCase):
    def setUp(self):
        scratch=Path(__file__).resolve().parents[1]/'.art-test-tmp'
        scratch.mkdir(exist_ok=True)
        self.temp=tempfile.TemporaryDirectory(dir=scratch);self.root=Path(self.temp.name)
        if not self.root.resolve().is_relative_to(scratch.resolve()):
            raise RuntimeError('Test cleanup path escapes designated scratch directory')
        self.addCleanup(self.temp.cleanup)
        (self.root/'Art/Officer').mkdir(parents=True);(self.root/'Game/Data').mkdir(parents=True)
        repo=Path(__file__).resolve().parents[1]
        self.contract=json.loads((repo/'Art/Officer/production-contract.json').read_text(encoding='utf-8'))
        self.contract['generation_backend']='comfyui'  # Explicit synthetic backend, independent of current art tool.
        self.profile=json.loads((repo/'Game/Data/officer_visual_profile.json').read_text(encoding='utf-8'))
        self.save()
        (self.root/'fixture.txt').write_text('explicitly synthetic test evidence')
        self.e={'path':'fixture.txt','sha256':p.sha(self.root/'fixture.txt')}
        self.ledger={'status':'CONTROLS_REVIEWED','sources':[self.e],
            'registration':{'uniform_scale':1,'translate':[0,0]},
            'landmarks':{k:{'kind':'observed-surface','source_xy':[32,48],
                'tolerance_native_px':0,'basis':'explicit synthetic fixture'} for k in p.DIMENSIONS},
            'controls':{k:self.e for k in ['observed_skin','feature_exclusion','hidden_construction','ownership','edit','preserve']},
            'control_review':{'reviewer':'synthetic test','observation':'No art acceptance',
                'evidence':{k:self.e for k in ['same_coordinate_overlay','head_body_proportions','pose','line_shading','visible_vs_hidden']}}}
        self.bind_reference()
        self.plan={'id':'synthetic-fixture' ,'stage':'front_authoring','contract_sha256':p.digest(self.contract),
            'feedback':['fixture'],'prior_failures':['fixture'],'change_scope':['fixture'],
            'preserve':['fixture'],'unresolved':[], 'inputs':[self.e], 'tools':[self.e], 'references':[self.e],
            'regressions':{k:'fixture method; not visual approval' for k in p.REVIEWS},
            'retained_decisions':self.contract['decisions'],
            'backend':{'name':'comfyui',**{k:self.e for k in ['workflow','model','edit_mask','preserve_mask']},
                'seed':123,'steps':20,'cfg':6,'sampler':'euler','scheduler':'normal','denoise':.4},
            'registration':{'scale_xy':[1,1],'per_part_fit':False,'per_frame_fit':False},
            'landmarks':{k:{'kind':'authoring-target','xy':[32,48],'basis':'synthetic fixture only'} for k in p.DIMENSIONS}}
        self.plan['appearance_correspondence']=self.correspondence()
        workflow={'1':{'class_type':'CheckpointLoaderSimple','inputs':{'ckpt_name':'fixture.txt'}},
            '2':{'class_type':'KSampler','inputs':{'seed':123,'steps':20,'cfg':6,'sampler_name':'euler','scheduler':'normal','denoise':.4}}}
        (self.root/'workflow.json').write_text(json.dumps(workflow))
        self.plan['backend']['workflow']={'path':'workflow.json','sha256':p.sha(self.root/'workflow.json')}
        (self.root/'Art/Officer/work-record.json').write_text('{"plans":{}}')
    def bind_reference(self):
        path=self.root/'appearance.json';path.write_text(json.dumps(self.ledger))
        self.contract['appearance_reference']={'path':'appearance.json','sha256':p.sha(path)}
        self.save()
    def correspondence(self):
        return {'reference_sha256':self.contract['appearance_reference']['sha256'],
            'registration':self.ledger['registration'],
            'targets':{k:[32,48] for k in p.DIMENSIONS}}
    def save(self):
        (self.root/'Art/Officer/production-contract.json').write_text(json.dumps(self.contract),encoding='utf-8')
        (self.root/'Game/Data/officer_visual_profile.json').write_text(json.dumps(self.profile),encoding='utf-8')
    def runplan(self):
        (self.root/'Art/Officer/work-record.json').write_text(json.dumps({'plans':{self.plan['id']:self.plan}}))
        return p.require_plan(self.root,self.plan['stage'],self.plan)
    def test_explicit_front_plan_can_validate_without_approving_art(self):
        self.assertEqual(len(self.runplan()),64)
    def test_actual_repository_without_plan_is_blocked(self):
        with self.assertRaises(p.Blocked):p.require_plan(Path(__file__).resolve().parents[1],'front_authoring')
    def test_reused_modules_match_recorded_source_bytes(self):
        base=Path(p.__file__).parent/'art_shared'
        for item in json.loads((base/'provenance.json').read_text(encoding='utf-8')):
            self.assertEqual(p.sha(base/item['name']),item['sha256'])
    def controls_fixture(self):
        actual=Path(p.__file__).resolve().parents[1]/'Art/Officer/BodyBaseV1/geometry.json'
        (self.root/'geometry.json').write_bytes(actual.read_bytes())
        prep={'scope':'deterministic-controls-only','contract_sha256':p.digest(self.contract),
              'feedback':['fixture'],'preserve':['fixture'],'tools':[self.e],'references':[self.e],
              'geometry':{'path':'geometry.json','sha256':p.sha(self.root/'geometry.json')}}
        spec=json.loads((self.root/'geometry.json').read_text())
        for name, xy in spec['landmarks'].items():self.ledger['landmarks'][name]['source_xy']=xy
        self.bind_reference()
        prep['contract_sha256']=p.digest(self.contract)
        prep['inputs']=[self.e]
        prep['appearance_correspondence']=self.correspondence()
        prep['appearance_correspondence']['targets']=spec['landmarks']
        return prep
    def save_controls_fixture(self,prep):
        (self.root/'Art/Officer/work-record.json').write_text(json.dumps({'control_preparation':prep}))
    def test_control_bootstrap_cannot_authorize_generation(self):
        prep=self.controls_fixture();self.save_controls_fixture(prep)
        self.assertEqual(len(p.require_control_plan(self.root,prep)),64)
        with self.assertRaises(p.Blocked):p.require_plan(self.root,'front_authoring')
    def test_control_bootstrap_rejects_changed_definition(self):
        prep=self.controls_fixture();self.save_controls_fixture(prep)
        (self.root/'geometry.json').write_text('{}')
        with self.assertRaises(p.Blocked):p.require_control_plan(self.root,prep)
    def test_control_bootstrap_rejects_invalid_masks_or_geometry(self):
        for kind in ['head-protection','bounds','landmarks','duplicate-id']:
            with self.subTest(kind=kind):
                prep=self.controls_fixture();spec=json.loads((self.root/'geometry.json').read_text())
                if kind=='head-protection':spec['protected_surfaces']=['pelvis']
                if kind=='bounds':spec['regions'][0]['polygon'][0]=[64,8]
                if kind=='landmarks':spec['landmarks'].pop('chin')
                if kind=='duplicate-id':spec['regions'][0]['id']=spec['regions'][1]['id']
                (self.root/'geometry.json').write_text(json.dumps(spec))
                prep['geometry']['sha256']=p.sha(self.root/'geometry.json');self.save_controls_fixture(prep)
                with self.assertRaises(p.Blocked):p.require_control_plan(self.root,prep)
    def test_reference_cannot_be_optional_or_replaced(self):
        for mutation in [lambda q:q.pop('appearance_correspondence'),
                         lambda q:q.update(inputs=[]),
                         lambda q:q['appearance_correspondence'].update(reference_sha256='other'),
                         lambda q:q['appearance_correspondence'].update(registration={'uniform_scale':2})]:
            q=copy.deepcopy(self.plan);mutation(q)
            with self.assertRaises(p.Blocked):p.require_appearance_reference(self.root,self.contract,q)
    def test_same_canvas_does_not_allow_character_landmark_drift(self):
        q=copy.deepcopy(self.plan);q['appearance_correspondence']['targets']['chin']=[32,49]
        with self.assertRaises(p.Blocked):p.require_appearance_reference(self.root,self.contract,q)
    def test_armor_outline_cannot_be_named_observed_anatomy(self):
        self.ledger['landmarks']['chin']['kind']='armor-outline';self.bind_reference()
        self.plan['appearance_correspondence']=self.correspondence()
        with self.assertRaises(p.Blocked):p.require_appearance_reference(self.root,self.contract,self.plan)
    def test_hidden_anatomy_requires_occluder_and_bounds(self):
        self.ledger['landmarks']['pelvis']={'kind':'authored-hidden','basis':'synthetic hidden anatomy',
            'occluder':'armor','allowed_native_bounds':[31,47,33,49]};self.bind_reference()
        self.plan['appearance_correspondence']=self.correspondence()
        p.require_appearance_reference(self.root,self.contract,self.plan)
        self.plan['appearance_correspondence']['targets']['pelvis']=[40,48]
        with self.assertRaises(p.Blocked):p.require_appearance_reference(self.root,self.contract,self.plan)
    def test_missing_control_and_false_review_do_not_lift_gate(self):
        for kind in ['pending','mask','review']:
            before=copy.deepcopy(self.ledger)
            if kind=='pending':self.ledger['status']='PENDING'
            elif kind=='mask':self.ledger['controls']['preserve']={'path':'absent','sha256':'x'}
            else:self.ledger['control_review']['observation']=''
            self.bind_reference();self.plan['appearance_correspondence']=self.correspondence()
            with self.assertRaises(p.Blocked):p.require_appearance_reference(self.root,self.contract,self.plan)
            self.ledger=before
    def test_original_source_change_invalidates_correspondence(self):
        (self.root/'fixture.txt').write_text('changed source')
        with self.assertRaises(p.Blocked):p.require_appearance_reference(self.root,self.contract,self.plan)
    def test_actual_rejected_controls_cannot_regenerate(self):
        root=Path(__file__).resolve().parents[1]
        work=json.loads((root/'Art/Officer/work-record.json').read_text(encoding='utf-8'))
        with self.assertRaises(p.Blocked):p.require_control_plan(root,work['control_preparation'])
        with self.assertRaises(p.Blocked):p.require_plan(root,'front_authoring',work['plans']['body-front-v1'])
    def test_source_annotation_preparation_does_not_authorize_generation(self):
        spec={'canvas':[64,96],'landmarks':self.ledger['landmarks'],
              'observed_surfaces':[{'name':'synthetic visible surface'}], 'registration':self.ledger['registration']}
        (self.root/'source-definition.json').write_text(json.dumps(spec))
        prep={'scope':'source-annotation-controls-only','contract_sha256':p.digest(self.contract),
              'retained_decisions':self.contract['decisions'],'sources':self.ledger['sources'],
              'registration':self.ledger['registration'],'tools':[self.e],'references':[self.e],
              'definition':{'path':'source-definition.json','sha256':p.sha(self.root/'source-definition.json')},
              'feedback':['fixture'],'prior_failures':['fixture']}
        (self.root/'Art/Officer/work-record.json').write_text(json.dumps({'reference_preparation':prep}))
        self.assertEqual(len(p.require_reference_preparation(self.root,prep)),64)
        with self.assertRaises(p.Blocked):p.require_plan(self.root,'front_authoring',self.plan)
        changed=copy.deepcopy(prep);changed['sources']=[]
        with self.assertRaises(p.Blocked):p.require_reference_preparation(self.root,changed)
    def test_builtin_edit_binds_actual_prompt_and_reference(self):
        self.contract['generation_backend']='builtin-imagegen';self.save()
        self.plan['contract_sha256']=p.digest(self.contract)
        self.plan['backend']={'name':'builtin-imagegen','prompt':self.e,
            'reference_images':[self.e],'transparent_background':True}
        self.assertEqual(len(self.runplan()),64)
        self.plan['backend']['reference_images']=[{'path':'absent','sha256':'x'}]
        with self.assertRaises(p.Blocked):self.runplan()
        self.plan['backend']['reference_images']=[self.e];self.plan['backend']['transparent_background']=False
        with self.assertRaises(p.Blocked):self.runplan()
    def test_missing_and_changed_prerequisites_are_rejected(self):
        mutations=[lambda q:q.update(unresolved=['not measured']),lambda q:q.update(inputs=[]),
            lambda q:q.update(tools=[]),lambda q:q.update(references=[]),lambda q:q.update(contract_sha256='stale'),
            lambda q:q.update(retained_decisions={}),lambda q:q['backend'].update(name='built-in-imagegen'),
            lambda q:q['backend'].update(seed=None),lambda q:q['backend'].update(edit_mask={'path':'missing','sha256':'x'}),
            lambda q:q['registration'].update(scale_xy=[1,2]),lambda q:q['registration'].update(per_part_fit=True),
            lambda q:q['registration'].update(per_frame_fit=True),lambda q:q['registration'].update(scale_xy=[float('nan')]*2),
            lambda q:q['landmarks'].pop('chin'),lambda q:q['landmarks']['chin'].update(xy=None),
            lambda q:q['landmarks']['chin'].update(xy=[64,5]),lambda q:q['regressions'].pop('face_identity')]
        original=copy.deepcopy(self.plan)
        for i, mutate in enumerate(mutations):
            with self.subTest(i=i):
                self.plan=copy.deepcopy(original);mutate(self.plan)
                with self.assertRaises((p.Blocked,ValueError,TypeError)):self.runplan()
    def test_changed_input_invalidates_plan(self):
        self.runplan();(self.root/'fixture.txt').write_text('changed')
        with self.assertRaises(p.Blocked):self.runplan()
    def test_unrecorded_plan_is_rejected(self):
        with self.assertRaises(p.Blocked):p.require_plan(self.root,'front_authoring',self.plan)
    def test_actual_workflow_must_match_claimed_settings(self):
        self.plan['backend']['seed']=999
        with self.assertRaises(p.Blocked):self.runplan()
    def test_evidence_path_traversal_rejected(self):
        with self.assertRaises(p.Blocked):p.evidence(self.root,{'path':'../outside','sha256':'x'})
    def test_contract_fixed_conditions_rejected(self):
        for name,value in [('canvas',[128,192]),('display_scale',2),('texture_filter','linear'),
                           ('alpha','soft'),('fit_policy','fit-every-part'),('portrait_policy','resize-small-sprite')]:
            with self.subTest(name=name):
                c=copy.deepcopy(self.contract);c[name]=value
                with self.assertRaises(p.Blocked):p.validate_contract(c,self.profile)
        for key in ['face_features','hair','beard','clothing','footwear','genital_detail']:
            c=copy.deepcopy(self.contract);c['base_body'][key]=True
            with self.assertRaises(p.Blocked):p.validate_contract(c,self.profile)
    def review(self,name,checks):
        binding=p.fingerprint(self.root,self.contract,{k:v for k,v in self.plan.items() if not k.endswith('_review')})
        self.plan[name]={'binding':binding,'status':'PASS','checks':{k:{'status':'PASS','reviewer':'unit-test-fixture',
            'observation':'synthetic test only','evidence':[self.e]} for k in checks}}
    def test_stage_cannot_skip_front_and_parts_review(self):
        self.plan['stage']='parts'
        with self.assertRaises(p.Blocked):self.runplan()
        self.review('front_review',{'base_body','face_on','hair_off','native_scale','white_black_checker'})
        self.runplan()
        self.plan['registration']['scale_xy']=[2,2]
        with self.assertRaises(p.Blocked):self.runplan()
    def test_empty_pass_string_cannot_release(self):
        self.plan['stage']='release';self.plan['front_review']={'status':'PASS'}
        with self.assertRaises(p.Blocked):self.runplan()
    def test_review_evidence_and_observation_required(self):
        self.plan['stage']='parts'
        self.review('front_review',{'base_body','face_on','hair_off','native_scale','white_black_checker'})
        self.plan['front_review']['checks']['face_on']['observation']=''
        with self.assertRaises(p.Blocked):self.runplan()
    def test_native_alpha_and_ownership(self):
        a=np.zeros((96,64,4),dtype=np.uint8);a[10:20,20:30]=[200,150,100,255]
        ids=np.zeros((96,64),dtype=np.uint8);ids[10:20,20:30]=1
        p.inspect_native(a,ids)
        ids[11,21]=0
        with self.assertRaises(ValueError):p.inspect_native(a,ids)
        ids[11,21]=1;a[11,21,3]=128
        with self.assertRaises(ValueError):p.inspect_native(a,ids)
    def test_hair_cannot_take_eye_pixels(self):
        a=np.zeros((96,64,4),dtype=np.uint8);a[20,25]=[30,20,10,255]
        ids=np.zeros((96,64),dtype=np.uint8);ids[20,25]=2
        face=np.zeros((96,64),bool);face[20,25]=True
        with self.assertRaises(p.Blocked):p.inspect_native(a,ids,protected_face=face,hair_ids=[2])
    def test_foreign_hip_pixel_cannot_enter_hand(self):
        a=np.zeros((96,64,4),dtype=np.uint8);a[30:33,10:13]=[200,150,100,255];a[40,20]=[20,30,40,255]
        ids=np.zeros((96,64),dtype=np.uint8);ids[30:33,10:13]=1;ids[40,20]=2
        hand,_=build_texture(a,ids,{1});hand[40,20]=a[40,20]
        with self.assertRaises(ValueError):audit_texture(hand,a,ids,{1})
    def test_undeclared_joint_overlap_rejected(self):
        a=np.zeros((96,64,4),dtype=np.uint8);a[10:14,10:14]=255
        ids=np.zeros((96,64),dtype=np.uint8);ids[10:14,10:14]=1
        with self.assertRaises(ValueError):build_texture(a,ids,{1},joints=[{'radius':50,'center':[10,10],'source_ids':[1]}])
    def test_recolor_preserves_other_material_and_alpha(self):
        a=np.zeros((96,64,4),dtype=np.uint8);a[10:20,20:30]=[200,30,30,255];b=a.copy()
        m=np.zeros((96,64),bool);m[10:20,20:30]=True;b[m,0]=30
        p.validate_recolor(a,b,m)
        b[5,5]=[255,0,0,255]
        with self.assertRaises(p.Blocked):p.validate_recolor(a,b,m)
    def test_same_canvas_does_not_allow_changed_head_size(self):
        a=np.zeros((96,64,4),dtype=np.uint8);a[5:30,22:42]=255
        mask=a[:,:,3]>0
        c={'head':{'bounds':[22,5,42,30],'tolerance_px':0,'basis':'synthetic fixed head fixture'}}
        p.validate_geometry(a,{'head':mask},c)
        a[30:34,22:42]=255
        with self.assertRaises(p.Blocked):p.validate_geometry(a,{'head':a[:,:,3]>0},c)
    def test_missing_geometry_or_background_annotation_rejected(self):
        a=np.zeros((96,64,4),dtype=np.uint8)
        with self.assertRaises(p.Blocked):p.validate_geometry(a,{}, {})
        with self.assertRaises(p.Blocked):p.validate_geometry(a,{'head':np.ones((96,64),bool)},
            {'head':{'bounds':[0,0,64,96],'tolerance_px':0,'basis':'fixture'}})
    def test_shared_selection_requires_both_representations(self):
        c={'hair':{'short':{'small':'hair_small_v1','portrait':'hair_portrait_v1'}}}
        r=p.resolve_selection({'hair':'short'},c)
        self.assertEqual(r['portrait']['hair'],'hair_portrait_v1')
        c['hair']['short']['small']=None
        with self.assertRaises(p.Blocked):p.resolve_selection({'hair':'short'},c)
        self.assertEqual(p.resolve_selection({'hair':'none'},c),{'small':{},'portrait':{}})
    def test_composition_uses_shared_coordinates_and_no_fit(self):
        body=Image.new('RGBA',(64,96),(200,150,100,255));hair=Image.new('RGBA',(64,96));hair.putpixel((20,10),(20,10,5,255))
        parts={'body':body,'hair':hair}
        self.assertEqual(p.compose_parts(parts,['body','hair'],{'body'}).tobytes(),body.tobytes())
        self.assertEqual(p.compose_parts(parts,['body','hair'],{'body','hair'}).getpixel((20,10)),(20,10,5,255))
        parts['hair']=Image.new('RGBA',(32,48))
        with self.assertRaises(p.Blocked):p.compose_parts(parts,['body','hair'],{'body','hair'})
    def test_actual_png_bundle_checks_pixels_and_rejects_bad_palette(self):
        source=np.zeros((96,64,4),np.uint8);source[5:30,22:42]=[200,150,100,255]
        ids=np.zeros((96,64),np.uint8);ids[5:30,22:42]=1
        def save(name,array):
            path=self.root/name;Image.fromarray(array).save(path)
            return {'path':name,'sha256':p.sha(path)}
        s=save('source.png',source);o=save('ids.png',ids)
        empty=save('empty.png',np.zeros((96,64),np.uint8));head=save('head.png',(ids>0).astype(np.uint8)*255)
        bundle={'source':s,'ownership':o,'protected_face':empty,'hair_ids':[2],
            'geometry_regions':{'head':head},'geometry_criteria':{'head':{'bounds':[22,5,42,30],'tolerance_px':0,'basis':'synthetic fixture'}},
            'palette_rgb':[[200,150,100]],'parts':{'body':{'image':s,'hidden':empty,'owner_ids':[1],'max_components':1}},
            'order':['body'],'recolor_cases':[]}
        self.assertEqual(p.validate_bundle(self.root,bundle)['reconstruction_rgba_difference'],0)
        bundle['palette_rgb']=[[255,0,0]]
        with self.assertRaises(p.Blocked):p.validate_bundle(self.root,bundle)
    def test_cache_reuses_exact_dependencies_and_invalidates_change(self):
        cache=RegressionCache(self.root/'cache.json');state={'input':'a'};calls=[]
        def check():calls.append(1);return {'status':'PASS'}
        self.assertEqual(cache.run('fixture',lambda:state.copy(),check)['execution'],'EXECUTED')
        self.assertEqual(cache.run('fixture',lambda:state.copy(),check)['execution'],'REUSED_EXACT_DEPENDENCIES')
        state['input']='b';cache.run('fixture',lambda:state.copy(),check);self.assertEqual(len(calls),2)
    def test_cache_failure_is_never_reused_as_pass(self):
        cache=RegressionCache(self.root/'cache.json')
        self.assertEqual(cache.run('fixture',lambda:{},lambda:{'status':'SKIP'})['status'],'FAIL')
        self.assertEqual(cache.run('fixture',lambda:{},lambda:{'status':'PASS'})['execution'],'EXECUTED')

if __name__=='__main__':unittest.main(verbosity=2)
