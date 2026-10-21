using System;
using System.IO;
using System.Threading.Tasks;
using TimelineVideoStudio;
using UnityEditor;
using UnityEngine;

public static class StudioPortabilityChecks
{
    public static void Build()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/portability"));
        Directory.CreateDirectory(root);
        string tools = Path.Combine(root, "tools");
        Debug.Log("PORTABILITY_CHECKS: resolver, relocation, fresh FFmpeg installation");
        string probe = Path.Combine(root, "resolver"); Directory.CreateDirectory(Path.Combine(probe, "tools"));
        string fake = Path.GetFullPath(Path.Combine(probe, "tools", "ffmpeg.exe")); File.WriteAllText(fake, "resolver fixture");
        if (StudioExternalTools.Resolve("ffmpeg", null, probe, "") != fake) throw new Exception("Bundled tools not resolved");
        if (StudioExternalTools.Resolve("ffmpeg", fake, root, "") != fake) throw new Exception("Explicit override not resolved");
        if (StudioExternalTools.Resolve("ffmpeg", null, root, Path.GetDirectoryName(fake)) != fake) throw new Exception("PATH tool not resolved");
        try { StudioExternalTools.Resolve("ffmpeg", Path.Combine(root,"missing.exe"), probe, ""); throw new Exception("Missing override accepted"); }
        catch (FileNotFoundException) { }
        string original = Path.Combine(root, "original"), moved = Path.Combine(root, "移動先");
        Directory.CreateDirectory(original); Directory.CreateDirectory(moved);
        string asset = Path.Combine(original, "画像.png"); File.WriteAllText(asset, "relocation fixture");
        var project = new StudioProject();project.clips.Add(new StudioClip {kind="Image",path=asset});
        string json = StudioProjectPaths.Serialize(project, Path.Combine(original,"project.json"));
        if (project.clips[0].path != asset || json.Contains(original.Replace("\\","\\\\"))) throw new Exception("Runtime path mutated or absolute path serialized");
        File.Copy(asset, Path.Combine(moved,"画像.png"),true);
        var loaded = JsonUtility.FromJson<StudioProject>(json);StudioProjectPaths.Resolve(loaded,Path.Combine(moved,"project.json"));
        if (!File.Exists(loaded.clips[0].path) || loaded.clips[0].path != Path.Combine(moved,"画像.png")) throw new Exception("Moved media not resolved");
        var legacy = JsonUtility.FromJson<StudioProject>(JsonUtility.ToJson(project));StudioProjectPaths.Resolve(legacy,Path.Combine(moved,"legacy.json"));
        if (legacy.clips[0].path != asset) throw new Exception("Legacy absolute path changed");
        Debug.Log("PORTABILITY_CHECKS: downloading verified tools to " + tools);
        Task.Run(() => StudioToolInstaller.Install(tools)).GetAwaiter().GetResult();
        foreach(string name in new[]{"ffmpeg","ffprobe"})
            if(!File.Exists(Path.Combine(tools,name+".exe")))throw new Exception("Downloaded tool missing: "+name);
        File.WriteAllText(Path.Combine(root,"PASS.txt"),"Resolver precedence; missing override; moved relative media; unchanged runtime/legacy paths; SHA-256 download and both -version checks passed.");
        Debug.Log("PORTABILITY_CHECKS_PASS");
        StudioBuild.BuildWindows();
    }
}
