using UnityEngine;
using UnityEngine.UI;

namespace NeonArena
{
    // A real second perimeter and connecting side faces expose the front sheet's
    // thickness. Only deliberate inspection changes its optical viewing angle.
    public sealed class MenuGlassPose024 : MonoBehaviour
    {
        MenuShowcase023 showcase;
        MenuGlassEdge024 edge;
        FieldGlassGraphic glass;
        float last=float.NaN;
        public void Bind(MenuShowcase023 source)
        {
            showcase=source;
            var rect=ArenaTheme.Rect("Glass side faces",transform,Vector2.zero,Vector2.zero);
            rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            edge=rect.gameObject.AddComponent<MenuGlassEdge024>();edge.raycastTarget=false;rect.SetAsFirstSibling();
            glass=transform.Find("Glass reflection").GetComponent<FieldGlassGraphic>();
        }
        void LateUpdate()
        {
            if(!showcase||!edge)return;
            float angle=Mathf.Sin((showcase.InspectionYaw+22)*Mathf.Deg2Rad);
            if(Mathf.Abs(angle-last)<.001f)return;
            last=angle;edge.ViewAngle=angle;edge.SetVerticesDirty();
            transform.localRotation=Quaternion.Euler(0,-7+angle*5,-1.2f);
            if(glass)glass.SetViewAngle(angle);
        }
    }

    public sealed class MenuGlassEdge024 : MaskableGraphic
    {
        public float ViewAngle;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;float l=r.xMin,b=r.yMin,w=r.width,h=r.height;
            Vector2 offset=new Vector2(8+ViewAngle*3,-6);
            Color cold=Color.Lerp(new Color(.10f,.23f,.29f,.92f),new Color(.29f,.43f,.48f,.95f),(ViewAngle+1)*.5f);
            Color warm=new Color(.36f,.21f,.14f,.85f);
            void Face(Vector2 a,Vector2 c,Color tint)
            {
                int n=vh.currentVertCount;
                vh.AddVert(a,tint,Vector2.zero);vh.AddVert(c,tint,Vector2.zero);
                vh.AddVert(c+offset,new Color(tint.r*.40f,tint.g*.40f,tint.b*.40f,.94f),Vector2.zero);
                vh.AddVert(a+offset,new Color(tint.r*.40f,tint.g*.40f,tint.b*.40f,.94f),Vector2.zero);
                vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
            }
            Face(new Vector2(l+9,b),new Vector2(l+w-9,b),cold);
            Face(new Vector2(l+w-9,b),new Vector2(l+w,b+9),Color.Lerp(cold,warm,.4f));
            Face(new Vector2(l+w,b+9),new Vector2(l+w,b+h),Color.Lerp(cold,warm,(ViewAngle+1)*.25f));
        }
    }
}
