using UnityEngine;

namespace TimelineVideoStudio
{
    public partial class StudioWindow
    {
        float browserShare=.20f,inspectorShare=.25f;
        int resizingSide;
        public Vector2 PanelWidthsFor(float width)
        {
            float available=Mathf.Max(0,width-Mathf.Min(360,width*.40f)-24);
            float left=Mathf.Clamp(width*browserShare,0,available);
            float right=Mathf.Clamp(width*inspectorShare,0,available-left);
            return new Vector2(left,right);
        }
        public Vector2 PanelWidths=>PanelWidthsFor(Screen.width);
        public void SetPanelWidth(bool left,float pixels)
        {
            var widths=PanelWidths;
            float remaining=Screen.width-Mathf.Min(360,Screen.width*.40f)-24;
            if(pixels<=10)pixels=0;
            if(left)browserShare=Mathf.Clamp(pixels,0,Mathf.Max(0,remaining-widths.y))/Screen.width;
            else inspectorShare=Mathf.Clamp(pixels,0,Mathf.Max(0,remaining-widths.x))/Screen.width;
        }
        void LoadPanelLayout(){browserShare=PlayerPrefs.GetFloat("StudioBrowserShare",.20f);inspectorShare=PlayerPrefs.GetFloat("StudioInspectorShare",.25f);}
        void HandlePanelResize(float bottom,Event e)
        {
            if(HandlePanelPointer(bottom,e.type,e.button,e.clickCount,e.mousePosition))e.Use();
        }
        public bool HandlePanelPointer(float bottom,EventType type,int button,int clicks,Vector2 mouse)
        {
            var widths=PanelWidths;var left=new Rect(Mathf.Max(0,widths.x-4),HeaderHeight,8,bottom-HeaderHeight);var right=new Rect(Mathf.Min(Screen.width-8,Screen.width-widths.y-4),HeaderHeight,8,bottom-HeaderHeight);
            if(type==EventType.MouseDown&&button==0&&(left.Contains(mouse)||right.Contains(mouse)))
            {
                if(clicks==2){browserShare=.20f;inspectorShare=.25f;SavePanelLayout();}else resizingSide=left.Contains(mouse)?1:2;
                return true;
            }
            if(type==EventType.MouseDrag&&resizingSide!=0){SetPanelWidth(resizingSide==1,resizingSide==1?mouse.x:Screen.width-mouse.x);return true;}
            if(type==EventType.MouseUp&&resizingSide!=0){resizingSide=0;SavePanelLayout();return true;}
            return false;
        }
        void SavePanelLayout(){PlayerPrefs.SetFloat("StudioBrowserShare",browserShare);PlayerPrefs.SetFloat("StudioInspectorShare",inspectorShare);PlayerPrefs.Save();}
        void DrawPanelDividers(float bottom)
        {
            var widths=PanelWidths;
            foreach(float x in new[]{Mathf.Max(0,widths.x),Mathf.Min(Screen.width-6,Screen.width-widths.y-4)})
            {
                DrawSolid(new Rect(x,HeaderHeight+2,6,bottom-HeaderHeight-4),new Color(.22f,.27f,.32f));
                DrawSolid(new Rect(x,(HeaderHeight+bottom)/2-20,6,40),new Color(.55f,.65f,.75f));
            }
        }
    }
}
