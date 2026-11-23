using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace NeonArena
{
    public enum FieldSurfaceRole { Auto, Shell, Recessed, Action }
    public static class FieldUI
    {
        public static readonly Color Cyan=new Color(.36f,.80f,.86f), Amber=new Color(1,.58f,.25f), Danger=new Color(.98f,.30f,.26f), Paper=new Color(.91f,.94f,.95f), Muted=new Color(.64f,.72f,.77f);
        static Sprite cut;
        // One tiny, shared nine-slice keeps corner geometry exact at every panel size.
        static Sprite Cut
        {
            get
            {
                if(cut)return cut;
                var t=new Texture2D(32,32,TextureFormat.RGBA32,false){name="Field console corner",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
                var pixels=new Color[1024];
                for(int y=0;y<32;y++)for(int x=0;x<32;x++)pixels[y*32+x]=new Color(1,1,1,Mathf.Clamp01(Mathf.Min(x+31-y,31-x+y)-7));
                t.SetPixels(pixels);t.Apply(false,true);
                cut=Sprite.Create(t,new Rect(0,0,32,32),Vector2.one*.5f,100,0,SpriteMeshType.FullRect,new Vector4(10,10,10,10));cut.name="Field console nine-slice";cut.hideFlags=HideFlags.HideAndDontSave;
                return cut;
            }
        }
        public static Image Panel(string name,Transform parent,Vector2 pos,Vector2 size,Color? accent=null,Vector2? anchor=null)
        {
            var image=ArenaTheme.Box(name,parent,pos,size,new Color(.030f,.046f,.065f,.64f),anchor);
            Decorate(image,accent??Cyan);
            SetRole(image,size.y>250?FieldSurfaceRole.Shell:FieldSurfaceRole.Recessed);return image;
        }
        public static void Decorate(Image panel,Color accent)
        {
            if(panel.transform.Find("Surface finish"))return;
            panel.sprite=Cut;panel.type=Image.Type.Sliced;
            var r=ArenaTheme.Rect("Surface finish",panel.transform,Vector2.zero,Vector2.zero);
            r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;
            var mesh=r.gameObject.AddComponent<FieldSurface>();mesh.accent=accent;mesh.raycastTarget=false;
            r.SetAsFirstSibling();
            var glass=ArenaTheme.Rect("Glass reflection",panel.transform,Vector2.zero,Vector2.zero);
            glass.anchorMin=Vector2.zero;glass.anchorMax=Vector2.one;glass.offsetMin=glass.offsetMax=Vector2.zero;
            var reflection=glass.gameObject.AddComponent<FieldGlassGraphic>();reflection.color=accent;reflection.raycastTarget=false;
            glass.SetAsFirstSibling();
        }
        public static void SetRole(Image panel,FieldSurfaceRole role)
        {
            var surface=panel.transform.Find("Surface finish");
            if(!surface){Decorate(panel,Cyan);surface=panel.transform.Find("Surface finish");}
            var finish=surface.GetComponent<FieldSurface>();finish.role=role;finish.SetVerticesDirty();
            var reflection=panel.transform.Find("Glass reflection");
            if(reflection){var glass=reflection.GetComponent<FieldGlassGraphic>();glass.role=role;glass.SetVerticesDirty();}
        }
        public static Button Action(Transform parent,string name,Vector2 pos,Vector2 size,string label,UnityEngine.Events.UnityAction action,bool primary=false)
        {
            var image=ArenaTheme.Box(name,parent,pos,size,Color.white);image.raycastTarget=true;
            Decorate(image,primary?new Color(.78f,.91f,.93f):Cyan);
            var button=image.gameObject.AddComponent<Button>();button.targetGraphic=image;
            SetRole(image,FieldSurfaceRole.Action);
            var palette=button.colors;
            palette.normalColor=primary?new Color(.70f,.84f,.86f):new Color(.08f,.12f,.15f);
            palette.highlightedColor=primary?new Color(.87f,.97f,.98f):new Color(.14f,.24f,.29f);
            palette.selectedColor=palette.highlightedColor;
            palette.pressedColor=primary?new Color(.43f,.64f,.68f):new Color(.035f,.075f,.095f);
            palette.disabledColor=new Color(.13f,.18f,.22f,.95f);palette.fadeDuration=.12f;button.colors=palette;
            ArenaTheme.Text("Label",image.transform,Vector2.zero,size-new Vector2(28,4),label,24,primary?new Color(.02f,.07f,.09f):Paper);
            button.onClick.AddListener(()=>{ArenaAudio.Instance?.Play(SoundCue.Select);action?.Invoke();});
            image.gameObject.AddComponent<FieldActionFeedback>();return button;
        }
    }
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FieldSurface : MaskableGraphic
    {
        public Color accent;
        public FieldSurfaceRole role;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;float l=r.xMin,b=r.yMin,w=r.width,h=r.height;
            var surfaceRole=role==FieldSurfaceRole.Auto?(transform.parent.GetComponent<Button>()?FieldSurfaceRole.Action:h>250?FieldSurfaceRole.Shell:FieldSurfaceRole.Recessed):role;
            void Quad(float x,float y,float width,float height,Color c)
            {
                int n=vh.currentVertCount;vh.AddVert(new Vector3(x,y),c,Vector2.zero);vh.AddVert(new Vector3(x+width,y),c,Vector2.zero);vh.AddVert(new Vector3(x+width,y+height),c,Vector2.zero);vh.AddVert(new Vector3(x,y+height),c,Vector2.zero);vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
            }
            Color A(Color c,float a){c.a=a;return c;}
            Vector2[] Shape(float inset)
            {
                float x=l+inset,y=b+inset,ww=w-2*inset,hh=h-2*inset,k=Mathf.Max(0,9-inset*.3f);
                return new[]{new Vector2(x,y),new Vector2(x+ww-k,y),new Vector2(x+ww,y+k),new Vector2(x+ww,y+hh),new Vector2(x+k,y+hh),new Vector2(x,y+hh-k)};
            }
            void Ring(float inset,float thickness,Color light,Color dark)
            {
                var outer=Shape(inset);var inner=Shape(inset+thickness);
                for(int i=0;i<6;i++){int j=(i+1)%6,n=vh.currentVertCount;Color c=i>=3?light:dark;vh.AddVert(outer[i],c,Vector2.zero);vh.AddVert(outer[j],c,Vector2.zero);vh.AddVert(inner[j],c,Vector2.zero);vh.AddVert(inner[i],c,Vector2.zero);vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);}
            }
            if(surfaceRole==FieldSurfaceRole.Shell)
            {
                // One fine metal perimeter; depth comes from a soft falling shadow.
                for(int i=0;i<15;i++)Quad(l+10+i*.2f,b-1-i,w-20,1,new Color(.001f,.003f,.008f,.33f*(1-i/15f)));
                Quad(l+w-1,b+10,3,h-20,new Color(.12f,.18f,.23f,.38f));
                Quad(l+11,b-4,w-18,3,new Color(.19f,.27f,.31f,.34f));
                Ring(0,1,new Color(.66f,.78f,.85f,.27f),new Color(.12f,.19f,.25f,.40f));
                Ring(2.6f,1,new Color(.60f,.72f,.81f,.12f),new Color(.09f,.12f,.18f,.48f));
                Quad(l+20,b+h-3,Mathf.Min(52,w*.14f),1,A(accent,.46f));
            }
            else if(surfaceRole==FieldSurfaceRole.Recessed)
            {
                // Content is inset, without the raised rim or status slash of an action.
                Ring(0,1,new Color(.67f,.76f,.81f,.36f),new Color(.12f,.17f,.22f,.52f));
                if(h>180){
                    for(int s=0;s<10;s++)Quad(l+10+s*.5f,b-7-s,w-7,1,new Color(.001f,.004f,.012f,.46f*(1-s/10f)));
                    Quad(l+w,b+8,6,h-17,new Color(.12f,.20f,.29f,.52f));
                    Quad(l+w+5,b+8,1,h-17,new Color(.38f,.50f,.60f,.32f));
                    Quad(l+9,b-6,w-8,5,new Color(.18f,.26f,.35f,.52f));
                }
                Ring(1.2f,1.2f,new Color(.002f,.006f,.01f,.24f),new Color(.32f,.42f,.49f,.08f));
                for(int i=0;i<5;i++)Quad(l+10,b-1-i,w-20,1,new Color(.002f,.006f,.01f,.09f*(1-i/5f)));
            }
            else
            {
                Ring(0,1,new Color(.63f,.74f,.79f,.24f),new Color(.03f,.05f,.07f,.30f));
                Quad(l+10,b-1,w-20,1,new Color(.002f,.006f,.01f,.16f));
                Quad(l+10,b-2,w-20,1,new Color(.002f,.006f,.01f,.07f));
            }
        }
    }
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FieldAtmosphere : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            void Quad(float x,float y,float w,float h,Color low,Color high)
            {
                int n=vh.currentVertCount;vh.AddVert(new Vector3(x,y),low,Vector2.zero);vh.AddVert(new Vector3(x+w,y),low,Vector2.zero);vh.AddVert(new Vector3(x+w,y+h),high,Vector2.zero);vh.AddVert(new Vector3(x,y+h),high,Vector2.zero);vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
            }
            var clear=new Color(.004f,.013f,.024f,0);var shade=new Color(.004f,.013f,.024f,.63f);
            Quad(-800,300,1600,150,clear,shade);Quad(-800,-450,1600,120,new Color(.004f,.013f,.024f,.72f),clear);
            // Leader terminates on the visible rear perimeter lava, above the central roof.
            var line=new Color(1,.58f,.25f,.68f);
            Quad(530,278,2,32,line,line);Quad(530,278,82,2,line,line);Quad(610,250,2,29,line,line);
            Quad(603,244,15,2,line,line);Quad(603,244,2,10,line,line);Quad(616,244,2,10,line,line);
        }
    }
    public sealed class FieldActionFeedback : MonoBehaviour,IPointerEnterHandler,IPointerDownHandler,IPointerUpHandler,IPointerExitHandler
    {
        RectTransform label;Vector2 rest;bool pressed;
        void Awake(){label=transform.Find("Label") as RectTransform;}
        void Restore(){if(label&&pressed)label.anchoredPosition=rest;pressed=false;}
        public void OnPointerEnter(PointerEventData e){if(GetComponent<Button>().interactable)ArenaAudio.Instance?.Play(SoundCue.Hover);}
        public void OnPointerDown(PointerEventData e){if(label&&GetComponent<Button>().interactable&&!pressed){rest=label.anchoredPosition;pressed=true;label.anchoredPosition=rest+Vector2.down*2;}}
        public void OnPointerUp(PointerEventData e){Restore();}
        public void OnPointerExit(PointerEventData e){Restore();}
        void OnDisable(){Restore();}
    }
}
