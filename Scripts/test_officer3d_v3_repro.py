from pathlib import Path
import sys,subprocess,json,uuid
import officer3d_v3_pipeline as gate
R=Path(sys.argv[1]);binding=gate.require(R,'export');out=R/'.art-test-tmp'/('officer3d-v3-repro-'+uuid.uuid4().hex)
assert out.resolve().is_relative_to((R/'.art-test-tmp').resolve())
log=subprocess.run([r'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe','--background','--disable-autoexec','--python',str(R/'Scripts/build_officer3d_v3.py'),'--',str(R),'--repro-out',str(out)],stdout=subprocess.PIPE,stderr=subprocess.STDOUT,encoding='utf-8',errors='replace')
(out/'producer.log').write_text(log.stdout,encoding='utf-8');assert log.returncode==0 and 'V3_EXPORT_COMPLETE' in log.stdout
result={'binding':binding,'same_environment_repeat':True,'producer_sha256':gate.sha(R/'Scripts/build_officer3d_v3.py'),'comparison':{}}
for level in ['near','mid','far']:
 a=gate.sha(R/f'Game/Assets/Officer3D/TrialV3/{level}.glb');b=gate.sha(out/(level+'.glb'));assert a==b,(level,a,b);result['comparison'][level]={'original':a,'repeat':b,'byte_exact':True}
(R/'Art/Officer3D/TrialV3/reproducibility.json').write_bytes((json.dumps(result,indent=2)+'\n').encode());print('V3_BYTE_EXACT_REPEAT_PASS')
