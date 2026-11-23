using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace NeonArena
{
    // The showcase owns every runtime resource. No gameplay object, light or material is changed.
    public sealed class MenuShowcase023 : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler, IPointerClickHandler
    {
        const int ShowcaseLayer=31;
        RawImage image;
        GameObject studio;
        Camera cameraRig;
        Transform modelRoot;
        RenderTexture target;
        readonly List<Material> materials=new List<Material>();
        Mesh dockMesh;Material dockMaterial;Transform leftFoot,rightFoot;Vector3 dockCenter;float dockRadius;
        float nextFrame,started;
        Vector3 focus;
        float modelHeight,yaw=-22,pitch=5,zoom=1;
        bool dirty=true,dragging;
        readonly Vector3[] screenCorners=new Vector3[4];
        public float InspectionYaw => yaw;
        public float InspectionPitch => pitch;
        public float InspectionZoom => zoom;
        public bool IsDragging => dragging;
        public RectTransform InspectionArea => transform as RectTransform;
        public bool RenderingActive => studio&&cameraRig&&target;
        public bool HasRenderTarget => target;
        public int RenderCount { get; private set; }
        public static bool ReducedMotion => PlayerPrefs.GetInt("MenuReducedMotion",0)==1;
        public static MenuShowcase023 Create(Transform parent)
        {
            var back=ArenaTheme.Rect("Showcase ambient field",parent,new Vector2(175,10),new Vector2(1020,850)).gameObject.AddComponent<MenuAmbient023>();
            back.raycastTarget=false;
            var r=ArenaTheme.Rect("Ranger live showcase",parent,new Vector2(175,10),new Vector2(1020,850));
            var result=r.gameObject.AddComponent<MenuShowcase023>();
            result.image=r.gameObject.AddComponent<RawImage>();result.image.raycastTarget=true;
            return result;
        }
        void OnEnable(){started=Time.unscaledTime;}
        void CreateStudio()
        {
            var prefab=Resources.Load<GameObject>("Frontier/Ranger");
            var shader=Resources.Load<Shader>("UI/MenuArmor023");
            if(!prefab||!shader||!shader.isSupported){enabled=false;return;}
            studio=new GameObject("Menu only isolated studio"){hideFlags=HideFlags.DontSave};
            studio.transform.position=new Vector3(12000,12000,12000);
            var model=Instantiate(prefab,studio.transform,false);model.name="Ranger presentation copy";modelRoot=model.transform;
            modelRoot.localPosition=Vector3.zero;modelRoot.localRotation=Quaternion.identity;
            foreach(var t in model.GetComponentsInChildren<Transform>(true))t.gameObject.layer=ShowcaseLayer;
            foreach(var collider in model.GetComponentsInChildren<Collider>(true))collider.enabled=false;
            foreach(var light in model.GetComponentsInChildren<Light>(true))light.enabled=false;
            foreach(var body in model.GetComponentsInChildren<Rigidbody>(true)){body.isKinematic=true;body.detectCollisions=false;}
            var animator=model.GetComponent<Animator>();
            if(animator){animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.Play("Idle",0,.36f);animator.Update(0);animator.enabled=false;}
            foreach(var behaviour in model.GetComponentsInChildren<MonoBehaviour>(true))behaviour.enabled=false;
            foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                skin.updateWhenOffscreen=true;
                // Gameplay bounds deliberately allow huge movement. Use actual posed geometry here.
                var bake=new Mesh();skin.BakeMesh(bake);bake.RecalculateBounds();skin.localBounds=bake.bounds;Destroy(bake);
            }
            Bounds bounds=new Bounds(modelRoot.position+Vector3.up,Vector3.zero);bool first=true;
            foreach(var renderer in model.GetComponentsInChildren<Renderer>())
            {
                if(!renderer.enabled)continue;
                if(first){bounds=renderer.bounds;first=false;}else bounds.Encapsulate(renderer.bounds);
                var originals=renderer.sharedMaterials;var replacements=new Material[originals.Length];
                for(int i=0;i<originals.Length;i++)
                {
                    var source=originals[i];var mat=new Material(shader){name="Menu armor / "+source.name,hideFlags=HideFlags.HideAndDontSave};
                    mat.mainTexture=source.mainTexture;mat.color=source.HasProperty("_Color")?source.color:Color.white;
                    if(source.HasProperty("_EmissionMap")&&source.GetTexture("_EmissionMap")){mat.SetTexture("_EmissionMap",source.GetTexture("_EmissionMap"));mat.SetFloat("_EmissionGain",.42f);}
                    replacements[i]=mat;materials.Add(mat);
                }
                renderer.sharedMaterials=replacements;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows=false;renderer.lightProbeUsage=UnityEngine.Rendering.LightProbeUsage.Off;renderer.reflectionProbeUsage=UnityEngine.Rendering.ReflectionProbeUsage.Off;
            }
            // Bounds are only used for framing: retain the imported mesh's authored proportions.
            focus=bounds.center;float height=Mathf.Max(bounds.size.y,1);modelHeight=height;
            var skeleton=model.GetComponent<CharacterModel>();
            if(skeleton&&skeleton.joints.Length>=15){leftFoot=skeleton.joints[11];rightFoot=skeleton.joints[14];}
            CreateDock(bounds,height);
            cameraRig=new GameObject("Menu portrait camera").AddComponent<Camera>();cameraRig.transform.SetParent(studio.transform,false);
            cameraRig.enabled=false;cameraRig.clearFlags=CameraClearFlags.SolidColor;cameraRig.backgroundColor=Color.clear;
            cameraRig.cullingMask=1<<ShowcaseLayer;cameraRig.orthographic=false;cameraRig.fieldOfView=28;
            cameraRig.nearClipPlane=.05f;cameraRig.farClipPlane=height*12;cameraRig.allowHDR=false;cameraRig.allowMSAA=true;cameraRig.useOcclusionCulling=false;
            cameraRig.transform.position=focus+new Vector3(height*.26f,height*.40f,height*3);cameraRig.transform.LookAt(focus);
            EnsureTarget();nextFrame=0;
        }
        void CreateDock(Bounds bounds,float height)
        {
            var shader=Resources.Load<Shader>("UI/MenuDock023");if(!shader)return;
            dockCenter=new Vector3(bounds.center.x,bounds.min.y-.002f,bounds.center.z);dockRadius=height*.285f;
            var dock=new GameObject("Low optical deployment dock",typeof(MeshFilter),typeof(MeshRenderer));dock.layer=ShowcaseLayer;
            dock.transform.SetParent(studio.transform,false);dock.transform.position=dockCenter;
            // A thin machined disk with a continuous bevel, not a collision or gameplay platform.
            const int steps=96;var vertices=new List<Vector3>();var uv=new List<Vector2>();var tris=new List<int>();
            vertices.Add(Vector3.zero);uv.Add(Vector2.one*.5f);
            float[] radius={.975f,1,.997f,.966f};float[] y={0,-.005f,-.025f,-.03f};
            for(int ring=0;ring<4;ring++)for(int i=0;i<=steps;i++)
            {
                float a=i*Mathf.PI*2/steps;var p=new Vector3(Mathf.Cos(a)*dockRadius*radius[ring],height*y[ring],Mathf.Sin(a)*dockRadius*radius[ring]);
                vertices.Add(p);uv.Add(new Vector2(p.x,p.z)/(dockRadius*2)+Vector2.one*.5f);
                if(i==steps)continue;
                int n=1+ring*(steps+1)+i;
                if(ring==0){tris.Add(0);tris.Add(n+1);tris.Add(n);}
                if(ring<3){int m=n+steps+1;tris.Add(n);tris.Add(n+1);tris.Add(m);tris.Add(m);tris.Add(n+1);tris.Add(m+1);}
            }
            dockMesh=new Mesh{name="Menu machined dock"};dockMesh.SetVertices(vertices);dockMesh.SetUVs(0,uv);dockMesh.SetTriangles(tris,0);dockMesh.RecalculateNormals();dockMesh.RecalculateBounds();
            dock.GetComponent<MeshFilter>().sharedMesh=dockMesh;dockMaterial=new Material(shader){hideFlags=HideFlags.HideAndDontSave};materials.Add(dockMaterial);
            var renderer=dock.GetComponent<MeshRenderer>();renderer.sharedMaterial=dockMaterial;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
        }
        void EnsureTarget()
        {
            int h=Mathf.Clamp(Mathf.RoundToInt(Screen.height*1.12f),768,1440),w=Mathf.RoundToInt(h*1020f/850f);
            if(target&&target.width==w&&target.height==h)return;
            if(target){FieldGlassBackdrop024.Clear(target);cameraRig.targetTexture=null;target.Release();Destroy(target);}
            target=new RenderTexture(w,h,24,RenderTextureFormat.ARGB32){name="Menu portrait only",antiAliasing=2,hideFlags=HideFlags.HideAndDontSave};
            target.Create();cameraRig.targetTexture=target;image.texture=target;nextFrame=0;dirty=true;
        }
        void LateUpdate()
        {
            var game=ArenaGame.Instance;
            if(!game||game.State!=ArenaState.Ready){Release();return;}
            if(!studio)CreateStudio();if(!cameraRig)return;
            EnsureTarget();
            float t=Time.unscaledTime-started;
            // Pointer input is direct even when ambient motion is disabled.
            modelRoot.localRotation=Quaternion.Euler(0,yaw+(ReducedMotion||dragging?0:Mathf.Sin(t*.22f)*1.4f),0);
            float angle=pitch*Mathf.Deg2Rad;
            cameraRig.transform.position=focus+new Vector3(modelHeight*.10f,Mathf.Sin(angle)*modelHeight*2.42f,Mathf.Cos(angle)*modelHeight*2.42f)/zoom;
            cameraRig.transform.LookAt(focus);
            if(dockMaterial&&leftFoot&&rightFoot)
            {
                Vector4 Foot(Transform foot){var p=foot.position-dockCenter;return new Vector4(p.x/(dockRadius*2)+.5f,p.z/(dockRadius*2)+.5f,0,0);}
                dockMaterial.SetVector("_FootA",Foot(leftFoot));dockMaterial.SetVector("_FootB",Foot(rightFoot));
            }
            if(!dirty&&Time.unscaledTime<nextFrame)return;
            nextFrame=Time.unscaledTime+(ReducedMotion?1f:1f/30f);cameraRig.Render();RenderCount++;dirty=false;
            InspectionArea.GetWorldCorners(screenCorners);
            var canvas=GetComponentInParent<Canvas>();Camera uiCamera=canvas&&canvas.renderMode!=RenderMode.ScreenSpaceOverlay?canvas.worldCamera:null;
            var lower=RectTransformUtility.WorldToScreenPoint(uiCamera,screenCorners[0]);var upper=RectTransformUtility.WorldToScreenPoint(uiCamera,screenCorners[2]);
            FieldGlassBackdrop024.Publish(target,new Rect(lower.x/Screen.width,lower.y/Screen.height,(upper.x-lower.x)/Screen.width,(upper.y-lower.y)/Screen.height));
        }
        bool CanInspect => isActiveAndEnabled&&!FrontEnd.SettingsOpen&&ArenaGame.Instance&&ArenaGame.Instance.State==ArenaState.Ready;
        public void RotateBy(float degrees){if(!CanInspect)return;yaw=Mathf.Repeat(yaw+degrees+180,360)-180;dirty=true;}
        public void ZoomBy(float amount){if(!CanInspect)return;zoom=Mathf.Clamp(zoom+amount,.82f,1.5f);dirty=true;}
        public void ResetView(){if(!CanInspect)return;yaw=-22;pitch=5;zoom=1;dirty=true;}
        public void OnBeginDrag(PointerEventData e){if(CanInspect&&e.button==PointerEventData.InputButton.Left)dragging=true;}
        public void OnDrag(PointerEventData e)
        {
            if(!CanInspect||!dragging)return;
            float scale=900f/Mathf.Max(1,Screen.height);RotateBy(-e.delta.x*.35f*scale);pitch=Mathf.Clamp(pitch-e.delta.y*.18f*scale,-16,22);dirty=true;
        }
        public void OnEndDrag(PointerEventData e){dragging=false;dirty=true;}
        public void OnScroll(PointerEventData e){ZoomBy(e.scrollDelta.y*.09f);}
        public void OnPointerClick(PointerEventData e){if(e.button==PointerEventData.InputButton.Left&&e.clickCount==2)ResetView();}
        void Release()
        {
            if(cameraRig){cameraRig.targetTexture=null;cameraRig.enabled=false;}
            if(image)image.texture=null;
            if(target){FieldGlassBackdrop024.Clear(target);target.Release();Destroy(target);target=null;}
            if(studio){studio.SetActive(false);Destroy(studio);studio=null;}
            foreach(var mat in materials)if(mat)Destroy(mat);materials.Clear();cameraRig=null;modelRoot=null;
            if(dockMesh)Destroy(dockMesh);dockMesh=null;dockMaterial=null;leftFoot=rightFoot=null;dragging=false;dirty=true;
        }
        void OnDisable(){Release();}
        void OnDestroy(){Release();}
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MenuAmbient023 : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            void Glow(Vector2 center,Vector2 radius,Color c)
            {
                int start=vh.currentVertCount;vh.AddVert(center,c,Vector2.zero);
                for(int i=0;i<=64;i++){float a=i*Mathf.PI*2/64;vh.AddVert(center+new Vector2(Mathf.Cos(a)*radius.x,Mathf.Sin(a)*radius.y),new Color(c.r,c.g,c.b,0),Vector2.zero);if(i>0)vh.AddTriangle(start,start+i,start+i+1);}
            }
            Glow(new Vector2(5,55),new Vector2(395,395),new Color(.002f,.009f,.016f,.71f));
            Glow(new Vector2(-120,160),new Vector2(265,335),new Color(.13f,.37f,.49f,.13f));
            Glow(new Vector2(235,130),new Vector2(295,255),new Color(.60f,.14f,.024f,.16f));
            Glow(new Vector2(15,-349),new Vector2(280,64),new Color(.002f,.009f,.014f,.85f));
        }
    }

    // Fade/settle only the presented sheet; buttons remain enabled throughout the 220 ms entry.
    public sealed class MenuSheetMotion023 : MonoBehaviour
    {
        RectTransform rect;CanvasGroup group;Vector2 rest;float entered;
        void Awake(){rect=(RectTransform)transform;rest=rect.anchoredPosition;group=GetComponent<CanvasGroup>();if(!group)group=gameObject.AddComponent<CanvasGroup>();}
        void OnEnable(){entered=Time.unscaledTime;}
        void Update()
        {
            float t=MenuShowcase023.ReducedMotion?1:Mathf.Clamp01((Time.unscaledTime-entered)/.22f);float eased=1-Mathf.Pow(1-t,3);
            rect.anchoredPosition=rest+Vector2.down*(10*(1-eased));group.alpha=.78f+.22f*eased;
        }
        void OnDisable(){if(rect)rect.anchoredPosition=rest;if(group)group.alpha=1;}
    }
}
