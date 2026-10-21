"""Create a standalone Windows ZIP from a tested Unity player build."""
import argparse,hashlib,json,re,struct,zipfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--build-dir',type=Path,default=ROOT/'Build')
parser.add_argument('--version',default='0.1.0')
args=parser.parse_args();build=args.build_dir.resolve()
for required in ('TimelineVideoStudio.exe','UnityPlayer.dll','TimelineVideoStudio_Data','MonoBleedingEdge'):
    if not (build/required).exists():raise SystemExit(f'Missing runtime component: {required}')
out=ROOT/'Releases';out.mkdir(exist_ok=True)
archive=out/f'TimelineVideoStudio-{args.version}-Windows-x64.zip'
files={}
for path in build.rglob('*'):
    if path.is_file() and path.suffix.lower() not in ('.pdb','.mdb','.log') and not any('DoNotShip' in p or 'ButDontShip' in p for p in path.parts):
        files[path.relative_to(build).as_posix()]=path
for name in ('README.md','OPERATIONS.md','CONTROL_API.md','AGENTS.md'):
    files[name]=ROOT/name
for path in (ROOT/'.agents').rglob('*'):
    if path.is_file() and '__pycache__' not in path.parts:files[path.relative_to(ROOT).as_posix()]=path
files['scripts/studio_cli.py']=ROOT/'scripts/studio_cli.py'
start='''Timeline Video Studio — Windows x64

1. Extract the entire ZIP into one folder.
2. Run TimelineVideoStudio.exe. Unity Editor is not required.
3. Keep UnityPlayer.dll, MonoBleedingEdge, and TimelineVideoStudio_Data beside the EXE.

FFmpeg and FFprobe are installed automatically if missing. Internet access is
needed for the first tool installation. Progress appears in the app.
See README.md and OPERATIONS.md for editing, tools and API settings.
'''
def strip_debug_paths(content):
    """Remove the compiler's local PDB location without changing managed code."""
    data=bytearray(content)
    pe=struct.unpack_from('<I',data,0x3c)[0]
    assert data[pe:pe+4]==b'PE\0\0'
    sections=struct.unpack_from('<H',data,pe+6)[0]
    size=struct.unpack_from('<H',data,pe+20)[0]
    opt=pe+24;magic=struct.unpack_from('<H',data,opt)[0]
    directory=opt+(112 if magic==0x20b else 96)
    rva,count=struct.unpack_from('<II',data,directory+6*8)
    if not rva:return content
    raw=None
    for i in range(sections):
        at=opt+size+i*40
        virtual_size,address,raw_size,pointer=struct.unpack_from('<IIII',data,at+8)
        if address<=rva<address+max(virtual_size,raw_size):raw=pointer+rva-address;break
    assert raw is not None
    for at in range(raw,raw+count,28):
        kind,length,_,pointer=struct.unpack_from('<IIII',data,at+12)
        if kind!=2 or data[pointer:pointer+4]!=b'RSDS':continue
        start=pointer+24;space=length-24
        old=bytes(data[start:start+space]).split(b'\0',1)[0]
        name=old.replace(b'\\',b'/').split(b'/')[-1]
        assert len(name)<space
        data[start:start+space]=name+b'\0'*(space-len(name))
    return bytes(data)
with zipfile.ZipFile(archive,'w',compression=zipfile.ZIP_DEFLATED,compresslevel=6) as z:
    for name,path in sorted(files.items()):
        if name=='TimelineVideoStudio_Data/Managed/Assembly-CSharp.dll':z.writestr(name,strip_debug_paths(path.read_bytes()))
        else:z.write(path,name)
    z.writestr('START-HERE.txt',start)
with zipfile.ZipFile(archive) as z:
    assert z.testzip() is None
digest=hashlib.sha256(archive.read_bytes()).hexdigest()
(out/(archive.name+'.sha256')).write_text(digest+'  '+archive.name+'\n',encoding='ascii')
report=dict(archive=str(archive),bytes=archive.stat().st_size,files=len(files)+1,sha256=digest,zipIntegrity=True)
(out/'package_report.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps(report),flush=True)
