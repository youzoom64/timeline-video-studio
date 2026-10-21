"""Resolve SFX anchors from Timeline Video Studio JSON without opening images."""
from pathlib import Path
import argparse
import copy
import json
import logging
import math
import urllib.request

FIELDS=('x','y','z','scale','scaleX','scaleY','rotation')
EPS=1e-6
LOG=logging.getLogger('timeline_sfx_events')


def read(path):
    return json.loads(Path(path).read_text(encoding='utf-8-sig'))


def write(path,data):
    target=Path(path)
    target.parent.mkdir(parents=True,exist_ok=True)
    target.write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')


def values(data,prefix=''):
    return {f:float(data.get(prefix+f[0].upper()+f[1:] if prefix else f,
                             1 if f.startswith('scale') else 0)) for f in FIELDS}


def changes(a,b):
    result={}
    for field in FIELDS:
        delta=b[field]-a[field]
        if field=='rotation':
            delta=delta%360
            if delta>180:
                delta-=360
        if abs(delta)>EPS:
            result[field]=dict(before=a[field],after=b[field],delta=delta)
    return result


def segments(clip):
    """Same from/key/next/end boundaries as StudioTransform.Evaluate."""
    duration=float(clip['duration'])
    keys=sorted(clip.get('keyframes',[]),key=lambda k:k['time'])
    assert all(0<float(k['time'])<=duration for k in keys), ('Invalid keyframe time',clip['id'])
    assert len({k['time'] for k in keys})==len(keys), ('Duplicate keyframe time',clip['id'])
    result=[]
    a=0.
    previous=values(clip)
    for key in keys:
        b=float(key['time'])
        if b>=duration:
            continue
        endpoint=values(key)
        result.append(dict(start=a,end=b,changes=changes(previous,endpoint),kind='interpolation'))
        following=values(key,'next')
        jump=changes(endpoint,following)
        if jump:
            result.append(dict(start=b,end=b,changes=jump,kind='step'))
        a=b
        previous=following
    if a<duration:
        result.append(dict(start=a,end=duration,changes=changes(previous,values(clip,'end')),kind='interpolation'))
    return result


def runs(clip,parameters,direction='change'):
    """Join adjacent motion intervals, including inserted intermediate keyframes."""
    assert parameters and set(parameters)<=set(FIELDS)
    assert direction in ('change','increase','decrease')
    result=[]
    for segment in segments(clip):
        selected={p:v for p,v in segment['changes'].items() if p in parameters}
        if direction=='increase' and any(v['delta']<=0 for v in selected.values()):
            selected={}
        if direction=='decrease' and any(v['delta']>=0 for v in selected.values()):
            selected={}
        if not selected:
            continue
        if result and abs(result[-1]['end']-segment['start'])<EPS:
            result[-1]['end']=segment['end']
            result[-1]['parts'].append(dict(**segment,selected=selected))
        else:
            result.append(dict(start=segment['start'],end=segment['end'],
                               parts=[dict(**segment,selected=selected)]))
    return result


def anchor(clip,trigger):
    kind=trigger['kind']
    if kind=='clip_enter':
        local=0.
        evidence=dict(parameter='start',value=clip['start'])
    elif kind=='clip_exit':
        local=float(clip['duration'])
        evidence=dict(parameter='duration',value=local)
    elif kind in ('parameter_run_start','parameter_run_end'):
        options=runs(clip,trigger['parameters'],trigger.get('direction','change'))
        index=trigger.get('run',0)
        if not 0<=index<len(options):
            raise ValueError(f"Motion anchor missing: {clip['id']} {trigger}")
        selected=options[index]
        local=selected['start'] if kind.endswith('_start') else selected['end']
        evidence=selected
    else:
        raise ValueError(f'Unknown trigger {kind}')
    absolute=float(clip['start'])+local+float(trigger.get('offset',0))
    assert math.isfinite(absolute) and absolute>=0
    return absolute,evidence


def resolve(project,bindings):
    clips={c['id']:c for c in project['clips']}
    results=[]
    for binding in bindings['bindings']:
        target=clips[binding['targetId']]
        assert target['kind']!='WAV'
        start,evidence=anchor(target,binding['trigger'])
        sound=clips[binding['sfxClipId']]
        assert sound['kind']=='WAV' and sound['audioTrack'] in (0,1)
        assert sound['path']==binding['approvedPath'], ('Sound choice changed',sound['id'])
        for member_id in binding.get('simultaneousTargetIds',[]):
            member_time,_=anchor(clips[member_id],binding['trigger'])
            if abs(member_time-start)>.001:
                raise ValueError(f'Simultaneous targets now have different timing: {binding["eventId"]}')
        results.append(dict(eventId=binding['eventId'],targetId=target['id'],sfxClipId=sound['id'],
                            category=binding['category'],trigger=binding['trigger'],
                            evidence=evidence,start=start,currentStart=sound['start'],
                            needsUpdate=abs(start-sound['start'])>.0002))
    assert len({r['sfxClipId'] for r in results})==len(results)
    expected={c['id'] for c in project['clips'] if c['kind']=='WAV' and c['audioTrack'] in (0,1)}
    assert expected=={r['sfxClipId'] for r in results}, 'Unbound SFX exist.'
    return results


def request(api,command=None):
    url=api.rstrip('/')+('/command' if command else '/state')
    body=json.dumps(command).encode() if command else None
    req=urllib.request.Request(url,data=body,headers={'Content-Type':'application/json'})
    with urllib.request.urlopen(req,timeout=60) as response:
        result=json.load(response)
    assert result.get('ok'),result
    return result


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('mode',choices=['scan','sync'])
    parser.add_argument('--project',required=True)
    parser.add_argument('--bindings')
    parser.add_argument('--out',required=True)
    parser.add_argument('--api')
    parser.add_argument('--apply',action='store_true')
    args=parser.parse_args()
    logging.basicConfig(level=logging.INFO,format='%(levelname)s %(message)s')
    LOG.info('%s %s',args.mode,args.project)
    live=request(args.api) if args.api else None
    if live:
        assert Path(live['projectPath']).resolve()==Path(args.project).resolve()
    project=live['project'] if live else read(args.project)
    if not live:
        for clip in project['clips']:
            if clip.get('path') and not Path(clip['path']).is_absolute():
                clip['path']=str((Path(args.project).resolve().parent/clip['path']).resolve())
    if args.mode=='scan':
        assert not args.apply
        result=[]
        for c in project['clips']:
            if c['kind']=='WAV':
                continue
            result.append(dict(targetId=c['id'],start=c['start'],duration=c['duration'],
                               entry=dict(parameter='start',value=c['start']),
                               changes=[s for s in segments(c) if s['changes']],
                               effects=c.get('effects',[]),groupId=c.get('groupId','')))
        write(args.out,dict(objects=result,groups=project.get('groups',[])))
        LOG.info('Scanned %d objects; changes are candidates, semantic roles come from bindings.',len(result))
        return
    if not args.bindings:
        parser.error('--bindings is required for sync')
    bindings=read(args.bindings)
    for binding in bindings['bindings']:
        if not Path(binding['approvedPath']).is_absolute():
            binding['approvedPath']=str((Path(args.bindings).resolve().parent/binding['approvedPath']).resolve())
    result=resolve(project,bindings)
    patches=[r for r in result if r['needsUpdate']]
    # Validate independent SFX lanes before applying a timing change.
    planned=copy.deepcopy(project)
    by_id={c['id']:c for c in planned['clips']}
    for item in result:
        by_id[item['sfxClipId']]['start']=item['start']
    for track in (0,1):
        lane=sorted((c for c in planned['clips'] if c['kind']=='WAV' and c['audioTrack']==track),key=lambda c:c['start'])
        assert all(a['start']+a['duration']<=b['start']+.001 for a,b in zip(lane,lane[1:])), 'Review overlapping events before syncing.'
    report=dict(project=args.project,source='JSON parameters and explicit event bindings',
                total=len(result),updates=len(patches),events=result,applied=False)
    write(args.out,report)
    if args.apply:
        assert live is not None, '--api is required for live apply'
        if patches:
            assert not live['playing'], 'Pause playback before timing edits.'
            backup=Path(args.out).with_suffix('.before.json')
            assert not backup.exists(), 'Choose a new output report to retain the previous backup.'
            write(backup,live)
            assert request(args.api)['project']==project, 'Project changed during calculation.'
            for item in patches:
                request(args.api,dict(op='clip.set',id=item['sfxClipId'],field='start',value=str(item['start'])))
            after=request(args.api)['project']
            allowed={r['sfxClipId'] for r in patches}
            for a,b in zip(project['clips'],after['clips']):
                assert a['id']==b['id']
                assert {k:v for k,v in a.items() if k!='start' or a['id'] not in allowed}=={k:v for k,v in b.items() if k!='start' or b['id'] not in allowed}
            assert all(abs(next(c['start'] for c in after['clips'] if c['id']==r['sfxClipId'])-r['start'])<.0002 for r in result)
            request(args.api,dict(op='project.save',path=args.project))
        report['applied']=True
        report['editorCommands']=len(patches)+(1 if patches else 0)
        write(args.out,report)
    LOG.info('Resolved %d bindings, %d timing changes, apply=%s',len(result),len(patches),args.apply)


if __name__=='__main__':
    main()
