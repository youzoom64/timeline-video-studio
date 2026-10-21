using System;
using System.Reflection;
using UnityEngine;
using TimelineVideoStudio;
public static class StudioPanelChecks
{
    public static void Build()
    {
        var go=new GameObject("Panel layout checks");
        float savedLeft=PlayerPrefs.GetFloat("StudioBrowserShare",.20f),savedRight=PlayerPrefs.GetFloat("StudioInspectorShare",.25f);
        try
        {
            var window=go.AddComponent<StudioWindow>();
            var small=window.PanelWidthsFor(1600);var large=window.PanelWidthsFor(2560);
            if(large.x<=small.x||large.y<=small.y)throw new Exception("Side panels do not grow with window");
            foreach(float width in new[]{800f,1200f,1600f,2560f})
            {
                foreach(float share in new[]{0f,.05f,.2f,.8f})
                {
                    typeof(StudioWindow).GetField("browserShare",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(window,share);
                    typeof(StudioWindow).GetField("inspectorShare",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(window,share);
                    var v=window.PanelWidthsFor(width);
                    if(v.x<0||v.y<0||width-v.x-v.y<Mathf.Min(360,width*.4f))throw new Exception("Invalid panel bounds "+width);
                }
            }
            float leftTarget=Screen.width*.2f,rightTarget=Screen.width*.25f;
            window.SetPanelWidth(true,0);window.SetPanelWidth(false,0);
            if(window.PanelWidths.x!=0||window.PanelWidths.y!=0)throw new Exception("Panels cannot fully collapse");
            window.HandlePanelPointer(600,EventType.MouseDown,0,1,new Vector2(2,150));
            if((int)typeof(StudioWindow).GetField("resizingSide",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(window)!=1)throw new Exception("Left handle MouseDown did not select resize");
            window.HandlePanelPointer(600,EventType.MouseDrag,0,1,new Vector2(leftTarget,150));
            if(Mathf.Abs(window.PanelWidths.x-leftTarget)>1)throw new Exception("Collapsed left edge cannot restore panel: "+window.PanelWidths+" screen="+Screen.width);
            window.HandlePanelPointer(600,EventType.MouseUp,0,1,Vector2.zero);
            window.HandlePanelPointer(600,EventType.MouseDown,0,1,new Vector2(Screen.width-2,150));
            window.HandlePanelPointer(600,EventType.MouseDrag,0,1,new Vector2(Screen.width-rightTarget,150));
            if(Mathf.Abs(window.PanelWidths.y-rightTarget)>1)throw new Exception("Collapsed right edge cannot restore panel");
            window.HandlePanelPointer(600,EventType.MouseUp,0,1,Vector2.zero);
            Debug.Log("PANEL_LAYOUT_PASS: responsive widths; fully collapse both panels; drag both collapsed edge handles to restore; preview bounds retained");
        }
        finally{PlayerPrefs.SetFloat("StudioBrowserShare",savedLeft);PlayerPrefs.SetFloat("StudioInspectorShare",savedRight);PlayerPrefs.Save();UnityEngine.Object.DestroyImmediate(go);}
        StudioBuild.BuildWindows();
    }
}
