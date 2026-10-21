using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using UnityEngine;
using UnityEngine.Playables;
using TimelineVideoStudio;

public static class StudioAudioEditingChecks
{
    public static void Build()
    {
        var go=new GameObject("Audio editing checks");StudioController c=null;
        string sample = Path.Combine(Path.GetTempPath(), "tvs-check-" + Guid.NewGuid().ToString("N") + ".wav");
        using (var writer = new BinaryWriter(File.Create(sample)))
        {
            int frames = 44100;
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+frames*2);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);
            writer.Write(44100);writer.Write(88200);writer.Write((short)2);writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(frames*2);
            for(int i=0;i<frames;i++)writer.Write((short)(Math.Sin(i*2*Math.PI*440/44100)*3000));
        }
        try
        {
            c=go.AddComponent<StudioController>();if(c.director==null)typeof(StudioController).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,null);
            var audio=new StudioClip { kind="WAV",path=sample,duration=.4f,audioTrack=0 };
            var visual=new StudioClip {kind="Cube",duration=4};
            c.project=new StudioProject {clips=new List<StudioClip>{audio,visual}};c.Rebuild();
            var graph=c.director.playableGraph;var original=c.Sound(audio.id);
            var window=go.AddComponent<StudioWindow>();typeof(StudioWindow).GetField("studio",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(window,c);
            typeof(StudioWindow).GetMethod("SelectSegment",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(window,new object[]{audio,0});
            foreach(int track in new[]{0,2,4})
            {
                c.Change(audio.id,a=>{a.audioTrack=track;a.start=1;a.volume=.8f;});
                if(c.Sound(audio.id)!=original || !graph.Equals(c.director.playableGraph))throw new Exception("Audio edit rebuilt visuals or reloaded WAV");
                var second=c.Split(audio.id,1.2f);
                if(Mathf.Abs(second.sourceOffset-.2f)>.0001f||Mathf.Abs(second.duration-.2f)>.0001f||second.audioTrack!=track)throw new Exception("Audio split wrong");
                var duplicate=c.Duplicate(second.id);if(duplicate.audioTrack!=track || duplicate.sourceOffset!=second.sourceOffset)throw new Exception("Audio duplicate wrong");
                c.Delete(duplicate.id);c.Delete(second.id);c.Change(audio.id,a=>{a.duration=.4f;a.sourceOffset=0;});
            }
            c.Change(audio.id,a=>a.volume=.6f);c.Undo();if(Mathf.Abs(c.Find(audio.id).volume-.8f)>.0001f || !graph.Equals(c.director.playableGraph))throw new Exception("Audio undo failed or rebuilt visuals");
            c.Redo();if(Mathf.Abs(c.Find(audio.id).volume-.6f)>.0001f)throw new Exception("Audio redo failed");
            c.Change(audio.id,a=>{a.sourceOffset=.1f;a.duration=100;});if(c.Find(audio.id).duration>c.SourceDuration(audio.id)-.1f+.0001f)throw new Exception("Source trim overflow");
            Debug.Log("AUDIO_EDITING_CHECKS_PASS: WAV selection; SFX/BGM/voice move, split, duplicate, delete, trim bounds, volume, undo/redo; visual graph and source reused");
        }
        finally {if(c!=null&&c.previewCamera!=null)UnityEngine.Object.DestroyImmediate(c.previewCamera.gameObject);UnityEngine.Object.DestroyImmediate(go);File.Delete(sample);}
        StudioBuild.BuildWindows();
    }
}
