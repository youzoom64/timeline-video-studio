using UnityEngine;

namespace TimelineVideoStudio
{
    public struct StudioTransformValue
    {
        public float x, y, z, scale, rotation, scaleX, scaleY;
    }

    public static class StudioTransform
    {
        public static StudioTransformValue Evaluate(StudioClip c, float seconds)
        {
            float a = 0, b = c.duration;
            var from = new StudioTransformValue { x=c.x, y=c.y, z=c.z, scale=c.scale, rotation=c.rotation, scaleX=c.scaleX, scaleY=c.scaleY };
            var to = new StudioTransformValue { x=c.endX, y=c.endY, z=c.endZ, scale=c.endScale, rotation=c.endRotation, scaleX=c.endScaleX, scaleY=c.endScaleY };
            if (c.keyframes != null) foreach (var key in c.keyframes)
            {
                if (key.time <= seconds && key.time > a) { a=key.time; from=new StudioTransformValue { x=key.nextX,y=key.nextY,z=key.nextZ,scale=key.nextScale,rotation=key.nextRotation,scaleX=key.nextScaleX,scaleY=key.nextScaleY }; }
                if (key.time > seconds && key.time < b) { b=key.time; to=new StudioTransformValue { x=key.x,y=key.y,z=key.z,scale=key.scale,rotation=key.rotation,scaleX=key.scaleX,scaleY=key.scaleY }; }
            }
            float t=b>a?Mathf.Clamp01((seconds-a)/(b-a)):0;
            return new StudioTransformValue { x=Mathf.Lerp(from.x,to.x,t),y=Mathf.Lerp(from.y,to.y,t),z=Mathf.Lerp(from.z,to.z,t),scale=Mathf.Lerp(from.scale,to.scale,t),rotation=Mathf.LerpAngle(from.rotation,to.rotation,t),scaleX=Mathf.Max(0,Mathf.Lerp(from.scaleX,to.scaleX,t)),scaleY=Mathf.Max(0,Mathf.Lerp(from.scaleY,to.scaleY,t)) };
        }
    }
}
