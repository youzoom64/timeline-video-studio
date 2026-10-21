from pathlib import Path
p=Path(__file__).resolve().parents[1]/'Assets/TimelineVideoStudio/Scripts/StudioExporter.cs';s=p.read_text(encoding='utf-8')
s=s.replace('int count = Mathf.CeilToInt(studio.project.duration * studio.project.fps);','int count = 0;')
s=s.replace('string audio = Path.Combine(temp, "audio.wav"); bool hasAudio = false;', '''while(studio.AudioEffectsBusy) { if(token.IsCancellationRequested)yield break;yield return null; }
                if(!string.IsNullOrEmpty(studio.audioEffectsError)){studio.status="Export failed: "+studio.audioEffectsError;yield break;}
                count=Mathf.CeilToInt(studio.project.duration*studio.project.fps);
                string audio = Path.Combine(temp, "audio.wav"); bool hasAudio = false;''')
s=s.replace('var left = new float[blockSize]; var right = new float[blockSize];','''var left=new float[6][];var right=new float[6][];var filters=new StudioAudioChain[6];
                for(int track=0;track<6;track++){left[track]=new float[blockSize];right[track]=new float[blockSize];filters[track]=studio.CreateTrackAudioChain(track,rate);}''')
s=s.replace('System.Array.Clear(left, 0, count); System.Array.Clear(right, 0, count);','for(int track=0;track<6;track++){System.Array.Clear(left[track],0,count);System.Array.Clear(right[track],0,count);}')
s=s.replace('int first = Mathf.Max(block,', 'float clipDuration=studio.AudioPlaybackDuration(e.clip);\n                        int first = Mathf.Max(block,')
s=s.replace('(e.clip.start + e.clip.duration)', '(e.clip.start + clipDuration)')
s=s.replace('(local + e.clip.sourceOffset)', '(local + studio.AudioSourceOffset(e.clip))')
s=s.replace('Mathf.Min(e.clip.fadeIn, e.clip.duration / 2)', 'Mathf.Min(e.clip.fadeIn, clipDuration / 2)').replace('Mathf.Min(e.clip.fadeOut, e.clip.duration / 2)', 'Mathf.Min(e.clip.fadeOut, clipDuration / 2)')
s=s.replace('(e.clip.duration - (float)local)', '(clipDuration - (float)local)')
s=s.replace('left[i - block] +=', 'left[Mathf.Clamp(e.clip.audioTrack,0,5)][i - block] +=').replace('right[i - block] +=', 'right[Mathf.Clamp(e.clip.audioTrack,0,5)][i - block] +=')
s=s.replace('bw.Write((short)(Mathf.Clamp(left[i], -1, 1) * 32767));\n                        bw.Write((short)(Mathf.Clamp(right[i], -1, 1) * 32767));', '''float l=0,r=0;
                        for(int track=0;track<6;track++)if(studio.AudioTrackEnabled(track)){float tl=left[track][i],tr=right[track][i];filters[track].Process(ref tl,ref tr);l+=tl;r+=tr;}
                        bw.Write((short)(Mathf.Clamp(l,-1,1)*32767));bw.Write((short)(Mathf.Clamp(r,-1,1)*32767));''')
p.write_text(s,encoding='utf-8')
