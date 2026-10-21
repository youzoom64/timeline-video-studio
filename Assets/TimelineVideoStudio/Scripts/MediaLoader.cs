using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Video;
using System.Diagnostics;

namespace TimelineVideoStudio
{
    public static class MediaLoader
    {
        static readonly Dictionary<string, Texture2D> imageCache = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string, long> imageVersions = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        public static void RetainImages(IEnumerable<StudioClip> clips)
        {
            var needed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var clip in clips) if (clip.kind == "Image") needed.Add(clip.path);
            var remove = new List<string>();
            foreach (var entry in imageCache)
                if (!needed.Contains(entry.Key) || !File.Exists(entry.Key) || File.GetLastWriteTimeUtc(entry.Key).Ticks != imageVersions[entry.Key])
                    remove.Add(entry.Key);
            foreach (var path in remove)
            {
                UnityEngine.Object.DestroyImmediate(imageCache[path]);
                imageCache.Remove(path);
                imageVersions.Remove(path);
            }
        }

        public static GameObject CreateVisual(StudioClip c)
        {
            GameObject go;
            if (c.kind == "Video")
            {
                if (!File.Exists(c.path)) throw new FileNotFoundException(c.path);
                go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = Path.GetFileName(c.path);
                var material = NewMaterial();
                go.GetComponent<MeshRenderer>().material = material;
                UnityEngine.Object.Destroy(go.GetComponent<Collider>());
                var player = go.AddComponent<VideoPlayer>();
                go.AddComponent<StudioVideoFrame>();
                player.playOnAwake = false;
                player.source = VideoSource.Url;
                player.url = c.path;
                player.audioOutputMode = VideoAudioOutputMode.None;
                player.renderMode = VideoRenderMode.MaterialOverride;
                player.targetMaterialRenderer = go.GetComponent<Renderer>();
                player.targetMaterialProperty = "_MainTex";
                player.isLooping = false;
                player.sendFrameReadyEvents = true;
                player.prepareCompleted += p =>
                {
                    go.transform.localScale = new Vector3((float)p.width / Mathf.Max(1, (float)p.height) * 4f, 4f, 1f);
                    p.Play();
                };
                player.frameReady += (p, frame) => p.Pause();
                player.Prepare();
            }
            else if (c.kind == "Image")
            {
                if (!File.Exists(c.path)) throw new FileNotFoundException(c.path);
                if (!imageCache.TryGetValue(c.path, out var texture) || texture == null)
                {
                    texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (!texture.LoadImage(File.ReadAllBytes(c.path), true))
                    {
                        UnityEngine.Object.DestroyImmediate(texture);
                        throw new InvalidDataException("Image decode failed: " + c.path);
                    }
                    imageCache[c.path] = texture;
                    imageVersions[c.path] = File.GetLastWriteTimeUtc(c.path).Ticks;
                }
                go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = Path.GetFileName(c.path);
                var material = NewMaterial();
                material.mainTexture = texture;
                go.GetComponent<MeshRenderer>().material = material;
                go.transform.localScale = new Vector3((float)texture.width / texture.height * 4f, 4f, 1f);
                UnityEngine.Object.Destroy(go.GetComponent<Collider>());
            }
            else if (c.kind == "Text")
            {
                go = new GameObject("Text");
                var text = go.AddComponent<TextMesh>();
                text.text = c.text;
                text.fontSize = 72;
                text.characterSize = 0.05f;
                text.anchor = TextAnchor.MiddleCenter;
                text.alignment = TextAlignment.Center;
                text.color = Color.white;
                var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (font != null)
                {
                    text.font = font;
                    go.GetComponent<MeshRenderer>().material = font.material;
                }
            }
            else if (c.kind == "OBJ")
            {
                if (!File.Exists(c.path)) throw new FileNotFoundException(c.path);
                go = new GameObject(Path.GetFileName(c.path));
                go.AddComponent<MeshFilter>().mesh = LoadObj(c.path);
                go.AddComponent<MeshRenderer>().material = NewMaterial();
            }
            else
            {
                go = GameObject.CreatePrimitive(c.kind == "Sphere" ? PrimitiveType.Sphere : PrimitiveType.Cube);
                go.name = c.kind;
                go.GetComponent<MeshRenderer>().material = NewMaterial();
            }
            if (c.kind != "Video") go.SetActive(false);
            return go;
        }

        public static float GetVideoDuration(string path)
        {
            var tools = StudioToolInstaller.Begin();
            if (!tools.IsCompleted) throw new InvalidOperationException("FFmpeg / FFprobe を自動取得しています。準備完了後に動画を追加してください。");
            if (tools.IsFaulted) throw new InvalidOperationException(StudioToolInstaller.Error);
            string ffprobe = StudioExternalTools.Resolve("ffprobe");
            if (!File.Exists(ffprobe)) throw new FileNotFoundException("FFprobe not found", ffprobe);
            var process = new Process { StartInfo = new ProcessStartInfo(ffprobe,
                "-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"" + path.Replace("\"", "") + "\"")
                { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true } };
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0 || !float.TryParse(output.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float duration) || duration <= 0)
                throw new InvalidDataException("Video duration could not be read: " + path);
            return duration;
        }

        static Material NewMaterial()
        {
            var baseMaterial = Resources.Load<Material>("StudioUnlit");
            if (baseMaterial == null) throw new InvalidOperationException("StudioUnlit material is missing");
            return new Material(baseMaterial);
        }

        static Mesh LoadObj(string path)
        {
            var source = new List<Vector3>();
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var culture = CultureInfo.InvariantCulture;
            foreach (var raw in File.ReadLines(path))
            {
                string line = raw.Trim();
                if (line.StartsWith("v "))
                {
                    var p = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (p.Length >= 4) source.Add(new Vector3(float.Parse(p[1], culture), float.Parse(p[2], culture), float.Parse(p[3], culture)));
                }
                else if (line.StartsWith("f "))
                {
                    var p = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    var face = new List<int>();
                    for (int i = 1; i < p.Length; i++)
                    {
                        var part = p[i].Split('/');
                        int n = int.Parse(part[0], culture);
                        int index = n < 0 ? source.Count + n : n - 1;
                        if (index < 0 || index >= source.Count) throw new InvalidDataException("OBJ face index out of range");
                        face.Add(vertices.Count);
                        vertices.Add(source[index]);
                    }
                    for (int i = 1; i + 1 < face.Count; i++)
                    {
                        triangles.Add(face[0]); triangles.Add(face[i]); triangles.Add(face[i + 1]);
                    }
                }
            }
            if (triangles.Count == 0) throw new InvalidDataException("OBJ has no faces");
            var mesh = new Mesh();
            if (vertices.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static AudioClip LoadWav(string path)
        {
            using (var fs = File.OpenRead(path))
            using (var br = new BinaryReader(fs))
            {
                if (new string(br.ReadChars(4)) != "RIFF") throw new InvalidDataException("Not RIFF WAV");
                br.ReadInt32();
                if (new string(br.ReadChars(4)) != "WAVE") throw new InvalidDataException("Not WAVE");
                short format = 0, channels = 0, bits = 0;
                int rate = 0;
                byte[] pcm = null;
                while (fs.Position + 8 <= fs.Length)
                {
                    string chunk = new string(br.ReadChars(4));
                    int size = br.ReadInt32();
                    long next = fs.Position + size + (size & 1);
                    if (size < 0 || next > fs.Length + 1) throw new InvalidDataException("Invalid WAV chunk");
                    if (chunk == "fmt ")
                    {
                        format = br.ReadInt16(); channels = br.ReadInt16(); rate = br.ReadInt32();
                        br.ReadInt32(); br.ReadInt16(); bits = br.ReadInt16();
                    }
                    else if (chunk == "data") pcm = br.ReadBytes(size);
                    fs.Position = next;
                }
                if (format != 1 || bits != 16 || channels < 1 || channels > 2 || pcm == null || rate <= 0)
                    throw new InvalidDataException("WAV must be 16-bit PCM mono or stereo");
                int samples = pcm.Length / 2;
                var data = new float[samples];
                for (int i = 0; i < samples; i++) data[i] = BitConverter.ToInt16(pcm, i * 2) / 32768f;
                var clip = AudioClip.Create(Path.GetFileName(path), samples / channels, channels, rate, false);
                clip.SetData(data, 0);
                return clip;
            }
        }
    }
}
