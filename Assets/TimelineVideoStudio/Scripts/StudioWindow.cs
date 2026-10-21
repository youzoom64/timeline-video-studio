using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace TimelineVideoStudio
{
    [RequireComponent(typeof(StudioController), typeof(StudioExporter), typeof(StudioApi))]
    public partial class StudioWindow : MonoBehaviour
    {
        StudioController studio;
        StudioExporter exporter;
        string folder;
        string pathField = "";
        string saveField = "";
        string exportField = "";
        string newTitle = "無題";
        string selectedCache = "";
        string[] edit = new string[3];
        Vector2 browserScroll;
        float zoom = 42f;
        float offset;
        public float TimelineZoom => zoom;
        public float TimelineScrollSeconds => offset / zoom;
        public void SetTimelineZoom(float pixelsPerSecond)
        {
            float anchor = Screen.width * 0.5f;
            Zoom(Mathf.Clamp(pixelsPerSecond, MinimumTimelineZoom(), 400f) / zoom, anchor);
        }
        public void SetTimelineScroll(float seconds) { offset = Mathf.Max(0, seconds) * zoom; }
        string dragClip;
        bool clipDragChanged;
        string dragGroup;
        bool groupDragChanged;
        bool previewDragChanged;
        bool groupSliderChanged;
        float originalGroupStart;
        readonly Dictionary<string, float> originalGroupStarts = new Dictionary<string, float>();
        string dragMode;
        float dragStartX;
        float originalStart;
        float originalDuration;
        int originalLayer;
        int originalAudioTrack;
        float originalSourceOffset;
        string dragFile;
        bool dragPreview;
        Vector2 previewStart;
        float originalX, originalY;
        bool rulerDragging;
        bool contextOpen;
        bool objectMenuOpen;
        bool objectMenuOpenPending;
        Vector2 objectMenuPosition = new Vector2(-1, -1);
        bool draggingObjectMenu;
        Vector2 objectMenuDragMouse;
        Vector2 objectMenuDragOrigin;
        Vector2 contextPosition;
        float contextTime;
        int contextLayer;
        int contextAudioTrack = 4, editAudioTrack = 4;
        string volumeEdit = "1", fadeInEdit = "0", fadeOutEdit = "0";
        Vector2 inspectorScroll;
        string contextClip;
        string pointX = "", pointY = "", pointScale = "", pointRotation = "";
        float selectedPoint = -1;
        int selectedSegment;
        string[] segmentEdit = new string[10];
        string[] axisEdit = new string[4];
        bool sliderGestureActive;
        Vector2 objectScroll;
        string selectedEffectId = "";
        string selectedGroupCache = "";
        string groupNameEdit = "";
        string[] groupEdit = new string[5];
        readonly HashSet<string> multiSelection = new HashSet<string>();
        int firstLayer;
        float timelineHeight = 294f;
        bool resizingTimeline;
        public int TimelineFirstRow { get { return firstLayer; } }
        public float TimelineHeight { get { return timelineHeight; } set { timelineHeight = Mathf.Clamp(value, 174f, Mathf.Max(174f, Screen.height - 260f)); } }
        public void ScrollTimelineRows(int value) { firstLayer = Mathf.Clamp(value, 0, Mathf.Max(0, studio.project.visualLayerCount + (studio.project.groups?.Count ?? 0) + 6 - Mathf.Max(1, Mathf.FloorToInt((timelineHeight - 82) / 23)))); }
        string effectA = "", effectB = "", effectC = "", effectD = "";
        string[] names = { "開始秒", "長さ（秒）", "レイヤー" };
        bool gpuExport = true;
        string bpmText = "120";
        string minimumText = "0";
        const float HeaderHeight=108;
        GUIStyle statusLabelStyle;
        readonly List<StudioScene> scenePages = new List<StudioScene>();
        void CacheScenePages()
        {
            scenePages.Clear();
            if(studio.project.scenes!=null)scenePages.AddRange(studio.project.scenes.OrderBy(scene=>scene.start));
        }

        void Start()
        {
            gpuExport = SystemInfo.graphicsDeviceVendor.IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) >= 0;
            Screen.SetResolution(1600, 900, FullScreenMode.Windowed);
            LoadPanelLayout();
            studio = GetComponent<StudioController>();
            exporter = GetComponent<StudioExporter>();
            folder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            saveField = Path.Combine(folder, "TimelineVideoStudio.json");
            exportField = Path.Combine(folder, "TimelineVideoStudio.mp4");
            studio.ProjectChanged += () => { CacheSelection(); bpmText = studio.project.bpm.ToString("0.##", CultureInfo.InvariantCulture); minimumText = studio.project.minimumDuration.ToString("0.##", CultureInfo.InvariantCulture); };
            bpmText = studio.project.bpm.ToString("0.##", CultureInfo.InvariantCulture);
            minimumText = studio.project.minimumDuration.ToString("0.##", CultureInfo.InvariantCulture);
            studio.ProjectChanged += CacheScenePages;
            CacheScenePages();
        }

        void OnGUI()
        {
            var e = Event.current;
            if (groupSliderChanged && !Input.GetMouseButton(0)) { studio.Rebuild(); groupSliderChanged = false; selectedGroupCache = ""; }
            if (e.type == EventType.Layout && objectMenuOpenPending) { objectMenuOpen = true; objectMenuOpenPending = false; }
            if (!Input.GetMouseButton(0)) sliderGestureActive = false;
            HandleShortcuts(e);
            var header = new Rect(0, 0, Screen.width, HeaderHeight);
            timelineHeight = Mathf.Clamp(timelineHeight, 174f, Mathf.Max(174f, Screen.height - 260f));
            var splitter = new Rect(0, Screen.height - timelineHeight - 4, Screen.width, 8);
            if (e.type == EventType.MouseDown && e.button == 0 && splitter.Contains(e.mousePosition)) { resizingTimeline = true; e.Use(); }
            if (resizingTimeline && e.type == EventType.MouseDrag) { timelineHeight = Mathf.Clamp(Screen.height - e.mousePosition.y, 174f, Mathf.Max(174f, Screen.height - 260f)); e.Use(); }
            if (e.type == EventType.MouseUp) resizingTimeline = false;
            var timeline = new Rect(0, Screen.height - timelineHeight, Screen.width, timelineHeight);
            HandlePanelResize(timeline.y,e);
            var widths=PanelWidths;
            var browser = new Rect(0, HeaderHeight, widths.x, timeline.y - HeaderHeight);
            var inspector = new Rect(Screen.width - widths.y, HeaderHeight, widths.y, timeline.y - HeaderHeight);
            var preview = new Rect(browser.xMax + 8, HeaderHeight+8, inspector.xMin - browser.xMax - 16, timeline.y - HeaderHeight-16);
            bool blockBehind = (objectMenuOpen && e.isMouse && (draggingObjectMenu || ObjectMenuRect().Contains(e.mousePosition))) || (audioFxOpen && e.isMouse && AudioFxRect().Contains(e.mousePosition));
            GUI.enabled = !blockBehind;
            if (!blockBehind) HandleTimelineWheel(timeline, e);
            DrawHeader(header);
            if(browser.width>=140)DrawBrowser(browser);
            DrawPreview(preview, e);
            if(inspector.width>=140)DrawInspector(inspector);
            DrawTimeline(timeline, e);
            DrawPanelDividers(timeline.y);
            DrawSolid(new Rect(0, timeline.y - 3, Screen.width, 6), resizingTimeline ? new Color(.45f, .65f, .8f) : new Color(.3f, .35f, .4f));
            DrawSolid(new Rect(Screen.width / 2 - 24, timeline.y - 1, 48, 2), new Color(.7f, .75f, .8f));
            GUI.enabled = true;
            DrawContextMenu(e);
            DrawObjectMenu(e);
            DrawAudioEffectsWindow();
            if (!string.IsNullOrEmpty(dragFile))
                GUI.Label(new Rect(e.mousePosition.x + 12, e.mousePosition.y + 12, 220, 25), Path.GetFileName(dragFile));
        }

        void DrawHeader(Rect r)
        {
            GUI.Box(r, "");
            GUILayout.BeginArea(new Rect(8, 5, r.width - 16, 30));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("新規作成", GUILayout.Width(90), GUILayout.Height(27))) studio.NewProject(newTitle);
            if (GUILayout.Button("開く", GUILayout.Width(70), GUILayout.Height(27))) Try(() => studio.Load(saveField));
            if (GUILayout.Button("保存", GUILayout.Width(70), GUILayout.Height(27))) Try(() => studio.Save(saveField));
            GUILayout.Space(12);
            if (GUILayout.Button("元に戻す Ctrl+Z", GUILayout.Width(120), GUILayout.Height(27))) studio.Undo();
            if (GUILayout.Button("やり直す Ctrl+Y", GUILayout.Width(120), GUILayout.Height(27))) studio.Redo();
            GUILayout.Space(12);
            if (GUILayout.Button("分割 S", GUILayout.Width(72), GUILayout.Height(27))) SplitSelected();
            if (GUILayout.Button("削除", GUILayout.Width(70), GUILayout.Height(27))) DeleteSelected();
            if (GUILayout.Button("グループ化", GUILayout.Width(90), GUILayout.Height(27))) Try(() => { var group = studio.Group(multiSelection); multiSelection.Clear(); studio.status = group.name + "を作成しました"; });
            GUILayout.FlexibleSpace();
            gpuExport = GUILayout.Toggle(gpuExport, "GPU NVENC", GUILayout.Width(90), GUILayout.Height(27));
            if (GUILayout.Button(exporter.busy ? "出力キャンセル" : "MP4出力", GUILayout.Width(120), GUILayout.Height(27))) Try(() => { if (exporter.busy) exporter.CancelExport(); else { exporter.useGpuEncoding = gpuExport; exporter.StartExport(exportField); } });
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
            float center = r.width * 0.5f;
            if (GUI.Button(new Rect(center - 195, 38, 52, 40), "|◀")) studio.Seek(0);
            if (GUI.Button(new Rect(center - 138, 38, 52, 40), "◀|")) studio.Seek(studio.playhead - 1f / studio.project.fps);
            var old = GUI.backgroundColor; GUI.backgroundColor = studio.playing ? new Color(.95f, .65f, .28f) : new Color(.4f, .84f, .48f);
            if (GUI.Button(new Rect(center - 80, 36, 160, 44), studio.playing ? "⏸ 一時停止 [Space]" : "▶ 再生 [Space]")) studio.TogglePlay();
            GUI.backgroundColor = old;
            if (GUI.Button(new Rect(center + 86, 38, 52, 40), "|▶")) studio.Seek(studio.playhead + 1f / studio.project.fps);
            if (GUI.Button(new Rect(center + 143, 38, 52, 40), "▶|")) studio.Seek(studio.project.duration);
            GUI.Label(new Rect(8, 46, Mathf.Max(100, center - 215), 30), studio.project.title + "  " + studio.playhead.ToString("F2") + " / " + studio.project.duration.ToString("F2") + " 秒");
            if(statusLabelStyle==null)statusLabelStyle=new GUIStyle(GUI.skin.label){wordWrap=false,alignment=TextAnchor.MiddleLeft,clipping=TextClipping.Clip};
            GUI.Label(new Rect(8, 78, r.width - 16, 26), string.IsNullOrEmpty(StudioToolInstaller.Status) ? studio.status : StudioToolInstaller.Status,statusLabelStyle);
        }

        void DrawBrowser(Rect r)
        {
            GUI.Box(r, "素材");
            GUILayout.BeginArea(new Rect(r.x + 8, r.y + 22, r.width - 16, r.height - 25));
            GUILayout.BeginHorizontal();
            folder = GUILayout.TextField(folder, GUILayout.MinWidth(0));
            if (GUILayout.Button("移動", GUILayout.Width(48))) { browserScroll = Vector2.zero; }
            GUILayout.EndHorizontal();
            if (GUILayout.Button(".. 親フォルダー")) folder = Directory.GetParent(folder)?.FullName ?? folder;
            browserScroll = GUILayout.BeginScrollView(browserScroll, GUILayout.Height(Mathf.Max(50, r.height - 184)));
            try
            {
                foreach (var dir in Directory.GetDirectories(folder).OrderBy(Path.GetFileName).Take(150))
                    if (GUILayout.Button("[+] " + Path.GetFileName(dir), GUI.skin.label)) folder = dir;
                foreach (var path in Directory.GetFiles(folder).OrderBy(Path.GetFileName).Take(300))
                {
                    string kind = Kind(path);
                    if (kind == null) continue;
                    var row = GUILayoutUtility.GetRect(new GUIContent(Path.GetFileName(path)), GUI.skin.button, GUILayout.Height(22));
                    if (GUI.Button(row, Path.GetFileName(path)) && Event.current.type == EventType.MouseUp)
                        Try(() => studio.Add(kind, path));
                    if (Event.current.type == EventType.MouseDown && row.Contains(Event.current.mousePosition)) dragFile = path;
                }
            }
            catch (Exception ex) { GUILayout.Label(ex.Message); }
            GUILayout.EndScrollView();
            GUILayout.Label("再生位置に追加:");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("立方体")) studio.Add("Cube");
            if (GUILayout.Button("球体")) studio.Add("Sphere");
            if (GUILayout.Button("文字")) studio.Add("Text", text: "文字");
            GUILayout.EndHorizontal();
            GUILayout.Label("素材のパス (PNG/JPG/MP4/MOV/WAV/OBJ):");
            pathField = GUILayout.TextField(pathField,GUILayout.MinWidth(0));
            if (GUILayout.Button("ファイルを追加")) Try(() => studio.Add(Kind(pathField) ?? throw new ArgumentException("対応する画像・動画・音声・OBJを指定してください"), pathField));
            GUILayout.EndArea();
        }

        void DrawPreview(Rect r, Event e)
        {
            GUI.Box(r, "プレビュー");
            if (studio.previewTexture == null) return;
            float ratio = (float)studio.project.width / studio.project.height;
            float w = Mathf.Min(r.width - 16, (r.height - 40) * ratio);
            float h = w / ratio;
            var image = new Rect(r.center.x - w / 2, r.center.y - h / 2 + 8, w, h);
            GUI.DrawTexture(image, studio.previewTexture, ScaleMode.StretchToFill);
            int page=-1;
            for(int i=0;i<scenePages.Count;i++){if(studio.playhead<scenePages[i].start)break;page=i;}
            if(page>=0)
            {
                var badge=new Rect(image.xMax-154,image.y+8,146,28);
                DrawSolid(badge,new Color(.02f,.04f,.07f,.86f));
                GUI.Label(badge,"シーン "+(page+1)+" / "+scenePages.Count,new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontSize=16,normal={textColor=new Color(.7f,.85f,1)}});
            }
            var selectedGroup = studio.FindGroup(studio.selectedId);
            if (GUI.enabled && selectedGroup != null && image.Contains(e.mousePosition))
            {
                if (e.type == EventType.MouseDown && e.button == 0)
                {
                    dragPreview = true; previewDragChanged = false; previewStart = e.mousePosition; originalX = selectedGroup.x; originalY = selectedGroup.y; e.Use();
                }
                else if (e.type == EventType.MouseDrag && dragPreview)
                {
                    float x = originalX + (e.mousePosition.x - previewStart.x) / image.width * 10f * ratio;
                    float y = originalY - (e.mousePosition.y - previewStart.y) / image.height * 10f;
                    if (Mathf.Abs(x - selectedGroup.x) > 0.0001f || Mathf.Abs(y - selectedGroup.y) > 0.0001f)
                    {
                        if (!previewDragChanged) { studio.Checkpoint(); previewDragChanged = true; }
                        selectedGroup.x = x; selectedGroup.y = y;
                    }
                    e.Use();
                }
            }
            if (GUI.enabled && studio.Find(studio.selectedId) is StudioClip c && c.kind != "WAV" && image.Contains(e.mousePosition))
            {
                if (e.type == EventType.MouseDown && e.button == 0)
                {
                    dragPreview = true; previewDragChanged = false; previewStart = e.mousePosition; originalX = c.x; originalY = c.y; e.Use();
                }
                else if (e.type == EventType.MouseDrag && dragPreview)
                {
                    float x = originalX + (e.mousePosition.x - previewStart.x) / image.width * 10f * ratio;
                    float y = originalY - (e.mousePosition.y - previewStart.y) / image.height * 10f;
                    if (Mathf.Abs(x - c.x) > 0.0001f || Mathf.Abs(y - c.y) > 0.0001f)
                    {
                        if (!previewDragChanged) { studio.Checkpoint(); previewDragChanged = true; }
                        c.x = x; c.y = y;
                    }
                    e.Use();
                }
            }
            if (e.type == EventType.MouseUp) { if (previewDragChanged) { studio.Rebuild(); selectedGroupCache = ""; } previewDragChanged = false; dragPreview = false; }
        }

        void DrawInspector(Rect r)
        {
            GUI.Box(r, "オブジェクト設定");
            GUILayout.BeginArea(new Rect(r.x + 8, r.y + 24, r.width - 16, r.height - 27));
            var group = studio.FindGroup(studio.selectedId);
            if (group != null) { DrawGroupInspector(group); GUILayout.EndArea(); return; }
            var c = studio.Find(studio.selectedId);
            inspectorScroll = GUILayout.BeginScrollView(inspectorScroll);
            if (c == null)
            {
                GUILayout.Label("タイムラインのオブジェクトを選択してください。",GUILayout.MaxWidth(r.width-42));
                GUILayout.Label("画面: " + studio.project.width + "×" + studio.project.height + " / " + studio.project.fps + " FPS");
                GUILayout.Label("動画の長さ: " + studio.project.duration.ToString("F2") + " 秒（最後のオブジェクトまで）");
                GUILayout.Label("最低動画長（秒）:");
                GUILayout.BeginHorizontal(); minimumText = GUILayout.TextField(minimumText); if (GUILayout.Button("設定", GUILayout.Width(46))) Try(() => { studio.Checkpoint(); studio.project.minimumDuration = Mathf.Max(0, float.Parse(minimumText, CultureInfo.InvariantCulture)); studio.Rebuild(); }); GUILayout.EndHorizontal();
                GUILayout.Label("プロジェクト JSON:"); saveField = GUILayout.TextField(saveField,GUILayout.MinWidth(0));
                GUILayout.Label("出力 MP4:"); exportField = GUILayout.TextField(exportField,GUILayout.MinWidth(0));
                GUILayout.Label("新規プロジェクト名:"); newTitle = GUILayout.TextField(newTitle);
                GUILayout.EndScrollView(); GUILayout.EndArea(); return;
            }
            if (selectedCache != c.id) CacheSelection();
            GUILayout.Label(KindLabel(c.kind) + " / " + (c.kind == "Text" ? c.text : Path.GetFileName(c.path)),GUILayout.MaxWidth(r.width-42));
            GUILayout.Label("タイムライン上の配置");
            for (int i = 0; i < edit.Length; i++)
            {
                if (c.kind == "WAV" && i == 2) continue;
                GUILayout.BeginHorizontal();
                GUILayout.Label(names[i], GUILayout.Width(72));
                edit[i] = GUILayout.TextField(edit[i]);
                GUILayout.EndHorizontal();
            }
            if (c.kind == "WAV") DrawAudioSettings();
            if (c.kind == "Text")
            {
                GUILayout.Label("文字:"); editText = GUILayout.TextField(editText);
            }
            if (GUILayout.Button("配置を反映")) ApplyEdit(c);
            if (c.kind != "WAV" && GUILayout.Button("動き・効果を編集")) { SelectSegment(c, 0); OpenObjectMenu(); }
            if (!string.IsNullOrEmpty(c.groupId))
            {
                var memberGroup = studio.FindGroup(c.groupId);
                GUILayout.Label("所属: " + (memberGroup?.name ?? "グループ"));
                if (GUILayout.Button("グループを選択")) { studio.selectedId = c.groupId; objectMenuOpen = false; }
                if (GUILayout.Button("グループ解除")) Try(() => studio.Ungroup(c.groupId));
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("複製")) studio.Duplicate(c.id);
            if (GUILayout.Button("削除")) DeleteSelected();
            GUILayout.EndHorizontal();
            GUILayout.Label("保存先 / 出力先:");
            saveField = GUILayout.TextField(saveField,GUILayout.MinWidth(0));
            exportField = GUILayout.TextField(exportField,GUILayout.MinWidth(0));
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
        void DrawGroupInspector(StudioGroup group)
        {
            if (selectedGroupCache != group.id)
            {
                selectedGroupCache = group.id;
                groupNameEdit = group.name;
                float[] values = { studio.GroupStart(group.id), group.x, group.y, group.scale, group.rotation };
                for (int i = 0; i < values.Length; i++) groupEdit[i] = values[i].ToString("0.###", CultureInfo.InvariantCulture);
                selectedEffectId = "";
            }
            objectScroll = GUILayout.BeginScrollView(objectScroll);
            GUILayout.Label("グループオブジェクト / " + studio.GroupMembers(group.id).Count + " 個");
            GUILayout.Label("時間 " + studio.GroupStart(group.id).ToString("F2") + "～" + studio.GroupEnd(group.id).ToString("F2") + " 秒");
            GUILayout.Label("名前"); groupNameEdit = GUILayout.TextField(groupNameEdit);
            string[] labels = { "開始秒", "X", "Y", "拡大率", "回転" };
            for (int i = 0; i < groupEdit.Length; i++)
            {
                float current = i == 0 ? studio.GroupStart(group.id) : i == 1 ? group.x : i == 2 ? group.y : i == 3 ? group.scale : group.rotation;
                float min = i == 0 ? 0f : i == 3 ? 0.01f : i == 4 ? -360f : -20f;
                float max = i == 0 ? Mathf.Max(30f, studio.project.duration) : i == 3 ? 5f : i == 4 ? 360f : 20f;
                min = Mathf.Min(min, current); max = Mathf.Max(max, current);
                GUILayout.BeginHorizontal();
                GUILayout.Label(labels[i], GUILayout.Width(58));
                float moved = GUILayout.HorizontalSlider(current, min, max, GUILayout.Width(112));
                groupEdit[i] = GUILayout.TextField(groupEdit[i], GUILayout.Width(66));
                GUILayout.EndHorizontal();
                if (Mathf.Abs(moved - current) > 0.0001f)
                {
                    if (!groupSliderChanged) { studio.Checkpoint(); groupSliderChanged = true; }
                    if (i == 0)
                    {
                        float delta = Mathf.Max(0, moved) - studio.GroupStart(group.id);
                        foreach (var member in studio.GroupMembers(group.id)) member.start = Mathf.Max(0, member.start + delta);
                    }
                    else if (i == 1) group.x = moved;
                    else if (i == 2) group.y = moved;
                    else if (i == 3) group.scale = Mathf.Max(0.001f, moved);
                    else group.rotation = moved;
                    selectedGroupCache = "";
                }
            }
            if (GUILayout.Button("グループに反映")) Try(() =>
            {
                var values = groupEdit.Select(v => float.Parse(v, CultureInfo.InvariantCulture)).ToArray();
                studio.ChangeGroup(group.id, g => { g.name = groupNameEdit; g.x = values[1]; g.y = values[2]; g.scale = values[3]; g.rotation = values[4]; });
                studio.MoveGroup(group.id, values[0]); selectedGroupCache = "";
            });
            GUILayout.Label("共通効果");
            foreach (var definition in StudioPlugins.List())
                if (GUILayout.Button("+ " + definition.title)) Try(() => studio.AddGroupEffect(group.id, definition.id));
            foreach (var effect in group.effects ?? new List<StudioEffect>())
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(effect.plugin + (effect.enabled ? " ✓" : " 無効"))) SelectEffect(effect);
                bool remove = GUILayout.Button("X", GUILayout.Width(28));
                GUILayout.EndHorizontal();
                if (remove) { Try(() => studio.DeleteGroupEffect(group.id, effect.id)); selectedEffectId = ""; break; }
            }
            var selected = group.effects?.FirstOrDefault(x => x.id == selectedEffectId);
            if (selected != null)
            {
                GUILayout.Label("効果の設定値 A / B / C / D");
                GUILayout.BeginHorizontal(); effectA = GUILayout.TextField(effectA); effectB = GUILayout.TextField(effectB); effectC = GUILayout.TextField(effectC); effectD = GUILayout.TextField(effectD); GUILayout.EndHorizontal();
                if (GUILayout.Button("設定を反映")) Try(() => studio.SetGroupEffect(group.id, selected.id, fx => { fx.a = float.Parse(effectA, CultureInfo.InvariantCulture); fx.b = float.Parse(effectB, CultureInfo.InvariantCulture); fx.c = float.Parse(effectC, CultureInfo.InvariantCulture); fx.d = float.Parse(effectD, CultureInfo.InvariantCulture); }));
                if (GUILayout.Button(selected.enabled ? "無効にする" : "有効にする")) Try(() => studio.SetGroupEffect(group.id, selected.id, fx => fx.enabled = !fx.enabled));
            }
            if (GUILayout.Button("グループ解除")) Try(() => { studio.Ungroup(group.id); selectedGroupCache = ""; });
            GUILayout.EndScrollView();
        }
        void DrawAudioSettings()
        {
            if(GUILayout.Button("音声エフェクト・プリセット…")){var c=studio.Find(studio.selectedId);if(c!=null)OpenAudioFx(c.id,c.audioTrack);}
            GUILayout.Label("音声トラック");
            editAudioTrack = GUILayout.SelectionGrid(editAudioTrack, StudioProject.AudioTrackNames, 2);
            var audioClip=studio.Find(studio.selectedId);
            if(audioClip!=null){bool enabled=GUILayout.Toggle(audioClip.loop,"ループ再生を有効");if(enabled!=audioClip.loop)Try(()=>studio.Change(audioClip.id,c=>c.loop=enabled));}
            GUILayout.BeginHorizontal(); GUILayout.Label("音量（倍）", GUILayout.Width(100)); volumeEdit = GUILayout.TextField(volumeEdit); GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal(); GUILayout.Label("フェード入（秒）", GUILayout.Width(110)); fadeInEdit = GUILayout.TextField(fadeInEdit); GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal(); GUILayout.Label("フェード出（秒）", GUILayout.Width(110)); fadeOutEdit = GUILayout.TextField(fadeOutEdit); GUILayout.EndHorizontal();
        }
        string editText = "";
        void CacheSelection()
        {
            var c = studio.Find(studio.selectedId);
            selectedCache = c?.id ?? "";
            if (c == null) return;
            float[] values = { c.start, c.duration, c.layer };
            for (int i = 0; i < values.Length; i++) edit[i] = values[i].ToString("0.###", CultureInfo.InvariantCulture);
            editText = c.text;
            editAudioTrack = Mathf.Clamp(c.audioTrack, 0, 5);
            volumeEdit = c.volume.ToString("0.###", CultureInfo.InvariantCulture);
            fadeInEdit = c.fadeIn.ToString("0.###", CultureInfo.InvariantCulture);
            fadeOutEdit = c.fadeOut.ToString("0.###", CultureInfo.InvariantCulture);
        }
        void ApplyEdit(StudioClip c)
        {
            Try(() =>
            {
                var values = edit.Select(v => float.Parse(v, CultureInfo.InvariantCulture)).ToArray();
                studio.Change(c.id, x =>
                {
                    x.start = values[0]; x.duration = values[1]; x.layer = Mathf.RoundToInt(values[2]);
                    if (x.kind == "Text") x.text = editText;
                    if (x.kind == "WAV") {
                        x.audioTrack = editAudioTrack;
                        x.volume = Mathf.Clamp(float.Parse(volumeEdit, CultureInfo.InvariantCulture), 0, 4);
                        x.fadeIn = Mathf.Max(0, float.Parse(fadeInEdit, CultureInfo.InvariantCulture));
                        x.fadeOut = Mathf.Max(0, float.Parse(fadeOutEdit, CultureInfo.InvariantCulture));
                    }
                });
                CacheSelection();
            });
        }

        public void HandleTimelineWheel(Rect r, Event e)
        {
            if (e.type != EventType.ScrollWheel || !r.Contains(e.mousePosition)) return;
            float delta = Mathf.Abs(e.delta.y) > .001f ? e.delta.y : e.delta.x;
            if (Mathf.Abs(delta) < .001f) return;
            bool shift = e.shift || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (e.control) Zoom(delta < 0 ? 1.35f : 1f / 1.35f, e.mousePosition.x);
            else if (shift) ScrollTimelineRows(firstLayer + (delta > 0 ? 1 : -1));
            else if (e.alt) offset = Mathf.Max(0, offset + delta * 60f);
            else offset = Mathf.Max(0, offset + delta * 25f);
            e.Use();
        }

        void DrawTimeline(Rect r, Event e)
        {
            GUI.Box(r, "");
            const float label = 88f, ruler = 58f, row = 23f;
            GUI.Label(new Rect(8, r.y + 5, 74, 21), "時間軸");
            if (GUI.Button(new Rect(90, r.y + 3, 24, 24), "-") ) Zoom(0.8f, r.width / 2);
            if (GUI.Button(new Rect(116, r.y + 3, 24, 24), "+") ) Zoom(1.25f, r.width / 2);
            if (GUI.Button(new Rect(680, r.y + 3, 52, 24), "全体")) FitTimeline(r.width - label - 8f);
            int frameNumber = Mathf.RoundToInt(studio.playhead * studio.project.fps);
            GUI.Label(new Rect(148, r.y + 5, 375, 22), "時間 " + Timecode(frameNumber, studio.project.fps) + "   フレーム " + frameNumber + "   FPS " + studio.project.fps);
            GUI.Label(new Rect(525, r.y + 5, 34, 22), "BPM");
            bpmText = GUI.TextField(new Rect(562, r.y + 3, 55, 23), bpmText);
            if (GUI.Button(new Rect(621, r.y + 3, 48, 23), "設定")) Try(() => { studio.Checkpoint(); studio.project.bpm = Mathf.Clamp(float.Parse(bpmText, CultureInfo.InvariantCulture), 20f, 300f); studio.Rebuild(); });
            GUI.Label(new Rect(740, r.y + 6, 480, 20), "Ctrl＋ホイール: 拡大縮小   Shift＋ホイール: 層の上下   Alt＋ホイール: 横移動");
            int groupRows = studio.project.groups?.Count ?? 0;
            int visualRows = studio.project.visualLayerCount;
            int totalRows = visualRows + groupRows + 6;
            int visibleRows = Mathf.Min(totalRows, Mathf.Max(1, Mathf.FloorToInt((r.height - ruler - 24) / row)));
            firstLayer = Mathf.Clamp(firstLayer, 0, Mathf.Max(0, totalRows - visibleRows));
            if (GUI.Button(new Rect(r.width - 235, r.y + 3, 78, 24), "層を挿入")) Try(() => studio.InsertLayer(Mathf.Min(firstLayer, visualRows)));
            if (GUI.Button(new Rect(r.width - 153, r.y + 3, 36, 24), "▲")) ScrollTimelineRows(firstLayer - 1);
            if (GUI.Button(new Rect(r.width - 113, r.y + 3, 36, 24), "▼")) ScrollTimelineRows(firstLayer + 1);
            GUI.Label(new Rect(r.width - 73, r.y + 6, 70, 20), (firstLayer + 1) + "-" + Mathf.Min(firstLayer + visibleRows, totalRows));
            var canvas = new Rect(label, r.y + ruler, r.width - label - 8, row * visibleRows);
            var seekZone = new Rect(label, r.y + 29, canvas.width, 29);
            DrawSolid(seekZone, new Color(.25f, .29f, .34f));
            GUI.Label(new Rect(8, r.y + 34, 75, 20), "シーク ▶");
            DrawSolid(new Rect(label, seekZone.yMax - 2, canvas.width, 2), new Color(.45f, .48f, .51f));
            float visibleStart = offset / zoom;
            float visibleEnd = (offset + canvas.width) / zoom;
            float beatLength = 60f / Mathf.Clamp(studio.project.bpm, 20f, 300f);
            int beatStep = Mathf.Max(1, Mathf.CeilToInt(8f / (beatLength * zoom)));
            int firstBeat = Mathf.Max(0, Mathf.CeilToInt(visibleStart / beatLength / beatStep) * beatStep);
            int lastBeat = Mathf.CeilToInt(visibleEnd / beatLength);
            for (int beat = firstBeat; beat <= lastBeat; beat += beatStep)
            {
                float x = label + beat * beatLength * zoom - offset;
                DrawSolid(new Rect(x, seekZone.yMax - 8, 1, canvas.height + 8), beat % 4 == 0 ? new Color(.31f, .62f, .67f, .7f) : new Color(.29f, .48f, .53f, .45f));
                if (beat % 4 == 0 && beatLength * zoom >= 100f)
                    GUI.Label(new Rect(x + 2, seekZone.yMax - 11, 35, 11), "B" + (beat / 4 + 1));
            }
            int secondStep = Mathf.Max(1, Mathf.CeilToInt(65f / zoom));
            int firstSecond = Mathf.Max(0, Mathf.CeilToInt(visibleStart / secondStep) * secondStep);
            for (int t = firstSecond; t <= Mathf.CeilToInt(visibleEnd); t += secondStep)
            {
                float x = label + t * zoom - offset;
                if (x < label || x > r.xMax) continue;
                DrawSolid(new Rect(x, r.y + 32, 1, canvas.height + 26), new Color(.25f, .28f, .32f));
                GUI.Label(new Rect(x + 2, r.y + 31, 58, 18), (t / 60).ToString("00") + ":" + (t % 60).ToString("00"));
            }
            if (zoom >= 75)
            {
                int step = Mathf.Max(1, Mathf.CeilToInt(32f * studio.project.fps / zoom));
                int startFrame = Mathf.Max(0, Mathf.FloorToInt(visibleStart * studio.project.fps));
                int endFrame = Mathf.CeilToInt(visibleEnd * studio.project.fps);
                for (int frame = startFrame; frame <= endFrame; frame++)
                {
                    if (frame % step != 0 || frame % studio.project.fps == 0) continue;
                    float x = label + frame * zoom / studio.project.fps - offset;
                    DrawSolid(new Rect(x, seekZone.y + 4, 1, 5), new Color(.7f, .7f, .7f));
                    if (zoom >= 150) GUI.Label(new Rect(x + 2, seekZone.y + 8, 34, 15), "F" + frame);
                }
            }
            for (int i = 0; i < visibleRows; i++)
            {
                float y = canvas.y + i * row;
                DrawSolid(new Rect(0, y, r.width, row - 1), i % 2 == 0 ? new Color(.17f, .19f, .22f) : new Color(.14f, .16f, .19f));
                int globalRow = firstLayer + i;
                bool audioLane = globalRow >= visualRows + groupRows;
                bool visualLane = globalRow < visualRows;
                int index = audioLane ? globalRow - visualRows - groupRows : globalRow;
                string laneTitle = audioLane ? StudioProject.AudioTrackNames[index] : visualLane ? "層 " + (index + 1) : studio.project.groups[globalRow - visualRows].name;
                bool enabled = audioLane ? studio.AudioTrackEnabled(index) : !visualLane || studio.VisualLayerEnabled(index);
                if (audioLane || visualLane)
                {
                    var titleRect = new Rect(2, y + 1, 84, row - 2);
                    var oldColor = GUI.color; if (!enabled) GUI.color = new Color(.55f, .55f, .55f);
                    if (GUI.Button(titleRect, (enabled ? "● " : "○ ") + laneTitle)) Try(() => studio.ToggleLayer(audioLane, index));
                    GUI.color = oldColor;
                    if(audioLane && e.type==EventType.MouseDown && e.button==1 && titleRect.Contains(e.mousePosition)){OpenAudioFx(null,index);e.Use();}
                }
                else GUI.Label(new Rect(8, y + 4, 78, 20), laneTitle);
            }
            string selectedGroupId = studio.FindGroup(studio.selectedId)?.id;
            foreach (var c in studio.project.clips.OrderBy(c => c.layer))
            {
                int lane = c.kind == "WAV" ? visualRows + groupRows + Mathf.Clamp(c.audioTrack, 0, 5) - firstLayer : c.layer - firstLayer;
                if (lane < 0 || lane >= visibleRows) continue;
                float x = label + c.start * zoom - offset;
                var cr = new Rect(x, canvas.y + lane * row + 2, Mathf.Max(4, c.duration * zoom), row - 5);
                if (cr.xMax < label || cr.x > r.xMax) continue;
                bool selectedGroupMember = !string.IsNullOrEmpty(selectedGroupId) && c.groupId == selectedGroupId;
                DrawSolid(cr, c.kind == "WAV" ? new Color(.25f, .60f, .37f) : selectedGroupMember ? new Color(.95f, .67f, .25f) : multiSelection.Contains(c.id) || c.id == studio.selectedId ? new Color(.95f, .67f, .25f) : !string.IsNullOrEmpty(c.groupId) ? new Color(.60f, .40f, .76f) : new Color(.34f, .49f, .75f));
                if (selectedGroupMember) DrawSolid(new Rect(cr.x, cr.y, cr.width, 2), Color.white);
                DrawSolid(new Rect(cr.x, cr.y + 1, Mathf.Min(7, cr.width / 2), cr.height - 2), new Color(1f, 1f, 1f, .48f));
                DrawSolid(new Rect(cr.xMax - Mathf.Min(7, cr.width / 2), cr.y + 1, Mathf.Min(7, cr.width / 2), cr.height - 2), new Color(1f, 1f, 1f, .48f));
                GUI.Label(new Rect(cr.x + 4, cr.y + 2, cr.width - 7, cr.height - 2), c.kind == "Text" ? c.text : (string.IsNullOrEmpty(c.path) ? KindLabel(c.kind) : Path.GetFileName(c.path)));
                if (c.keyframes != null)
                    foreach (var point in c.keyframes)
                    {
                        float px = cr.x + point.time * zoom;
                        DrawSolid(new Rect(px - 2, cr.y + 2, 4, cr.height - 4), Color.yellow);
                    }
                if (GUI.enabled && e.type == EventType.MouseDown && e.button == 1 && cr.Contains(e.mousePosition) && canvas.Contains(e.mousePosition))
                {
                    studio.selectedId = c.id; CacheSelection(); OpenContext(e.mousePosition, c.id, Mathf.Clamp((e.mousePosition.x - label + offset) / zoom, c.start, c.start + c.duration), c.layer); e.Use();
                }
                if (GUI.enabled && e.type == EventType.MouseDown && e.button == 0 && cr.Contains(e.mousePosition))
                {
                    if (e.control) { if (!multiSelection.Add(c.id)) multiSelection.Remove(c.id); }
                    else { multiSelection.Clear(); multiSelection.Add(c.id); }
                    studio.selectedId = c.id; CacheSelection();
                    SelectSegment(c, SegmentAt(c, Mathf.Clamp((e.mousePosition.x - cr.x) / zoom, 0, c.duration)));
                    if (e.clickCount >= 2) { OpenObjectMenu(); contextOpen = false; e.Use(); continue; }
                    dragClip = c.id; originalStart = c.start; originalDuration = c.duration; originalLayer = c.layer; originalAudioTrack=c.audioTrack;originalSourceOffset=c.sourceOffset;
                    clipDragChanged = false;
                    dragStartX = e.mousePosition.x;
                    dragMode = cr.width < 20 ? "move" : e.mousePosition.x - cr.x < 7 ? "left" : cr.xMax - e.mousePosition.x < 7 ? "right" : "move";
                    e.Use();
                }
            }
            for (int gi = 0; gi < groupRows; gi++)
            {
                int lane = visualRows + gi - firstLayer;
                if (lane < 0 || lane >= visibleRows) continue;
                var group = studio.project.groups[gi];
                var members = studio.GroupMembers(group.id);
                if (members.Count == 0) continue;
                float start = studio.GroupStart(group.id), end = studio.GroupEnd(group.id);
                var gr = new Rect(label + start * zoom - offset, canvas.y + lane * row + 2, Mathf.Max(4, (end - start) * zoom), row - 5);
                if (gr.xMax < label || gr.x > r.xMax) continue;
                DrawSolid(gr, studio.selectedId == group.id ? new Color(.95f, .67f, .25f) : new Color(.55f, .36f, .72f));
                GUI.Label(new Rect(gr.x + 4, gr.y + 1, gr.width - 6, gr.height - 2), "▣ " + group.name + " (" + members.Count + ")");
                if (GUI.enabled && e.type == EventType.MouseDown && e.button == 0 && gr.Contains(e.mousePosition))
                {
                    studio.selectedId = group.id; selectedGroupCache = ""; objectMenuOpen = false;
                    if (e.clickCount >= 2) { OpenObjectMenu(); contextOpen = false; e.Use(); continue; }
                    dragGroup = group.id; originalGroupStart = start; dragStartX = e.mousePosition.x;
                    groupDragChanged = false;
                    originalGroupStarts.Clear(); foreach (var member in members) originalGroupStarts[member.id] = member.start;
                    e.Use();
                }
            }
            if (GUI.enabled && e.type == EventType.MouseDown && e.button == 1 && canvas.Contains(e.mousePosition))
            {
                contextAudioTrack = Mathf.Clamp(firstLayer + Mathf.FloorToInt((e.mousePosition.y - canvas.y) / row) - visualRows - groupRows, 0, 5);
                OpenContext(e.mousePosition, "", Mathf.Max(0, (e.mousePosition.x - label + offset) / zoom),
                    Mathf.Clamp(firstLayer + Mathf.FloorToInt((e.mousePosition.y - canvas.y) / row), 0, studio.project.visualLayerCount)); e.Use();
            }
            float head = label + studio.playhead * zoom - offset;
            DrawSolid(new Rect(head, seekZone.y, 2, canvas.height + seekZone.height), Color.red);
            if (GUI.enabled && e.type == EventType.MouseDown && e.button == 0 && seekZone.Contains(e.mousePosition))
            {
                rulerDragging = true; studio.Seek(Mathf.Max(0, (e.mousePosition.x - label + offset) / zoom)); e.Use();
            }
            if (GUI.enabled && e.type == EventType.MouseDrag && rulerDragging)
            {
                studio.Seek(Mathf.Max(0, (e.mousePosition.x - label + offset) / zoom)); e.Use();
            }
            if (GUI.enabled && e.type == EventType.MouseDrag && !string.IsNullOrEmpty(dragClip))
            {
                var c = studio.Find(dragClip);
                if (c != null)
                {
                    float delta = Mathf.Round((e.mousePosition.x - dragStartX) / zoom * studio.project.fps) / studio.project.fps;
                    float nextStart = c.start, nextDuration = c.duration, nextSourceOffset=c.sourceOffset;
                    int nextLayer = c.layer;
                    int nextAudioTrack = c.audioTrack;
                    if (dragMode == "move")
                    {
                        nextStart = Mathf.Max(0, originalStart + delta);
                        if (c.kind != "WAV") nextLayer = Mathf.Clamp(firstLayer + Mathf.FloorToInt((e.mousePosition.y - canvas.y) / row), 0, studio.project.visualLayerCount - 1);
                        else nextAudioTrack = Mathf.Clamp(firstLayer + Mathf.FloorToInt((e.mousePosition.y - canvas.y) / row) - visualRows - groupRows, 0, 5);
                    }
                    else if (dragMode == "right") nextDuration = Mathf.Min(studio.SourceDuration(c.id)-originalSourceOffset,Mathf.Max(1f / studio.project.fps, originalDuration + delta));
                    else {
                        float earliest=(c.kind=="WAV"&&!c.loop||c.kind=="Video")?Mathf.Max(0,originalStart-originalSourceOffset):0;
                        nextStart=Mathf.Clamp(originalStart+delta,earliest,originalStart+originalDuration-1f/studio.project.fps);
                        nextDuration=originalDuration-(nextStart-originalStart);
                        if(c.kind=="WAV"||c.kind=="Video") nextSourceOffset=originalSourceOffset+nextStart-originalStart;
                        if(c.kind=="WAV"&&c.loop)nextSourceOffset=Mathf.Repeat(nextSourceOffset,studio.RawSourceDuration(c.id));
                    }
                    if (Mathf.Abs(nextStart - c.start) > 0.0001f || Mathf.Abs(nextDuration - c.duration) > 0.0001f || nextLayer != c.layer || nextAudioTrack != c.audioTrack)
                    {
                        if (!clipDragChanged) { studio.Checkpoint(); clipDragChanged = true; }
                        c.start = nextStart; c.duration = nextDuration; c.sourceOffset=nextSourceOffset; c.layer = nextLayer; c.audioTrack = nextAudioTrack;
                    }
                    e.Use();
                }
            }
            if (GUI.enabled && e.type == EventType.MouseDrag && !string.IsNullOrEmpty(dragGroup))
            {
                float delta = Mathf.Round((e.mousePosition.x - dragStartX) / zoom * studio.project.fps) / studio.project.fps;
                delta = Mathf.Max(-originalGroupStart, delta);
                if (Mathf.Abs(delta) >= 0.5f / studio.project.fps)
                {
                    if (!groupDragChanged) { studio.Checkpoint(); groupDragChanged = true; }
                    foreach (var member in studio.GroupMembers(dragGroup))
                        if (originalGroupStarts.TryGetValue(member.id, out float start)) member.start = start + delta;
                    selectedGroupCache = "";
                }
                e.Use();
            }
            if (GUI.enabled && e.type == EventType.MouseUp)
            {
                if (!string.IsNullOrEmpty(dragClip)) { if (clipDragChanged) studio.CommitClipTiming(dragClip,originalAudioTrack); CacheSelection(); dragClip = ""; clipDragChanged = false; }
                if (!string.IsNullOrEmpty(dragGroup)) { if (groupDragChanged) studio.Rebuild(); dragGroup = ""; groupDragChanged = false; originalGroupStarts.Clear(); selectedGroupCache = ""; }
                if (!string.IsNullOrEmpty(dragFile) && canvas.Contains(e.mousePosition))
                {
                    string kind = Kind(dragFile);
                    if (kind != null) Try(() => {
                        var added = studio.Add(kind, dragFile, start: Mathf.Max(0, (e.mousePosition.x - label + offset) / zoom), layer: Mathf.Clamp(firstLayer + Mathf.RoundToInt((e.mousePosition.y - canvas.y) / row), 0, studio.project.visualLayerCount - 1));
                        if (kind == "WAV") studio.Change(added.id, c => c.audioTrack = Mathf.Clamp(firstLayer + Mathf.FloorToInt((e.mousePosition.y - canvas.y) / row) - visualRows - groupRows, 0, 5));
                    });
                }
                dragFile = ""; rulerDragging = false;
            }
            GUI.Label(new Rect(8, r.yMax - 23, r.width - 16, 20), "グループバーを選択・ドラッグ: 一括移動   Ctrl+クリック: 複数選択   右クリック: 追加・編集   Space: 再生");
            if (!GUI.enabled && e.type == EventType.MouseUp) { if(clipDragChanged) studio.CommitClipTiming(dragClip,originalAudioTrack);if(groupDragChanged)studio.Rebuild(); dragClip = ""; clipDragChanged = false; dragGroup = ""; groupDragChanged = false; rulerDragging = false; }
        }

        Rect ObjectMenuRect()
        {
            float width = 600;
            float height = Mathf.Clamp(Screen.height - 300, 320, 650);
            if (objectMenuPosition.x < 0) objectMenuPosition = new Vector2(Mathf.Max(270, Screen.width - 740), 105);
            return new Rect(objectMenuPosition.x, objectMenuPosition.y, width, height);
        }
        void OpenObjectMenu() { objectMenuOpenPending = true; }

        void OpenContext(Vector2 position, string id, float time, int layer)
        {
            contextPosition = position; contextClip = id; contextTime = Mathf.Round(time * studio.project.fps) / studio.project.fps;
            contextLayer = layer; contextOpen = true; objectMenuOpen = false;
        }

        void DrawContextMenu(Event e)
        {
            if (!contextOpen) return;
            var items = new System.Collections.Generic.List<(string, Action)>();
            if (string.IsNullOrEmpty(contextClip))
            {
                if (contextLayer < studio.project.visualLayerCount)
                {
                    items.Add(("追加 / 画像ファイル...", () => AddFileAt("Image")));
                    items.Add(("追加 / 動画ファイル...", () => AddFileAt("Video")));
                    items.Add(("追加 / 文字", () => studio.Add("Text", text: "文字", start: contextTime, layer: contextLayer)));
                    items.Add(("追加 / 立方体 (3D)", () => studio.Add("Cube", start: contextTime, layer: contextLayer)));
                    items.Add(("追加 / 球体 (3D)", () => studio.Add("Sphere", start: contextTime, layer: contextLayer)));
                    items.Add(("追加 / OBJモデル...", () => AddFileAt("OBJ")));
                }
                items.Add(("追加 / WAV音声...", () => AddFileAt("WAV")));
                items.Add(("ここに映像レイヤーを挿入", () => studio.InsertLayer(contextLayer)));
                items.Add(("選択物をここに複製", () => { var copy = studio.Duplicate(studio.selectedId); studio.Change(copy.id, x => { x.start = contextTime; if(x.kind=="WAV") x.audioTrack=contextAudioTrack;else x.layer = Mathf.Clamp(contextLayer, 0, studio.project.visualLayerCount - 1); }); }));
                items.Add(("ここへシーク", () => studio.Seek(contextTime)));
            }
            else
            {
                var c = studio.Find(contextClip);
                if (c == null) { contextOpen = false; return; }
                items.Add(("オブジェクト設定...", () => { studio.selectedId = c.id; CacheSelection(); SelectSegment(c, SegmentAt(c, contextTime - c.start)); OpenObjectMenu(); }));
                if (c.kind != "WAV")
                {
                    items.Add(("中間点 / ここに追加", () => { studio.AddKeyframe(c.id, contextTime); SelectSegment(c, SegmentAt(c, contextTime - c.start)); OpenObjectMenu(); }));
                    var closest = c.keyframes?.OrderBy(p => Mathf.Abs(c.start + p.time - contextTime)).FirstOrDefault();
                    if (closest != null && Mathf.Abs(c.start + closest.time - contextTime) <= 5f / zoom)
                    {
                        float pointAt = c.start + closest.time;
                        items.Add(("中間点 / 区間を編集", () => { SelectSegment(c, SegmentAt(c, closest.time)); OpenObjectMenu(); }));
                        items.Add(("中間点 / 削除", () => studio.DeleteKeyframe(c.id, pointAt)));
                    }
                }
                items.Add(("ここで分割", () => studio.Split(c.id, contextTime)));
                items.Add(("複製", () => studio.Duplicate(c.id)));
                items.Add(("削除", () => studio.Delete(c.id)));
            }
            float h = items.Count * 25 + 8;
            var rect = new Rect(Mathf.Clamp(contextPosition.x, 0, Screen.width - 240), Mathf.Clamp(contextPosition.y, 0, Screen.height - h), 238, h);
            GUI.Box(rect, "");
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (GUI.Button(new Rect(rect.x + 4, rect.y + 4 + i * 25, rect.width - 8, 24), item.Item1))
                { contextOpen = false; Try(item.Item2); break; }
            }
            if (e.type == EventType.MouseDown && !rect.Contains(e.mousePosition)) { contextOpen = false; e.Use(); }
        }

        void AddFileAt(string kind)
        {
            if (Kind(pathField) != kind) throw new ArgumentException("先に素材欄へ " + kind + " ファイルのパスを入力してください");
            var added = studio.Add(kind, pathField, start: contextTime, layer: Mathf.Clamp(contextLayer, 0, studio.project.visualLayerCount - 1));
            if (kind == "WAV") studio.Change(added.id, c => c.audioTrack = contextAudioTrack);
        }

        void OpenPoint(StudioClip c, float time)
        {
            selectedPoint = time;
            var point = c.keyframes?.FirstOrDefault(p => Mathf.Abs(p.time - time) < 0.5f / studio.project.fps);
            if (point == null) return;
            pointX = point.x.ToString(CultureInfo.InvariantCulture);
            pointY = point.y.ToString(CultureInfo.InvariantCulture);
            pointScale = point.scale.ToString(CultureInfo.InvariantCulture);
            pointRotation = point.rotation.ToString(CultureInfo.InvariantCulture);
        }
        int SegmentAt(StudioClip c, float localTime) { return c.keyframes == null ? 0 : c.keyframes.Count(p => p.time <= localTime); }
        void SelectSegment(StudioClip c, int index)
        {
            if (c.kind == "WAV") { selectedSegment = 0; return; }
            selectedSegment = index;
            var s = studio.GetSegment(c.id, index);
            float[] values = { s.startX, s.startY, s.startZ, s.startScale, s.startRotation, s.endX, s.endY, s.endZ, s.endScale, s.endRotation };
            for (int i = 0; i < values.Length; i++) segmentEdit[i] = values[i].ToString("0.###", CultureInfo.InvariantCulture);
            float[] axes={s.startScaleX,s.startScaleY,s.endScaleX,s.endScaleY};
            for(int i=0;i<4;i++) axisEdit[i]=axes[i].ToString("0.###",CultureInfo.InvariantCulture);
        }

        void DrawObjectMenu(Event e)
        {
            if (!objectMenuOpen) return;
            var c = studio.Find(studio.selectedId);
            var group = studio.FindGroup(studio.selectedId);
            if (c == null && group == null) { objectMenuOpen = false; return; }
            float width = 600;
            float height = Mathf.Clamp(Screen.height - 300, 320, 650);
            ObjectMenuRect();
            objectMenuPosition.x = Mathf.Clamp(objectMenuPosition.x, 0, Mathf.Max(0, Screen.width - width));
            objectMenuPosition.y = Mathf.Clamp(objectMenuPosition.y, 0, Mathf.Max(0, Screen.height - height));
            var rect = new Rect(objectMenuPosition.x, objectMenuPosition.y, width, height);
            var title = new Rect(rect.x, rect.y, rect.width, 25);
            var closeButton = new Rect(rect.xMax - 28, rect.y + 2, 24, 20);
            if (e.type == EventType.MouseDown && e.button == 0 && title.Contains(e.mousePosition) && !closeButton.Contains(e.mousePosition))
            {
                draggingObjectMenu = true; objectMenuDragMouse = e.mousePosition; objectMenuDragOrigin = objectMenuPosition; e.Use();
            }
            else if (e.type == EventType.MouseDrag && draggingObjectMenu)
            {
                objectMenuPosition = objectMenuDragOrigin + e.mousePosition - objectMenuDragMouse;
                objectMenuPosition.x = Mathf.Clamp(objectMenuPosition.x, 0, Mathf.Max(0, Screen.width - width));
                objectMenuPosition.y = Mathf.Clamp(objectMenuPosition.y, 0, Mathf.Max(0, Screen.height - height));
                rect.position = objectMenuPosition; e.Use();
            }
            else if (e.type == EventType.MouseUp && draggingObjectMenu) { draggingObjectMenu = false; e.Use(); }
            string titleText = group != null ? "グループオブジェクト設定" : "オブジェクト設定: " + KindLabel(c.kind);
            GUI.Box(rect, titleText);
            DrawSolid(new Rect(rect.x + 1, rect.y + 1, rect.width - 2, 22), new Color(.22f, .28f, .34f));
            GUI.Label(new Rect(rect.x + 9, rect.y + 2, rect.width - 45, 20), titleText + "   （ここをドラッグして移動）");
            if (GUI.Button(closeButton, "X")) { objectMenuOpen = false; draggingObjectMenu = false; }
            GUILayout.BeginArea(new Rect(rect.x + 9, rect.y + 24, rect.width - 18, rect.height - 32));
            if (group != null)
            {
                DrawGroupInspector(group);
                GUILayout.EndArea();
                return;
            }
            objectScroll = GUILayout.BeginScrollView(objectScroll);
            GUILayout.Label("オブジェクトID: " + c.id.Substring(0, 8));
            DrawObjectSeekBar(c);
            if (c.kind != "WAV")
            {
                GUILayout.Label("動きの区間を選択");
                GUILayout.BeginHorizontal();
                for (int index = 0; index <= (c.keyframes?.Count ?? 0); index++)
                {
                    int current = index;
                    var section = studio.GetSegment(c.id, index);
                    if (GUILayout.Button(section.start.ToString("F1") + "-" + section.end.ToString("F1"), GUILayout.Width(75))) SelectSegment(c, current);
                }
                GUILayout.EndHorizontal();
                var segment = studio.GetSegment(c.id, Mathf.Clamp(selectedSegment, 0, c.keyframes?.Count ?? 0));
                GUILayout.Label("選択中: " + segment.start.ToString("F2") + "～" + segment.end.ToString("F2") + " 秒");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("区間開始へ", GUILayout.Width(100))) SeekVisibleObjectTime(c, c.start + segment.start);
                if (GUILayout.Button("区間終了へ", GUILayout.Width(100))) SeekVisibleObjectTime(c, c.start + segment.end, c.start + segment.end);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal(); GUILayout.Label("開始値", GUILayout.Width(210)); GUILayout.Label("項目", GUILayout.Width(76)); GUILayout.Label("終了値"); GUILayout.EndHorizontal();
                string[] labels = { "X", "Y", "Z", "拡大率", "回転" };
                for (int i = 0; i < 5; i++)
                {
                    float limit = i == 3 ? 5f : i == 4 ? 360f : 20f;
                    float min = i == 3 ? 0f : -limit;
                    float max = limit;
                    float startValue = SegmentValue(segment, i);
                    float endValue = SegmentValue(segment, i + 5);
                    min = Mathf.Min(min, startValue, endValue);
                    max = Mathf.Max(max, startValue, endValue);
                    GUILayout.BeginHorizontal();
                    float newStart = GUILayout.HorizontalSlider(startValue, min, max, GUILayout.Width(136));
                    segmentEdit[i] = GUILayout.TextField(segmentEdit[i], GUILayout.Width(67));
                    GUILayout.Label(labels[i], GUILayout.Width(76));
                    float newEnd = GUILayout.HorizontalSlider(endValue, min, max, GUILayout.Width(136));
                    segmentEdit[i + 5] = GUILayout.TextField(segmentEdit[i + 5], GUILayout.Width(67));
                    GUILayout.EndHorizontal();
                    if (Mathf.Abs(newStart - startValue) > 0.0001f || Mathf.Abs(newEnd - endValue) > 0.0001f)
                    {
                        if (e.shift)
                        {
                            newStart = Mathf.Lerp(startValue, newStart, 0.1f);
                            newEnd = Mathf.Lerp(endValue, newEnd, 0.1f);
                        }
                        SetSegmentSlider(c, i, newStart, newEnd);
                    }
                }
                DrawAxisControls(c, segment);
                if (GUILayout.Button("区間の開始値・終了値を反映")) Try(() => { var v = segmentEdit.Select(x => float.Parse(x, CultureInfo.InvariantCulture)).ToArray(); studio.SetSegment(c.id, selectedSegment, new StudioSegment { startX = v[0], startY = v[1], startZ = v[2], startScale = v[3], startRotation = v[4], endX = v[5], endY = v[6], endZ = v[7], endScale = v[8], endRotation = v[9], startScaleX=ParseAxis(0),startScaleY=ParseAxis(1),endScaleX=ParseAxis(2),endScaleY=ParseAxis(3) }); SelectSegment(c,selectedSegment); });
                if (GUILayout.Button("再生位置に中間点を追加")) Try(() => { studio.AddKeyframe(c.id, studio.playhead); SelectSegment(c, SegmentAt(c, studio.playhead - c.start)); });
                if (c.keyframes != null && c.keyframes.Count > 0)
                {
                    GUILayout.Label("中間点（押すと削除）:");
                    foreach (var point in c.keyframes)
                    {
                        if (GUILayout.Button("中間点を削除 +" + point.time.ToString("F2") + "秒")) { float at = c.start + point.time; Try(() => studio.DeleteKeyframe(c.id, at)); SelectSegment(c, 0); break; }
                    }
                }
                GUILayout.Space(8);
                GUILayout.Label("効果・アニメーション");
                foreach (var definition in StudioPlugins.List())
                    if (GUILayout.Button("+ " + definition.title)) Try(() => studio.AddEffect(c.id, definition.id));
                foreach (var effect in c.effects ?? new System.Collections.Generic.List<StudioEffect>())
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button(effect.plugin + (effect.enabled ? " ✓" : " 無効"))) SelectEffect(effect);
                    bool remove = GUILayout.Button("X", GUILayout.Width(28));
                    GUILayout.EndHorizontal();
                    if (remove) { Try(() => studio.DeleteEffect(c.id, effect.id)); if (selectedEffectId == effect.id) selectedEffectId = ""; break; }
                }
                var selectedEffect = c.effects?.FirstOrDefault(x => x.id == selectedEffectId);
                if (selectedEffect != null)
                {
                    GUILayout.Label("効果の設定値 A / B / C / D");
                    GUILayout.BeginHorizontal(); effectA = GUILayout.TextField(effectA); effectB = GUILayout.TextField(effectB); effectC = GUILayout.TextField(effectC); effectD = GUILayout.TextField(effectD); GUILayout.EndHorizontal();
                    if (GUILayout.Button("効果の設定を反映")) Try(() => studio.SetEffect(c.id, selectedEffectId, fx => { fx.a = float.Parse(effectA, CultureInfo.InvariantCulture); fx.b = float.Parse(effectB, CultureInfo.InvariantCulture); fx.c = float.Parse(effectC, CultureInfo.InvariantCulture); fx.d = float.Parse(effectD, CultureInfo.InvariantCulture); }));
                    if (GUILayout.Button(selectedEffect.enabled ? "効果を無効にする" : "効果を有効にする")) Try(() => studio.SetEffect(c.id, selectedEffectId, fx => fx.enabled = !fx.enabled));
                }
                var memberGroup = studio.FindGroup(c.groupId);
                if (memberGroup != null)
                {
                    GUILayout.Space(10);
                    GUILayout.Label(memberGroup.name + " の共通効果（所属オブジェクト " + studio.project.clips.Count(x => x.groupId == memberGroup.id) + " 個）");
                    foreach (var definition in StudioPlugins.List())
                        if (GUILayout.Button("グループに追加: " + definition.title)) Try(() => studio.AddGroupEffect(memberGroup.id, definition.id));
                    foreach (var effect in memberGroup.effects ?? new List<StudioEffect>())
                    {
                        GUILayout.BeginHorizontal();
                        if (GUILayout.Button(effect.plugin + (effect.enabled ? " ✓" : " 無効"))) SelectEffect(effect);
                        bool remove = GUILayout.Button("X", GUILayout.Width(28));
                        GUILayout.EndHorizontal();
                        if (remove) { Try(() => studio.DeleteGroupEffect(memberGroup.id, effect.id)); selectedEffectId = ""; break; }
                    }
                    var selectedGroupEffect = memberGroup.effects?.FirstOrDefault(x => x.id == selectedEffectId);
                    if (selectedGroupEffect != null)
                    {
                        GUILayout.Label("グループ効果の設定値 A / B / C / D");
                        GUILayout.BeginHorizontal(); effectA = GUILayout.TextField(effectA); effectB = GUILayout.TextField(effectB); effectC = GUILayout.TextField(effectC); effectD = GUILayout.TextField(effectD); GUILayout.EndHorizontal();
                        if (GUILayout.Button("グループ効果の設定を反映")) Try(() => studio.SetGroupEffect(memberGroup.id, selectedEffectId, fx => { fx.a = float.Parse(effectA, CultureInfo.InvariantCulture); fx.b = float.Parse(effectB, CultureInfo.InvariantCulture); fx.c = float.Parse(effectC, CultureInfo.InvariantCulture); fx.d = float.Parse(effectD, CultureInfo.InvariantCulture); }));
                        if (GUILayout.Button(selectedGroupEffect.enabled ? "グループ効果を無効にする" : "グループ効果を有効にする")) Try(() => studio.SetGroupEffect(memberGroup.id, selectedEffectId, fx => fx.enabled = !fx.enabled));
                    }
                }
            }
            GUILayout.Space(8);
            GUILayout.Label("タイムライン上の配置");
            for (int i = 0; i < edit.Length; i++)
            {
                if (c.kind == "WAV" && i == 2) continue;
                GUILayout.BeginHorizontal(); GUILayout.Label(names[i], GUILayout.Width(75)); edit[i] = GUILayout.TextField(edit[i]); GUILayout.EndHorizontal();
            }
            if (c.kind == "WAV") DrawAudioSettings();
            if (c.kind == "Text") { GUILayout.Label("文字"); editText = GUILayout.TextField(editText); }
            if (GUILayout.Button("配置を反映")) ApplyEdit(c);
            if (GUILayout.Button("閉じる")) objectMenuOpen = false;
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void DrawObjectSeekBar(StudioClip clip)
        {
            float duration = Mathf.Max(1f / studio.project.fps, clip.duration);
            float lastVisible = LastVisibleFrame(clip.start, clip.start + duration, studio.project.fps);
            float localEnd = Mathf.Max(0f, lastVisible - clip.start);
            float localTime = Mathf.Clamp(studio.playhead - clip.start, 0f, localEnd);
            GUILayout.BeginHorizontal();
            bool snappedStart = GUILayout.Button("開始へスナップ", GUILayout.Width(104));
            var bar = GUILayoutUtility.GetRect(260, 24, GUILayout.ExpandWidth(true));
            DrawSolid(new Rect(bar.x, bar.y + 11, bar.width, 3), new Color(.28f, .32f, .37f));
            if (clip.keyframes != null)
                foreach (var point in clip.keyframes)
                {
                    float x = bar.x + Mathf.Clamp01(point.time / duration) * bar.width;
                    DrawSolid(new Rect(x - 1, bar.y + 14, 2, 7), Color.yellow);
                }
            float seek = GUI.HorizontalSlider(bar, localTime, 0f, localEnd);
            if (snappedStart) SeekVisibleObjectTime(clip, clip.start);
            else if (Mathf.Abs(seek - localTime) > 0.0001f) SeekVisibleObjectTime(clip, clip.start + seek);
            if (GUILayout.Button("終了へスナップ", GUILayout.Width(104))) SeekVisibleObjectTime(clip, lastVisible);
            GUILayout.EndHorizontal();
            GUILayout.Label("位置 " + Mathf.Clamp(studio.playhead - clip.start, 0f, localEnd).ToString("F2") + " / " + duration.ToString("F2") + " 秒");
        }

        static float LastVisibleFrame(float start, float exclusiveEnd, int fps)
        {
            fps = Mathf.Max(1, fps);
            int first = Mathf.CeilToInt(start * fps - 0.0001f);
            int last = Mathf.CeilToInt(exclusiveEnd * fps - 0.0001f) - 1;
            return Mathf.Max(first, last) / (float)fps;
        }

        void SeekVisibleObjectTime(StudioClip clip, float seconds, float exclusiveEnd = float.PositiveInfinity)
        {
            studio.playing = false;
            int fps = Mathf.Max(1, studio.project.fps);
            float first = Mathf.CeilToInt(clip.start * fps - 0.0001f) / (float)fps;
            float last = LastVisibleFrame(clip.start, Mathf.Min(clip.start + clip.duration, exclusiveEnd), fps);
            float frame = Mathf.Round(seconds * fps) / fps;
            studio.Seek(Mathf.Clamp(frame, first, last));
        }

        float ParseAxis(int index) { return Mathf.Max(0,float.Parse(axisEdit[index],CultureInfo.InvariantCulture)); }
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

        static float SegmentValue(StudioSegment segment, int index)
        {
            switch (index)
            {
                case 0: return segment.startX;
                case 1: return segment.startY;
                case 2: return segment.startZ;
                case 3: return segment.startScale;
                case 4: return segment.startRotation;
                case 5: return segment.endX;
                case 6: return segment.endY;
                case 7: return segment.endZ;
                case 8: return segment.endScale;
                default: return segment.endRotation;
            }
        }

        void SetSegmentSlider(StudioClip clip, int property, float startValue, float endValue)
        {
            Try(() =>
            {
                var segment = studio.GetSegment(clip.id, selectedSegment);
                switch (property)
                {
                    case 0: segment.startX = startValue; segment.endX = endValue; break;
                    case 1: segment.startY = startValue; segment.endY = endValue; break;
                    case 2: segment.startZ = startValue; segment.endZ = endValue; break;
                    case 3: segment.startScale = startValue; segment.endScale = endValue; break;
                    case 4: segment.startRotation = startValue; segment.endRotation = endValue; break;
                }
                studio.SetSegment(clip.id, selectedSegment, segment, !sliderGestureActive);
                sliderGestureActive = true;
                SelectSegment(clip, selectedSegment);
            });
        }

        void SelectEffect(StudioEffect effect)
        {
            selectedEffectId = effect.id;
            effectA = effect.a.ToString(CultureInfo.InvariantCulture);
            effectB = effect.b.ToString(CultureInfo.InvariantCulture);
            effectC = effect.c.ToString(CultureInfo.InvariantCulture);
            effectD = effect.d.ToString(CultureInfo.InvariantCulture);
        }

        float MinimumTimelineZoom()
        {
            float width = Mathf.Max(100f, Screen.width - 120f);
            return Mathf.Max(0.001f, Mathf.Min(0.05f, width / Mathf.Max(1f, studio.project.duration * 2f)));
        }

        void FitTimeline(float canvasWidth)
        {
            float duration = Mathf.Max(1f, studio.project.duration);
            zoom = Mathf.Clamp(canvasWidth / (duration * 1.05f), MinimumTimelineZoom(), 400f);
            offset = 0f;
        }

        void Zoom(float factor, float anchor)
        {
            float time = (anchor - 88 + offset) / zoom;
            zoom = Mathf.Clamp(zoom * factor, MinimumTimelineZoom(), 400f);
            offset = Mathf.Max(0, time * zoom - (anchor - 88));
        }
        static string Timecode(int frames, int fps)
        {
            fps = Mathf.Max(1, fps);
            return (frames / (fps * 60)).ToString("00") + ":" + ((frames / fps) % 60).ToString("00") + ":" + (frames % fps).ToString("00");
        }
        static void DrawSolid(Rect r, Color color)
        {
            var old = GUI.color; GUI.color = color; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = old;
        }
        static string Kind(string path)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".png": case ".jpg": case ".jpeg": return "Image";
                case ".wav": return "WAV";
                case ".mp4": case ".mov": case ".webm": return "Video";
                case ".obj": return "OBJ";
                default: return null;
            }
        }
        static string KindLabel(string kind)
        {
            switch (kind)
            {
                case "Image": return "画像";
                case "Video": return "動画";
                case "Text": return "文字";
                case "Cube": return "立方体";
                case "Sphere": return "球体";
                case "WAV": return "音声";
                case "OBJ": return "3Dモデル";
                default: return kind;
            }
        }
        void SplitSelected() { if (studio.Find(studio.selectedId) != null) Try(() => studio.Split(studio.selectedId, studio.playhead)); }
        void DeleteSelected() { if (studio.Find(studio.selectedId) != null) Try(() => studio.Delete(studio.selectedId)); }
        void Try(Action action) { try { action(); } catch (Exception ex) { studio.status = ex.Message; } }
        void HandleShortcuts(Event e)
        {
            if (e.type != EventType.KeyDown) return;
            if (e.keyCode == KeyCode.Space) { GUIUtility.keyboardControl = 0; studio.TogglePlay(); e.Use(); return; }
            if (GUIUtility.keyboardControl != 0) return;
            if (e.control && e.keyCode == KeyCode.Z) { studio.Undo(); e.Use(); }
            else if (e.control && e.keyCode == KeyCode.Y) { studio.Redo(); e.Use(); }
            else if (e.control && e.keyCode == KeyCode.S) { Try(() => studio.Save(saveField)); e.Use(); }
            else if (e.keyCode == KeyCode.S) { SplitSelected(); e.Use(); }
            else if (e.keyCode == KeyCode.P) { var c = studio.Find(studio.selectedId); if (c != null) Try(() => studio.AddKeyframe(c.id, studio.playhead)); e.Use(); }
            else if (e.keyCode == KeyCode.Delete) { DeleteSelected(); e.Use(); }
            else if (e.keyCode == KeyCode.RightArrow) { studio.Seek(studio.playhead + 1f / studio.project.fps); e.Use(); }
            else if (e.keyCode == KeyCode.LeftArrow) { studio.Seek(studio.playhead - 1f / studio.project.fps); e.Use(); }
        }
    }
}
