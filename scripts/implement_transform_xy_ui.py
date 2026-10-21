from pathlib import Path
p=Path(__file__).resolve().parents[1]/'Assets/TimelineVideoStudio/Scripts/StudioWindow.cs';s=p.read_text(encoding='utf-8')
s=s.replace('string[] segmentEdit = new string[10];','string[] segmentEdit = new string[10];\n        string[] axisEdit = new string[4];')
s=s.replace('for (int i = 0; i < values.Length; i++) segmentEdit[i] = values[i].ToString("0.###", CultureInfo.InvariantCulture);','''for (int i = 0; i < values.Length; i++) segmentEdit[i] = values[i].ToString("0.###", CultureInfo.InvariantCulture);
            float[] axes={s.startScaleX,s.startScaleY,s.endScaleX,s.endScaleY};
            for(int i=0;i<4;i++) axisEdit[i]=axes[i].ToString("0.###",CultureInfo.InvariantCulture);''')
s=s.replace('                if (GUILayout.Button("区間の開始値・終了値を反映"))', '                DrawAxisControls(c, segment);\n                if (GUILayout.Button("区間の開始値・終了値を反映"))')
s=s.replace('endScale = v[8], endRotation = v[9] });','endScale = v[8], endRotation = v[9], startScaleX=ParseAxis(0),startScaleY=ParseAxis(1),endScaleX=ParseAxis(2),endScaleY=ParseAxis(3) }); SelectSegment(c,selectedSegment);')
insert='''        float ParseAxis(int index) { return Mathf.Max(0,float.Parse(axisEdit[index],CultureInfo.InvariantCulture)); }
        void DrawAxisControls(StudioClip clip, StudioSegment segment)
        {
            bool linked=GUILayout.Toggle(clip.lockAspect,"縦横比固定");
            if(linked!=clip.lockAspect) Try(()=>studio.Change(clip.id,c=>c.lockAspect=linked));
            for(int axis=0;axis<2;axis++)
            {
                float start=axis==0?segment.startScaleX:segment.startScaleY;
                float end=axis==0?segment.endScaleX:segment.endScaleY;
                float max=Mathf.Max(5,start,end);
                GUILayout.BeginHorizontal();
                float a=GUILayout.HorizontalSlider(start,0,max,GUILayout.Width(136));
                axisEdit[axis]=GUILayout.TextField(axisEdit[axis],GUILayout.Width(67));
                GUILayout.Label(axis==0?"横倍率":"縦倍率",GUILayout.Width(76));
                float b=GUILayout.HorizontalSlider(end,0,max,GUILayout.Width(136));
                axisEdit[axis+2]=GUILayout.TextField(axisEdit[axis+2],GUILayout.Width(67));
                GUILayout.EndHorizontal();
                if(Mathf.Abs(a-start)>.00001f || Mathf.Abs(b-end)>.00001f)
                {
                    if(axis==0) { segment.startScaleX=a; segment.endScaleX=b; }
                    else { segment.startScaleY=a; segment.endScaleY=b; }
                    Try(()=> { studio.SetSegment(clip.id,selectedSegment,segment,!sliderGestureActive); sliderGestureActive=true; SelectSegment(clip,selectedSegment); });
                }
            }
        }

'''
s=s.replace('        static float SegmentValue(',insert+'        static float SegmentValue(')
p.write_text(s,encoding='utf-8')
