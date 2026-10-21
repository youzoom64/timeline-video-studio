"""Verify relative media and CPU MP4 output in an isolated built editor."""
import argparse,json,math,os,shutil,struct,subprocess,time,urllib.request,wave
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--exe',type=Path,default=ROOT/'Build/TimelineVideoStudio.exe')
parser.add_argument('--tools-dir',type=Path,required=True)
parser.add_argument('--port',type=int,default=18765)
args=parser.parse_args()
folder=ROOT/'Logs/portable-runtime';folder.mkdir(parents=True,exist_ok=True)
original=folder/'original';moved=folder/'moved';original.mkdir(exist_ok=True)
sample=original/'tone.wav'
with wave.open(str(sample),'wb') as wav:
    wav.setparams((1,2,44100,0,'NONE','not compressed'))
    wav.writeframes(b''.join(struct.pack('<h',round(2500*math.sin(2*math.pi*440*i/44100))) for i in range(22050)))
env=os.environ.copy();env['TIMELINE_STUDIO_API_PORT']=str(args.port);env['TIMELINE_STUDIO_TOOLS_DIR']=str(args.tools_dir.resolve())
env['PATH']=''
for key in ('TIMELINE_STUDIO_FFMPEG','TIMELINE_STUDIO_FFPROBE'):env.pop(key,None)
print('Launch isolated editor with empty PATH and managed tools',flush=True)
proc=subprocess.Popen([str(args.exe.resolve()),'-batchmode','-logFile',str(folder/'player.log')],env=env,creationflags=subprocess.CREATE_NO_WINDOW)
base=f'http://127.0.0.1:{args.port}'
def call(data=None):
    req=urllib.request.Request(base+('/command' if data else '/state'),data=json.dumps(data).encode() if data else None,headers={'Content-Type':'application/json'})
    with urllib.request.urlopen(req,timeout=60) as r:out=json.load(r)
    if data:assert out['ok'],out.get('error')
    return out
try:
    for _ in range(100):
        if proc.poll() is not None:raise RuntimeError('Test editor exited before API startup')
        try:call();break
        except OSError:time.sleep(.5)
    else:raise RuntimeError('Test API startup timeout')
    call(dict(op='project.save',path=str(original/'project.json')))
    call(dict(op='clip.add',kind='Cube',start=0,layer=0))
    added=call(dict(op='clip.add',kind='WAV',path='tone.wav',start=0,layer=0))
    call(dict(op='project.save',path=str(original/'project.json')))
    saved=json.loads((original/'project.json').read_text(encoding='utf-8-sig'))
    assert next(c for c in saved['clips'] if c['kind']=='WAV')['path']=='tone.wav'
    shutil.copytree(original,moved,dirs_exist_ok=True)
    call(dict(op='project.load',path=str(moved/'project.json')))
    loaded=call()['project'];assert next(c for c in loaded['clips'] if c['kind']=='WAV')['path']==str(moved/'tone.wav')
    print('Moved project and relative WAV loaded; export CPU MP4',flush=True)
    output=moved/'result.mp4'
    call(dict(op='export.mp4',path=str(output),gpu=False))
    for _ in range(180):
        state=call();status=state['status']
        if status.startswith('Exported:'):break
        assert 'failed' not in status.lower(),status
        time.sleep(.25)
    else:raise RuntimeError('Export timeout: '+status)
    command=[str(args.tools_dir.resolve()/'ffprobe.exe'),'-v','error','-show_entries','stream=codec_type','-show_entries','format=duration','-of','json',str(output)]
    probe=json.loads(subprocess.check_output(command,env=env,creationflags=subprocess.CREATE_NO_WINDOW))
    assert {s['codec_type'] for s in probe['streams']}=={'video','audio'}
    assert float(probe['format']['duration'])>=4
    report=dict(relativeSave=True,movedProjectLoaded=True,emptyPath=True,managedToolsResolved=True,cpuMp4VideoAndAudio=True,output=str(output),probe=probe)
    (folder/'report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(report,ensure_ascii=False),flush=True)
finally:
    proc.terminate();proc.wait(timeout=15)
