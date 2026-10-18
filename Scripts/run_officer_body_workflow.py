"""Execute only the persisted, hash-checked ComfyUI body material workflow."""
from pathlib import Path
import argparse,json,sys,shutil,urllib.request

def request(base,route,payload=None):
    data=None if payload is None else json.dumps(payload).encode('utf-8')
    req=urllib.request.Request(base+route,data=data,headers={'Content-Type':'application/json'})
    with urllib.request.urlopen(req,timeout=30) as response:return json.load(response)

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--root',type=Path,required=True);ap.add_argument('--submit',action='store_true');ap.add_argument('--collect',action='store_true');a=ap.parse_args()
    r=a.root.resolve();sys.path.insert(0,str(r/'Scripts'));import officer_art_pipeline as gate
    f=r/'Art/Officer/BodyBaseV1';w=json.loads((r/'Art/Officer/work-record.json').read_text(encoding='utf-8'))
    plan=w['plans']['body-front-v1'];binding=gate.require_plan(r,'front_authoring',plan)
    base='http://127.0.0.1:8189';service=r/'.utmp/officer-body-server'
    if a.submit:
        for source,target in [('comfy-input.png','body-front-v1-input.png'),('comfy-edit-mask.png','body-front-v1-edit.png')]:
            shutil.copyfile(f/'controls'/source,service/'input'/target)
            if gate.sha(f/'controls'/source)!=gate.sha(service/'input'/target):raise ValueError('Upload/input mismatch')
        stats=request(base,'/system_stats');(f/'runtime-environment.json').write_text(json.dumps(stats,indent=2)+'\n',encoding='utf-8')
        workflow=json.loads((f/'workflow-api.json').read_text(encoding='utf-8'))
        response=request(base,'/prompt',{'prompt':workflow,'client_id':'hakutai-body-base-v1'})
        response['validated_plan_binding']=binding
        (f/'submission.json').write_text(json.dumps(response,indent=2)+'\n',encoding='utf-8');print(json.dumps(response))
    elif a.collect:
        submission=json.loads((f/'submission.json').read_text());job=submission['prompt_id']
        history=request(base,'/history/'+job)
        if job not in history:print('GENERATION_PENDING');return
        entry=history[job];(f/'generation-history.json').write_text(json.dumps(entry,indent=2)+'\n',encoding='utf-8')
        if not entry['status']['completed'] or entry['status']['status_str']!='success':raise ValueError('Generation failed: '+str(entry['status']))
        image=entry['outputs']['8']['images'][0]
        raw=service/'output'/image['subfolder']/image['filename']
        if not raw.resolve().is_relative_to((service/'output').resolve()):raise ValueError('Unexpected output path')
        # Keep full original local; project owns only the inspected 64x96 production assets.
        manifest={'path':str(raw),'sha256':gate.sha(raw),'plan_binding':binding}
        (f/'generated-material.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8');print(json.dumps(manifest))
    else:raise ValueError('Choose --submit or --collect')

if __name__=='__main__':main()
