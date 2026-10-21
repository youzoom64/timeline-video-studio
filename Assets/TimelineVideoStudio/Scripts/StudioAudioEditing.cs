using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace TimelineVideoStudio
{
    public partial class StudioController
    {
        readonly Dictionary<string, AudioClip> waveCache = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, long> waveVersions = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        readonly List<PlayableDirector> audioDirectors = new List<PlayableDirector>();
        readonly List<TimelineAsset> audioTimelines = new List<TimelineAsset>();
        readonly Dictionary<string, TimelineClip> visualBindings = new Dictionary<string, TimelineClip>();
        AudioClip CachedWave(string path)
        {
            long stamp=File.GetLastWriteTimeUtc(path).Ticks;
            if(!waveCache.TryGetValue(path,out var audio) || audio==null || waveVersions[path]!=stamp)
            {
                if(audio!=null) Destroy(audio);
                audio=MediaLoader.LoadWav(path);waveCache[path]=audio;waveVersions[path]=stamp;
            }
            return audio;
        }
        void RetainWaves()
        {
            var needed=new HashSet<string>(project.clips.Where(c=>c.kind=="WAV").Select(c=>c.path),StringComparer.OrdinalIgnoreCase);
            foreach(var path in waveCache.Keys.ToArray()) if(!needed.Contains(path)) { Destroy(waveCache[path]);waveCache.Remove(path);waveVersions.Remove(path); }
        }
        void UpdateDuration()
        {
            project.duration=Mathf.Max(1f/Mathf.Max(1,project.fps),project.minimumDuration,project.clips.Count==0?0:project.clips.Max(c=>c.start+(c.kind=="WAV"?AudioPlaybackDuration(c)+(AudioTrackSettings(c.audioTrack).bypass?0:StudioAudioDsp.Tail(AudioTrackSettings(c.audioTrack).effects)):c.duration)));
            if(timeline!=null) { timeline.durationMode=TimelineAsset.DurationMode.FixedLength;timeline.fixedDuration=project.duration; }
            if(director!=null && director.playableGraph.IsValid() && director.playableGraph.GetRootPlayableCount()>0) director.playableGraph.GetRootPlayable(0).SetDuration(project.duration);
            foreach(var t in audioTimelines) if(t!=null) t.fixedDuration=project.duration;
            foreach(var ad in audioDirectors)if(ad.playableGraph.IsValid()&&ad.playableGraph.GetRootPlayableCount()>0)ad.playableGraph.GetRootPlayable(0).SetDuration(project.duration);
        }
        void RefreshAudioTrack(int index)
        {
            index=Mathf.Clamp(index,0,5);
            while(audioSources.Count<6)
            {
                var trackObject=new GameObject(StudioProject.AudioTrackNames[audioSources.Count]);trackObject.transform.SetParent(transform,false);
                var source=trackObject.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=0;
                audioSources.Add(source);
                var d=trackObject.AddComponent<PlayableDirector>();d.playOnAwake=false;d.timeUpdateMode=DirectorUpdateMode.UnscaledGameTime;d.extrapolationMode=DirectorWrapMode.Hold;audioDirectors.Add(d);audioTimelines.Add(null);
            }
            var output=audioSources[index];output.volume=1;output.mute=!AudioTrackEnabled(index);
            var ad=audioDirectors[index];ad.Stop();
            var old=audioTimelines[index];
            var asset=ScriptableObject.CreateInstance<TimelineAsset>();asset.durationMode=TimelineAsset.DurationMode.FixedLength;asset.fixedDuration=project.duration;
            var track=asset.CreateTrack<AudioTrack>(null,StudioProject.AudioTrackNames[index]);
            foreach(var c in project.clips.Where(c=>c.kind=="WAV" && c.audioTrack==index))
            {
                var audio=ResolveAudio(c);sounds[c.id]=audio;
                var tc=track.CreateClip<AudioPlayableAsset>();((AudioPlayableAsset)tc.asset).clip=audio;((AudioPlayableAsset)tc.asset).loop=c.loop&&!UsesProcessedAudio(c);
                JsonUtility.FromJsonOverwrite("{\"m_ClipProperties\":{\"volume\":"+Mathf.Clamp(c.volume,0,4).ToString(System.Globalization.CultureInfo.InvariantCulture)+"}}",tc.asset);
                float offset=UsesProcessedAudio(c)?0:c.sourceOffset;
                tc.start=c.start;tc.clipIn=Mathf.Max(0,offset);tc.duration=UsesProcessedAudio(c)?audio.length:c.loop?c.duration:Mathf.Min(c.duration,Mathf.Max(0,audio.length-offset));
                tc.easeInDuration=Mathf.Min(c.fadeIn,(float)tc.duration/2);tc.easeOutDuration=Mathf.Min(c.fadeOut,(float)tc.duration/2);
                tc.mixInCurve=AnimationCurve.Linear(0,0,1,1);tc.mixOutCurve=AnimationCurve.Linear(0,1,1,0);tc.displayName=Path.GetFileName(c.path);
            }
            audioTimelines[index]=asset;ad.playableAsset=asset;ad.SetGenericBinding(track,output);ad.RebuildGraph();ad.time=playhead;ad.Evaluate();ConfigureTrackFilter(index);if(playing) ad.Play();if(old!=null) Destroy(old);
        }
        void SyncAudioPlayback()
        {
            foreach(var ad in audioDirectors)
            {
                if(playing && ad.state!=PlayState.Playing) { ad.time=playhead;ad.Play(); }
                else if(!playing && ad.state==PlayState.Playing) ad.Pause();
            }
        }
        public float SourceDuration(string id)
        {
            var c=Find(id);return c!=null&&c.kind=="WAV"&&c.loop?float.PositiveInfinity:RawSourceDuration(id);
        }
        public float RawSourceDuration(string id)
        {
            var c=Find(id);if(c==null)return float.PositiveInfinity;
            if(c.kind=="WAV")return CachedWave(c.path).length;
            if(c.kind=="Video" && videos.TryGetValue(id,out var player) && player.isPrepared)return (float)player.length;
            return float.PositiveInfinity;
        }
        void NormalizeTiming(StudioClip c)
        {
            c.start=Mathf.Max(0,c.start);c.duration=Mathf.Max(1f/project.fps,c.duration);c.sourceOffset=Mathf.Max(0,c.sourceOffset);
            if(c.kind=="WAV" || c.kind=="Video")
            {
                float source=RawSourceDuration(c.id);
                if(c.kind=="WAV"&&c.loop)c.sourceOffset=Mathf.Repeat(c.sourceOffset,source);
                else{c.sourceOffset=Mathf.Min(c.sourceOffset,Mathf.Max(0,source-1f/project.fps));c.duration=Mathf.Min(c.duration,source-c.sourceOffset);}
            }
        }
        public void CommitClipTiming(string id,int previousAudioTrack)
        {
            var c=Find(id);if(c==null)return;NormalizeTiming(c);UpdateDuration();
            if(c.kind=="WAV") { RefreshAudioTrack(previousAudioTrack);if(previousAudioTrack!=c.audioTrack) RefreshAudioTrack(c.audioTrack); }
            else if(visualBindings.TryGetValue(id,out var tc))
            {
                tc.start=c.start;tc.duration=c.duration;var a=(VisualPlayableAsset)tc.asset;a.clip=c;a.layer=c.layer;
                director.RebuildGraph();director.time=playhead;director.Evaluate();
            }
            ProjectChanged?.Invoke();
        }
        StudioClip SplitAudio(StudioClip c,float at)
        {
            Checkpoint();float local=at-c.start;
            var second=JsonUtility.FromJson<StudioClip>(JsonUtility.ToJson(c));second.id=Guid.NewGuid().ToString("N");second.start=at;second.duration=c.duration-local;second.sourceOffset+=local;second.fadeIn=0;c.duration=local;c.fadeOut=0;
            if(second.loop)second.sourceOffset=Mathf.Repeat(second.sourceOffset,RawSourceDuration(c.id));
            project.clips.Add(second);selectedId=second.id;UpdateDuration();RefreshAudioTrack(c.audioTrack);ProjectChanged?.Invoke();return second;
        }
    }
}
