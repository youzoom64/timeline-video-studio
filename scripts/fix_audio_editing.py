from pathlib import Path
R=Path(__file__).resolve().parents[1]/'Assets/TimelineVideoStudio/Scripts'
p=R/'StudioController.cs';s=p.read_text(encoding='utf-8').replace('public class StudioController : MonoBehaviour','public partial class StudioController : MonoBehaviour')
s=s.replace('            previewAudioPeak = 0;','            SyncAudioPlayback();\n            previewAudioPeak = 0;')
s=s.replace('            director.Evaluate();\n        }\n        public void TogglePlay()', '            director.Evaluate();\n            foreach(var ad in audioDirectors) if(ad.playableAsset!=null) { ad.time=playhead;ad.Evaluate(); }\n        }\n        public void TogglePlay()')
s=s.replace('            foreach (var clip in sounds.Values) if (clip != null) Destroy(clip);','            foreach(var ad in audioDirectors) ad.Stop();\n            RetainWaves();')
s=s.replace('            visuals.Clear(); sounds.Clear(); videos.Clear();','            visuals.Clear(); sounds.Clear(); videos.Clear(); visualBindings.Clear();')
a=s.index('            var audioTracks = new AudioTrack[');b=s.index('            foreach (var c in project.clips)',a);s=s[:a]+s[b:]
a=s.index('                        var audio = MediaLoader.LoadWav(c.path);',s.index('        public void Rebuild()'));b=s.index('                    }\n                    else',a)
s=s[:a]+'                        sounds[c.id] = CachedWave(c.path);\n'+s[b:]
s=s.replace('                        var asset = (VisualPlayableAsset)tc.asset;','                        visualBindings[c.id]=tc;\n                        var asset = (VisualPlayableAsset)tc.asset;')
s=s.replace('            director.playableAsset = timeline;', '            UpdateDuration();\n            director.playableAsset = timeline;')
s=s.replace('            for (int i = 0; i < audioTracks.Length; i++) director.SetGenericBinding(audioTracks[i], audioSources[i]);','            for(int i=0;i<6;i++) RefreshAudioTrack(i);')
s=s.replace('if (kind == "WAV") clip.duration = MediaLoader.LoadWav(path).length;', 'if (kind == "WAV") clip.duration = CachedWave(path).length;')
s=s.replace('            selectedId = clip.id;\n            Rebuild();','            selectedId = clip.id;\n            if(kind=="WAV") { UpdateDuration();RefreshAudioTrack(clip.audioTrack);ProjectChanged?.Invoke(); } else Rebuild();')
s=s.replace('            if (selectedId == id) selectedId = "";\n            Rebuild();','            if (selectedId == id) selectedId = "";\n            if(c.kind=="WAV") { sounds.Remove(c.id);UpdateDuration();RefreshAudioTrack(c.audioTrack);RetainWaves();ProjectChanged?.Invoke(); } else Rebuild();')
s=s.replace('            float local = at - c.start;','            if(c.kind=="WAV") return SplitAudio(c,at);\n            float local = at - c.start;')
s=s.replace('            selectedId = copy.id;\n            Rebuild();','            selectedId = copy.id;\n            if(copy.kind=="WAV") { UpdateDuration();RefreshAudioTrack(copy.audioTrack);ProjectChanged?.Invoke(); } else Rebuild();')
s=s.replace('            string metadata = TransformMetadata(c);','            int oldAudioTrack=c.audioTrack;string oldPath=c.path;string oldKind=c.kind;string oldText=c.text;\n            string metadata = TransformMetadata(c);')
s=s.replace('            if (c.kind != "WAV" && metadata == TransformMetadata(c)) RefreshTransform(); else Rebuild();','''            if(c.kind=="WAV" && oldKind=="WAV") CommitClipTiming(c.id,oldAudioTrack);
            else if(c.kind!="WAV" && metadata==TransformMetadata(c)) RefreshTransform();
            else if(c.kind==oldKind && c.path==oldPath && c.text==oldText) CommitClipTiming(c.id,oldAudioTrack);
            else Rebuild();''')
p.write_text(s,encoding='utf-8')
p=R/'StudioWindow.cs';s=p.read_text(encoding='utf-8')
s=s.replace('int originalLayer;','int originalLayer;\n        int originalAudioTrack;\n        float originalSourceOffset;')
s=s.replace('originalLayer = c.layer;', 'originalLayer = c.layer; originalAudioTrack=c.audioTrack;originalSourceOffset=c.sourceOffset;')
s=s.replace('float nextStart = c.start, nextDuration = c.duration;', 'float nextStart = c.start, nextDuration = c.duration, nextSourceOffset=c.sourceOffset;')
s=s.replace('else if (dragMode == "right") nextDuration = Mathf.Max(1f / studio.project.fps, originalDuration + delta);','else if (dragMode == "right") nextDuration = Mathf.Min(studio.SourceDuration(c.id)-originalSourceOffset,Mathf.Max(1f / studio.project.fps, originalDuration + delta));')
s=s.replace('else { nextStart = Mathf.Max(0, originalStart + delta); nextDuration = Mathf.Max(1f / studio.project.fps, originalDuration - (nextStart - originalStart)); }','''else {
                        float earliest=(c.kind=="WAV"||c.kind=="Video")?Mathf.Max(0,originalStart-originalSourceOffset):0;
                        nextStart=Mathf.Clamp(originalStart+delta,earliest,originalStart+originalDuration-1f/studio.project.fps);
                        nextDuration=originalDuration-(nextStart-originalStart);
                        if(c.kind=="WAV"||c.kind=="Video") nextSourceOffset=originalSourceOffset+nextStart-originalStart;
                    }''')
s=s.replace('c.start = nextStart; c.duration = nextDuration;', 'c.start = nextStart; c.duration = nextDuration; c.sourceOffset=nextSourceOffset;')
s=s.replace('if (clipDragChanged) studio.Rebuild(); CacheSelection();', 'if (clipDragChanged) studio.CommitClipTiming(dragClip,originalAudioTrack); CacheSelection();')
s=s.replace('x.start = contextTime; x.layer = Mathf.Clamp(contextLayer, 0, studio.project.visualLayerCount - 1);','x.start = contextTime; if(x.kind=="WAV") x.audioTrack=contextAudioTrack;else x.layer = Mathf.Clamp(contextLayer, 0, studio.project.visualLayerCount - 1);')
s=s.replace('if (clipDragChanged || groupDragChanged) studio.Rebuild();','if(clipDragChanged) studio.CommitClipTiming(dragClip,originalAudioTrack);if(groupDragChanged)studio.Rebuild();')
p.write_text(s,encoding='utf-8')
