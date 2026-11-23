using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace NeonArena
{
    public static class ArenaTheme
    {
        static Font font;
        public static Font Font => font ? font : (font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei UI", "Microsoft YaHei", "Arial" }, 24));
        public static readonly Color Ink = new Color(.045f,.055f,.075f), Paper = new Color(.92f,.91f,.86f), Gold = new Color(.93f,.78f,.43f), Muted = new Color(.62f,.69f,.73f);
        public static RectTransform Rect(string name, Transform parent, Vector2 p, Vector2 size, Vector2? anchor = null)
        {
            var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(parent,false);
            r.anchorMin = r.anchorMax = anchor ?? Vector2.one * .5f; r.pivot = Vector2.one * .5f; r.anchoredPosition=p; r.sizeDelta=size; return r;
        }
        public static Image Box(string name, Transform parent, Vector2 p, Vector2 size, Color color, Vector2? anchor = null)
        { var x=Rect(name,parent,p,size,anchor).gameObject.AddComponent<Image>(); x.color=color; x.raycastTarget=false; return x; }
        public static Text Text(string name, Transform parent, Vector2 p, Vector2 size, string value, int fontSize, Color color, TextAnchor align=TextAnchor.MiddleCenter, Vector2? anchor=null)
        { var t=Rect(name,parent,p,size,anchor).gameObject.AddComponent<Text>(); t.font=Font; t.text=value; t.fontSize=fontSize; t.color=color; t.alignment=align; t.raycastTarget=false; return t; }
        public static ArenaGlyph Icon(string name, Transform parent, Vector2 p, float size, int kind, Color color)
        { var g=Rect(name,parent,p,new Vector2(size,size)).gameObject.AddComponent<ArenaGlyph>(); g.kind=kind; g.color=color; g.raycastTarget=false; return g; }
        public static void Fill(Image image, float amount)
        { image.rectTransform.localScale=new Vector3(Mathf.Clamp01(amount),1,1); }
    }
    // Original line art, authored as UI geometry and sharp at any screen resolution.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ArenaGlyph : MaskableGraphic
    {
        public int kind;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r=rectTransform.rect; float s=Mathf.Min(r.width,r.height)*.5f;
            void Line(Vector2 a, Vector2 b, float width=.07f)
            {
                a*=s; b*=s; Vector2 normal=new Vector2(-(b-a).y,(b-a).x).normalized*s*width*.5f;
                int n=vh.currentVertCount; vh.AddVert(a-normal,color,Vector2.zero); vh.AddVert(a+normal,color,Vector2.zero); vh.AddVert(b+normal,color,Vector2.zero); vh.AddVert(b-normal,color,Vector2.zero); vh.AddTriangle(n,n+1,n+2); vh.AddTriangle(n,n+2,n+3);
            }
            void Ring(float radius)
            { for(int i=0;i<40;i++){float a=i*Mathf.PI/20,b=(i+1)*Mathf.PI/20;Line(new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,.022f);} }
            Ring(.88f);
            if(kind==0) { for(int i=-1;i<=1;i++){float x=i*.28f;Line(new Vector2(x,-.5f),new Vector2(x,.45f));Line(new Vector2(x-.13f,.21f),new Vector2(x,.45f));Line(new Vector2(x+.13f,.21f),new Vector2(x,.45f));} }
            else if(kind==1) { Line(new Vector2(-.6f,-.35f),new Vector2(.4f,.22f));Line(new Vector2(.4f,.22f),new Vector2(-.2f,.57f));Line(new Vector2(-.2f,.57f),new Vector2(-.06f,.29f));Line(new Vector2(-.2f,.57f),new Vector2(.12f,.61f));Line(new Vector2(.53f,-.5f),new Vector2(.53f,.56f),.04f); }
            else if(kind==2) { Line(new Vector2(-.65f,-.4f),new Vector2(.65f,.4f),.13f);Line(new Vector2(-.62f,-.13f),new Vector2(.38f,.47f),.025f);Line(new Vector2(-.4f,-.48f),new Vector2(.6f,.12f),.025f);Ring(.26f); }
            else if(kind==3) {Line(new Vector2(-.48f,0),new Vector2(.48f,0),.2f);Line(new Vector2(0,-.48f),new Vector2(0,.48f),.2f);}
            else if(kind==4) {for(int i=0;i<3;i++){float x=(i-1)*.37f;Line(new Vector2(x-.17f,-.4f),new Vector2(x+.12f,0),.1f);Line(new Vector2(x+.12f,0),new Vector2(x-.17f,.4f),.1f);}}
            else if(kind==5) {Ring(.57f);Line(Vector2.zero,new Vector2(0,.4f));Line(Vector2.zero,new Vector2(.32f,-.18f));Line(new Vector2(.2f,.73f),new Vector2(.58f,.65f));}
            else if(kind==7) {Line(new Vector2(-.4f,-.15f),new Vector2(-.4f,.45f),.18f);Line(new Vector2(.4f,-.15f),new Vector2(.4f,.45f),.18f);Line(new Vector2(-.4f,-.25f),new Vector2(-.4f,-.6f),.1f);Line(new Vector2(.4f,-.25f),new Vector2(.4f,-.6f),.1f);Line(new Vector2(-.2f,.4f),new Vector2(.2f,.4f),.12f);}
            else { for(int i=0;i<4;i++){float a=i*Mathf.PI*.5f;Vector2 d=new Vector2(Mathf.Cos(a),Mathf.Sin(a));Line(d*.42f,d*.68f,.04f);} Ring(.23f); }
        }
    }
    public sealed class RewardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public RectTransform card;
        public Image border;
        bool over;
        public void OnPointerEnter(PointerEventData e) { if(!GetComponent<Button>().interactable)return; over=true; ArenaAudio.Instance?.Play(SoundCue.Hover); }
        public void OnPointerExit(PointerEventData e) => over=false;
        void Update() { if(!card)return; float size=over?1.035f:1;card.localScale=Vector3.Lerp(card.localScale,Vector3.one*size,1-Mathf.Exp(-18*Time.unscaledDeltaTime)); if(border)border.color=over?ArenaTheme.Gold:new Color(.45f,.5f,.54f,.5f); }
        void OnDisable(){over=false;if(card)card.localScale=Vector3.one;}
    }
}
