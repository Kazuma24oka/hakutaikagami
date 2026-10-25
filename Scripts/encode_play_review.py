from pathlib import Path
import json,hashlib,subprocess,re,sys
ROOT=Path('C:/Workspace/Projects/Games/hakutaikagami')
FFMPEG=ROOT/'.tools/video-tools/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def encode(root=ROOT):
 folder=root/'Docs/Design/PlayReviewV1/runtime-local/playreview-personal';manifest=folder/'film-manifest.json';j=json.loads(manifest.read_bytes());assert j['NativeMovie'] and j['FixedRecordingFps']==30
 key=Path(j['Directory']).name;assert re.fullmatch('[0-9a-f]{32}',key)
 source=folder/('native-'+key+'.avi');dest=folder/('native-'+key+'.mp4');assert source.is_file() and not source.is_symlink()
 q=subprocess.run([str(FFMPEG),'-y','-hide_banner','-i',str(source),'-an','-c:v','libx264','-preset','medium','-crf','20','-pix_fmt','yuv420p','-movflags','+faststart',str(dest)],stdout=subprocess.PIPE,stderr=subprocess.STDOUT,creationflags=subprocess.CREATE_NO_WINDOW,timeout=180)
 (folder/'encode.log').write_bytes(q.stdout);assert q.returncode==0 and dest.stat().st_size>50000,q.stdout[-4000:]
 q=subprocess.run([str(FFMPEG),'-hide_banner','-i',str(dest),'-map','0:v:0','-an','-f','null','NUL'],stdout=subprocess.PIPE,stderr=subprocess.STDOUT,creationflags=subprocess.CREATE_NO_WINDOW,timeout=180)
 (folder/'decode.log').write_bytes(q.stdout);assert q.returncode==0,q.stdout[-4000:]
 text=q.stdout.decode(errors='replace');frames=re.findall(r'frame=\s*(\d+)',text);assert frames and int(frames[-1])>100 and '1280x900' in text and '30 fps' in text
 result=dict(source=source.relative_to(root).as_posix(),source_sha256=sha(source),video=dest.relative_to(root).as_posix(),video_sha256=sha(dest),native_manifest_sha256=sha(manifest),decoded_frames=int(frames[-1]),fixed_recording_fps=30,encoder_sha256=sha(FFMPEG),scope='Godot built-in fixed30fps recording;real-time synchronization disabled. Re-encoded without interpolation or time-scale edits. Not wall-clock performance evidence.')
 (folder/'encoded-video.json').write_bytes(json.dumps(result,ensure_ascii=False,indent=2).encode());print('ENCODED',dest,'frames',result['decoded_frames'],flush=True);return result
if __name__=='__main__':encode(Path(sys.argv[1]) if len(sys.argv)>1 else ROOT)
