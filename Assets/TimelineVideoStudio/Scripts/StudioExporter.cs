using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace TimelineVideoStudio
{
    public class StudioExporter : MonoBehaviour
    {
        public static string FfmpegPath => StudioExternalTools.Resolve("ffmpeg");
        StudioController studio;
        public bool busy;
        public float progress;
        public bool useGpuEncoding = true;
        CancellationTokenSource cancellation;
        Process encoder;
        public int renderedFrames, reusedFrames;
        void Awake() { studio = GetComponent<StudioController>(); useGpuEncoding = SystemInfo.graphicsDeviceVendor.IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) >= 0; }

        public void CancelExport()
        {
            if (!busy) return;
            cancellation?.Cancel();
            try { if (encoder != null && !encoder.HasExited) encoder.Kill(); } catch { }
            studio.status = "出力をキャンセルしています…";
        }
        public void StartExport(string path)
        {
            if (busy) throw new InvalidOperationException("Export already running");
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Output MP4 path required");
            cancellation = new CancellationTokenSource();
            StartCoroutine(Export(Path.GetFullPath(path), cancellation.Token));
        }
        IEnumerator Export(string output, CancellationToken token)
        {
            busy = true; progress = 0; renderedFrames = 0; reusedFrames = 0;
            studio.playing = false;
            string temp = Path.Combine(Path.GetDirectoryName(output), ".tvs_export_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            string pending = Path.Combine(temp, "output.mp4");
            int count = 0;
            float oldTime = studio.playhead;
            Process process = null; Texture2D texture = null;
            BlockingCollection<byte[]> queue = null; Task writer = null;
            var watch = Stopwatch.StartNew();
            try
            {
                var tools = StudioToolInstaller.Begin();
                while (!tools.IsCompleted) { if (token.IsCancellationRequested) yield break; studio.status = StudioToolInstaller.Status; yield return null; }
                if (tools.IsFaulted) { studio.status = "Export failed: " + StudioToolInstaller.Error; yield break; }
                while(studio.AudioEffectsBusy) { if(token.IsCancellationRequested)yield break;yield return null; }
                if(!string.IsNullOrEmpty(studio.audioEffectsError)){studio.status="Export failed: "+studio.audioEffectsError;yield break;}
                count=Mathf.CeilToInt(studio.project.duration*studio.project.fps);
                string audio = Path.Combine(temp, "audio.wav"); bool hasAudio = false;
                studio.status = "音声を書き出しています…";
                yield return MixAudio(audio, studio.project.duration, result => hasAudio = result, token);
                if (token.IsCancellationRequested) yield break;
                string error = ""; Exception startError = null;
                try
                {
                    string args = "-y -hide_banner -loglevel error -f rawvideo -pixel_format rgb24 -video_size " + studio.project.width + "x" + studio.project.height + " -framerate " + studio.project.fps + " -i pipe:0";
                    if (hasAudio) args += " -i " + Quote(audio);
                    args += " -vf vflip";
                    args += useGpuEncoding ? " -c:v h264_nvenc -pix_fmt yuv420p -preset p4 -cq 19" : " -c:v libx264 -pix_fmt yuv420p -crf 18 -preset veryfast";
                    if (hasAudio) args += " -c:a aac -b:a 192k -shortest";
                    args += " -frames:v " + count + " " + Quote(pending);
                    process = new Process { StartInfo = new ProcessStartInfo(FfmpegPath, args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardError = true, RedirectStandardOutput = true } };
                    process.Start(); encoder = process;
                    process.ErrorDataReceived += (_, e) => { if (e.Data != null) error = e.Data; };
                    process.BeginErrorReadLine(); process.BeginOutputReadLine();
                    queue = new BlockingCollection<byte[]>(4);
                    var writeQueue = queue; var writeProcess = process;
                    writer = Task.Run(() => {
                        try {
                            foreach (var bytes in writeQueue.GetConsumingEnumerable(token)) writeProcess.StandardInput.BaseStream.Write(bytes, 0, bytes.Length);
                            writeProcess.StandardInput.Close();
                        } catch { if (!token.IsCancellationRequested) throw; }
                    });
                    texture = new Texture2D(studio.project.width, studio.project.height, TextureFormat.RGB24, false);
                }
                catch (Exception e) { startError = e; }
                if (startError != null) { studio.status = "Export failed: " + startError.Message; yield break; }
                byte[] last = null; long previousSignature = 0;
                for (int frame = 0; frame < count; frame++)
                {
                    if (token.IsCancellationRequested) yield break;
                    if (writer.IsFaulted || process.HasExited) { studio.status = "Export failed: " + error; yield break; }
                    float time = (float)frame / studio.project.fps;
                    bool animated; long signature = VisualSignature(time, out animated);
                    if (last == null || animated || signature != previousSignature)
                    {
                        studio.Seek(time);
                        float waitStart = Time.realtimeSinceStartup;
                        while (!studio.VideoFramesReady() && Time.realtimeSinceStartup - waitStart < 10f) { if (token.IsCancellationRequested) yield break; yield return null; }
                        if (!studio.VideoFramesReady()) { studio.status = "Export failed: 動画フレームの読み込みがタイムアウトしました"; yield break; }
                        Exception renderError = null;
                        try { last = ReadFrame(texture); renderedFrames++; }
                        catch (Exception e) { renderError = e; }
                        if (renderError != null) { studio.status = "Export failed: " + renderError.Message; yield break; }
                    }
                    else { studio.playhead = time; reusedFrames++; }
                    previousSignature = signature;
                    while (!queue.TryAdd(last)) { if (token.IsCancellationRequested) yield break; if (writer.IsFaulted || process.HasExited) { studio.status = "Export failed: " + error; yield break; } yield return null; }
                    progress = (float)(frame + 1) / count * .98f;
                    studio.status = "MP4出力 " + (frame + 1) + " / " + count + "（再利用 " + reusedFrames + "）";
                    if (frame % 24 == 0) yield return null;
                }
                queue.CompleteAdding();
                while (!process.HasExited) { if (token.IsCancellationRequested) yield break; studio.status = "MP4を仕上げています…"; yield return null; }
                if (process.ExitCode != 0 || !File.Exists(pending)) { studio.status = "FFmpeg failed: " + error; yield break; }
                File.Copy(pending, output, true);
                progress = 1; studio.status = "Exported: " + output;
                UnityEngine.Debug.Log("EXPORT_STATS seconds=" + watch.Elapsed.TotalSeconds.ToString("F2") + " rendered=" + renderedFrames + " reused=" + reusedFrames);
            }
            finally
            {
                if (process != null) { try { if (!process.HasExited) process.Kill(); } catch { } }
                if (encoder == process) encoder = null;
                if (texture != null) Destroy(texture);
                if (token.IsCancellationRequested) { studio.status = "出力をキャンセルしました"; progress = 0; }
                studio.Seek(oldTime); busy = false;
                Task.Run(() => { try { if (writer != null) writer.Wait(5000); } catch { } try { process?.Dispose(); queue?.Dispose(); Directory.Delete(temp, true); } catch { } });
            }
        }
        long VisualSignature(float time, out bool animated)
        {
            animated = false; long signature = 17;
            foreach (var c in studio.project.clips)
            {
                if (c.kind == "WAV" || time < c.start || time >= c.start + c.duration || !studio.VisualLayerEnabled(c.layer)) continue;
                unchecked { signature = signature * 31 + c.id.GetHashCode(); }
                if (c.scaleX != c.endScaleX || c.scaleY != c.endScaleY || c.kind == "Video" || c.x != c.endX || c.y != c.endY || c.z != c.endZ || c.scale != c.endScale || c.rotation != c.endRotation || (c.keyframes != null && c.keyframes.Count > 0)) animated = true;
                if (c.effects != null) foreach (var e in c.effects) if (e.enabled) animated = true;
                var group = studio.FindGroup(c.groupId);
                if (group?.effects != null) foreach (var e in group.effects) if (e.enabled) animated = true;
            }
            return signature;
        }
        byte[] ReadFrame(Texture2D texture)
        {
            studio.previewCamera.Render(); var previous = RenderTexture.active;
            try { RenderTexture.active = studio.previewTexture; texture.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0, false); return texture.GetRawTextureData<byte>().ToArray(); }
            finally { RenderTexture.active = previous; }
        }
        static string Quote(string path) { return "\"" + path.Replace("\"", "") + "\""; }

        IEnumerator MixAudio(string path, float duration, Action<bool> complete, System.Threading.CancellationToken token)
        {
            var entries = new List<(StudioClip clip, AudioClip audio, float[] data, float duration, float offset, int track, bool loop)>();
            foreach (var c in studio.project.clips)
            {
                if (c.kind != "WAV") continue;
                if (!studio.AudioTrackEnabled(c.audioTrack)) continue;
                if (token.IsCancellationRequested) yield break;
                var a = studio.Sound(c.id);
                if (a == null) continue;
                var data = new float[a.samples * a.channels];
                a.GetData(data, 0);
                entries.Add((c, a, data, studio.AudioPlaybackDuration(c), studio.AudioSourceOffset(c), Mathf.Clamp(c.audioTrack,0,5),c.loop&&!studio.UsesProcessedAudio(c)));
                if ((entries.Count & 15) == 0) yield return null;
            }
            if (entries.Count == 0) { complete(false); yield break; }
            const int rate = 44100;
            int total = Mathf.CeilToInt(duration * rate);
            using (var fs = File.Create(path))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); bw.Write(36 + total * 4);
                bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); bw.Write(16);
                bw.Write((short)1); bw.Write((short)2); bw.Write(rate); bw.Write(rate * 4); bw.Write((short)4); bw.Write((short)16);
                bw.Write(System.Text.Encoding.ASCII.GetBytes("data")); bw.Write(total * 4);
                const int blockSize = 4096;
                var left=new float[6][];var right=new float[6][];var filters=new StudioAudioChain[6];
                for(int track=0;track<6;track++){left[track]=new float[blockSize];right[track]=new float[blockSize];filters[track]=studio.CreateTrackAudioChain(track,rate);}
                for (int block = 0; block < total; block += blockSize)
                {
                    if (token.IsCancellationRequested) yield break;
                    if ((block / blockSize & 63) == 0) yield return null;
                    int count = Mathf.Min(blockSize, total - block);
                    for(int track=0;track<6;track++){System.Array.Clear(left[track],0,count);System.Array.Clear(right[track],0,count);}
                    foreach (var e in entries)
                    {
                        float clipDuration=e.duration;
                        int first = Mathf.Max(block, Mathf.CeilToInt(e.clip.start * rate));
                        int last = Mathf.Min(block + count, Mathf.CeilToInt((e.clip.start + clipDuration) * rate));
                        for (int i = first; i < last; i++)
                        {
                            double local = (double)i / rate - e.clip.start;
                            int sample = (int)((local + e.offset) * e.audio.frequency);
                            if(e.loop&&e.audio.samples>0)sample=((sample%e.audio.samples)+e.audio.samples)%e.audio.samples;
                            if (sample < 0 || sample >= e.audio.samples) continue;
                            int offset = sample * e.audio.channels;
                            float gain = e.clip.volume;
                            float fadeIn = Mathf.Min(e.clip.fadeIn, clipDuration / 2);
                            float fadeOut = Mathf.Min(e.clip.fadeOut, clipDuration / 2);
                            if (fadeIn > 0) gain *= Mathf.Clamp01((float)local / fadeIn);
                            if (fadeOut > 0) gain *= Mathf.Clamp01((clipDuration - (float)local) / fadeOut);
                            left[e.track][i - block] += e.data[offset] * gain;
                            right[e.track][i - block] += e.data[offset + (e.audio.channels == 2 ? 1 : 0)] * gain;
                        }
                    }
                    for (int i = 0; i < count; i++)
                    {
                        float l=0,r=0;
                        for(int track=0;track<6;track++)if(studio.AudioTrackEnabled(track)){float tl=left[track][i],tr=right[track][i];filters[track].Process(ref tl,ref tr);l+=tl;r+=tr;}
                        bw.Write((short)(Mathf.Clamp(l,-1,1)*32767));bw.Write((short)(Mathf.Clamp(r,-1,1)*32767));
                    }
                }
            }
            complete(true);
        }
    }
}
