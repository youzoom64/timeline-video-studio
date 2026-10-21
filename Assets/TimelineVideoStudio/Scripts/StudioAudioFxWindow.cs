using System;
using System.Linq;
using UnityEngine;

namespace TimelineVideoStudio
{
    public partial class StudioWindow
    {
        bool audioFxOpen,audioFxGesture;string audioFxClip;int audioFxTrack,audioPreset=-1;
        Vector2 audioFxScroll;Rect audioFxRect=new Rect(360,120,620,540);
        Rect AudioFxRect()=>audioFxRect;
        void OpenAudioFx(string id,int track){audioFxClip=id;audioFxTrack=track;audioFxOpen=true;audioPreset=-1;audioFxScroll=Vector2.zero;}
        void DrawAudioEffectsWindow()
        {
            if(!audioFxOpen)return;if(!Input.GetMouseButton(0))audioFxGesture=false;
            audioFxRect.width=Mathf.Min(620,Screen.width-20);audioFxRect.height=Mathf.Min(600,Screen.height-50);audioFxRect.x=Mathf.Clamp(audioFxRect.x,0,Screen.width-audioFxRect.width);audioFxRect.y=Mathf.Clamp(audioFxRect.y,0,Screen.height-audioFxRect.height);
            audioFxRect=GUI.Window(84531,audioFxRect,DrawAudioFxContents,"音声エフェクト");
        }
        void DrawAudioFxContents(int window)
        {
            var clip=string.IsNullOrEmpty(audioFxClip)?null:studio.Find(audioFxClip);
            if(!string.IsNullOrEmpty(audioFxClip)&&clip==null){audioFxOpen=false;return;}
            GUILayout.BeginHorizontal();GUILayout.Label(clip!=null?"対象クリップ: "+System.IO.Path.GetFileName(clip.path):"対象トラック: "+StudioProject.AudioTrackNames[audioFxTrack]);
            if(GUILayout.Button("閉じる",GUILayout.Width(60)))audioFxOpen=false;GUILayout.EndHorizontal();
            if(clip!=null&&GUILayout.Button("このクリップがあるトラック全体を編集")){audioFxClip=null;audioPreset=-1;}
            bool bypass=studio.AudioEffectsBypassed(audioFxClip,audioFxTrack);bool next=GUILayout.Toggle(bypass,"バイパス（元の音を試聴）");if(next!=bypass)Try(()=>studio.BypassAudioEffects(audioFxClip,audioFxTrack,next));
            GUILayout.Label("プリセット（選ぶと現在の組み合わせを置き換えます）");
            int preset=GUILayout.SelectionGrid(audioPreset,StudioAudioDsp.Presets,5);
            if(preset!=audioPreset&&preset>=0){audioPreset=preset;Try(()=>studio.EditAudioEffects(audioFxClip,audioFxTrack,list=>{list.Clear();list.AddRange(StudioAudioDsp.Preset(StudioAudioDsp.Presets[preset]));}));}
            audioFxScroll=GUILayout.BeginScrollView(audioFxScroll);
            var effects=studio.AudioEffects(audioFxClip,audioFxTrack);
            for(int i=0;i<effects.Count;i++)
            {
                var effect=effects[i];int at=i;var def=Array.Find(StudioAudioDsp.Definitions,d=>d.kind==effect.kind);if(def==null)continue;
                GUILayout.BeginVertical(GUI.skin.box);GUILayout.BeginHorizontal();GUILayout.Label((i+1)+". "+def.title,GUILayout.Width(155));
                bool enabled=GUILayout.Toggle(effect.enabled,"有効",GUILayout.Width(70));if(enabled!=effect.enabled)Try(()=>studio.EditAudioEffects(audioFxClip,audioFxTrack,list=>effect.enabled=enabled));
                if(GUILayout.Button("↑",GUILayout.Width(32))&&i>0)Try(()=>studio.EditAudioEffects(audioFxClip,audioFxTrack,list=>{list.RemoveAt(at);list.Insert(at-1,effect);}));
                if(GUILayout.Button("↓",GUILayout.Width(32))&&i+1<effects.Count)Try(()=>studio.EditAudioEffects(audioFxClip,audioFxTrack,list=>{list.RemoveAt(at);list.Insert(at+1,effect);}));
                bool remove=GUILayout.Button("削除",GUILayout.Width(60));GUILayout.EndHorizontal();
                for(int p=0;p<def.labels.Length;p++)
                {
                    int parameter=p;float value=p==0?effect.a:p==1?effect.b:effect.c;
                    GUILayout.BeginHorizontal();GUILayout.Label(def.labels[p],GUILayout.Width(130));float changed=GUILayout.HorizontalSlider(value,def.min[p],def.max[p]);GUILayout.Label(value.ToString("0.###"),GUILayout.Width(65));GUILayout.EndHorizontal();
                    if(Mathf.Abs(changed-value)>.000001f)Try(()=>{studio.EditAudioEffects(audioFxClip,audioFxTrack,list=>{if(parameter==0)effect.a=changed;else if(parameter==1)effect.b=changed;else effect.c=changed;},!audioFxGesture);audioFxGesture=true;audioPreset=-1;});
                }
                GUILayout.BeginHorizontal();GUILayout.Label("掛かり具合",GUILayout.Width(130));float mix=GUILayout.HorizontalSlider(effect.mix,0,1);GUILayout.Label((effect.mix*100).ToString("0")+"%",GUILayout.Width(65));GUILayout.EndHorizontal();
                if(Mathf.Abs(mix-effect.mix)>.000001f)Try(()=>{studio.EditAudioEffects(audioFxClip,audioFxTrack,list=>effect.mix=mix,!audioFxGesture);audioFxGesture=true;audioPreset=-1;});
                GUILayout.EndVertical();if(remove){Try(()=>studio.EditAudioEffects(audioFxClip,audioFxTrack,list=>list.Remove(effect)));break;}
            }
            GUILayout.Label("エフェクトを追加");
            for(int i=0;i<StudioAudioDsp.Definitions.Length;i+=3)
            {
                GUILayout.BeginHorizontal();for(int j=i;j<Mathf.Min(i+3,StudioAudioDsp.Definitions.Length);j++){string kind=StudioAudioDsp.Definitions[j].kind;if(GUILayout.Button(StudioAudioDsp.Definitions[j].title))Try(()=>studio.EditAudioEffects(audioFxClip,audioFxTrack,list=>list.Add(StudioAudioDsp.Create(kind))));}GUILayout.EndHorizontal();
            }
            if(studio.AudioEffectsBusy)GUILayout.Label("対象の音声を処理しています…");if(!string.IsNullOrEmpty(studio.audioEffectsError))GUILayout.Label(studio.audioEffectsError);
            GUILayout.EndScrollView();GUI.DragWindow(new Rect(0,0,audioFxRect.width,20));
        }
    }
}
