using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using UnityEngine;

namespace TimelineVideoStudio
{
    [Serializable]
    public class StudioCommand
    {
        public string op;
        public string id;
        public string[] ids;
        public string effectId;
        public string plugin;
        public string kind;
        public string path;
        public string text;
        public string title;
        public string field;
        public string value;
        public float start = -1;
        public float at;
        public float seconds;
        public int layer;
        public bool gpu = true;
        public int segment;
        public StudioSegment values;
    }

    [Serializable]
    public class StudioApiState
    {
        public bool ok = true;
        public string error = "";
        public string status;
        public string projectPath;
        public string selectedId;
        public float playhead;
        public bool playing;
        public float previewAudioPeak;
        public StudioProject project;
        public StudioPluginDefinition[] plugins;
        public StudioSegment segmentValues;
        public float timelineZoom;
        public float timelineScrollSeconds;
        public int timelineFirstRow;
        public float timelineHeight;
        public float browserWidth,inspectorWidth;
        public StudioAudioEffectDefinition[] audioEffectDefinitions;
        public string[] audioPresets;
        public bool audioEffectsBusy;
        public string audioEffectsError;
    }

    public class StudioApi : MonoBehaviour
    {
        public int port = 18764;
        StudioController studio;
        HttpListener listener;
        Thread thread;
        readonly ConcurrentQueue<ApiJob> jobs = new ConcurrentQueue<ApiJob>();
        volatile bool running;
        StudioSegment lastSegment;

        class ApiJob
        {
            public string op;
            public string body;
            public byte[] result;
            public string mime = "application/json; charset=utf-8";
            public readonly ManualResetEventSlim done = new ManualResetEventSlim(false);
        }

        void Start()
        {
            studio = GetComponent<StudioController>();
            try
            {
                string configuredPort = Environment.GetEnvironmentVariable("TIMELINE_STUDIO_API_PORT");
                if (int.TryParse(configuredPort, out int requestedPort) && requestedPort >= 1024 && requestedPort <= 65535)
                    port = requestedPort;
                listener = new HttpListener();
                listener.Prefixes.Add("http://127.0.0.1:" + port + "/");
                listener.Start();
                running = true;
                thread = new Thread(ListenLoop) { IsBackground = true, Name = "Timeline Video Studio API" };
                thread.Start();
                Debug.Log("Studio API: http://127.0.0.1:" + port);
            }
            catch (Exception e) { Debug.LogError("Studio API unavailable: " + e.Message); }
        }

        void ListenLoop()
        {
            while (running)
            {
                HttpListenerContext context;
                try { context = listener.GetContext(); }
                catch { break; }
                ThreadPool.QueueUserWorkItem(_ => Handle(context));
            }
        }

        void Handle(HttpListenerContext context)
        {
            try
            {
                var path = context.Request.Url.AbsolutePath.Trim('/').ToLowerInvariant();
                var job = new ApiJob { op = path };
                if (context.Request.HttpMethod == "POST")
                {
                    using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8)) job.body = reader.ReadToEnd();
                }
                jobs.Enqueue(job);
                if (!job.done.Wait(60000)) throw new TimeoutException("Editor did not answer within 60 seconds");
                context.Response.ContentType = job.mime;
                context.Response.AddHeader("Access-Control-Allow-Origin", "http://127.0.0.1:" + port);
                context.Response.ContentLength64 = job.result.Length;
                context.Response.OutputStream.Write(job.result, 0, job.result.Length);
            }
            catch (Exception e)
            {
                try
                {
                    var bytes = Encoding.UTF8.GetBytes("{\"ok\":false,\"error\":\"" + e.Message.Replace("\"", "'") + "\"}");
                    context.Response.ContentType = "application/json";
                    context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                }
                catch { }
            }
            finally { try { context.Response.Close(); } catch { } }
        }

        void Update()
        {
            int count = 0;
            while (count++ < 20 && jobs.TryDequeue(out var job))
            {
                try
                {
                    if (job.op == "frame")
                    {
                        job.result = Capture(); job.mime = "image/png";
                    }
                    else
                    {
                        var command = string.IsNullOrWhiteSpace(job.body) ? new StudioCommand { op = job.op } : JsonUtility.FromJson<StudioCommand>(job.body);
                        if (command == null) command = new StudioCommand();
                        if (string.IsNullOrEmpty(command.op)) command.op = job.op;
                        Execute(command);
                        job.result = Encoding.UTF8.GetBytes(JsonUtility.ToJson(State()));
                    }
                }
                catch (Exception e)
                {
                    job.result = Encoding.UTF8.GetBytes(JsonUtility.ToJson(new StudioApiState { ok = false, error = e.Message, status = studio.status }));
                }
                finally { job.done.Set(); }
            }
        }

        public StudioApiState State()
        {
            var window = GetComponent<StudioWindow>();
            return new StudioApiState { project = studio.project, projectPath = studio.projectPath, selectedId = studio.selectedId,
                playhead = studio.playhead, playing = studio.playing, previewAudioPeak = studio.previewAudioPeak, status = studio.status, plugins = StudioPlugins.List().ToArray(), segmentValues = lastSegment,
                timelineZoom = window.TimelineZoom, timelineScrollSeconds = window.TimelineScrollSeconds, timelineFirstRow = window.TimelineFirstRow, timelineHeight = window.TimelineHeight,browserWidth=window.PanelWidths.x,inspectorWidth=window.PanelWidths.y, audioEffectDefinitions=StudioAudioDsp.Definitions,audioPresets=StudioAudioDsp.Presets,audioEffectsBusy=studio.AudioEffectsBusy,audioEffectsError=studio.audioEffectsError };
        }

        public void Execute(StudioCommand c)
        {
            switch (c.op)
            {
                case "state": break;
                case "project.new": studio.NewProject(c.title ?? "Untitled"); break;
                case "project.save": studio.Save(c.path); break;
                case "project.load": studio.Load(c.path); break;
                case "project.set": SetProject(c.field, c.value); break;
                case "clip.add": studio.Add(c.kind, c.path, c.text, c.start, c.layer); break;
                case "layer.insert": studio.InsertLayer(c.layer); break;
                case "group.create": studio.Group(c.ids ?? throw new ArgumentException("Clip ids required")); break;
                case "layer.toggle": studio.ToggleLayer(c.kind == "WAV", c.layer); break;
                case "group.select": if (studio.FindGroup(c.id) == null) throw new ArgumentException("Group not found"); studio.selectedId = c.id; break;
                case "group.move": studio.MoveGroup(c.id, c.start); break;
                case "group.set": studio.ChangeGroup(c.id, g => SetGroup(g, c.field, c.value)); break;
                case "group.delete": studio.Ungroup(c.id); break;
                case "group.effect.add": studio.AddGroupEffect(c.id, c.plugin); break;
                case "group.effect.set": studio.SetGroupEffect(c.id, c.effectId, effect => SetEffect(effect, c.field, c.value)); break;
                case "group.effect.delete": studio.DeleteGroupEffect(c.id, c.effectId); break;
                case "clip.delete": studio.Delete(c.id); break;
                case "clip.duplicate": studio.Duplicate(c.id); break;
                case "clip.split": studio.Split(c.id, c.at); break;
                case "clip.set": studio.Change(c.id, clip => SetClip(clip, c.field, c.value)); break;
                case "point.add": studio.AddKeyframe(c.id, c.at); break;
                case "point.delete": studio.DeleteKeyframe(c.id, c.at); break;
                case "point.set": studio.SetKeyframe(c.id, c.at, point => SetPoint(point, c.field, c.value)); break;
                case "segment.get": lastSegment = studio.GetSegment(c.id, c.segment); break;
                case "segment.set": studio.SetSegment(c.id, c.segment, c.values ?? throw new ArgumentException("Segment values required")); lastSegment = studio.GetSegment(c.id, c.segment); break;
                case "plugin.list": break;
                case "effect.add": studio.AddEffect(c.id, c.plugin); break;
                case "effect.set": studio.SetEffect(c.id, c.effectId, effect => SetEffect(effect, c.field, c.value)); break;
                case "effect.delete": studio.DeleteEffect(c.id, c.effectId); break;
                case "clip.select": if (studio.Find(c.id) == null) throw new ArgumentException("Clip not found"); studio.selectedId = c.id; break;
                case "transport.seek": studio.Seek(c.seconds); break;
                case "transport.play": studio.playing = true; break;
                case "transport.pause": studio.playing = false; break;
                case "transport.toggle": studio.TogglePlay(); break;
                case "timeline.zoom": GetComponent<StudioWindow>().SetTimelineZoom(Number(c.value)); break;
                case "timeline.scroll": GetComponent<StudioWindow>().SetTimelineScroll(c.seconds); break;
                case "timeline.vertical": GetComponent<StudioWindow>().ScrollTimelineRows(int.Parse(c.value)); break;
                case "timeline.height": GetComponent<StudioWindow>().TimelineHeight = Number(c.value); break;
                case "layout.browserWidth": GetComponent<StudioWindow>().SetPanelWidth(true,Number(c.value)); break;
                case "layout.inspectorWidth": GetComponent<StudioWindow>().SetPanelWidth(false,Number(c.value)); break;
                case "history.undo": studio.Undo(); break;
                case "history.redo": studio.Redo(); break;
                case "export.mp4": var exporter = GetComponent<StudioExporter>(); exporter.useGpuEncoding = c.gpu; exporter.StartExport(c.path); break;
                case "export.cancel": GetComponent<StudioExporter>().CancelExport(); break;
                case "audio.preset": case "audio.effect.add": case "audio.effect.set": case "audio.effect.delete": case "audio.effect.move": case "audio.effects.bypass": AudioCommand(c); break;
                default: throw new ArgumentException("Unknown operation: " + c.op);
            }
        }

        static float Number(string value) { return float.Parse(value, CultureInfo.InvariantCulture); }
        void SetProject(string field, string value)
        {
            studio.Checkpoint();
            switch (field)
            {
                case "title": studio.project.title = value; break;
                case "duration": studio.project.minimumDuration = Mathf.Max(0f, Number(value)); break;
                case "width": studio.project.width = Mathf.Clamp(int.Parse(value), 320, 3840); studio.ResetRenderTarget(); break;
                case "height": studio.project.height = Mathf.Clamp(int.Parse(value), 180, 2160); studio.ResetRenderTarget(); break;
                case "fps": studio.project.fps = Mathf.Clamp(int.Parse(value), 1, 60); break;
                case "bpm": studio.project.bpm = Mathf.Clamp(Number(value), 20f, 300f); break;
                default: throw new ArgumentException("Unknown project field: " + field);
            }
            studio.Rebuild();
        }
        void AudioCommand(StudioCommand c)
        {
            int track=Mathf.Clamp(c.layer,0,5);var list=studio.AudioEffects(c.id,track);
            if(c.op=="audio.effects.bypass"){studio.BypassAudioEffects(c.id,track,bool.Parse(c.value));return;}
            if(c.op=="audio.preset"){var preset=StudioAudioDsp.Preset(c.text);studio.EditAudioEffects(c.id,track,e=>{e.Clear();e.AddRange(preset);});return;}
            if(c.op=="audio.effect.add"){var added=StudioAudioDsp.Create(c.plugin);studio.EditAudioEffects(c.id,track,e=>e.Add(added));return;}
            var effect=list.FirstOrDefault(e=>e.id==c.effectId)??throw new ArgumentException("Audio effect not found");
            if(c.op=="audio.effect.delete"){studio.EditAudioEffects(c.id,track,e=>e.Remove(effect));return;}
            if(c.op=="audio.effect.move"){int at=Mathf.Clamp(c.segment,0,list.Count-1);studio.EditAudioEffects(c.id,track,e=>{e.Remove(effect);e.Insert(at,effect);});return;}
            if(c.field=="enabled"){bool enabled=bool.Parse(c.value);studio.EditAudioEffects(c.id,track,e=>effect.enabled=enabled);return;}
            float value=Number(c.value);if(float.IsNaN(value)||float.IsInfinity(value))throw new ArgumentException("Finite audio parameter required");
            var def=StudioAudioDsp.Definitions.First(d=>d.kind==effect.kind);int parameter=c.field=="a"?0:c.field=="b"?1:c.field=="c"?2:-1;
            if(c.field=="mix")value=Mathf.Clamp01(value);else if(parameter>=0&&parameter<def.labels.Length)value=Mathf.Clamp(value,def.min[parameter],def.max[parameter]);else throw new ArgumentException("Unknown audio parameter");
            studio.EditAudioEffects(c.id,track,e=>{if(c.field=="a")effect.a=value;else if(c.field=="b")effect.b=value;else if(c.field=="c")effect.c=value;else effect.mix=value;});
        }
        static void SetClip(StudioClip clip, string field, string value)
        {
            switch (field)
            {
                case "start": clip.start = Number(value); break;
                case "duration": clip.duration = Number(value); break;
                case "layer": clip.layer = int.Parse(value); break;
                case "audioTrack": clip.audioTrack = Mathf.Clamp(int.Parse(value), 0, 5); break;
                case "loop": clip.loop = bool.Parse(value); break;
                case "volume": clip.volume = Mathf.Clamp(Number(value), 0, 4); break;
                case "fadeIn": clip.fadeIn = Mathf.Max(0, Number(value)); break;
                case "fadeOut": clip.fadeOut = Mathf.Max(0, Number(value)); break;
                case "sourceOffset": clip.sourceOffset = Mathf.Max(0, Number(value)); break;
                case "x": clip.x = Number(value); break;
                case "y": clip.y = Number(value); break;
                case "z": clip.z = Number(value); break;
                case "scaleX": if (clip.lockAspect) clip.scaleY = clip.scaleX > .000001f ? clip.scaleY * Mathf.Max(0,Number(value)) / clip.scaleX : Mathf.Max(0,Number(value)); clip.scaleX = Mathf.Max(0,Number(value)); break;
                case "scaleY": if (clip.lockAspect) clip.scaleX = clip.scaleY > .000001f ? clip.scaleX * Mathf.Max(0,Number(value)) / clip.scaleY : Mathf.Max(0,Number(value)); clip.scaleY = Mathf.Max(0,Number(value)); break;
                case "endScaleX": if (clip.lockAspect) clip.endScaleY = clip.endScaleX > .000001f ? clip.endScaleY * Mathf.Max(0,Number(value)) / clip.endScaleX : Mathf.Max(0,Number(value)); clip.endScaleX = Mathf.Max(0,Number(value)); break;
                case "endScaleY": if (clip.lockAspect) clip.endScaleX = clip.endScaleY > .000001f ? clip.endScaleX * Mathf.Max(0,Number(value)) / clip.endScaleY : Mathf.Max(0,Number(value)); clip.endScaleY = Mathf.Max(0,Number(value)); break;
                case "lockAspect": clip.lockAspect=bool.Parse(value); break;
                case "scale": clip.scale = Number(value); break;
                case "endX": clip.endX = Number(value); break;
                case "endY": clip.endY = Number(value); break;
                case "endZ": clip.endZ = Number(value); break;
                case "endScale": clip.endScale = Number(value); break;
                case "rotation": clip.rotation = Number(value); break;
                case "endRotation": clip.endRotation = Number(value); break;
                case "text": clip.text = value; break;
                case "path": clip.path = value; break;
                default: throw new ArgumentException("Unknown clip field: " + field);
            }
        }
        static void SetGroup(StudioGroup group, string field, string value)
        {
            switch (field)
            {
                case "name": group.name = value; break;
                case "x": group.x = Number(value); break;
                case "y": group.y = Number(value); break;
                case "scale": group.scale = Number(value); break;
                case "rotation": group.rotation = Number(value); break;
                default: throw new ArgumentException("Unknown group field: " + field);
            }
        }
        static void SetPoint(StudioKeyframe point, string field, string value)
        {
            switch (field)
            {
                case "nextScaleY": point.nextScaleY = Mathf.Max(0,Number(value)); break;
                case "nextScaleX": point.nextScaleX = Mathf.Max(0,Number(value)); break;
                case "scaleY": point.scaleY = Mathf.Max(0,Number(value)); break;
                case "scaleX": point.scaleX = Mathf.Max(0,Number(value)); break;
                case "x": point.x = Number(value); break;
                case "y": point.y = Number(value); break;
                case "z": point.z = Number(value); break;
                case "scale": point.scale = Mathf.Max(0.001f, Number(value)); break;
                case "rotation": point.rotation = Number(value); break;
                case "nextX": point.nextX = Number(value); break;
                case "nextY": point.nextY = Number(value); break;
                case "nextZ": point.nextZ = Number(value); break;
                case "nextScale": point.nextScale = Number(value); break;
                case "nextRotation": point.nextRotation = Number(value); break;
                default: throw new ArgumentException("Unknown point field: " + field);
            }
        }
        static void SetEffect(StudioEffect effect, string field, string value)
        {
            switch (field)
            {
                case "a": effect.a = Number(value); break;
                case "b": effect.b = Number(value); break;
                case "c": effect.c = Number(value); break;
                case "d": effect.d = Number(value); break;
                case "enabled": effect.enabled = bool.Parse(value); break;
                default: throw new ArgumentException("Unknown effect field: " + field);
            }
        }

        byte[] Capture()
        {
            studio.Seek(studio.playhead);
            studio.previewCamera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = studio.previewTexture;
            var texture = new Texture2D(studio.project.width, studio.project.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            texture.Apply();
            byte[] data = texture.EncodeToPNG();
            Destroy(texture);
            RenderTexture.active = previous;
            return data;
        }

        void OnDestroy()
        {
            running = false;
            try { listener?.Stop(); listener?.Close(); } catch { }
        }
    }
}
