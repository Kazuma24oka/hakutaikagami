"""Shared sprite texture ownership checks. Semantic mask review remains a separate release gate."""
import hashlib
import json
from pathlib import Path
import numpy as np


def component_sizes(mask):
    pending=mask.copy();sizes=[];height,width=pending.shape
    for y,x in np.argwhere(pending):
        if not pending[y,x]:continue
        stack=[(int(y),int(x))];pending[y,x]=False;size=0
        while stack:
            yy,xx=stack.pop();size+=1
            for dy in [-1,0,1]:
                for dx in [-1,0,1]:
                    ny,nx=yy+dy,xx+dx
                    if 0<=ny<height and 0<=nx<width and pending[ny,nx]:pending[ny,nx]=False;stack.append((ny,nx))
        sizes.append(size)
    return sorted(sizes,reverse=True)


def validate_source(source, ids):
    a=np.asarray(source)
    if a.ndim!=3 or a.shape[2]!=4 or ids.shape!=a.shape[:2]:raise ValueError('RGBA/ID dimensions differ')
    if not np.issubdtype(ids.dtype,np.integer):raise ValueError('IDs must be integer labels')
    if not set(np.unique(a[:,:,3])).issubset({0,255}):raise ValueError('Native alpha must be binary')
    if not np.array_equal(ids>0,a[:,:,3]>0):raise ValueError('Ownership coverage differs from source')
    if np.any(ids<0):raise ValueError('Negative material ID')


def permitted_pixels(source,ids,owned_ids,joints=()):
    validate_source(source,ids)
    owned=np.isin(ids,list(owned_ids));shared=np.zeros_like(owned)
    yy,xx=np.indices(ids.shape)
    for joint in joints:
        radius=joint['radius']
        if not 0<radius<=joint.get('approved_radius_limit',3):raise ValueError('Unbounded joint overlap')
        x,y=joint['center'];zone=(xx-x)**2+(yy-y)**2<=radius**2
        shared|=zone&np.isin(ids,joint['source_ids'])&~owned
    return owned,shared


def audit_texture(texture,source,ids,owned_ids,joints=(),construction_mask=None,max_components=1):
    a=np.asarray(source);t=np.asarray(texture)
    owned,shared=permitted_pixels(a,ids,owned_ids,joints)
    hidden=np.zeros_like(owned)if construction_mask is None else np.asarray(construction_mask,dtype=bool)
    if hidden.shape!=ids.shape:raise ValueError('Construction mask size differs')
    if np.any(hidden&(owned|shared)):raise ValueError('Hidden construction overlaps observed ownership')
    live=t[:,:,3]>0
    if np.any(live&~(owned|shared|hidden)):raise ValueError('Foreign material copied into texture')
    observed=live&~hidden
    if not np.array_equal(t[observed],a[observed]):raise ValueError('Observed source pixels altered')
    if not set(np.unique(t[:,:,3])).issubset({0,255}):raise ValueError('Texture alpha changed')
    pieces=component_sizes(live)
    if len(pieces)>max_components:raise ValueError('Isolated texture component; review mask instead of auto deleting')
    return {'owned_pixels':int((live&owned).sum()),'shared_joint_pixels':int((live&shared).sum()),
            'constructed_pixels':int((live&hidden).sum()),'undeclared_foreign_pixels':0,'components':pieces}


def build_texture(source,ids,owned_ids,joints=(),construction_mask=None):
    """Copy only owners/declared joint pixels; complete hidden pixels from owned samples only."""
    a=np.asarray(source);owned,shared=permitted_pixels(a,ids,owned_ids,joints)
    hidden=np.zeros_like(owned)if construction_mask is None else np.asarray(construction_mask,dtype=bool)
    if np.any(hidden&(owned|shared)):raise ValueError('Hidden construction overlaps observed ownership')
    out=np.zeros_like(a);out[owned|shared]=a[owned|shared]
    samples=np.argwhere(owned)
    if hidden.any()and not len(samples):raise ValueError('No owned samples for hidden completion')
    if hidden.any():
        # Neutral unobserved fill: sample an actual owned median colour, not a foreign edge/outline.
        rgb=a[owned,:3].astype(float);target=np.median(rgb,axis=0)
        sample=np.argmin(((rgb-target)**2).sum(axis=1));sy,sx=samples[sample]
        out[hidden]=a[sy,sx]
    return out,audit_texture(out,a,ids,owned_ids,joints,hidden)


def contract_fingerprint(source,ids,specification):
    validate_source(source,ids)
    h=hashlib.sha256();h.update(hashlib.sha256(Path(__file__).read_bytes()).digest())
    h.update(np.asarray(source).tobytes());h.update(np.asarray(ids,dtype='<i4').tobytes())
    h.update(json.dumps(specification,sort_keys=True,separators=(',',':')).encode('utf-8'))
    return h.hexdigest()


def require_release(fingerprint,approval):
    """No production promotion from technical reconstruction or stale visual approval alone."""
    if approval.get('contract_sha256')!=fingerprint:raise ValueError('Missing/stale source-mask-contract approval')
    for key in ['source_contours','isolated_parts','joint_and_hidden_regions','normal_slow_motion']:
        if approval.get(key)!='PASS':raise ValueError('Release review incomplete: '+key)
        evidence=approval.get('evidence',{}).get(key,{})
        path=Path(evidence.get('path',''))
        if not path.is_file() or hashlib.sha256(path.read_bytes()).hexdigest()!=evidence.get('sha256'):
            raise ValueError('Missing/stale review evidence: '+key)
    return True


def require_semantic_review(source,ids,reviews,direction):
    """Block walk exports until this exact source and full semantic mask were reviewed.

    This verifies review provenance, not anatomical correctness. The independent
    contour/removal/probe inspections must happen before the review is recorded.
    A mask generated by the current extractor cannot approve itself at export.
    """
    validate_source(source,ids)
    review=reviews.get(direction,{})
    if review.get('status')!='TECHNICAL_REVIEWED':
        raise ValueError('Direction semantic review missing: '+direction)
    source_sha=hashlib.sha256(np.asarray(source).tobytes()).hexdigest()
    ids_sha=hashlib.sha256(np.asarray(ids,dtype=np.uint8).tobytes()).hexdigest()
    if review.get('source_RGBA_sha256')!=source_sha or review.get('material_ID_sha256')!=ids_sha:
        raise ValueError('Source or semantic mask changed after review: '+direction)
    if review.get('requires_user_acceptance'):
        acceptance=review.get('user_acceptance',{})
        if acceptance.get('status')!='USER_ACCEPTED':raise ValueError('Mask user review pending: '+direction)
        if acceptance.get('source_RGBA_sha256')!=source_sha or acceptance.get('material_ID_sha256')!=ids_sha:
            raise ValueError('Mask user acceptance stale: '+direction)
    for key in ['source_contours','arm_removed_body','isolated_arm_hand','displacement_probe']:
        if review.get('checks',{}).get(key)!='PASS':
            raise ValueError('Semantic inspection incomplete: '+key)
        evidence=review.get('evidence',{}).get(key,{})
        path=Path(evidence.get('path',''))
        if not path.is_file()or hashlib.sha256(path.read_bytes()).hexdigest()!=evidence.get('sha256'):
            raise ValueError('Semantic inspection evidence changed: '+key)
    return True
