using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using TimelineVideoStudio;

public static class StudioTransformChecks
{
    static void Equal(float actual,float expected,string label) { if(Mathf.Abs(actual-expected)>.0001f) throw new Exception(label+": "+actual+" != "+expected); }
    public static void Build()
    {
        var old=JsonUtility.FromJson<StudioClip>("{\"kind\":\"Cube\",\"duration\":4,\"scale\":1,\"endScale\":1}");
        Equal(old.scaleX,1,"legacy X"); Equal(old.endScaleY,1,"legacy end Y");
        var oldPoint=JsonUtility.FromJson<StudioKeyframe>("{\"time\":2}"); Equal(oldPoint.nextScaleX,1,"legacy point");
        var go=new GameObject("XY transform checks"); StudioController controller=null;
        try
        {
            controller=go.AddComponent<StudioController>();
            if(controller.director==null) typeof(StudioController).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(controller,null);
            var c=new StudioClip { kind="Cube",duration=4,scale=1,endScale=1,scaleX=0,endScaleX=2,scaleY=1,endScaleY=1 };
            controller.project=new StudioProject { width=640,height=360,clips=new List<StudioClip>{c} };
            controller.ResetRenderTarget(); controller.Rebuild(); controller.Seek(1);
            var graph=controller.director.playableGraph;
            var visuals=(Dictionary<string,GameObject>)typeof(StudioController).GetField("visuals",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(controller);
            var target=visuals[c.id]; var material=target.GetComponent<Renderer>().sharedMaterial;
            Equal(target.transform.localScale.x,.5f,"horizontal"); Equal(target.transform.localScale.y,1,"independent Y");
            var seg=controller.GetSegment(c.id,0);seg.endScaleY=3;controller.SetSegment(c.id,0,seg);
            Equal(target.transform.localScale.y,1.5f,"vertical");
            if(!graph.Equals(controller.director.playableGraph)||visuals[c.id]!=target||target.GetComponent<Renderer>().sharedMaterial!=material) throw new Exception("Parameter change rebuilt resources");
            var point=controller.AddKeyframe(c.id,2);Equal(point.scaleX,1,"insert X");Equal(point.nextScaleY,2,"insert Y");
            var before=StudioTransform.Evaluate(c,1.5f);
            controller.Change(c.id,x=>x.scaleX=.25f);controller.Undo();
            var restored=controller.Find(c.id);Equal(StudioTransform.Evaluate(restored,1.5f).scaleX,before.scaleX,"undo");
            if(!graph.Equals(controller.director.playableGraph)||visuals[c.id]!=target) throw new Exception("Transform undo rebuilt resources");
            controller.Redo();Equal(controller.Find(c.id).scaleX,.25f,"redo");
            controller.Undo();restored=controller.Find(c.id);
            var at=StudioTransform.Evaluate(restored,1.25f);var end=StudioTransform.Evaluate(restored,3.25f);
            var second=controller.Split(c.id,1.25f);Equal(second.scaleX,at.scaleX,"split boundary X");Equal(StudioTransform.Evaluate(second,2).scaleY,end.scaleY,"split curve Y");
            var duplicate=controller.Duplicate(second.id);Equal(duplicate.scaleX,second.scaleX,"duplicate X");
            var serialized=JsonUtility.FromJson<StudioProject>(JsonUtility.ToJson(controller.project));Equal(serialized.clips[1].scaleX,second.scaleX,"save reload");
            controller.Change(second.id,x=>x.lockAspect=true);seg=controller.GetSegment(second.id,0);float ratio=seg.startScaleY/seg.startScaleX;seg.startScaleX*=2;controller.SetSegment(second.id,0,seg);Equal(controller.GetSegment(second.id,0).startScaleY/controller.GetSegment(second.id,0).startScaleX,ratio,"aspect ratio");
            Debug.Log("TRANSFORM_XY_CHECKS_PASS: legacy, zero, axis interpolation, point, split, duplicate, undo/redo, save/reload, aspect, graph/resource reuse");
        }
        finally { if(controller!=null && controller.previewCamera!=null) UnityEngine.Object.DestroyImmediate(controller.previewCamera.gameObject);UnityEngine.Object.DestroyImmediate(go); }
        StudioBuild.BuildWindows();
    }
}
