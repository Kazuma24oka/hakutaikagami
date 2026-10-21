from pathlib import Path
from verify_camera_pan import source_files as measured_sources
import subprocess,uuid,sys,json,hashlib
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'Docs/Design/TerrainPerformanceV1/runtime-local'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 label=sys.argv[1];input_paths=sorted(set(measured_sources()+[Path(__file__)]));input_hashes={p.relative_to(ROOT).as_posix():sha(p) for p in input_paths};OUT.mkdir(parents=True,exist_ok=True);exe=ROOT/'.tools/godot-4.5.2/Godot_v4.5.2-stable_mono_win64/Godot_v4.5.2-stable_mono_win64_console.exe'
 result=subprocess.run([str(exe),'--path',str(ROOT/'Game'),'--resolution','1100x720','res://Scenes/ContinuousWorldTrial.tscn','--','--continuous-check','--terrain-perf','--case=coverage','--save-key='+uuid.uuid4().hex,'--perf-label='+label],cwd=ROOT,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,creationflags=subprocess.CREATE_NO_WINDOW,timeout=480)
 log=result.stdout.decode('utf8',errors='replace');(OUT/(label+'.log')).write_bytes(log.encode());assert result.returncode==0 and 'TERRAIN_PERF_PASSED' in log and 'ERROR:' not in log,log
 assert input_hashes=={p.relative_to(ROOT).as_posix():sha(p) for p in input_paths},'measurement inputs changed';j=json.loads((OUT/(label+'.json')).read_bytes());assert len(j['Rows'])==216 and len({r['Node'] for r in j['Rows']})==108 and all(r['Triangles']==2048 for r in j['Rows'])
 times=sorted(r['RefreshMs'] for r in j['Rows']);summary={'sources':input_hashes,'samples':216,'cpu_refresh_ms_p50':times[108],'cpu_refresh_ms_p95':times[205],'cpu_refresh_ms_max':times[-1],'working_bytes_max':max(r['WorkingBytes'] for r in j['Rows']),'godot_video_bytes_max':max(r['VideoBytes'] for r in j['Rows']),'warm_refresh_ms_p95':sorted(r['WarmRefreshMs'] for r in j['Rows'])[205],'measurement':'CPU refresh not GPU frametime or FPS','source_sha256':sha(ROOT/'Game/Scripts/MidTerrainRenderer.cs'),'game_dll_sha256':sha(ROOT/'Game/.godot/mono/temp/bin/Debug/HakutaiKagami.Game.dll')}
 (OUT/(label+'-summary.json')).write_bytes((json.dumps(summary,indent=2)+'\n').encode());print(json.dumps({k:v for k,v in summary.items() if k!='sources'}),flush=True)
if __name__=='__main__':main()
