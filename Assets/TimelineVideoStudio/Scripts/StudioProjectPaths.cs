using System;
using System.IO;
using UnityEngine;

namespace TimelineVideoStudio
{
    public static class StudioProjectPaths
    {
        public static string Serialize(StudioProject project, string projectFile)
        {
            var saved = JsonUtility.FromJson<StudioProject>(JsonUtility.ToJson(project));
            string folder = Path.GetDirectoryName(Path.GetFullPath(projectFile));
            foreach (var clip in saved.clips)
            {
                if (string.IsNullOrWhiteSpace(clip.path)) continue;
                string path = Path.GetFullPath(clip.path);
                if (string.Equals(Path.GetPathRoot(folder), Path.GetPathRoot(path), StringComparison.OrdinalIgnoreCase))
                    clip.path = Path.GetRelativePath(folder, path).Replace('\\', '/');
            }
            return JsonUtility.ToJson(saved, true);
        }

        public static void Resolve(StudioProject project, string projectFile)
        {
            string folder = Path.GetDirectoryName(Path.GetFullPath(projectFile));
            foreach (var clip in project.clips)
                if (!string.IsNullOrWhiteSpace(clip.path) && !Path.IsPathRooted(clip.path))
                    clip.path = Path.GetFullPath(Path.Combine(folder, clip.path));
        }
    }
}
