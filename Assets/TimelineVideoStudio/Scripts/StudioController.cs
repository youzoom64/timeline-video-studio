using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.Video;

namespace TimelineVideoStudio
{
    public partial class StudioController : MonoBehaviour
    {
        public StudioProject project = new StudioProject();
        public bool VisualLayerEnabled(int index) { return project.hiddenVisualLayers == null || !project.hiddenVisualLayers.Contains(index); }
        public bool AudioTrackEnabled(int index) { return project.mutedAudioTracks == null || !project.mutedAudioTracks.Contains(index); }
        public void ToggleLayer(bool audio, int index)
        {
            Checkpoint();
            if (project.hiddenVisualLayers == null) project.hiddenVisualLayers = new List<int>();
            if (project.mutedAudioTracks == null) project.mutedAudioTracks = new List<int>();
            var disabled = audio ? project.mutedAudioTracks : project.hiddenVisualLayers;
            if (!disabled.Remove(index)) disabled.Add(index);
            if (audio)
            {
                if (index >= 0 && index < audioSources.Count) audioSources[index].mute = !AudioTrackEnabled(index);
            }
            else if (director.playableAsset != null) director.Evaluate();
            ProjectChanged?.Invoke();
        }
        public string projectPath = "";
        public string selectedId = "";
        public string status = "準備完了";
        public string exportPath = "";
        public float playhead;
        public bool playing;
        public float previewAudioPeak;
        readonly float[] audioSamples = new float[512];
        readonly List<AudioSource> audioSources = new List<AudioSource>();
        public Camera previewCamera;
        public RenderTexture previewTexture;
        public PlayableDirector director;
        TimelineAsset timeline;
        readonly Dictionary<string, GameObject> visuals = new Dictionary<string, GameObject>();
        readonly Dictionary<string, AudioClip> sounds = new Dictionary<string, AudioClip>();
        readonly Dictionary<string, VideoPlayer> videos = new Dictionary<string, VideoPlayer>();
        readonly List<string> undo = new List<string>();
        readonly List<string> redo = new List<string>();
        public event Action ProjectChanged;

        void Awake()
        {
            Application.runInBackground = true;
            if (Application.isPlaying) StudioToolInstaller.Begin();
            gameObject.name = "Timeline Video Studio";
            director = gameObject.AddComponent<PlayableDirector>();
            director.playOnAwake = false;
            director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
            director.extrapolationMode = DirectorWrapMode.Hold;
            var cameraObject = new GameObject("Export and Preview Camera");
            previewCamera = cameraObject.AddComponent<Camera>();
            previewCamera.cullingMask = 1 << 30;
            previewCamera.orthographic = true;
            previewCamera.orthographicSize = 5f;
            previewCamera.transform.position = new Vector3(0, 0, -10);
            previewCamera.transform.rotation = Quaternion.identity;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(0.055f, 0.065f, 0.085f);
            previewCamera.nearClipPlane = 0.01f;
            previewCamera.farClipPlane = 100f;
            ResetRenderTarget();
            Rebuild();
        }

        void Update()
        {
            UpdateAudioEffects();
            if (playing && director.playableAsset != null)
            {
                if (director.state != PlayState.Playing)
                {
                    if (playhead >= project.duration) playhead = 0;
                    director.time = playhead;
                    director.Play();
                }
                playhead = Mathf.Clamp((float)director.time, 0, project.duration);
                if (director.time >= project.duration) playing = false;
            }
            if (!playing && director.state == PlayState.Playing) director.Pause();
            SyncAudioPlayback();
            previewAudioPeak = 0;
            if (playing) foreach (var source in audioSources)
            {
                if (source == null) continue;
                source.GetOutputData(audioSamples, 0);
                foreach (var sample in audioSamples) previewAudioPeak = Mathf.Max(previewAudioPeak, Mathf.Abs(sample));
            }
        }

        public void ResetRenderTarget()
        {
            if (previewTexture != null) { previewCamera.targetTexture = null; previewTexture.Release(); Destroy(previewTexture); }
            previewTexture = new RenderTexture(project.width, project.height, 24, RenderTextureFormat.ARGB32);
            previewTexture.Create();
            previewCamera.aspect = (float)project.width / project.height;
            previewCamera.targetTexture = previewTexture;
        }

        public StudioClip Find(string id) { return project.clips.FirstOrDefault(c => c.id == id); }
        public StudioGroup FindGroup(string id) { return project.groups?.FirstOrDefault(g => g.id == id); }
        public List<StudioClip> GroupMembers(string id) { return project.clips.Where(c => c.groupId == id).ToList(); }
        public float GroupStart(string id) { var members = GroupMembers(id); return members.Count == 0 ? 0 : members.Min(c => c.start); }
        public float GroupEnd(string id) { var members = GroupMembers(id); return members.Count == 0 ? 0 : members.Max(c => c.start + c.duration); }
        public void MoveGroup(string id, float start, bool checkpoint = true)
        {
            if (FindGroup(id) == null) throw new ArgumentException("Group not found: " + id);
            var members = GroupMembers(id);
            if (members.Count == 0) throw new ArgumentException("Group has no objects: " + id);
            if (checkpoint) Checkpoint();
            float delta = Mathf.Max(0, start) - GroupStart(id);
            foreach (var c in members) c.start = Mathf.Max(0, c.start + delta);
            Rebuild();
        }
        public void ChangeGroup(string id, Action<StudioGroup> edit, bool checkpoint = true)
        {
            var group = FindGroup(id) ?? throw new ArgumentException("Group not found: " + id);
            if (checkpoint) Checkpoint(); edit(group); group.scale = Mathf.Max(0.001f, group.scale); Rebuild();
        }
        public void InsertLayer(int index)
        {
            index = Mathf.Clamp(index, 0, Mathf.Max(1, project.visualLayerCount));
            Checkpoint();
            foreach (var c in project.clips.Where(c => c.kind != "WAV" && c.layer >= index)) c.layer++;
            project.visualLayerCount = Mathf.Max(1, project.visualLayerCount) + 1;
            Rebuild();
        }
        public StudioGroup Group(IEnumerable<string> ids)
        {
            var members = ids.Distinct().Select(Find).Where(c => c != null && c.kind != "WAV").ToList();
            if (members.Count < 2) throw new ArgumentException("グループには映像オブジェクトを2つ以上選択してください");
            Checkpoint();
            var group = new StudioGroup { name = "グループ " + (project.groups.Count + 1) };
            project.groups.Add(group);
            foreach (var c in members) c.groupId = group.id;
            project.groups.RemoveAll(g => g.id != group.id && !project.clips.Any(c => c.groupId == g.id));
            selectedId = group.id;
            Rebuild(); return group;
        }
        public void Ungroup(string id)
        {
            var group = FindGroup(id) ?? throw new ArgumentException("Group not found: " + id);
            Checkpoint(); foreach (var c in project.clips.Where(c => c.groupId == id)) c.groupId = "";
            project.groups.Remove(group); Rebuild();
            if (selectedId == id) selectedId = "";
        }
        public StudioEffect AddGroupEffect(string groupId, string pluginId)
        {
            var group = FindGroup(groupId) ?? throw new ArgumentException("Group not found: " + groupId);
            Checkpoint(); var effect = StudioPlugins.Create(pluginId); group.effects.Add(effect); Rebuild(); return effect;
        }
        public void SetGroupEffect(string groupId, string effectId, Action<StudioEffect> edit)
        {
            var effect = FindGroup(groupId)?.effects?.FirstOrDefault(e => e.id == effectId) ?? throw new ArgumentException("Effect not found: " + effectId);
            Checkpoint(); edit(effect); Rebuild();
        }
        public void DeleteGroupEffect(string groupId, string effectId)
        {
            var group = FindGroup(groupId) ?? throw new ArgumentException("Group not found: " + groupId);
            var effect = group.effects?.FirstOrDefault(e => e.id == effectId) ?? throw new ArgumentException("Effect not found: " + effectId);
            Checkpoint(); group.effects.Remove(effect); Rebuild();
        }
        public StudioKeyframe AddKeyframe(string id, float at)
        {
            var c = Find(id);
            if (c == null || c.kind == "WAV") throw new ArgumentException("Visual clip required");
            if (at <= c.start || at >= c.start + c.duration) throw new ArgumentException("Intermediate point must be inside the clip");
            float time = Mathf.Round((at - c.start) * project.fps) / project.fps;
            if (time <= 0 || time >= c.duration) throw new ArgumentException("Intermediate point must be inside the clip");
            if (c.keyframes == null) c.keyframes = new List<StudioKeyframe>();
            if (c.keyframes.Any(k => Mathf.Abs(k.time - time) < 0.5f / project.fps)) throw new ArgumentException("Intermediate point already exists");
            var v = StudioTransform.Evaluate(c, time);
            var point = new StudioKeyframe { time=time, x=v.x,y=v.y,z=v.z,scale=v.scale,rotation=v.rotation,scaleX=v.scaleX,scaleY=v.scaleY,
                nextX=v.x,nextY=v.y,nextZ=v.z,nextScale=v.scale,nextRotation=v.rotation,nextScaleX=v.scaleX,nextScaleY=v.scaleY };
            Checkpoint(); c.keyframes.Add(point); c.keyframes.Sort((a, b) => a.time.CompareTo(b.time)); RefreshTransform(); return point;
        }
        public void DeleteKeyframe(string id, float at)
        {
            var c = Find(id);
            var point = c?.keyframes?.OrderBy(k => Mathf.Abs(c.start + k.time - at)).FirstOrDefault();
            if (point == null || Mathf.Abs(c.start + point.time - at) > 0.5f / project.fps) throw new ArgumentException("Intermediate point not found");
            Checkpoint(); c.keyframes.Remove(point); RefreshTransform();
        }
        public void SetKeyframe(string id, float at, Action<StudioKeyframe> edit)
        {
            var c = Find(id);
            var point = c?.keyframes?.OrderBy(k => Mathf.Abs(c.start + k.time - at)).FirstOrDefault();
            if (point == null || Mathf.Abs(c.start + point.time - at) > 0.5f / project.fps) throw new ArgumentException("Intermediate point not found");
            Checkpoint();
            float oldX=point.scaleX,oldY=point.scaleY,oldNextX=point.nextScaleX,oldNextY=point.nextScaleY;
            edit(point);
            if(c.lockAspect)
            {
                if(point.scaleX!=oldX && point.scaleY==oldY) point.scaleY=oldX>.000001f?oldY*point.scaleX/oldX:point.scaleX;
                else if(point.scaleY!=oldY && point.scaleX==oldX) point.scaleX=oldY>.000001f?oldX*point.scaleY/oldY:point.scaleY;
                if(point.nextScaleX!=oldNextX && point.nextScaleY==oldNextY) point.nextScaleY=oldNextX>.000001f?oldNextY*point.nextScaleX/oldNextX:point.nextScaleX;
                else if(point.nextScaleY!=oldNextY && point.nextScaleX==oldNextX) point.nextScaleX=oldNextY>.000001f?oldNextX*point.nextScaleY/oldNextY:point.nextScaleY;
            }
            RefreshTransform();
        }
        public StudioSegment GetSegment(string id, int index)
        {
            var c = Find(id) ?? throw new ArgumentException("Clip not found: " + id);
            if (c.kind == "WAV") throw new ArgumentException("Visual clip required");
            var points = (c.keyframes ?? new List<StudioKeyframe>()).OrderBy(x => x.time).ToList();
            if (index < 0 || index > points.Count) throw new ArgumentException("Segment index out of range");
            var previous = index == 0 ? null : points[index - 1];
            var next = index == points.Count ? null : points[index];
            return new StudioSegment { index = index, start = previous == null ? 0 : previous.time, end = next == null ? c.duration : next.time,
                startX = previous == null ? c.x : previous.nextX, startY = previous == null ? c.y : previous.nextY, startZ = previous == null ? c.z : previous.nextZ,
                startScaleX = previous == null ? c.scaleX : previous.nextScaleX, startScaleY = previous == null ? c.scaleY : previous.nextScaleY,
                startScale = previous == null ? c.scale : previous.nextScale, startRotation = previous == null ? c.rotation : previous.nextRotation,
                endX = next == null ? c.endX : next.x, endY = next == null ? c.endY : next.y, endZ = next == null ? c.endZ : next.z,
                endScaleX = next == null ? c.endScaleX : next.scaleX, endScaleY = next == null ? c.endScaleY : next.scaleY,
                endScale = next == null ? c.endScale : next.scale, endRotation = next == null ? c.endRotation : next.rotation };
        }
        public void SetSegment(string id, int index, StudioSegment values, bool recordUndo = true)
        {
            var c = Find(id) ?? throw new ArgumentException("Clip not found: " + id);
            if (c.lockAspect)
            {
                var old=GetSegment(id,index);
                if(values.startScaleX!=old.startScaleX && values.startScaleY==old.startScaleY) values.startScaleY=old.startScaleX>0.000001f?old.startScaleY*values.startScaleX/old.startScaleX:values.startScaleX;
                else if(values.startScaleY!=old.startScaleY && values.startScaleX==old.startScaleX) values.startScaleX=old.startScaleY>0.000001f?old.startScaleX*values.startScaleY/old.startScaleY:values.startScaleY;
                if(values.endScaleX!=old.endScaleX && values.endScaleY==old.endScaleY) values.endScaleY=old.endScaleX>0.000001f?old.endScaleY*values.endScaleX/old.endScaleX:values.endScaleX;
                else if(values.endScaleY!=old.endScaleY && values.endScaleX==old.endScaleX) values.endScaleX=old.endScaleY>0.000001f?old.endScaleX*values.endScaleY/old.endScaleY:values.endScaleY;
            }
            var points = (c.keyframes ?? new List<StudioKeyframe>()).OrderBy(x => x.time).ToList();
            if (index < 0 || index > points.Count) throw new ArgumentException("Segment index out of range");
            if (recordUndo) Checkpoint();
            if (index == 0) { c.x = values.startX; c.y = values.startY; c.z = values.startZ; c.scale = values.startScale; c.rotation = values.startRotation; c.scaleX = Mathf.Max(0,values.startScaleX); c.scaleY = Mathf.Max(0,values.startScaleY); }
            else { var p = points[index - 1]; p.nextX = values.startX; p.nextY = values.startY; p.nextZ = values.startZ; p.nextScale = values.startScale; p.nextRotation = values.startRotation; p.nextScaleX = Mathf.Max(0,values.startScaleX); p.nextScaleY = Mathf.Max(0,values.startScaleY); }
            if (index == points.Count) { c.endX = values.endX; c.endY = values.endY; c.endZ = values.endZ; c.endScale = values.endScale; c.endRotation = values.endRotation; c.endScaleX = Mathf.Max(0,values.endScaleX); c.endScaleY = Mathf.Max(0,values.endScaleY); }
            else { var p = points[index]; p.x = values.endX; p.y = values.endY; p.z = values.endZ; p.scale = values.endScale; p.rotation = values.endRotation; p.scaleX = Mathf.Max(0,values.endScaleX); p.scaleY = Mathf.Max(0,values.endScaleY); }
            RefreshTransform();
        }
        public StudioEffect AddEffect(string clipId, string pluginId)
        {
            var c = Find(clipId);
            if (c == null || c.kind == "WAV") throw new ArgumentException("Visual clip required");
            var effect = StudioPlugins.Create(pluginId);
            Checkpoint();
            if (c.effects == null) c.effects = new List<StudioEffect>();
            c.effects.Add(effect); Rebuild(); return effect;
        }
        public void SetEffect(string clipId, string effectId, Action<StudioEffect> edit)
        {
            var effect = Find(clipId)?.effects?.FirstOrDefault(e => e.id == effectId);
            if (effect == null) throw new ArgumentException("Effect not found: " + effectId);
            Checkpoint(); edit(effect); Rebuild();
        }
        public void DeleteEffect(string clipId, string effectId)
        {
            var c = Find(clipId);
            var effect = c?.effects?.FirstOrDefault(e => e.id == effectId);
            if (effect == null) throw new ArgumentException("Effect not found: " + effectId);
            Checkpoint(); c.effects.Remove(effect); Rebuild();
        }
        public void Checkpoint()
        {
            undo.Add(JsonUtility.ToJson(project));
            if (undo.Count > 100) undo.RemoveAt(0);
            redo.Clear();
        }
        public void Undo()
        {
            if (undo.Count == 0) return;
            redo.Add(JsonUtility.ToJson(project));
            var restored = JsonUtility.FromJson<StudioProject>(undo[undo.Count - 1]);
            undo.RemoveAt(undo.Count - 1);
            if (!RestoreTransformHistory(restored)) { project=restored; Rebuild(); }
        }
        public void Redo()
        {
            if (redo.Count == 0) return;
            undo.Add(JsonUtility.ToJson(project));
            var restored = JsonUtility.FromJson<StudioProject>(redo[redo.Count - 1]);
            redo.RemoveAt(redo.Count - 1);
            if (!RestoreTransformHistory(restored)) { project=restored; Rebuild(); }
        }

        public StudioClip Add(string kind, string path = "", string text = "", float start = -1, int layer = 0)
        {
            if (kind != "Image" && kind != "Video" && kind != "OBJ" && kind != "WAV" && kind != "Cube" && kind != "Sphere" && kind != "Text")
                throw new ArgumentException("Unsupported kind: " + kind);
            if (!string.IsNullOrWhiteSpace(path) && !Path.IsPathRooted(path))
                path = Path.GetFullPath(Path.Combine(string.IsNullOrEmpty(projectPath) ? Directory.GetCurrentDirectory() : Path.GetDirectoryName(projectPath), path));
            if ((kind == "Image" || kind == "Video" || kind == "OBJ" || kind == "WAV") && !File.Exists(path))
                throw new FileNotFoundException(path);
            Checkpoint();
            var clip = new StudioClip { kind = kind, path = path, text = text, start = start < 0 ? playhead : start, layer = layer };
            if (kind == "WAV") clip.duration = CachedWave(path).length;
            if (kind == "Video") clip.duration = MediaLoader.GetVideoDuration(path);
            project.clips.Add(clip);
            selectedId = clip.id;
            if(kind=="WAV") { UpdateDuration();RefreshAudioTrack(clip.audioTrack);ProjectChanged?.Invoke(); } else Rebuild();
            return clip;
        }

        public void Delete(string id)
        {
            var c = Find(id);
            if (c == null) throw new ArgumentException("Clip not found: " + id);
            Checkpoint();
            project.clips.Remove(c);
            if (!string.IsNullOrEmpty(c.groupId) && !project.clips.Any(x => x.groupId == c.groupId)) project.groups.RemoveAll(g => g.id == c.groupId);
            if (selectedId == id) selectedId = "";
            if(c.kind=="WAV") { sounds.Remove(c.id);UpdateDuration();RefreshAudioTrack(c.audioTrack);RetainWaves();ProjectChanged?.Invoke(); } else Rebuild();
        }

        public StudioClip Split(string id, float at)
        {
            var c = Find(id);
            if (c == null || at <= c.start || at >= c.start + c.duration) throw new ArgumentException("Split position outside clip");
            if(c.kind=="WAV") return SplitAudio(c,at);
            float local = at - c.start;
            int segmentIndex = (c.keyframes ?? new List<StudioKeyframe>()).Count(p => p.time <= local);
            var segment = GetSegment(id, segmentIndex);
            float fraction = (local - segment.start) / Mathf.Max(0.0001f, segment.end - segment.start);
            float boundaryX = Mathf.Lerp(segment.startX, segment.endX, fraction);
            float boundaryY = Mathf.Lerp(segment.startY, segment.endY, fraction);
            float boundaryZ = Mathf.Lerp(segment.startZ, segment.endZ, fraction);
            float boundaryScale = Mathf.Lerp(segment.startScale, segment.endScale, fraction);
            float boundaryRotation = Mathf.LerpAngle(segment.startRotation, segment.endRotation, fraction);
            float boundaryScaleX = Mathf.Lerp(segment.startScaleX, segment.endScaleX, fraction);
            float boundaryScaleY = Mathf.Lerp(segment.startScaleY, segment.endScaleY, fraction);
            var exactPoint = c.keyframes?.FirstOrDefault(p => Mathf.Abs(p.time - local) < 0.5f / project.fps);
            Checkpoint();
            var second = JsonUtility.FromJson<StudioClip>(JsonUtility.ToJson(c));
            second.id = Guid.NewGuid().ToString("N");
            second.start = at;
            if (c.kind == "Video" || c.kind == "WAV") second.sourceOffset += local;
            if (c.kind == "WAV") { second.fadeIn = 0; c.fadeOut = 0; }
            second.duration = c.start + c.duration - at;
            second.x = exactPoint == null ? boundaryX : exactPoint.nextX;
            second.y = exactPoint == null ? boundaryY : exactPoint.nextY;
            second.z = exactPoint == null ? boundaryZ : exactPoint.nextZ;
            second.scale = exactPoint == null ? boundaryScale : exactPoint.nextScale;
            second.rotation = exactPoint == null ? boundaryRotation : exactPoint.nextRotation;
            second.scaleX = exactPoint == null ? boundaryScaleX : exactPoint.nextScaleX; second.scaleY = exactPoint == null ? boundaryScaleY : exactPoint.nextScaleY;
            if (second.keyframes != null) { second.keyframes = second.keyframes.Where(k => k.time > at - c.start).ToList(); foreach (var key in second.keyframes) key.time -= at - c.start; }
            if (c.keyframes != null) c.keyframes = c.keyframes.Where(k => k.time < at - c.start).ToList();
            c.duration = at - c.start;
            c.endX = exactPoint == null ? boundaryX : exactPoint.x;
            c.endY = exactPoint == null ? boundaryY : exactPoint.y;
            c.endZ = exactPoint == null ? boundaryZ : exactPoint.z;
            c.endScale = exactPoint == null ? boundaryScale : exactPoint.scale;
            c.endRotation = exactPoint == null ? boundaryRotation : exactPoint.rotation;
            c.endScaleX = exactPoint == null ? boundaryScaleX : exactPoint.scaleX; c.endScaleY = exactPoint == null ? boundaryScaleY : exactPoint.scaleY;
            project.clips.Add(second);
            selectedId = second.id;
            Rebuild();
            return second;
        }

        public StudioClip Duplicate(string id)
        {
            var c = Find(id);
            if (c == null) throw new ArgumentException("Clip not found: " + id);
            Checkpoint();
            var copy = JsonUtility.FromJson<StudioClip>(JsonUtility.ToJson(c));
            copy.id = Guid.NewGuid().ToString("N");
            copy.groupId = "";
            copy.start += c.duration;
            project.clips.Add(copy);
            selectedId = copy.id;
            if(copy.kind=="WAV") { UpdateDuration();RefreshAudioTrack(copy.audioTrack);ProjectChanged?.Invoke(); } else Rebuild();
            return copy;
        }

        static string TransformMetadata(StudioClip c)
        {
            var copy = JsonUtility.FromJson<StudioClip>(JsonUtility.ToJson(c));
            copy.x=copy.y=copy.z=copy.endX=copy.endY=copy.endZ=copy.rotation=copy.endRotation=0;
            copy.scale=copy.endScale=copy.scaleX=copy.scaleY=copy.endScaleX=copy.endScaleY=1;
            copy.lockAspect=false; copy.keyframes=new List<StudioKeyframe>();
            return JsonUtility.ToJson(copy);
        }
        public void RefreshTransform()
        {
            if (!playing && director != null && director.playableAsset != null) { director.time=playhead; director.Evaluate(); }
            ProjectChanged?.Invoke();
        }
        bool RestoreTransformHistory(StudioProject restored)
        {
            if (project.clips.Count != restored.clips.Count || project.width != restored.width || project.height != restored.height || project.fps != restored.fps || project.visualLayerCount != restored.visualLayerCount || project.minimumDuration != restored.minimumDuration) return false;
            if (string.Join("|",project.groups.Select(g=>JsonUtility.ToJson(g))) != string.Join("|",restored.groups.Select(g=>JsonUtility.ToJson(g)))) return false;
            if (string.Join(",",project.hiddenVisualLayers) != string.Join(",",restored.hiddenVisualLayers) || string.Join(",",project.mutedAudioTracks) != string.Join(",",restored.mutedAudioTracks)) return false;
            var originals=project.clips.ToDictionary(c=>c.id);
            var changedAudio=new HashSet<int>();var changedVisual=new List<string>();
            for(int i=0;i<6;i++){var oldSettings=AudioTrackSettings(i);var restoredSettings=restored.audioTracks?.FirstOrDefault(t=>t.index==i);if(restoredSettings==null||JsonUtility.ToJson(oldSettings)!=JsonUtility.ToJson(restoredSettings))changedAudio.Add(i);}
            foreach (var c in restored.clips)
            {
                if(!originals.TryGetValue(c.id,out var old) || old.kind!=c.kind || old.path!=c.path || old.text!=c.text) return false;
                if(JsonUtility.ToJson(old)!=JsonUtility.ToJson(c))
                {
                    if(c.kind=="WAV") { changedAudio.Add(old.audioTrack);changedAudio.Add(c.audioTrack); }
                    else if(TransformMetadata(old)!=TransformMetadata(c))changedVisual.Add(c.id);
                }
            }
            for (int i=0;i<restored.clips.Count;i++) { var c=restored.clips[i]; var old=originals[c.id]; JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(c),old); restored.clips[i]=old; }
            project=restored;UpdateDuration();foreach(var track in changedAudio)RefreshAudioTrack(track);foreach(var id in changedVisual)CommitClipTiming(id,0);RefreshTransform(); return true;
        }

        public void Change(string id, Action<StudioClip> edit, bool checkpoint = true)
        {
            var c = Find(id);
            if (c == null) throw new ArgumentException("Clip not found: " + id);
            if (checkpoint) Checkpoint();
            int oldAudioTrack=c.audioTrack;string oldPath=c.path;string oldKind=c.kind;string oldText=c.text;
            string metadata = TransformMetadata(c);
            edit(c);
            c.start = Mathf.Max(0, c.start);
            c.duration = Mathf.Max(1f / project.fps, c.duration);
            c.layer = Mathf.Clamp(c.layer, 0, Mathf.Max(0, project.visualLayerCount - 1));
            if(c.kind=="WAV" && oldKind=="WAV") CommitClipTiming(c.id,oldAudioTrack);
            else if(c.kind!="WAV" && metadata==TransformMetadata(c)) RefreshTransform();
            else if(c.kind==oldKind && c.path==oldPath && c.text==oldText) CommitClipTiming(c.id,oldAudioTrack);
            else Rebuild();
        }

        public void NewProject(string title = "Untitled")
        {
            Checkpoint();
            project = new StudioProject { title = title };
            projectPath = "";
            selectedId = "";
            playhead = 0;
            ResetRenderTarget();
            Rebuild();
        }
        public void Save(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Project path required");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            File.WriteAllText(path, StudioProjectPaths.Serialize(project, path));
            projectPath = Path.GetFullPath(path);
            status = "保存しました: " + projectPath;
        }
        public void Load(string path)
        {
            var loaded = JsonUtility.FromJson<StudioProject>(File.ReadAllText(path));
            if (loaded == null || loaded.clips == null) throw new InvalidDataException("Invalid project JSON");
            StudioProjectPaths.Resolve(loaded, path);
            Checkpoint();
            project = loaded;
            if (project.visualLayerCount < 1) project.visualLayerCount = 8;
            if (project.groups == null) project.groups = new List<StudioGroup>();
            foreach (var group in project.groups) if (group.scale <= 0) group.scale = 1f;
            if (previewTexture == null || previewTexture.width != project.width || previewTexture.height != project.height) ResetRenderTarget();
            projectPath = Path.GetFullPath(path);
            selectedId = "";
            playhead = 0;
            Rebuild();
            status = "開きました: " + projectPath;
        }
        public void Seek(float seconds)
        {
            playhead = Mathf.Clamp(seconds, 0, project.duration);
            if (director.playableAsset == null) return;
            director.time = playhead;
            director.Evaluate();
            foreach(var ad in audioDirectors) if(ad.playableAsset!=null) { ad.time=playhead;ad.Evaluate(); }
            for(int i=0;i<audioSources.Count;i++)ConfigureTrackFilter(i);
        }
        public void TogglePlay() { playing = !playing; if (playing && playhead >= project.duration) Seek(0); }

        public void Rebuild()
        {
            playing = false;
            project.duration = Mathf.Max(1f / Mathf.Max(1, project.fps), project.minimumDuration,
                project.clips.Count == 0 ? 0 : project.clips.Max(c => c.start + c.duration));
            if (director.playableAsset != null) director.Stop();
            foreach (var entry in visuals)
            {
                var go = entry.Value;
                if (go == null) continue;
                go.SetActive(false);
                var renderer = go.GetComponent<Renderer>();
                if (renderer != null && renderer.sharedMaterial != null && go.GetComponent<TextMesh>() == null)
                    Destroy(renderer.sharedMaterial);
                Destroy(go);
            }
            foreach(var ad in audioDirectors) ad.Stop();
            RetainWaves();
            foreach (var video in videos.Values) if (video != null) { video.Stop(); Destroy(video); }
            visuals.Clear(); sounds.Clear(); videos.Clear(); visualBindings.Clear();
            MediaLoader.RetainImages(project.clips);
            if (timeline != null) Destroy(timeline);
            timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            timeline.name = project.title;
            var visualTracks = new VisualTrack[Mathf.Max(1, project.visualLayerCount)];
            for (int i = 0; i < visualTracks.Length; i++) visualTracks[i] = timeline.CreateTrack<VisualTrack>(null, "Layer " + (i + 1));
            foreach (var c in project.clips)
            {
                try
                {
                    if (c.kind == "WAV")
                    {
                        sounds[c.id] = CachedWave(c.path);
                    }
                    else
                    {
                        var go = MediaLoader.CreateVisual(c);
                        if (c.kind == "Video") videos[c.id] = go.GetComponent<VideoPlayer>();
                        go.layer = 30;
                        visuals[c.id] = go;
                        var tc = visualTracks[Mathf.Clamp(c.layer, 0, visualTracks.Length - 1)].CreateClip<VisualPlayableAsset>();
                        visualBindings[c.id]=tc;
                        var asset = (VisualPlayableAsset)tc.asset;
                        asset.controller = this; asset.layer = c.layer; asset.clip = c;
                        asset.target = go;
                        asset.fromPosition = new Vector3(c.x, c.y, c.z - c.layer * 0.01f);
                        asset.toPosition = new Vector3(c.endX, c.endY, c.endZ - c.layer * 0.01f);
                        asset.baseSize = go.transform.localScale;
                        asset.fromScale = c.scale;
                        asset.toScale = c.endScale;
                        asset.rotation = c.rotation;
                        asset.endRotation = c.endRotation;
                        asset.keyframes = c.keyframes;
                        var group = FindGroup(c.groupId);
                        asset.effects = c.effects == null ? new List<StudioEffect>() : new List<StudioEffect>(c.effects);
                        asset.groupEffects = group?.effects;
                        asset.groupStart = group == null ? 0 : GroupStart(group.id);
                        asset.clipStart = c.start;
                        asset.groupX = group?.x ?? 0;
                        asset.groupY = group?.y ?? 0;
                        asset.groupScale = group?.scale ?? 1;
                        asset.groupRotation = group?.rotation ?? 0;
                        asset.video = videos.ContainsKey(c.id) ? videos[c.id] : null;
                        asset.sourceOffset = c.sourceOffset;
                        tc.start = c.start;
                        tc.duration = c.duration;
                        tc.displayName = c.kind == "Text" ? c.text : (string.IsNullOrEmpty(c.path) ? c.kind : Path.GetFileName(c.path));
                    }
                }
                catch (Exception e) { status = "素材の読み込みエラー: " + e.Message; Debug.LogException(e); }
            }
            UpdateDuration();
            director.playableAsset = timeline;
            director.RebuildGraph();
            for(int i=0;i<6;i++) RefreshAudioTrack(i);
            Seek(playhead);
            ProjectChanged?.Invoke();
        }

        public AudioClip Sound(string id) { sounds.TryGetValue(id, out var clip); return clip; }
        public bool VideoFramesReady()
        {
            foreach (var c in project.clips)
            {
                if (c.kind != "Video" || playhead < c.start || playhead >= c.start + c.duration) continue;
                if (!videos.TryGetValue(c.id, out var player) || player == null || !player.GetComponent<StudioVideoFrame>().Ready) return false;
            }
            return true;
        }
        public void SetExportStatus(string message) { status = message; }
    }
}
