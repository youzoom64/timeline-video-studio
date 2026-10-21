using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace TimelineVideoStudio
{
    public partial class StudioController
    {
        class ProcessedAudio { public string key;public AudioClip audio; }
        class PendingAudio { public string key,id;public int rate,channels;public Task<float[]> task; }
        readonly Dictionary<string,ProcessedAudio> processedAudio=new Dictionary<string,ProcessedAudio>();
        readonly List<PendingAudio> pendingAudio=new List<PendingAudio>();
        readonly Dictionary<string,float> queuedAudioEffects=new Dictionary<string,float>();
        public string audioEffectsError="";
        public bool AudioEffectsBusy=>pendingAudio.Count>0||queuedAudioEffects.Count>0;
        public StudioAudioTrackSettings AudioTrackSettings(int index)
        {
            if(project.audioTracks==null)project.audioTracks=new List<StudioAudioTrackSettings>();
            var settings=project.audioTracks.FirstOrDefault(x=>x.index==index);if(settings==null){settings=new StudioAudioTrackSettings{index=index};project.audioTracks.Add(settings);}return settings;
        }
        public List<StudioAudioEffect> AudioEffects(string id,int track)
        {
            if(!string.IsNullOrEmpty(id)){var c=Find(id)??throw new ArgumentException("Clip not found");if(c.kind!="WAV")throw new ArgumentException("Audio clip required");return c.audioEffects??(c.audioEffects=new List<StudioAudioEffect>());}
            return AudioTrackSettings(Mathf.Clamp(track,0,5)).effects;
        }
        public bool AudioEffectsBypassed(string id,int track)=>string.IsNullOrEmpty(id)?AudioTrackSettings(track).bypass:Find(id).bypassAudioEffects;
        public void EditAudioEffects(string id,int track,Action<List<StudioAudioEffect>> edit,bool checkpoint=true)
        {
            if(checkpoint)Checkpoint();edit(AudioEffects(id,track));audioEffectsError="";
            if(string.IsNullOrEmpty(id)){ConfigureTrackFilter(track);UpdateDuration();}else queuedAudioEffects[id]=Time.realtimeSinceStartup+.15f;
            ProjectChanged?.Invoke();
        }
        public void BypassAudioEffects(string id,int track,bool bypass)
        {
            Checkpoint();if(string.IsNullOrEmpty(id)){AudioTrackSettings(track).bypass=bypass;ConfigureTrackFilter(track);UpdateDuration();}else{Find(id).bypassAudioEffects=bypass;queuedAudioEffects[id]=Time.realtimeSinceStartup;}
            ProjectChanged?.Invoke();
        }
        void ConfigureTrackFilter(int track)
        {
            if(track<0||track>=audioSources.Count)return;var filter=audioSources[track].GetComponent<StudioTrackAudioFilter>();if(filter==null)filter=audioSources[track].gameObject.AddComponent<StudioTrackAudioFilter>();
            var settings=AudioTrackSettings(track);filter.Configure(settings.bypass?null:settings.effects);
        }
        bool HasClipAudioEffects(StudioClip c)=>!c.bypassAudioEffects&&c.audioEffects!=null&&c.audioEffects.Any(e=>e.enabled&&e.mix>0);
        string AudioEffectsKey(StudioClip c)=>c.path+"|"+System.IO.File.GetLastWriteTimeUtc(c.path).Ticks+"|"+c.sourceOffset.ToString("R")+"|"+c.duration.ToString("R")+"|"+c.loop+"|"+string.Join("|",c.audioEffects.Select(e=>JsonUtility.ToJson(e)));
        AudioClip ResolveAudio(StudioClip c)
        {
            var raw=CachedWave(c.path);if(!HasClipAudioEffects(c))return raw;
            string key=AudioEffectsKey(c);
            if(processedAudio.TryGetValue(c.id,out var cached)&&cached.key==key&&cached.audio!=null)return cached.audio;
            if(!pendingAudio.Any(p=>p.id==c.id&&p.key==key))
            {
                var data=new float[raw.samples*raw.channels];raw.GetData(data,0);
                int rate=raw.frequency,channels=raw.channels,first=Mathf.RoundToInt(c.sourceOffset*rate),frames=Mathf.RoundToInt(c.duration*rate);
                var effects=c.audioEffects.Select(e=>JsonUtility.FromJson<StudioAudioEffect>(JsonUtility.ToJson(e))).ToList();
                bool loop=c.loop;pendingAudio.Add(new PendingAudio{id=c.id,key=key,rate=rate,channels=channels,task=Task.Run(()=>StudioAudioDsp.Render(data,rate,channels,first,frames,effects,loop))});
                status="音声エフェクトを処理しています…";
            }
            return raw;
        }
        public bool UsesProcessedAudio(StudioClip c)=>HasClipAudioEffects(c)&&processedAudio.TryGetValue(c.id,out var cache)&&cache.key==AudioEffectsKey(c)&&cache.audio!=null;
        public float AudioSourceOffset(StudioClip c)=>UsesProcessedAudio(c)?0:c.sourceOffset;
        public float AudioPlaybackDuration(StudioClip c)=>c.duration+(HasClipAudioEffects(c)?StudioAudioDsp.Tail(c.audioEffects):0);
        public StudioAudioChain CreateTrackAudioChain(int track,int rate){var s=AudioTrackSettings(track);return new StudioAudioChain(s.bypass?null:s.effects,rate);}
        void UpdateAudioEffects()
        {
            foreach(var id in processedAudio.Keys.ToArray())if(Find(id)==null){if(processedAudio[id].audio!=null)Destroy(processedAudio[id].audio);processedAudio.Remove(id);}
            var tracks=new HashSet<int>();
            foreach(var id in queuedAudioEffects.Keys.ToArray())if(Time.realtimeSinceStartup>=queuedAudioEffects[id]){queuedAudioEffects.Remove(id);var clip=Find(id);if(clip!=null)tracks.Add(clip.audioTrack);}
            for(int i=pendingAudio.Count-1;i>=0;i--)
            {
                var p=pendingAudio[i];if(!p.task.IsCompleted)continue;pendingAudio.RemoveAt(i);var c=Find(p.id);
                if(p.task.IsFaulted){audioEffectsError=p.task.Exception.GetBaseException().Message;status="音声エフェクトエラー: "+audioEffectsError;continue;}
                if(c==null||!HasClipAudioEffects(c)||p.key!=AudioEffectsKey(c))continue;
                if(processedAudio.TryGetValue(c.id,out var old)&&old.audio!=null)Destroy(old.audio);
                var data=p.task.Result;var audio=AudioClip.Create("FX "+System.IO.Path.GetFileName(c.path),data.Length/p.channels,p.channels,p.rate,false);audio.SetData(data,0);
                processedAudio[c.id]=new ProcessedAudio{key=p.key,audio=audio};tracks.Add(c.audioTrack);
            }
            if(tracks.Count>0){UpdateDuration();foreach(int track in tracks)RefreshAudioTrack(track);ProjectChanged?.Invoke();if(!AudioEffectsBusy&&string.IsNullOrEmpty(audioEffectsError))status="音声の変更を反映しました";}
        }
    }
}
