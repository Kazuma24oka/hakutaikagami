"""Shared contract checks. No generation backend, visual approval or implicit fitting.

An art tool must call require_plan BEFORE writing. This cannot control arbitrary
external scripts or image-generation calls; AGENTS.md covers those entry points.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import numpy as np
from PIL import Image
from art_shared.part_ownership import validate_source, audit_texture
from art_shared.regression_runner import RegressionCache, environment

STAGES = {'front_authoring', 'parts', 'directions', 'motion', 'release'}
REVIEWS = {'dimensions', 'face_identity', 'color_texture', 'alpha_edges',
           'part_ownership', 'occlusion', 'gait_contact', 'cross_direction'}
DIMENSIONS = {'scalp_top', 'chin', 'neck', 'shoulder_right', 'shoulder_left',
              'pelvis', 'elbow_right', 'elbow_left', 'wrist_right', 'wrist_left',
              'knee_right', 'knee_left', 'ankle_right', 'ankle_left',
              'sole_right', 'sole_left'}

class Blocked(ValueError):
    pass

def digest(data):
    return hashlib.sha256(json.dumps(data, sort_keys=True, ensure_ascii=False,
                                     allow_nan=False).encode('utf-8')).hexdigest()

def sha(path):
    h=hashlib.sha256()
    with Path(path).open('rb') as stream:
        for block in iter(lambda:stream.read(1024*1024),b''):h.update(block)
    return h.hexdigest()

def model_evidence(root,entry):
    path=Path(entry['path'])
    if not path.is_absolute():return evidence(root,entry)
    registry=json.loads((Path(root)/'Art/Officer/tool-audit.json').read_text(encoding='utf-8'))['model']
    if path.resolve()!=Path(registry['path']).resolve() or entry['sha256']!=registry['sha256']:
        raise Blocked('External model is not the explicitly audited local model')
    if not path.is_file() or sha(path)!=entry['sha256']:
        raise Blocked('Audited local model changed')
    return path

def evidence(root, entry):
    root = Path(root).resolve()
    path = (root / entry['path']).resolve()
    if not path.is_relative_to(root) or not path.is_file():
        raise Blocked('Evidence missing or outside repository: ' + str(path))
    if sha(path) != entry['sha256']:
        raise Blocked('Evidence changed: ' + entry['path'])
    return path

def validate_contract(contract, profile):
    if contract['canvas'] != profile['canvas_pixels'] or contract['canvas'] != [64, 96]:
        raise Blocked('Adopted canvas changed')
    if contract['display_scale'] != profile['default_display_scale'] or contract['display_scale'] != 1:
        raise Blocked('Adopted display scale changed')
    expected = {'face_features': False, 'hair': False, 'beard': False,
                'clothing': False, 'footwear': False, 'genital_detail': False,
                'surface': 'skin-colored-featureless-pelvis', 'adult': True}
    if contract['base_body'] != expected:
        raise Blocked('Base must be a hairless, faceless unclothed adult construction surface')
    if contract['texture_filter'] != 'nearest' or contract['alpha'] != 'binary':
        raise Blocked('Native sampling/alpha contract changed')
    if contract['fit_policy'] != 'one-uniform-registration-no-part-fit':
        raise Blocked('Per-part/per-frame fitting is forbidden')
    if contract['portrait_policy'] != 'six-head-separate-art-shared-selection-ids':
        raise Blocked('Portrait linkage changed')

def require_control_plan(root, preparation):
    """Bootstrap only deterministic geometry/masks, before a generation plan exists.

    Does not authorize model execution, a finished candidate, or stage advancement.
    This solves the prerequisite cycle without dummy preexisting mask evidence.
    """
    root=Path(root)
    contract=json.loads((root/'Art/Officer/production-contract.json').read_text(encoding='utf-8'))
    profile=json.loads((root/'Game/Data/officer_visual_profile.json').read_text(encoding='utf-8'))
    validate_contract(contract,profile)
    work=json.loads((root/'Art/Officer/work-record.json').read_text(encoding='utf-8'))
    if work.get('control_preparation') != preparation or preparation.get('scope')!='deterministic-controls-only':
        raise Blocked('Missing persisted control-construction plan')
    if preparation['contract_sha256']!=digest(contract) or not preparation['feedback'] or not preparation['preserve']:
        raise Blocked('Stale/missing construction conditions')
    for e in preparation['tools']+preparation['references']:evidence(root,e)
    spec=json.loads(evidence(root,preparation['geometry']).read_text(encoding='utf-8'))
    if spec['canvas']!=[64,96] or set(spec['landmarks'])!=DIMENSIONS:
        raise Blocked('Incomplete native geometry')
    names=[r['name'] for r in spec['regions']];ids=[r['id'] for r in spec['regions']]
    if len(set(ids))!=len(ids) or set(spec['paint_order'])!=set(ids) or len(spec['paint_order'])!=len(ids):
        raise Blocked('Ambiguous control ownership/order')
    if not {'head','neck','chest','abdomen','pelvis'}.issubset(names):
        raise Blocked('Missing adult body construction regions')
    for r in spec['regions']:
        if any(type(x) is not int or type(y) is not int or not (0<=x<64 and 0<=y<96) for x,y in r['polygon']):
            raise Blocked('Construction outside native canvas')
    if set(spec['protected_surfaces'])!={'head','pelvis'}:
        raise Blocked('Blank head/featureless pelvis must be protected')
    for x,y in spec['landmarks'].values():
        if not (type(x) is int and type(y) is int and 0<=x<64 and 0<=y<96):
            raise Blocked('Invalid construction landmark')
    return digest(preparation)

def fingerprint(root, contract, plan):
    bindings = []
    for kind in ('inputs', 'tools', 'references'):
        for entry in plan[kind]:
            evidence(root, entry)
            bindings.append([kind, entry['path'], entry['sha256']])
    # Include the gate and the shared extraction code automatically.
    return digest({'contract': contract, 'plan': {k:v for k,v in plan.items() if k != 'results'},
                   'bindings': bindings, 'gate': sha(__file__),
                   'ownership_code': sha(Path(__file__).parent/'art_shared/part_ownership.py')})

def require_plan(root, stage, plan=None):
    """Refuse omissions, stale inputs, undeclared fallback and inherited approvals."""
    root = Path(root)
    contract = json.loads((root/'Art/Officer/production-contract.json').read_text(encoding='utf-8'))
    profile = json.loads((root/'Game/Data/officer_visual_profile.json').read_text(encoding='utf-8'))
    validate_contract(contract, profile)
    if stage not in STAGES:
        raise Blocked('Unknown production stage')
    if not plan:
        raise Blocked('No recorded change plan; rejected trial is quarantined')
    work=json.loads((root/'Art/Officer/work-record.json').read_text(encoding='utf-8'))
    if work.get('plans',{}).get(plan.get('id')) != plan:
        raise Blocked('Plan is not the exact persisted work-record entry')
    if plan['stage'] != stage or plan['contract_sha256'] != digest(contract):
        raise Blocked('Wrong stage or stale production contract')
    for name in ('feedback', 'prior_failures', 'change_scope', 'preserve'):
        if not plan[name]: raise Blocked('Missing repair record: ' + name)
    if plan['unresolved']:
        raise Blocked('Unresolved preconditions: ' + '; '.join(plan['unresolved']))
    if not all(plan[name] for name in ('inputs', 'tools', 'references')):
        raise Blocked('Missing bound input, processing code or reference')
    if set(plan['regressions']) != REVIEWS or any(not v for v in plan['regressions'].values()):
        raise Blocked('Full regression coverage required')
    if set(plan['retained_decisions']) != set(contract['decisions']):
        raise Blocked('Cumulative decisions omitted')
    if plan['retained_decisions'] != contract['decisions']:
        raise Blocked('Cumulative decision scope changed')
    backend = plan['backend']
    if backend['name'] != contract['generation_backend']:
        raise Blocked('Undeclared backend substitution')
    if backend['name'] == 'comfyui':
        for name in ('workflow', 'edit_mask', 'preserve_mask'):
            evidence(root, backend[name])
        model_evidence(root,backend['model'])
        for name in ('seed', 'steps', 'cfg', 'sampler', 'scheduler', 'denoise'):
            if backend.get(name) is None: raise Blocked('Missing backend setting: ' + name)
        if not isinstance(backend['seed'], int) or backend['seed'] < 0:
            raise Blocked('Invalid seed')
        workflow=json.loads(evidence(root,backend['workflow']).read_text(encoding='utf-8'))
        samplers=[node['inputs'] for node in workflow.values() if node.get('class_type')=='KSampler']
        checkpoints=[node['inputs']['ckpt_name'] for node in workflow.values() if node.get('class_type')=='CheckpointLoaderSimple']
        if len(samplers)!=1 or checkpoints != [Path(backend['model']['path']).name]:
            raise Blocked('Workflow sampler/model binding is unsupported or inconsistent')
        setting_names={'seed':'seed','steps':'steps','cfg':'cfg','sampler':'sampler_name','scheduler':'scheduler','denoise':'denoise'}
        if any(samplers[0].get(actual)!=backend[declared] for declared,actual in setting_names.items()):
            raise Blocked('Actual workflow settings differ from declared fixed settings')
    registration = plan['registration']
    sx, sy = registration['scale_xy']
    if not all(isinstance(v,(int,float)) and math.isfinite(v) for v in [sx,sy]) or sx <= 0 or sx != sy:
        raise Blocked('Only finite positive uniform scale is allowed')
    if registration.get('per_part_fit') is not False or registration.get('per_frame_fit') is not False:
        raise Blocked('Per-part or per-frame fit forbidden')
    if set(plan['landmarks']) != DIMENSIONS:
        raise Blocked('Missing independent body landmarks')
    for name, mark in plan['landmarks'].items():
        if mark['kind'] != 'authoring-target' or not mark['basis']:
            raise Blocked('Body targets must declare their construction basis')
        xy = mark['xy']
        if len(xy)!=2 or any(type(v) is not int for v in xy) or not (0<=xy[0]<64 and 0<=xy[1]<96):
            raise Blocked('Landmark missing/non-integer/outside canvas: '+name)
    if stage != 'front_authoring':
        require_review(root, contract, plan, 'front_review', {'base_body','face_on','hair_off','native_scale','white_black_checker'})
    if stage in {'directions','motion','release'}:
        require_review(root, contract, plan, 'parts_review', {'contours','isolated_parts','removed_parts','displacement','overlap_hidden'})
    if stage in {'motion','release'}:
        require_review(root, contract, plan, 'directions_review', {'all_requested_views','character_sides','occlusion','proportions'})
    if stage == 'release':
        require_review(root, contract, plan, 'motion_review', {'all_frames','normal_slow','contact','runtime'})
        bundle=evidence(root,plan['candidate_bundle'])
        validate_bundle(root,json.loads(bundle.read_text(encoding='utf-8')))
    return fingerprint(root, contract, plan)

def require_review(root, contract, plan, name, required):
    r = plan.get(name, {})
    # Review binds exact current contract/inputs/masks/tools, not a free PASS flag.
    expected = fingerprint(root, contract, {k:v for k,v in plan.items() if not k.endswith('_review')})
    if r.get('binding') != expected or r.get('status') != 'PASS':
        raise Blocked('Missing/stale scoped review: '+name)
    if set(r.get('checks',{})) != required:
        raise Blocked('Incomplete scoped review: '+name)
    for check in r['checks'].values():
        if check['status']!='PASS' or not check['reviewer'] or not check['observation'] or not check['evidence']:
            raise Blocked('Review requires actual observation and evidence')
        for item in check['evidence']: evidence(root,item)

def inspect_native(image, owners, *, canvas=(64,96), protected_face=None, hair_ids=()):
    a=np.asarray(image)
    if a.shape!=(canvas[1],canvas[0],4): raise Blocked('Native canvas mismatch')
    validate_source(a,owners)
    if protected_face is not None:
        protected_face=np.asarray(protected_face,dtype=bool)
        if protected_face.shape!=owners.shape: raise Blocked('Face protection size differs')
        if np.any(protected_face & np.isin(owners,hair_ids)):
            raise Blocked('Hair owns protected facial features')
    return {'technical_only':True,'visible_pixels':int((a[:,:,3]>0).sum())}

def validate_recolor(source, changed, material_mask):
    a,b=np.asarray(source),np.asarray(changed);m=np.asarray(material_mask,dtype=bool)
    if a.shape!=b.shape or m.shape!=a.shape[:2]: raise Blocked('Recolor coordinate mismatch')
    if not np.array_equal(a[:,:,3],b[:,:,3]) or not np.array_equal(a[~m],b[~m]):
        raise Blocked('Recolor changed alpha or protected pixels')

def validate_geometry(source, region_masks, criteria):
    """Measure actual visible regions, not just the cell or claimed landmark JSON.

    Semantic correctness of each region still needs its source-bound review.
    Bounds are exclusive at right/bottom; all criteria must have a recorded basis.
    """
    a=np.asarray(source)
    if set(region_masks)!=set(criteria) or not criteria:
        raise Blocked('Geometry criteria/regions omitted')
    results={}
    for name, mask in region_masks.items():
        m=np.asarray(mask,dtype=bool);c=criteria[name]
        if m.shape!=a.shape[:2] or not m.any() or np.any(m & (a[:,:,3]==0)):
            raise Blocked('Missing/outside-source geometry region: '+name)
        ys,xs=np.where(m);bounds=[int(xs.min()),int(ys.min()),int(xs.max())+1,int(ys.max())+1]
        expected=c['bounds'];tol=c['tolerance_px']
        if len(expected)!=4 or type(tol) is not int or tol<0 or not c['basis']:
            raise Blocked('Geometry target/tolerance has no valid basis')
        if any(abs(v-e)>tol for v,e in zip(bounds,expected)):
            raise Blocked('Actual geometry differs from fixed source: '+name)
        results[name]=bounds
    return results

def resolve_selection(selection, catalog):
    """One selection -> two separately authored representations. Missing never substitutes."""
    out={'small':{},'portrait':{}}
    for slot, part_id in selection.items():
        if part_id=='none': continue
        entry=catalog[slot][part_id]
        for representation in out:
            value=entry.get(representation)
            if not value: raise Blocked('Missing corresponding '+representation+' part: '+part_id)
            out[representation][slot]=value
    return out

def compose_parts(parts, order, enabled):
    im=Image.new('RGBA',(64,96))
    for key in order:
        if key not in enabled: continue
        layer=parts[key]
        if layer.size!=(64,96): raise Blocked('Part coordinates differ; implicit fitting forbidden')
        im.alpha_composite(layer)
    return im

def validate_bundle(root, bundle):
    """One asset-bound entrance for actual pixels, masks, dimensions and assembly.

    Authoring semantics/appearance still need the independent scoped reviews.
    All images stay on the same canvas; no hidden resampling or nearest fills.
    """
    def load(entry,mode='RGBA'):
        with Image.open(evidence(root,entry)) as im:return np.array(im.convert(mode))
    source=load(bundle['source']);ids=load(bundle['ownership'],'L')
    protected=load(bundle['protected_face'],'L')>0
    result=inspect_native(source,ids,protected_face=protected,hair_ids=bundle['hair_ids'])
    region_masks={k:load(v,'L')>0 for k,v in bundle['geometry_regions'].items()}
    result['geometry']=validate_geometry(source,region_masks,bundle['geometry_criteria'])
    colors={tuple(v) for v in bundle['palette_rgb']}
    if not colors or any(tuple(rgb) not in colors for rgb in source[source[:,:,3]>0,:3]):
        raise Blocked('Actual visible color outside fixed palette')
    definitions=bundle['parts'];order=bundle['order']
    if set(order)!=set(definitions) or len(order)!=len(definitions):
        raise Blocked('Part order missing/duplicate')
    textures={}
    for key,part in definitions.items():
        texture=load(part['image'])
        hidden=load(part['hidden'],'L')>0
        audit_texture(texture,source,ids,part['owner_ids'],part.get('joints',()),hidden,
                      max_components=part['max_components'])
        textures[key]=Image.fromarray(texture)
    assembled=np.array(compose_parts(textures,order,set(order)))
    if not np.array_equal(assembled,source):raise Blocked('Neutral assembly differs from reference')
    for case in bundle['recolor_cases']:
        validate_recolor(source,load(case['image']),load(case['mask'],'L')>0)
    result['reconstruction_rgba_difference']=0
    result['status']='TECHNICAL_ONLY_NOT_APPEARANCE_APPROVAL'
    return result

def cached_bundle(root, bundle, force=False):
    def snapshot():
        entries=[]
        def visit(value):
            if isinstance(value,dict):
                if 'path' in value and 'sha256' in value:
                    path=evidence(root,value);entries.append([value['path'],sha(path)])
                for item in value.values():visit(item)
            elif isinstance(value,list):
                for item in value:visit(item)
        visit(bundle)
        return {'bundle':bundle,'files':entries,'code':sha(__file__),
                'shared':sha(Path(__file__).parent/'art_shared/part_ownership.py'),
                'cache_code':sha(Path(__file__).parent/'art_shared/regression_runner.py'),
                'environment':environment(),'scope':'technical-only-no-visual-approval'}
    cache=RegressionCache(Path(root)/'.utmp/officer-art/technical-validation.json')
    return cache.run('candidate',snapshot,lambda:{'status':'PASS',
        'scope':'technical-only-no-visual-approval','checks':validate_bundle(root,bundle)},force=force)

def main():
    p=argparse.ArgumentParser();p.add_argument('--root',type=Path,default=Path(__file__).resolve().parents[1]);p.add_argument('--stage',choices=sorted(STAGES),default='front_authoring');p.add_argument('--plan',type=Path);p.add_argument('--check-candidate',type=Path);p.add_argument('--force',action='store_true')
    a=p.parse_args()
    try:
        if a.check_candidate:
            result=cached_bundle(a.root,json.loads(a.check_candidate.read_text(encoding='utf-8')),a.force)
            print(json.dumps(result,ensure_ascii=False));return 0 if result['status']=='PASS' else 2
        result=require_plan(a.root,a.stage,json.loads(a.plan.read_text(encoding='utf-8')) if a.plan else None)
        print('OFFICER_ART_PLAN_VALID '+result)
    except (ValueError,KeyError,TypeError,OSError) as e:
        print('OFFICER_ART_BLOCKED: '+str(e));return 2
    return 0

if __name__=='__main__':raise SystemExit(main())
