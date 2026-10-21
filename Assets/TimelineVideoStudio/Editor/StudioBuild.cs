using System.IO;
using TimelineVideoStudio;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class StudioBuild
{
    const string ScenePath = "Assets/TimelineVideoStudio/Scenes/Studio.unity";

    [MenuItem("Tools/Timeline Video Studio/Create Scene")]
    public static void CreateScene()
    {
        EnsureMaterial();
        Directory.CreateDirectory("Assets/TimelineVideoStudio/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var screenCamera = new GameObject("Screen Camera");
        var camera = screenCamera.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.cullingMask = 0;
        camera.backgroundColor = new Color(.08f, .09f, .11f);
        camera.depth = -10;
        screenCamera.tag = "MainCamera";
        screenCamera.AddComponent<AudioListener>();
        var app = new GameObject("Timeline Video Studio App");
        app.AddComponent<StudioController>();
        app.AddComponent<StudioExporter>();
        app.AddComponent<StudioApi>();
        app.AddComponent<StudioWindow>();
        var light = new GameObject("3D Light");
        var directional = light.AddComponent<Light>();
        directional.type = LightType.Directional;
        directional.intensity = 1.2f;
        directional.transform.rotation = Quaternion.Euler(45, -25, 0);
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        PlayerSettings.productName = "Timeline Video Studio";
        PlayerSettings.companyName = "Personal Tools";
        PlayerSettings.defaultScreenWidth = 1600;
        PlayerSettings.defaultScreenHeight = 900;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        AssetDatabase.SaveAssets();
        Debug.Log("Timeline Video Studio scene created at " + ScenePath);
    }

    [MenuItem("Tools/Timeline Video Studio/Build Windows App")]
    public static void BuildWindows()
    {
        EnsureMaterial();
        CreateScene();
        var outputDir = System.Environment.GetEnvironmentVariable("TIMELINE_STUDIO_BUILD_DIR");
        if (string.IsNullOrWhiteSpace(outputDir)) outputDir = "Build";
        outputDir = Path.GetFullPath(outputDir);
        Directory.CreateDirectory(outputDir);
        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = Path.Combine(outputDir, "TimelineVideoStudio.exe"),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };
        var report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            throw new System.Exception("Build failed: " + report.summary.result);
        Debug.Log("Built: " + options.locationPathName);
        var pluginFolder = Path.Combine(outputDir, "Plugins");
        Directory.CreateDirectory(pluginFolder);
        foreach (var plugin in Directory.GetFiles(Path.GetFullPath("Plugins"), "*.json"))
            File.Copy(plugin, Path.Combine(pluginFolder, Path.GetFileName(plugin)), true);
    }

    static void EnsureMaterial()
    {
        const string path = "Assets/Resources/StudioUnlit.mat";
        Directory.CreateDirectory("Assets/Resources");
        if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/TimelineVideoStudio/Shaders/StudioUnlit.shader");
        if (shader == null) { AssetDatabase.Refresh(); shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/TimelineVideoStudio/Shaders/StudioUnlit.shader"); }
        if (shader == null) throw new System.Exception("StudioUnlit shader not imported");
        AssetDatabase.CreateAsset(new Material(shader), path);
        AssetDatabase.SaveAssets();
    }
}
