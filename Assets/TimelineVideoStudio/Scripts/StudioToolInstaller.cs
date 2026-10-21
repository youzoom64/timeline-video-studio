using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace TimelineVideoStudio
{
    public static class StudioToolInstaller
    {
        const string DownloadUrl = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";
        static Task installation;
        public static volatile string Status = "";
        public static bool Running => installation != null && !installation.IsCompleted;
        public static string Error => installation != null && installation.IsFaulted ? installation.Exception.GetBaseException().Message : "";

        public static Task Begin()
        {
            if (installation != null && !installation.IsFaulted) return installation;
            try
            {
                StudioExternalTools.Resolve("ffmpeg"); StudioExternalTools.Resolve("ffprobe");
                Status = ""; return installation = Task.CompletedTask;
            }
            catch (FileNotFoundException) { }
            string folder = StudioExternalTools.ManagedDirectory;
            Status = "FFmpeg / FFprobe を自動取得しています…";
            installation = Task.Run(() => Install(folder));
            return installation;
        }

        public static async Task Install(string folder)
        {
            Directory.CreateDirectory(folder);
            string stage = Path.Combine(folder, ".download-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stage);
            try
            {
                string zip = Path.Combine(stage, "ffmpeg.zip");
                using (var client = new DownloadClient())
                {
                    client.DownloadProgressChanged += (_, e) => Status = "FFmpeg / FFprobe を自動取得しています… " + e.ProgressPercentage + "％";
                    string expected = await client.DownloadStringTaskAsync(DownloadUrl + ".sha256");
                    expected = Regex.Match(expected, "[a-fA-F0-9]{64}").Value;
                    if (expected.Length != 64) throw new InvalidDataException("FFmpeg checksum could not be read");
                    await client.DownloadFileTaskAsync(new Uri(DownloadUrl), zip);
                    Status = "FFmpeg / FFprobe を検証しています…";
                    using (var sha = SHA256.Create())
                    using (var file = File.OpenRead(zip))
                        if (!BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "").Equals(expected, StringComparison.OrdinalIgnoreCase))
                            throw new InvalidDataException("FFmpeg download checksum mismatch. Retry the operation.");
                }
                using (var archive = ZipFile.OpenRead(zip))
                {
                    foreach (var entry in archive.Entries)
                    {
                        string normalized = entry.FullName.Replace('\\', '/');
                        if (normalized.EndsWith("/bin/ffmpeg.exe") || normalized.EndsWith("/bin/ffprobe.exe"))
                            entry.ExtractToFile(Path.Combine(stage, entry.Name));
                        else if (entry.Name.StartsWith("LICENSE", StringComparison.OrdinalIgnoreCase))
                            entry.ExtractToFile(Path.Combine(stage, entry.Name));
                    }
                }
                foreach (string tool in new[] { "ffmpeg", "ffprobe" })
                {
                    string file = Path.Combine(stage, tool + ".exe");
                    using (var process = Process.Start(new ProcessStartInfo(file, "-version") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true }))
                    {
                        process.StandardOutput.ReadToEnd(); process.StandardError.ReadToEnd();
                        if (!process.WaitForExit(30000)) { process.Kill(); throw new TimeoutException(tool + " verification timed out"); }
                        if (process.ExitCode != 0) throw new InvalidDataException(tool + " verification failed");
                    }
                }
                foreach (var file in Directory.GetFiles(stage))
                {
                    if (Path.GetExtension(file).Equals(".zip", StringComparison.OrdinalIgnoreCase)) continue;
                    string target = Path.Combine(folder, Path.GetFileName(file));
                    if (!File.Exists(target)) File.Move(file, target);
                }
                Status = "";
            }
            catch (Exception e) { Status = "FFmpeg / FFprobe の準備に失敗しました: " + e.Message; throw; }
            finally { Directory.Delete(stage, true); }
        }

        sealed class DownloadClient : WebClient
        {
            protected override WebRequest GetWebRequest(Uri address)
            {
                var request = base.GetWebRequest(address); request.Timeout = 60000; return request;
            }
        }
    }
}
