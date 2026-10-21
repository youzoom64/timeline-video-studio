from pathlib import Path
p=Path(__file__).resolve().parents[1]/'Assets/TimelineVideoStudio/Scripts/StudioWindow.cs';s=p.read_text(encoding='utf-8')
def sub(a,b):
 global s
 assert a in s,a
 s=s.replace(a,b)
sub('int firstGroup;', '''float timelineHeight = 294f;
        bool resizingTimeline;
        public int TimelineFirstRow { get { return firstLayer; } }
        public void ScrollTimelineRows(int value) { firstLayer = Mathf.Clamp(value, 0, Mathf.Max(0, studio.project.visualLayerCount + (studio.project.groups?.Count ?? 0) + 6 - Mathf.Max(1, Mathf.FloorToInt((timelineHeight - 82) / 23)))); }''')
sub('int groupRows = Mathf.Clamp(studio.project.groups?.Count ?? 0, 1, 6);\n            var timeline = new Rect(0, Screen.height - 409 - groupRows * 23, Screen.width, 409 + groupRows * 23);', '''timelineHeight = Mathf.Clamp(timelineHeight, 174f, Mathf.Max(174f, Screen.height - 260f));
            var splitter = new Rect(0, Screen.height - timelineHeight - 4, Screen.width, 8);
            if (e.type == EventType.MouseDown && e.button == 0 && splitter.Contains(e.mousePosition)) { resizingTimeline = true; e.Use(); }
            if (resizingTimeline && e.type == EventType.MouseDrag) { timelineHeight = Mathf.Clamp(Screen.height - e.mousePosition.y, 174f, Mathf.Max(174f, Screen.height - 260f)); e.Use(); }
            if (e.type == EventType.MouseUp) resizingTimeline = false;
            var timeline = new Rect(0, Screen.height - timelineHeight, Screen.width, timelineHeight);''')
sub('DrawTimeline(timeline, e);', '''DrawTimeline(timeline, e);
            DrawSolid(new Rect(0, timeline.y - 3, Screen.width, 6), resizingTimeline ? new Color(.45f, .65f, .8f) : new Color(.3f, .35f, .4f));
            DrawSolid(new Rect(Screen.width / 2 - 24, timeline.y - 1, 48, 2), new Color(.7f, .75f, .8f));''')
start=s.index('            int groupRows = Mathf.Clamp(',s.index('void DrawTimeline'))
end=s.index('            if (GUI.enabled && e.type == EventType.ScrollWheel',start)
s=s[:start]+'''            int groupRows = studio.project.groups?.Count ?? 0;
            int visualRows = studio.project.visualLayerCount;
            int totalRows = visualRows + groupRows + 6;
            int visibleRows = Mathf.Max(1, Mathf.FloorToInt((r.height - ruler - 24) / row));
            firstLayer = Mathf.Clamp(firstLayer, 0, Mathf.Max(0, totalRows - visibleRows));
            if (GUI.Button(new Rect(r.width - 235, r.y + 3, 78, 24), "層を挿入")) Try(() => studio.InsertLayer(Mathf.Min(firstLayer, visualRows)));
            if (GUI.Button(new Rect(r.width - 153, r.y + 3, 36, 24), "▲")) ScrollTimelineRows(firstLayer - 1);
            if (GUI.Button(new Rect(r.width - 113, r.y + 3, 36, 24), "▼")) ScrollTimelineRows(firstLayer + 1);
            GUI.Label(new Rect(r.width - 73, r.y + 6, 70, 20), (firstLayer + 1) + "-" + Mathf.Min(firstLayer + visibleRows, totalRows));
            var canvas = new Rect(label, r.y + ruler, r.width - label - 8, row * visibleRows);
''' + s[end:]
sub('firstLayer = Mathf.Clamp(firstLayer + (e.delta.y > 0 ? 1 : -1), 0, Mathf.Max(0, studio.project.visualLayerCount - 8))','ScrollTimelineRows(firstLayer + (e.delta.y > 0 ? 1 : -1))')
sub('i < 14 + groupRows','i < visibleRows')
sub('bool audioLane = i >= 8 + groupRows;','int globalRow = firstLayer + i;\n                bool audioLane = globalRow >= visualRows + groupRows;')
sub('bool visualLane = i < 8;','bool visualLane = globalRow < visualRows;')
sub('int index = audioLane ? i - 8 - groupRows : firstLayer + i;','int index = audioLane ? globalRow - visualRows - groupRows : globalRow;')
sub('"G " + (firstGroup + i - 7)','studio.project.groups[globalRow - visualRows].name')
sub('8 + groupRows + Mathf.Clamp(c.audioTrack, 0, 5) : c.layer - firstLayer','visualRows + groupRows + Mathf.Clamp(c.audioTrack, 0, 5) - firstLayer : c.layer - firstLayer')
sub('if (lane < 0 || (c.kind != "WAV" && lane >= 8)) continue;','if (lane < 0 || lane >= visibleRows) continue;')
sub('for (int gi = firstGroup; gi < Mathf.Min(studio.project.groups.Count, firstGroup + groupRows); gi++)','for (int gi = 0; gi < groupRows; gi++)')
sub('var group = studio.project.groups[gi];','int lane = visualRows + gi - firstLayer;\n                if (lane < 0 || lane >= visibleRows) continue;\n                var group = studio.project.groups[gi];')
sub('(8 + gi - firstGroup) * row','lane * row')
sub('Mathf.FloorToInt((e.mousePosition.y - canvas.y) / row) - 8 - groupRows','firstLayer + Mathf.FloorToInt((e.mousePosition.y - canvas.y) / row) - visualRows - groupRows')
sub('Mathf.FloorToInt((e.mousePosition.y - canvas.y) / row) >= 8 ? studio.project.visualLayerCount : Mathf.Clamp(firstLayer + Mathf.FloorToInt((e.mousePosition.y - canvas.y) / row), 0, studio.project.visualLayerCount)','Mathf.Clamp(firstLayer + Mathf.FloorToInt((e.mousePosition.y - canvas.y) / row), 0, studio.project.visualLayerCount)')
p.write_text(s,encoding='utf-8')
