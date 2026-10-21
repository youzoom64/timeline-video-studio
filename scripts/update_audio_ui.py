from pathlib import Path
p=Path(__file__).resolve().parents[1]/'Assets/TimelineVideoStudio/Scripts/StudioWindow.cs'
s=p.read_text(encoding='utf-8')
def replace(a,b,count=1):
 global s
 assert s.count(a)>=count,a
 s=s.replace(a,b,count)
replace('int contextLayer;','int contextLayer;\n        int contextAudioTrack = 4, editAudioTrack = 4;\n        string volumeEdit = "1", fadeInEdit = "0", fadeOutEdit = "0";')
replace('Screen.height - 294 - groupRows * 23, Screen.width, 294 + groupRows * 23','Screen.height - 409 - groupRows * 23, Screen.width, 409 + groupRows * 23')
replace('row * (9 + groupRows)','row * (14 + groupRows)')
replace('i < 9 + groupRows','i < 14 + groupRows')
replace('i == 8 + groupRows ? "音声" : i >= 8 ?', 'i >= 8 + groupRows ? StudioProject.AudioTrackNames[i - 8 - groupRows] : i >= 8 ?')
replace('c.kind == "WAV" ? 8 + groupRows : c.layer - firstLayer','c.kind == "WAV" ? 8 + groupRows + Mathf.Clamp(c.audioTrack, 0, 5) : c.layer - firstLayer')
replace('int nextLayer = c.layer;','int nextLayer = c.layer;\n                    int nextAudioTrack = c.audioTrack;')
replace('if (c.kind != "WAV") nextLayer = Mathf.Clamp(firstLayer + Mathf.RoundToInt((e.mousePosition.y - canvas.y) / row), 0, studio.project.visualLayerCount - 1);','if (c.kind != "WAV") nextLayer = Mathf.Clamp(firstLayer + Mathf.RoundToInt((e.mousePosition.y - canvas.y) / row), 0, studio.project.visualLayerCount - 1);\n                        else nextAudioTrack = Mathf.Clamp(Mathf.FloorToInt((e.mousePosition.y - canvas.y) / row) - 8 - groupRows, 0, 5);')
replace('nextLayer != c.layer)','nextLayer != c.layer || nextAudioTrack != c.audioTrack)')
replace('c.layer = nextLayer;','c.layer = nextLayer; c.audioTrack = nextAudioTrack;')
replace('OpenContext(e.mousePosition, "", Mathf.Max', 'contextAudioTrack = Mathf.Clamp(Mathf.FloorToInt((e.mousePosition.y - canvas.y) / row) - 8 - groupRows, 0, 5);\n                OpenContext(e.mousePosition, "", Mathf.Max')
replace('studio.Add(kind, pathField, start: contextTime, layer: Mathf.Clamp(contextLayer, 0, studio.project.visualLayerCount - 1));','var added = studio.Add(kind, pathField, start: contextTime, layer: Mathf.Clamp(contextLayer, 0, studio.project.visualLayerCount - 1));\n            if (kind == "WAV") studio.Change(added.id, c => c.audioTrack = contextAudioTrack);')
replace('editText = c.text;', '''editText = c.text;
            editAudioTrack = Mathf.Clamp(c.audioTrack, 0, 5);
            volumeEdit = c.volume.ToString("0.###", CultureInfo.InvariantCulture);
            fadeInEdit = c.fadeIn.ToString("0.###", CultureInfo.InvariantCulture);
            fadeOutEdit = c.fadeOut.ToString("0.###", CultureInfo.InvariantCulture);''')
replace('if (x.kind == "Text") x.text = editText;', '''if (x.kind == "Text") x.text = editText;
                    if (x.kind == "WAV") {
                        x.audioTrack = editAudioTrack;
                        x.volume = Mathf.Clamp(float.Parse(volumeEdit, CultureInfo.InvariantCulture), 0, 4);
                        x.fadeIn = Mathf.Max(0, float.Parse(fadeInEdit, CultureInfo.InvariantCulture));
                        x.fadeOut = Mathf.Max(0, float.Parse(fadeOutEdit, CultureInfo.InvariantCulture));
                    }''')
# Inspector and expanded object menu share the same audio controls.
replace('if (c.kind == "Text")\n', 'if (c.kind == "WAV") DrawAudioSettings();\n            if (c.kind == "Text")\n')
replace('if (c.kind == "Text") { GUILayout.Label("文字");','if (c.kind == "WAV") DrawAudioSettings();\n            if (c.kind == "Text") { GUILayout.Label("文字");')
replace('string editText = "";', '''void DrawAudioSettings()
        {
            GUILayout.Label("音声トラック");
            editAudioTrack = GUILayout.SelectionGrid(editAudioTrack, StudioProject.AudioTrackNames, 2);
            GUILayout.BeginHorizontal(); GUILayout.Label("音量（倍）", GUILayout.Width(100)); volumeEdit = GUILayout.TextField(volumeEdit); GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal(); GUILayout.Label("フェード入（秒）", GUILayout.Width(110)); fadeInEdit = GUILayout.TextField(fadeInEdit); GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal(); GUILayout.Label("フェード出（秒）", GUILayout.Width(110)); fadeOutEdit = GUILayout.TextField(fadeOutEdit); GUILayout.EndHorizontal();
        }
        string editText = "";''')
# Audio assignment has its own named selector rather than the visual-layer field.
replace('for (int i = 0; i < edit.Length; i++)\n            {','for (int i = 0; i < edit.Length; i++)\n            {\n                if (c.kind == "WAV" && i == 2) continue;',2)
# Browser drag onto an audio row assigns that row as well.
replace('if (kind != null) Try(() => studio.Add(kind, dragFile, start: Mathf.Max(0, (e.mousePosition.x - label + offset) / zoom), layer: Mathf.Clamp(firstLayer + Mathf.RoundToInt((e.mousePosition.y - canvas.y) / row), 0, studio.project.visualLayerCount - 1)));', '''if (kind != null) Try(() => {
                        var added = studio.Add(kind, dragFile, start: Mathf.Max(0, (e.mousePosition.x - label + offset) / zoom), layer: Mathf.Clamp(firstLayer + Mathf.RoundToInt((e.mousePosition.y - canvas.y) / row), 0, studio.project.visualLayerCount - 1));
                        if (kind == "WAV") studio.Change(added.id, c => c.audioTrack = Mathf.Clamp(Mathf.FloorToInt((e.mousePosition.y - canvas.y) / row) - 8 - groupRows, 0, 5));
                    });''')
p.write_text(s,encoding='utf-8')
