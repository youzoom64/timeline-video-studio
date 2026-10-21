using System;
using System.Reflection;
using UnityEngine;
using TimelineVideoStudio;

public static class StudioInteractionChecks
{
    public static void Build()
    {
        var go = new GameObject("Timeline interaction checks");
        try
        {
            var controller = go.AddComponent<StudioController>();
            controller.project.visualLayerCount = 20;
            var window = go.AddComponent<StudioWindow>();
            typeof(StudioWindow).GetField("studio", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(window, controller);
            foreach (float height in new[] { 215f, 294f, 440f })
            {
                typeof(StudioWindow).GetField("timelineHeight", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(window, height);
                foreach (bool horizontal in new[] { false, true })
                {
                    window.ScrollTimelineRows(0);
                    var e = new Event { type = EventType.ScrollWheel, modifiers = EventModifiers.Shift, mousePosition = new Vector2(150, 150), delta = horizontal ? new Vector2(3, 0) : new Vector2(0, 3) };
                    window.HandleTimelineWheel(new Rect(0, 100, 600, height), e);
                    if (window.TimelineFirstRow != 1) throw new Exception("Shift wheel down failed after resize: " + height + " horizontal=" + horizontal);
                    e = new Event { type = EventType.ScrollWheel, modifiers = EventModifiers.Shift, mousePosition = new Vector2(150, 150), delta = horizontal ? new Vector2(-3, 0) : new Vector2(0, -3) };
                    window.HandleTimelineWheel(new Rect(0, 100, 600, height), e);
                    if (window.TimelineFirstRow != 0) throw new Exception("Shift wheel up failed after resize");
                }
            }
            Debug.Log("INTERACTION_CHECKS_PASS: Shift wheel X/Y, up/down, at three resized heights");
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
        StudioBuild.BuildWindows();
    }
}
