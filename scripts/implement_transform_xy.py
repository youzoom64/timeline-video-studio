from pathlib import Path
R=Path(__file__).resolve().parents[1]/'Assets/TimelineVideoStudio/Scripts'
def load(name):return (R/name).read_text(encoding='utf-8')
def save(name,s):(R/name).write_text(s,encoding='utf-8')
s=load('VisualTrack.cs')
s=s.replace('public GameObject target;', 'public StudioClip clip;\n        public GameObject target;')
s=s.replace('b.controller = controller; b.layer = layer;', 'b.controller = controller; b.layer = layer; b.clip = clip;')
a=s.index('                double duration = input.GetDuration();');b=s.index('                if (b.effects != null)',a)
s=s[:a]+'''                var value = StudioTransform.Evaluate(b.clip, (float)input.GetTime());
                var state = new StudioEffectState { x=value.x, y=value.y, z=value.z - b.layer * .01f, scale=value.scale, rotation=value.rotation, color=Color.white };
'''+s[b:]
s=s.replace('b.target.transform.localScale = size * state.scale;', 'b.target.transform.localScale = Vector3.Scale(size * state.scale, new Vector3(value.scaleX, value.scaleY, 1));')
save('VisualTrack.cs',s)
s=load('StudioController.cs')
s=s.replace('asset.controller = this; asset.layer = c.layer;', 'asset.controller = this; asset.layer = c.layer; asset.clip = c;')
# The central evaluator is also used when inserting and splitting points.
a=s.index('            float t = time / c.duration;');b=s.index('            Checkpoint(); c.keyframes.Add(point)',a)
s=s[:a]+'''            var v = StudioTransform.Evaluate(c, time);
            var point = new StudioKeyframe { time=time, x=v.x,y=v.y,z=v.z,scale=v.scale,rotation=v.rotation,scaleX=v.scaleX,scaleY=v.scaleY,
                nextX=v.x,nextY=v.y,nextZ=v.z,nextScale=v.scale,nextRotation=v.rotation,nextScaleX=v.scaleX,nextScaleY=v.scaleY };
'''+s[b:]
s=s.replace('c.keyframes.Sort((a, b) => a.time.CompareTo(b.time)); Rebuild(); return point;', 'c.keyframes.Sort((a, b) => a.time.CompareTo(b.time)); RefreshTransform(); return point;')
s=s.replace('Checkpoint(); c.keyframes.Remove(point); Rebuild();', 'Checkpoint(); c.keyframes.Remove(point); RefreshTransform();')
s=s.replace('Checkpoint(); edit(point); Rebuild();', 'Checkpoint(); edit(point); RefreshTransform();')
s=s.replace('startScale = previous == null ? c.scale : previous.nextScale,', 'startScaleX = previous == null ? c.scaleX : previous.nextScaleX, startScaleY = previous == null ? c.scaleY : previous.nextScaleY,\n                startScale = previous == null ? c.scale : previous.nextScale,')
s=s.replace('endScale = next == null ? c.endScale : next.scale,', 'endScaleX = next == null ? c.endScaleX : next.scaleX, endScaleY = next == null ? c.endScaleY : next.scaleY,\n                endScale = next == null ? c.endScale : next.scale,')
s=s.replace('c.rotation = values.startRotation; }','c.rotation = values.startRotation; c.scaleX = Mathf.Max(0,values.startScaleX); c.scaleY = Mathf.Max(0,values.startScaleY); }')
s=s.replace('p.nextRotation = values.startRotation; }','p.nextRotation = values.startRotation; p.nextScaleX = Mathf.Max(0,values.startScaleX); p.nextScaleY = Mathf.Max(0,values.startScaleY); }')
s=s.replace('c.endRotation = values.endRotation; }','c.endRotation = values.endRotation; c.endScaleX = Mathf.Max(0,values.endScaleX); c.endScaleY = Mathf.Max(0,values.endScaleY); }')
s=s.replace('p.rotation = values.endRotation; }','p.rotation = values.endRotation; p.scaleX = Mathf.Max(0,values.endScaleX); p.scaleY = Mathf.Max(0,values.endScaleY); }')
a=s.index('        public void SetSegment(');b=s.index('        public StudioEffect AddEffect',a)
s=s[:a]+s[a:b].replace('            Rebuild();','            RefreshTransform();')+s[b:]
s=s.replace('float boundaryRotation = Mathf.LerpAngle(segment.startRotation, segment.endRotation, fraction);','float boundaryRotation = Mathf.LerpAngle(segment.startRotation, segment.endRotation, fraction);\n            float boundaryScaleX = Mathf.Lerp(segment.startScaleX, segment.endScaleX, fraction);\n            float boundaryScaleY = Mathf.Lerp(segment.startScaleY, segment.endScaleY, fraction);')
s=s.replace('second.rotation = exactPoint == null ? boundaryRotation : exactPoint.nextRotation;', 'second.rotation = exactPoint == null ? boundaryRotation : exactPoint.nextRotation;\n            second.scaleX = exactPoint == null ? boundaryScaleX : exactPoint.nextScaleX; second.scaleY = exactPoint == null ? boundaryScaleY : exactPoint.nextScaleY;')
s=s.replace('c.endRotation = exactPoint == null ? boundaryRotation : exactPoint.rotation;', 'c.endRotation = exactPoint == null ? boundaryRotation : exactPoint.rotation;\n            c.endScaleX = exactPoint == null ? boundaryScaleX : exactPoint.scaleX; c.endScaleY = exactPoint == null ? boundaryScaleY : exactPoint.scaleY;')
# Metadata-only changes stay on the same live object and graph.
a=s.index('            edit(c);',s.index('        public void Change('))
s=s[:a]+s[a:].replace('            edit(c);','''            string metadata = TransformMetadata(c);
            edit(c);''',1)
a=s.index('        public void Change(');b=s.index('        public void NewProject',a)
s=s[:a]+s[a:b].replace('            Rebuild();','            if (c.kind != "WAV" && metadata == TransformMetadata(c)) RefreshTransform(); else Rebuild();')+s[b:]
insert='''        static string TransformMetadata(StudioClip c)
        {
            var copy = JsonUtility.FromJson<StudioClip>(JsonUtility.ToJson(c));
            copy.x=copy.y=copy.z=copy.endX=copy.endY=copy.endZ=copy.rotation=copy.endRotation=0;
            copy.scale=copy.endScale=copy.scaleX=copy.scaleY=copy.endScaleX=copy.endScaleY=1;
            copy.lockAspect=false; copy.keyframes=new List<StudioKeyframe>();
            return JsonUtility.ToJson(copy);
        }
        public void RefreshTransform()
        {
            if (!playing && director != null && director.playableAsset != null) { director.time=playhead; director.Evaluate(); }
            ProjectChanged?.Invoke();
        }
        bool RestoreTransformHistory(StudioProject restored)
        {
            if (project.clips.Count != restored.clips.Count || project.width != restored.width || project.height != restored.height || project.fps != restored.fps || project.visualLayerCount != restored.visualLayerCount) return false;
            if (JsonUtility.ToJson(project.groupsWrapper()) != JsonUtility.ToJson(restored.groupsWrapper())) return false;
            if (string.Join(",",project.hiddenVisualLayers) != string.Join(",",restored.hiddenVisualLayers) || string.Join(",",project.mutedAudioTracks) != string.Join(",",restored.mutedAudioTracks)) return false;
            var originals=project.clips.ToDictionary(c=>c.id);
            foreach (var c in restored.clips) if (!originals.TryGetValue(c.id,out var old) || TransformMetadata(old)!=TransformMetadata(c)) return false;
            for (int i=0;i<restored.clips.Count;i++) { var c=restored.clips[i]; var old=originals[c.id]; JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(c),old); restored.clips[i]=old; }
            project=restored; RefreshTransform(); return true;
        }
'''
# Compare group JSON without changing the public schema.
insert=insert.replace('JsonUtility.ToJson(project.groupsWrapper()) != JsonUtility.ToJson(restored.groupsWrapper())','string.Join("|",project.groups.Select(g=>JsonUtility.ToJson(g))) != string.Join("|",restored.groups.Select(g=>JsonUtility.ToJson(g)))')
s=s.replace('        public void Change(string id,',insert+'\n        public void Change(string id,')
for stack in ['undo','redo']:
 s=s.replace(f'project = JsonUtility.FromJson<StudioProject>({stack}[{stack}.Count - 1]);',f'var restored = JsonUtility.FromJson<StudioProject>({stack}[{stack}.Count - 1]);')
 s=s.replace(f'{stack}.RemoveAt({stack}.Count - 1);\n            Rebuild();',f'{stack}.RemoveAt({stack}.Count - 1);\n            if (!RestoreTransformHistory(restored)) {{ project=restored; Rebuild(); }}')
save('StudioController.cs',s)
# APIs keep all existing fields, add separate scale channels.
s=load('StudioApi.cs')
s=s.replace('case "scale": clip.scale = Number(value); break;', '''case "scaleX": if (clip.lockAspect) clip.scaleY = clip.scaleX > .000001f ? clip.scaleY * Mathf.Max(0,Number(value)) / clip.scaleX : Mathf.Max(0,Number(value)); clip.scaleX = Mathf.Max(0,Number(value)); break;
                case "scaleY": if (clip.lockAspect) clip.scaleX = clip.scaleY > .000001f ? clip.scaleX * Mathf.Max(0,Number(value)) / clip.scaleY : Mathf.Max(0,Number(value)); clip.scaleY = Mathf.Max(0,Number(value)); break;
                case "endScaleX": if (clip.lockAspect) clip.endScaleY = clip.endScaleX > .000001f ? clip.endScaleY * Mathf.Max(0,Number(value)) / clip.endScaleX : Mathf.Max(0,Number(value)); clip.endScaleX = Mathf.Max(0,Number(value)); break;
                case "endScaleY": if (clip.lockAspect) clip.endScaleX = clip.endScaleY > .000001f ? clip.endScaleX * Mathf.Max(0,Number(value)) / clip.endScaleY : Mathf.Max(0,Number(value)); clip.endScaleY = Mathf.Max(0,Number(value)); break;
                case "lockAspect": clip.lockAspect=bool.Parse(value); break;
                case "scale": clip.scale = Number(value); break;''')
pos=s.index('        static void SetPoint')
for field in ['scaleX','scaleY','nextScaleX','nextScaleY']:
 s=s[:pos]+s[pos:].replace('switch (field)\n            {',f'switch (field)\n            {{\n                case "{field}": point.{field} = Mathf.Max(0,Number(value)); break;',1)
save('StudioApi.cs',s)
s=load('StudioExporter.cs').replace('if (c.kind == "Video" || c.x != c.endX', 'if (c.scaleX != c.endScaleX || c.scaleY != c.endScaleY || c.kind == "Video" || c.x != c.endX')
save('StudioExporter.cs',s)
