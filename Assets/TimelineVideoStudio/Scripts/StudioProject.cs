using System;
using System.Collections.Generic;
using UnityEngine;

namespace TimelineVideoStudio
{
    [Serializable]
    public class StudioKeyframe
    {
        public float time;
        public float x;
        public float y;
        public float z;
        public float scale = 1f;
        public float rotation;
        public float nextX;
        public float nextY;
        public float nextZ;
        public float nextScale = 1f;
        public float nextRotation;
        public float scaleX = 1f, scaleY = 1f;
        public float nextScaleX = 1f, nextScaleY = 1f;
    }

    [Serializable]
    public class StudioEffect
    {
        public string id = Guid.NewGuid().ToString("N");
        public string plugin = "shake";
        public bool enabled = true;
        public float a = 0.2f;
        public float b = 5f;
        public float c = 1f;
        public float d = 1f;
    }

    [Serializable]
    public class StudioSegment
    {
        public int index;
        public float start, end;
        public float startX, startY, startZ, startScale = 1f, startRotation;
        public float endX, endY, endZ, endScale = 1f, endRotation;
        public float startScaleX = 1f, startScaleY = 1f;
        public float endScaleX = 1f, endScaleY = 1f;
    }

    [Serializable]
    public class StudioClip
    {
        public string id = Guid.NewGuid().ToString("N");
        public string kind = "Image";
        public string path = "";
        public string text = "";
        public float start;
        public float duration = 5f;
        public int layer;
        public string groupId = "";
        public float sourceOffset;
        public int audioTrack = 4;
        public bool loop;
        public float volume = 1f;
        public float fadeIn;
        public float fadeOut;
        public bool bypassAudioEffects;
        public List<StudioAudioEffect> audioEffects = new List<StudioAudioEffect>();
        public float x;
        public float y;
        public float z;
        public float scale = 1f;
        public float endX;
        public float endY;
        public float endZ;
        public float endScale = 1f;
        public float rotation;
        public float endRotation;
        public float scaleX = 1f, scaleY = 1f;
        public float endScaleX = 1f, endScaleY = 1f;
        public bool lockAspect;
        public List<StudioKeyframe> keyframes = new List<StudioKeyframe>();
        public List<StudioEffect> effects = new List<StudioEffect>();
    }

    [Serializable]
    public class StudioGroup
    {
        public string id = Guid.NewGuid().ToString("N");
        public string name = "グループ";
        public float x;
        public float y;
        public float scale = 1f;
        public float rotation;
        public List<StudioEffect> effects = new List<StudioEffect>();
    }

    [Serializable]
    public class StudioScene
    {
        public float start;
        public string title = "";
    }

    [Serializable]
    public class StudioProject
    {
        public static readonly string[] AudioTrackNames = { "効果音1", "効果音2", "BGM1", "BGM2", "音声1", "音声2" };
        public string title = "Untitled";
        public int width = 1280;
        public int height = 720;
        public int fps = 30;
        public float bpm = 120f;
        public float duration = 1f / 30f;
        public float minimumDuration;
        public int visualLayerCount = 8;
        public List<StudioScene> scenes = new List<StudioScene>();
        public List<int> hiddenVisualLayers = new List<int>();
        public List<int> mutedAudioTracks = new List<int>();
        public List<StudioAudioTrackSettings> audioTracks = CreateAudioTrackSettings();
        static List<StudioAudioTrackSettings> CreateAudioTrackSettings() { var tracks=new List<StudioAudioTrackSettings>();for(int i=0;i<6;i++)tracks.Add(new StudioAudioTrackSettings {index=i});return tracks; }
        public List<StudioGroup> groups = new List<StudioGroup>();
        public List<StudioClip> clips = new List<StudioClip>();
    }
}
