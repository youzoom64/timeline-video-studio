using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace TimelineVideoStudio
{
    [Serializable]
    public class StudioPluginDefinition
    {
        public string id;
        public string title;
        public string type;
        public float a = 0.2f;
        public float b = 5f;
        public float c = 1f;
        public float d = 1f;
    }

    public struct StudioEffectState
    {
        public float x, y, z, scale, rotation;
        public Color color;
    }

    // Compile a .NET Framework/Mono DLL against Assembly-CSharp.dll and UnityEngine DLLs.
    public interface IStudioEffectPlugin
    {
        string Id { get; }
        string Title { get; }
        void Apply(ref StudioEffectState state, StudioEffect effect, float localSeconds);
    }

    public static class StudioPlugins
    {
        static readonly Dictionary<string, StudioPluginDefinition> definitions = new Dictionary<string, StudioPluginDefinition>();
        static readonly Dictionary<string, IStudioEffectPlugin> custom = new Dictionary<string, IStudioEffectPlugin>();
        static bool loaded;
        public static IEnumerable<StudioPluginDefinition> List() { EnsureLoaded(); return definitions.Values.OrderBy(x => x.title); }
        public static StudioPluginDefinition Find(string id) { EnsureLoaded(); definitions.TryGetValue(id, out var value); return value; }

        static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            Add(new StudioPluginDefinition { id = "shake", title = "揺れ", type = "shake", a = .2f, b = 8f });
            Add(new StudioPluginDefinition { id = "pulse", title = "拡大縮小", type = "pulse", a = .15f, b = 2f });
            Add(new StudioPluginDefinition { id = "swing", title = "首振り回転", type = "swing", a = 10f, b = 2f });
            Add(new StudioPluginDefinition { id = "spin", title = "連続回転", type = "spin", a = 90f });
            Add(new StudioPluginDefinition { id = "tint", title = "色変更", type = "tint", a = 1f, b = .5f, c = .5f, d = 1f });
            string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
            try
            {
                Directory.CreateDirectory(folder);
                foreach (var path in Directory.GetFiles(folder, "*.json"))
                {
                    try { Add(JsonUtility.FromJson<StudioPluginDefinition>(File.ReadAllText(path))); }
                    catch (Exception e) { Debug.LogWarning("Plugin JSON " + path + ": " + e.Message); }
                }
                foreach (var path in Directory.GetFiles(folder, "*.dll"))
                {
                    try
                    {
                        var assembly = Assembly.LoadFrom(path);
                        foreach (var type in assembly.GetTypes().Where(t => typeof(IStudioEffectPlugin).IsAssignableFrom(t) && !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) != null))
                        {
                            var plugin = (IStudioEffectPlugin)Activator.CreateInstance(type);
                            custom[plugin.Id] = plugin;
                            Add(new StudioPluginDefinition { id = plugin.Id, title = plugin.Title, type = "dll" });
                        }
                    }
                    catch (Exception e) { Debug.LogWarning("Plugin DLL " + path + ": " + e.Message); }
                }
            }
            catch (Exception e) { Debug.LogWarning("Plugin folder: " + e.Message); }
        }

        static void Add(StudioPluginDefinition definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.id) || string.IsNullOrWhiteSpace(definition.type)) return;
            definitions[definition.id] = definition;
        }

        public static StudioEffect Create(string pluginId)
        {
            var definition = Find(pluginId) ?? throw new ArgumentException("Plugin not found: " + pluginId);
            return new StudioEffect { plugin = pluginId, a = definition.a, b = definition.b, c = definition.c, d = definition.d };
        }

        public static void Apply(ref StudioEffectState state, StudioEffect effect, float localSeconds)
        {
            if (effect == null || !effect.enabled) return;
            var definition = Find(effect.plugin);
            if (definition == null) return;
            float phase = localSeconds * effect.b * Mathf.PI * 2f;
            switch (definition.type)
            {
                case "shake": state.x += Mathf.Sin(phase) * effect.a; state.y += Mathf.Sin(phase * 1.37f) * effect.a; break;
                case "pulse": state.scale *= 1f + effect.a * Mathf.Sin(phase); break;
                case "swing": state.rotation += effect.a * Mathf.Sin(phase); break;
                case "spin": state.rotation += effect.a * localSeconds; break;
                case "tint": state.color *= new Color(effect.a, effect.b, effect.c, effect.d); break;
                case "dll": if (custom.TryGetValue(effect.plugin, out var plugin)) plugin.Apply(ref state, effect, localSeconds); break;
            }
        }
    }
}
