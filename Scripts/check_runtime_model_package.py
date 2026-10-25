"""Read-only preflight for the provisional model package. Never decides distribution rights."""
from pathlib import Path
import argparse,hashlib,json,struct,sys,re

def inspect(root):
    game=root/'Game';paths=[game/'Assets/Officer3D/TrialV3'/f'{level}.glb' for level in ['near','mid','far']]+[game/'Assets/Officer3D/TrialV1'/f for f in ['normal.png','orm.png']]+[game/'Assets/Officer3D/TrialV2/materials.png',game/'Shaders/Officer3DV3.gdshader']
    missing=[p.relative_to(root).as_posix() for p in paths if not p.is_file()]
    if missing:return dict(status='MISSING_LOCAL_ASSETS',missing=missing,scope='Source-only Git checkout may compile; runtime model package is separate. Do not copy or publish unverified external assets automatically.')
    catalog=root/'Core/Appearance/OfficerAppearance.cs';actor=game/'Scripts/Officer3DCustomActor.cs';paths += [catalog,actor]
    declaration=re.search(r'string\[\] Slots=\{([^}]+)\}',catalog.read_text(encoding='utf8'));assert declaration,'Appearance slot contract missing'
    required_slots=re.findall(r'"([^"]+)"',declaration.group(1))
    models={};files={p.relative_to(root).as_posix():dict(sha256=hashlib.sha256(p.read_bytes()).hexdigest(),bytes=p.stat().st_size) for p in paths}
    for level in ['near','mid','far']:
        p=game/'Assets/Officer3D/TrialV3'/f'{level}.glb';data=p.read_bytes();magic,version,length=struct.unpack_from('<4sII',data);assert magic==b'glTF' and version==2 and length==len(data),f'Invalid GLB: {p.name}'
        n,kind=struct.unpack_from('<II',data,12);assert kind==0x4E4F534A,'JSON chunk missing';g=json.loads(data[20:20+n]);nodes=g['nodes'];meshes={x['name']:len(g['meshes'][x['mesh']]['primitives']) for x in nodes if 'mesh' in x}
        assert all('slot_'+slot in meshes for slot in required_slots),'Appearance slot missing from model'
        for item in g.get('buffers',[])+g.get('images',[]):
            uri=item.get('uri')
            if not uri or uri.startswith('data:'):continue
            from urllib.parse import unquote
            dependency=(p.parent/unquote(uri)).resolve();assert dependency.is_relative_to(game.resolve()) and dependency.is_file(),'External GLB dependency missing or outside Game'
            files[dependency.relative_to(root).as_posix()]=dict(sha256=hashlib.sha256(dependency.read_bytes()).hexdigest(),bytes=dependency.stat().st_size)
        materials={g['materials'][q['material']]['name'] for m in g['meshes'] for q in m['primitives'] if 'material' in q};assert 'skin_face' in materials,'skin_face material missing';assert all(s.startswith(('skin_','gear_','feature_')) for s in materials),'Unbound runtime material'
        assert len(g['skins'])==1,'Exactly one shared skeleton expected';bones=[nodes[i]['name'] for i in g['skins'][0]['joints']];assert len(bones)==len(set(bones)) and any(s.endswith('Foot') for s in bones),'Named feet/bones missing'
        clips=[a['name'].split('/')[-1] for a in g['animations']];assert {'idle','walk','attack'}<=set(clips),'Required clips missing'
        models[level]=dict(meshes=meshes,bones=bones,clips=clips,materials=sorted(materials))
    for level in ['mid','far']:
        assert models[level]['meshes']==models['near']['meshes'],f'{level}: slot/surface mapping differs'
        assert models[level]['bones']==models['near']['bones'],f'{level}: skeleton order differs'
        assert models[level]['clips']==models['near']['clips'],f'{level}: clip IDs differ'
    return dict(status='PASSED',scope='Structural package check only; actual Godot color/equipment/motion review remains required.',rights='Not evaluated. Existing provisional assets remain local-only.',files=files,models=models)

def main():
    p=argparse.ArgumentParser();p.add_argument('--root',type=Path,default=Path(__file__).resolve().parents[1]);p.add_argument('--output',type=Path);a=p.parse_args()
    result=inspect(a.root.resolve());out=json.dumps(result,ensure_ascii=False,indent=2)+'\n'
    if a.output:a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(out,encoding='utf8',newline='\n')
    print(result['status']);return 0 if result['status']=='PASSED' else 2
if __name__=='__main__':sys.exit(main())
