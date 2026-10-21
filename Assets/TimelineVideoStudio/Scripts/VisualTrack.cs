using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using System.Collections.Generic;
using UnityEngine.Video;

namespace TimelineVideoStudio
{
    [TrackClipType(typeof(VisualPlayableAsset))]
    public class VisualTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            return ScriptPlayable<VisualMixer>.Create(graph, inputCount);
        }
    }

    public class VisualPlayableAsset : PlayableAsset
    {
        public StudioController controller;
        public int layer;
        public StudioClip clip;
        public GameObject target;
        public Vector3 fromPosition;
        public Vector3 toPosition;
        public float fromScale = 1f;
        public float toScale = 1f;
        public float rotation;
        public float endRotation;
        public Vector3 baseSize = Vector3.one;
        public List<StudioKeyframe> keyframes;
        public List<StudioEffect> effects;
        public List<StudioEffect> groupEffects;
        public float groupStart, clipStart, groupX, groupY, groupScale = 1f, groupRotation;
        public VideoPlayer video;
        public float sourceOffset;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var p = ScriptPlayable<VisualBehaviour>.Create(graph);
            var b = p.GetBehaviour();
            b.controller = controller; b.layer = layer; b.clip = clip;
            b.target = target;
            b.fromPosition = fromPosition;
            b.toPosition = toPosition;
            b.fromScale = fromScale;
            b.toScale = toScale;
            b.rotation = rotation;
            b.endRotation = endRotation;
            b.baseSize = baseSize;
            b.keyframes = keyframes;
            b.effects = effects;
            b.groupEffects = groupEffects;
            b.groupStart = groupStart; b.clipStart = clipStart;
            b.groupX = groupX; b.groupY = groupY; b.groupScale = groupScale; b.groupRotation = groupRotation;
            b.video = video;
            b.sourceOffset = sourceOffset;
            return p;
        }
    }

    public class VisualBehaviour : PlayableBehaviour
    {
        public StudioController controller;
        public int layer;
        public StudioClip clip;
        public GameObject target;
        public Vector3 fromPosition;
        public Vector3 toPosition;
        public float fromScale;
        public float toScale;
        public float rotation;
        public float endRotation;
        public Vector3 baseSize;
        public List<StudioKeyframe> keyframes;
        public List<StudioEffect> effects;
        public List<StudioEffect> groupEffects;
        public float groupStart, clipStart, groupX, groupY, groupScale = 1f, groupRotation;
        public VideoPlayer video;
        public float sourceOffset;
    }

    public class VisualMixer : PlayableBehaviour
    {
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            for (int i = 0; i < playable.GetInputCount(); i++)
            {
                var input = (ScriptPlayable<VisualBehaviour>)playable.GetInput(i);
                var b = input.GetBehaviour();
                if (b.target == null) continue;
                bool visible = b.controller != null && b.clip != null
                    ? b.controller.playhead >= b.clip.start && b.controller.playhead < b.clip.start + b.clip.duration && b.controller.VisualLayerEnabled(b.layer)
                    : playable.GetInputWeight(i) > 0.001f;
                b.target.SetActive(visible);
                if (!visible) continue;
                if (b.video != null && b.video.isPrepared)
                {
                    double sourceTime = b.sourceOffset + input.GetTime();
                    b.video.GetComponent<StudioVideoFrame>().Show(sourceTime);
                }
                var value = StudioTransform.Evaluate(b.clip, (float)input.GetTime());
                var state = new StudioEffectState { x=value.x, y=value.y, z=value.z - b.layer * .01f, scale=value.scale, rotation=value.rotation, color=Color.white };
                if (b.effects != null)
                    foreach (var effect in b.effects)
                        try { StudioPlugins.Apply(ref state, effect, (float)input.GetTime()); }
                        catch (System.Exception e) { Debug.LogWarning("Effect " + effect.plugin + ": " + e.Message); }
                if (b.groupEffects != null)
                    foreach (var effect in b.groupEffects)
                        try { StudioPlugins.Apply(ref state, effect, b.clipStart - b.groupStart + (float)input.GetTime()); }
                        catch (System.Exception e) { Debug.LogWarning("Group effect " + effect.plugin + ": " + e.Message); }
                var radians = b.groupRotation * Mathf.Deg2Rad;
                float px = state.x * b.groupScale, py = state.y * b.groupScale;
                state.x = b.groupX + px * Mathf.Cos(radians) - py * Mathf.Sin(radians);
                state.y = b.groupY + px * Mathf.Sin(radians) + py * Mathf.Cos(radians);
                state.scale *= b.groupScale;
                state.rotation += b.groupRotation;
                b.target.transform.position = new Vector3(state.x, state.y, state.z);
                var size = b.video != null && b.video.isPrepared && b.video.height > 0
                    ? new Vector3((float)b.video.width / b.video.height * 4f, 4f, 1f) : b.baseSize;
                b.target.transform.localScale = Vector3.Scale(size * state.scale, new Vector3(value.scaleX, value.scaleY, 1));
                b.target.transform.rotation = Quaternion.Euler(0f, 0f, state.rotation);
                var renderer = b.target.GetComponent<Renderer>();
                if (renderer != null && renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty("_Color")) renderer.material.color = state.color;
            }
        }
    }
}
