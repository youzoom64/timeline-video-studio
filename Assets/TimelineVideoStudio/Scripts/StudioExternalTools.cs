using System;
using System.IO;
using UnityEngine;

namespace TimelineVideoStudio
{
    public static class StudioExternalTools
    {
        public static readonly string ManagedDirectory = Path.GetFullPath(Environment.GetEnvironmentVariable("TIMELINE_STUDIO_TOOLS_DIR") ?? Path.Combine(Application.persistentDataPath, "tools"));
        public static string Resolve(string tool)
        {
            string setting = "TIMELINE_STUDIO_" + tool.ToUpperInvariant();
            string configured = Environment.GetEnvironmentVariable(setting);
            string root = Path.GetDirectoryName(Application.dataPath);
            try { return Resolve(tool, configured, root, Environment.GetEnvironmentVariable("PATH")); }
            catch (FileNotFoundException) when (string.IsNullOrWhiteSpace(configured))
            {
                string cached = Path.Combine(ManagedDirectory, tool + ".exe");
                if (File.Exists(cached)) return cached;
                throw;
            }
        }

        public static string Resolve(string tool, string configured, string root, string searchPath)
        {
            string name = tool + (Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer ? ".exe" : "");
            if (!string.IsNullOrWhiteSpace(configured))
            {
                string value = Environment.ExpandEnvironmentVariables(configured.Trim().Trim('"'));
                if (Directory.Exists(value)) value = Path.Combine(value, name);
                if (File.Exists(value)) return Path.GetFullPath(value);
                throw new FileNotFoundException("TIMELINE_STUDIO_" + tool.ToUpperInvariant() + " points to a missing executable", value);
            }
            foreach (string folder in new[] { "tools", Path.Combine("tools", "bin"), "bin", "" })
            {
                string file = Path.Combine(root, folder, name);
                if (File.Exists(file)) return Path.GetFullPath(file);
            }
            foreach (string folder in (searchPath ?? "").Split(Path.PathSeparator))
            {
                string value = folder.Trim().Trim('"');
                if (value.Length == 0) continue;
                try
                {
                    string file = Path.Combine(Environment.ExpandEnvironmentVariables(value), name);
                    if (File.Exists(file)) return Path.GetFullPath(file);
                }
                catch (ArgumentException) { }
            }
            throw new FileNotFoundException(tool + " was not found. Put it in the application's tools folder, add it to PATH, or set TIMELINE_STUDIO_" + tool.ToUpperInvariant() + ".");
        }
    }
}
