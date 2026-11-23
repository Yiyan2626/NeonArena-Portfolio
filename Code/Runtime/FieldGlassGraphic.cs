using UnityEngine;
using UnityEngine.UI;

namespace NeonArena
{
    // Shared world-source optical material; no grab pass or animated sweep.
    // Surface geometry is separate from text and raycasts, so visual depth cannot move controls.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FieldGlassGraphic : MaskableGraphic
    {
        public FieldSurfaceRole role;
        public float viewAngle;
        public float quietInterior;
        public float coating;
        public void SetCoating(float value){coating=Mathf.Clamp01(value);SetVerticesDirty();}
        public void SetViewAngle(float normalized){float value=Mathf.Clamp(normalized,-1,1);if(Mathf.Abs(viewAngle-value)<.001f)return;viewAngle=value;SetVerticesDirty();}
        public void SetQuietInterior(float value){quietInterior=Mathf.Clamp01(value);SetVerticesDirty();}
        static Material sharedFinish;
        protected override void OnEnable()
        {
            base.OnEnable();
            if(!sharedFinish)
            {
                var shader=Resources.Load<Shader>("UI/FieldGlass023");
                if(shader)sharedFinish=new Material(shader){name="Field glass / optical transmission 024",hideFlags=HideFlags.HideAndDontSave};
            }
            if(sharedFinish)material=sharedFinish;
            if(canvas)canvas.additionalShaderChannels|=AdditionalCanvasShaderChannels.TexCoord1|AdditionalCanvasShaderChannels.TexCoord2;
            raycastTarget=false;
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;
            var resolved=role==FieldSurfaceRole.Auto?(transform.parent.GetComponent<Button>()?FieldSurfaceRole.Action:r.height>250?FieldSurfaceRole.Shell:FieldSurfaceRole.Recessed):role;
            var tint=color;if(!sharedFinish)tint=new Color(.65f,.76f,.82f,.065f);
            for(int i=0;i<4;i++)
            {
                float x=i==1||i==2?1:0,y=i>=2?1:0;
                var v=UIVertex.simpleVert;v.position=new Vector3(Mathf.Lerp(r.xMin,r.xMax,x),Mathf.Lerp(r.yMin,r.yMax,y));
                // Legacy HUD decorations stay deliberately quieter than modal surfaces.
                v.color=tint;v.uv0=new Vector2(x,y);v.uv1=new Vector4(r.width,r.height,(float)resolved,role==FieldSurfaceRole.Auto?.18f:1f);v.uv2=new Vector4(viewAngle,quietInterior,coating,0);vh.AddVert(v);
            }
            vh.AddTriangle(0,1,2);vh.AddTriangle(0,2,3);
        }
    }
}
