using System;
using System.Collections.Generic;

namespace TimelineVideoStudio
{
    [Serializable] public class StudioAudioEffect
    {
        public string id=Guid.NewGuid().ToString("N"), kind="lowpass";
        public bool enabled=true; public float a,b,c,mix=1;
    }
    [Serializable] public class StudioAudioTrackSettings
    {
        public int index; public bool bypass;
        public List<StudioAudioEffect> effects=new List<StudioAudioEffect>();
    }
    [Serializable] public class StudioAudioEffectDefinition
    {
        public string kind,title;public string[] labels;public float[] defaults,min,max;
        public StudioAudioEffectDefinition(string k,string t,string[] l,float[] d,float[] lo,float[] hi){kind=k;title=t;labels=l;defaults=d;min=lo;max=hi;}
    }
    public static class StudioAudioDsp
    {
        public static readonly StudioAudioEffectDefinition[] Definitions={
            new StudioAudioEffectDefinition("lowpass","高音を削る",new[]{"上限 Hz","共鳴"},new[]{3000f,.707f},new[]{80f,.3f},new[]{18000f,3f}),
            new StudioAudioEffectDefinition("highpass","低音を削る",new[]{"下限 Hz","共鳴"},new[]{300f,.707f},new[]{20f,.3f},new[]{6000f,3f}),
            new StudioAudioEffectDefinition("distortion","歪み",new[]{"強さ"},new[]{2f},new[]{1f},new[]{12f}),
            new StudioAudioEffectDefinition("compressor","コンプレッサー",new[]{"しきい値 dB","比率","戻り ms"},new[]{-18f,4f,120f},new[]{-48f,1f,20f},new[]{0f,12f,800f}),
            new StudioAudioEffectDefinition("limiter","リミッター",new[]{"上限 dB"},new[]{-1f},new[]{-24f},new[]{0f}),
            new StudioAudioEffectDefinition("gate","ノイズゲート",new[]{"しきい値 dB"},new[]{-40f},new[]{-70f},new[]{-10f}),
            new StudioAudioEffectDefinition("delay","ディレイ",new[]{"遅れ ms","繰り返し"},new[]{180f,.3f},new[]{10f,0f},new[]{1000f,.85f}),
            new StudioAudioEffectDefinition("reverb","リバーブ",new[]{"響き 秒","こもり"},new[]{2f,.35f},new[]{.2f,0f},new[]{6f,.95f}),
            new StudioAudioEffectDefinition("tremolo","音量の揺れ",new[]{"速さ Hz","深さ"},new[]{2f,.3f},new[]{.1f,0f},new[]{20f,1f}),
            new StudioAudioEffectDefinition("ringmod","金属的な変調",new[]{"周波数 Hz"},new[]{60f},new[]{20f},new[]{400f}),
            new StudioAudioEffectDefinition("bitcrush","音質を粗くする",new[]{"ビット数","間引き"},new[]{8f,2f},new[]{4f,1f},new[]{16f,16f}),
            new StudioAudioEffectDefinition("noise","通信ノイズ",new[]{"音量"},new[]{.004f},new[]{0f},new[]{.03f})
        };
        public static readonly string[] Presets={"なし","ラジオ","無線","洞窟","水中","電話","壁越し","遠くの声","ロボット","拡声器"};
        public static StudioAudioEffect Create(string kind)
        {
            var d=Array.Find(Definitions,x=>x.kind==kind)??throw new ArgumentException("Unknown audio effect: "+kind);
            return new StudioAudioEffect {kind=kind,a=d.defaults[0],b=d.defaults.Length>1?d.defaults[1]:0,c=d.defaults.Length>2?d.defaults[2]:0};
        }
        static StudioAudioEffect Fx(string k,float a,float b=0,float wet=1){var e=Create(k);e.a=a;if(b!=0)e.b=b;e.mix=wet;return e;}
        public static List<StudioAudioEffect> Preset(string name)
        {
            switch(name)
            {
                case "なし":return new List<StudioAudioEffect>();
                case "ラジオ":return new List<StudioAudioEffect>{Fx("highpass",300),Fx("lowpass",3400),Fx("compressor",-18,4),Fx("distortion",2,0,.2f)};
                case "無線":return new List<StudioAudioEffect>{Fx("highpass",500),Fx("lowpass",2600),Fx("distortion",3,0,.3f),Fx("noise",.003f)};
                case "洞窟":return new List<StudioAudioEffect>{Fx("highpass",80),Fx("lowpass",6500),Fx("reverb",3,.45f,.45f),Fx("delay",160,.25f,.12f)};
                case "水中":return new List<StudioAudioEffect>{Fx("lowpass",500),Fx("tremolo",.8f,.12f),Fx("reverb",.7f,.75f,.12f)};
                case "電話":return new List<StudioAudioEffect>{Fx("highpass",400),Fx("lowpass",3000),Fx("bitcrush",10,2,.25f)};
                case "壁越し":return new List<StudioAudioEffect>{Fx("lowpass",700),Fx("highpass",60)};
                case "遠くの声":return new List<StudioAudioEffect>{Fx("lowpass",2200),Fx("reverb",1.5f,.5f,.45f)};
                case "ロボット":return new List<StudioAudioEffect>{Fx("ringmod",70,0,.65f),Fx("distortion",2,0,.15f)};
                case "拡声器":return new List<StudioAudioEffect>{Fx("highpass",450),Fx("lowpass",4500),Fx("distortion",3,0,.4f),Fx("delay",110,.15f,.12f)};
                default:throw new ArgumentException("Unknown audio preset: "+name);
            }
        }
        public static float Tail(IList<StudioAudioEffect> effects)
        {
            float seconds=0;if(effects!=null)foreach(var e in effects)if(e.enabled&&e.mix>0){if(e.kind=="reverb")seconds+=e.a;if(e.kind=="delay")seconds+=e.a/1000f*6;}
            return Math.Min(8,seconds);
        }
        public static float[] Render(float[] source,int rate,int channels,int first,int frames,IList<StudioAudioEffect> effects,bool loop=false)
        {
            int count=frames+(int)(Tail(effects)*rate);var output=new float[count*channels];var chain=new StudioAudioChain(effects,rate);
            for(int i=0;i<count;i++)
            {
                int at=(first+i)*channels;if(loop&&source.Length>0)at=((at%source.Length)+source.Length)%source.Length;float l=i<frames&&at>=0&&at<source.Length?source[at]:0;float r=i<frames&&at>=0&&at+channels-1<source.Length?source[at+channels-1]:0;
                chain.Process(ref l,ref r);output[i*channels]=l;if(channels>1)output[i*channels+1]=r;
            }
            return output;
        }
    }
    public sealed class StudioAudioChain
    {
        readonly Stage[] stages;
        public StudioAudioChain(IList<StudioAudioEffect> effects,int rate)
        {
            var list=new List<Stage>();if(effects!=null)foreach(var e in effects)if(e.enabled)list.Add(new Stage(e,rate));stages=list.ToArray();
        }
        public void Process(ref float l,ref float r){foreach(var stage in stages)stage.Process(ref l,ref r);}
        public void ProcessInterleaved(float[] data,int channels)
        {
            for(int i=0;i<data.Length;i+=channels){float l=data[i],r=channels>1?data[i+1]:l;Process(ref l,ref r);data[i]=l;if(channels>1)data[i+1]=r;}
        }
        sealed class Stage
        {
            readonly string kind;readonly float a,b,c,wet;readonly int rate;
            double b0,b1,b2,a1,a2,zl1,zl2,zr1,zr2,phase,env,gate=1;
            float[][] dl,dr;int[] positions;float[] dampingL,dampingR,feedback;
            float heldL,heldR;int heldCount;uint random=137;
            public Stage(StudioAudioEffect e,int sr)
            {
                kind=e.kind;a=e.a;b=e.b;c=e.c;wet=Math.Max(0,Math.Min(1,e.mix));rate=sr;
                if(kind=="lowpass"||kind=="highpass")
                {
                    double w=2*Math.PI*Math.Max(10,Math.Min(a,sr*.45))/sr,cos=Math.Cos(w),alpha=Math.Sin(w)/(2*Math.Max(.1,b)),norm=1+alpha;
                    b0=(kind=="lowpass"?(1-cos):(1+cos))/2/norm;b1=(kind=="lowpass"?1-cos:-(1+cos))/norm;b2=b0;a1=-2*cos/norm;a2=(1-alpha)/norm;
                }
                if(kind=="delay"||kind=="reverb")
                {
                    double[] delays=kind=="delay"?new[]{Math.Max(.001,a/1000.0)}:new[]{.0297,.0371,.0411,.0437};
                    dl=new float[delays.Length][];dr=new float[delays.Length][];positions=new int[delays.Length];dampingL=new float[delays.Length];dampingR=new float[delays.Length];feedback=new float[delays.Length];
                    for(int i=0;i<delays.Length;i++){int length=Math.Max(1,(int)(delays[i]*sr));dl[i]=new float[length];dr[i]=new float[length];feedback[i]=kind=="delay"?Math.Max(0,Math.Min(.9f,b)):(float)Math.Pow(.001,delays[i]/Math.Max(.1,a));}
                }
            }
            public void Process(ref float left,ref float right)
            {
                float l=left,r=right;
                switch(kind)
                {
                    case "lowpass":case "highpass":
                        double x=b0*l+zl1;zl1=b1*l-a1*x+zl2;zl2=b2*l-a2*x;l=(float)x;
                        x=b0*r+zr1;zr1=b1*r-a1*x+zr2;zr2=b2*r-a2*x;r=(float)x;break;
                    case "distortion":l=(float)(Math.Tanh(l*Math.Max(1,a))/Math.Tanh(Math.Max(1,a)));r=(float)(Math.Tanh(r*Math.Max(1,a))/Math.Tanh(Math.Max(1,a)));break;
                    case "compressor":
                        double peak=Math.Max(Math.Abs(l),Math.Abs(r)),coef=Math.Exp(-1/(rate*(peak>env?.01:Math.Max(.02,c/1000.0))));env=coef*env+(1-coef)*peak;
                        double threshold=Math.Pow(10,a/20),gain=env>threshold?Math.Pow(env/threshold,1/Math.Max(1,b)-1):1;l*=(float)gain;r*=(float)gain;break;
                    case "limiter":float limit=(float)Math.Pow(10,a/20);l=Math.Max(-limit,Math.Min(limit,l));r=Math.Max(-limit,Math.Min(limit,r));break;
                    case "gate":double target=Math.Max(Math.Abs(l),Math.Abs(r))>=Math.Pow(10,a/20)?1:0;gate+=(target-gate)*.01;l*=(float)gate;r*=(float)gate;break;
                    case "delay":case "reverb":
                        float sumL=0,sumR=0;
                        for(int i=0;i<dl.Length;i++){int pos=positions[i];float yl=dl[i][pos],yr=dr[i][pos];float damp=kind=="reverb"?Math.Max(.01f,1-b):1;dampingL[i]+=(yl-dampingL[i])*damp;dampingR[i]+=(yr-dampingR[i])*damp;dl[i][pos]=l+dampingL[i]*feedback[i];dr[i][pos]=r+dampingR[i]*feedback[i];positions[i]=(pos+1)%dl[i].Length;sumL+=yl;sumR+=yr;}l=sumL/dl.Length;r=sumR/dr.Length;break;
                    case "tremolo":float g=1-Math.Max(0,Math.Min(1,b))*(float)(.5+.5*Math.Sin(phase));l*=g;r*=g;phase=(phase+2*Math.PI*a/rate)%(2*Math.PI);break;
                    case "ringmod":float mod=(float)Math.Sin(phase);l*=mod;r*=mod;phase=(phase+2*Math.PI*a/rate)%(2*Math.PI);break;
                    case "bitcrush":if(heldCount--<=0){float levels=(float)Math.Pow(2,Math.Max(2,Math.Min(16,a))-1);heldL=(float)Math.Round(l*levels)/levels;heldR=(float)Math.Round(r*levels)/levels;heldCount=Math.Max(1,(int)b)-1;}l=heldL;r=heldR;break;
                    case "noise":random=random*1664525+1013904223;l+=((random>>8)/8388608f-1)*a;random=random*1664525+1013904223;r+=((random>>8)/8388608f-1)*a;break;
                }
                left=left+(l-left)*wet;right=right+(r-right)*wet;
            }
        }
    }
    public class StudioTrackAudioFilter:UnityEngine.MonoBehaviour
    {
        volatile StudioAudioChain chain;
        public void Configure(IList<StudioAudioEffect> effects){chain=new StudioAudioChain(effects,UnityEngine.AudioSettings.outputSampleRate);}
        void OnAudioFilterRead(float[] data,int channels){var current=chain;current?.ProcessInterleaved(data,channels);}
    }
}
