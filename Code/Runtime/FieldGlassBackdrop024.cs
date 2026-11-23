using UnityEngine;

namespace NeonArena
{
    // Explicit optical source: never captures the overlay canvas or its own glass.
    // Menu publishes its isolated portrait RT. Terminal owns a small, world-only camera.
    public sealed class FieldGlassBackdrop024 : MonoBehaviour
    {
        static Texture owner;
        Camera capture;
        RenderTexture target;
        RenderTexture rawSource,blurStage;
        Material frost;
        float nextFrame;
        public bool HasRenderTarget => target;
        public bool FrostActive => rawSource&&blurStage&&frost;
        public bool HasAllocatedResources => target||capture||rawSource||blurStage||frost;
        public int RenderCount { get; private set; }
        public static void Publish(Texture texture, Rect screenUVRect,bool frosted=false)
        {
            if(!texture)return;
            owner=texture;
            Shader.SetGlobalTexture("_FieldBackdrop024",texture);
            Shader.SetGlobalVector("_FieldBackdropRect024",new Vector4(screenUVRect.x,screenUVRect.y,Mathf.Max(.001f,screenUVRect.width),Mathf.Max(.001f,screenUVRect.height)));
            Shader.SetGlobalFloat("_FieldBackdropReady024",1);
            Shader.SetGlobalFloat("_FieldBackdropFrosted025",frosted?1:0);
        }
        public static void Clear(Texture texture)
        {
            if(owner!=texture)return;
            owner=null;Shader.SetGlobalFloat("_FieldBackdropReady024",0);
            Shader.SetGlobalTexture("_FieldBackdrop024",Texture2D.blackTexture);
        }
        void LateUpdate()
        {
            var g=ArenaGame.Instance;
            if(!g||!g.Frontier||!g.Frontier.TerminalOpen){Release();return;}
            var source=g.player.viewCamera;if(!source)return;
            int width=640,height=Mathf.Max(180,Mathf.RoundToInt(width/(float)Screen.width*Screen.height));
            if(!target||target.height!=height)
            {
                Release();
                target=new RenderTexture(width,height,16,RenderTextureFormat.ARGB32){name="Terminal optical source / world only",hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear};target.Create();
                var shader=Resources.Load<Shader>("UI/FieldFrost025");
                if(shader&&shader.isSupported)
                {
                    frost=new Material(shader){hideFlags=HideFlags.HideAndDontSave};
                    rawSource=new RenderTexture(width,height,16,RenderTextureFormat.ARGB32){name="Terminal raw world source",filterMode=FilterMode.Bilinear,hideFlags=HideFlags.HideAndDontSave};rawSource.Create();
                    blurStage=new RenderTexture(width,height,0,RenderTextureFormat.ARGB32){name="Terminal frost intermediate",filterMode=FilterMode.Bilinear,hideFlags=HideFlags.HideAndDontSave};blurStage.Create();
                }
                capture=new GameObject("Terminal optical camera 640px").AddComponent<Camera>();capture.gameObject.hideFlags=HideFlags.HideAndDontSave;capture.enabled=false;
                nextFrame=0;
            }
            if(Time.unscaledTime>=nextFrame)
            {
                capture.CopyFrom(source);capture.enabled=false;capture.targetTexture=rawSource?rawSource:target;
                capture.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);
                capture.cullingMask=source.cullingMask&~(1<<5)&~(1<<31);capture.allowHDR=false;capture.allowMSAA=false;capture.depthTextureMode=DepthTextureMode.None;
                capture.Render();
                if(frost&&rawSource&&blurStage)
                {
                    frost.SetVector("_FrostStep",new Vector4(3.5f/width,0,0,0));Graphics.Blit(rawSource,blurStage,frost);
                    frost.SetVector("_FrostStep",new Vector4(0,3.5f/height,0,0));Graphics.Blit(blurStage,target,frost);
                }
                RenderCount++;nextFrame=Time.unscaledTime+.12f;
            }
            Publish(target,new Rect(0,0,1,1),true);
        }
        void Release()
        {
            if(target){Clear(target);if(capture)capture.targetTexture=null;target.Release();Destroy(target);target=null;}
            if(rawSource){rawSource.Release();Destroy(rawSource);rawSource=null;}
            if(blurStage){blurStage.Release();Destroy(blurStage);blurStage=null;}
            if(frost){Destroy(frost);frost=null;}
            if(capture){Destroy(capture.gameObject);capture=null;}
        }
        void OnDisable(){Release();}
        void OnDestroy(){Release();}
    }
}
