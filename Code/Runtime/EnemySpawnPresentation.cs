using UnityEngine;

namespace NeonArena
{
    // The pooled body keeps its full scale. Only its visual root descends; physics stays grounded.
    [DefaultExecutionOrder(150)]
    public sealed class EnemySpawnPresentation : MonoBehaviour
    {
        public const float Duration=.65f;
        public bool Active {get;private set;}
        public float Elapsed {get;private set;}
        public bool HideBody=>Active&&Elapsed<.08f;
        public float RevealProgress=>Mathf.Clamp01((Elapsed-.08f)/.44f);
        public Transform EffectRoot {get;private set;}
        EnemyController owner;
        Vector3 rest;
        bool initialized;
        Material material;
        LineRenderer ring,frame,scan;
        CharacterModel cachedModel;
        Renderer[] bodySurfaces;
        Material[][] originalMaterials,revealMaterials;
        MaterialPropertyBlock scanProperties;
        bool materialsApplied;
        static readonly int ScanHeight=Shader.PropertyToID("_ArrivalHeight");
        static readonly int ScanActive=Shader.PropertyToID("_ArrivalActive");
        public void Begin(EnemyController enemy)
        {
            owner=enemy;
            if(!initialized)
            {
                initialized=true;rest=owner.visual.localPosition;
                EffectRoot=new GameObject("Arrival landing frame").transform;
                EffectRoot.SetParent(transform,false);
                material=RuntimeArt.Material(new Color(1,.48f,.10f),1.4f);
                ring=RuntimeArt.Line(EffectRoot,"Ground arrival ring",material,.045f);
                frame=RuntimeArt.Line(EffectRoot,"Descent volume edges",material,.025f);
                scan=RuntimeArt.Line(EffectRoot,"Body materialization scan",material,.025f);
                scan.positionCount=33;
                ring.positionCount=33;frame.positionCount=16;
            }
            Active=true;Elapsed=0;
            owner.visual.localPosition=rest;
            PrepareMaterials();ApplyScan();
            Present();
        }
        void Update(){Step(Time.deltaTime);}
        public void Step(float dt)
        {
            if(!Active||dt<=0||!ArenaGame.Instance||!ArenaGame.Instance.IsPlaying||ArenaGame.Instance.Cinema.FinaleActive)return;
            Elapsed=Mathf.Min(Duration,Elapsed+dt);
            float drop=Mathf.Clamp01((Elapsed-.14f)/.32f);
            float height=.85f*(1-drop*drop);
            if(Elapsed>.46f)height=-Mathf.Sin(Mathf.Clamp01((Elapsed-.46f)/.19f)*Mathf.PI)*.09f;
            owner.visual.localPosition=rest+Vector3.up*height;
            if(Elapsed>=Duration){Active=false;owner.visual.localPosition=rest;RestoreMaterials();}
            else ApplyScan();
            owner.RefreshVisibility();Present();
        }
        void LateUpdate(){if(Active)ApplyScan();Present();}
        void PrepareMaterials()
        {
            if(cachedModel!=owner.Motion.Model)
            {
                ReleaseMaterials();cachedModel=owner.Motion.Model;
                bodySurfaces=cachedModel.GetComponentsInChildren<Renderer>(true);
                originalMaterials=new Material[bodySurfaces.Length][];
                revealMaterials=new Material[bodySurfaces.Length][];
                var shader=Resources.Load<Shader>("EnemyArrival026");
                for(int i=0;i<bodySurfaces.Length;i++)
                {
                    originalMaterials[i]=bodySurfaces[i].sharedMaterials;
                    revealMaterials[i]=new Material[originalMaterials[i].Length];
                    for(int j=0;j<originalMaterials[i].Length;j++)
                    {
                        var source=originalMaterials[i][j];
                        // Clone the material, retaining its albedo texture, UV transform and PBR values.
                        var arrival=new Material(source){name=source.name+" arrival",shader=shader};
                        revealMaterials[i][j]=arrival;
                    }
                }
                scanProperties=new MaterialPropertyBlock();
            }
            for(int i=0;i<bodySurfaces.Length;i++)if(bodySurfaces[i])bodySurfaces[i].sharedMaterials=revealMaterials[i];
            materialsApplied=true;
        }
        Bounds BodyBounds()
        {
            // Imported skins use an intentionally oversized 6 m culling box; scan the actual mesh.
            var skin=owner.Motion.Model.skin;Bounds local=skin.sharedMesh.bounds;
            Bounds bounds=new Bounds(skin.transform.TransformPoint(local.center),Vector3.zero);
            for(int i=0;i<8;i++)
            {
                Vector3 corner=local.center+Vector3.Scale(local.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                bounds.Encapsulate(skin.transform.TransformPoint(corner));
            }
            return bounds;
        }
        void ApplyScan()
        {
            if(!materialsApplied)return;
            Bounds bounds=BodyBounds();
            float height=Mathf.Lerp(bounds.min.y-.05f,bounds.max.y+.08f,RevealProgress);
            for(int i=0;i<bodySurfaces.Length;i++)
            {
                var surface=bodySurfaces[i];if(!surface)continue;
                surface.GetPropertyBlock(scanProperties);
                scanProperties.SetFloat(ScanHeight,height);scanProperties.SetFloat(ScanActive,1);
                surface.SetPropertyBlock(scanProperties);
            }
        }
        void RestoreMaterials()
        {
            if(!materialsApplied)return;
            for(int i=0;i<bodySurfaces.Length;i++)
            {
                var surface=bodySurfaces[i];if(!surface)continue;
                surface.sharedMaterials=originalMaterials[i];
                surface.GetPropertyBlock(scanProperties);scanProperties.SetFloat(ScanActive,0);surface.SetPropertyBlock(scanProperties);
            }
            materialsApplied=false;
        }
        void ReleaseMaterials()
        {
            RestoreMaterials();
            if(revealMaterials!=null)for(int i=0;i<revealMaterials.Length;i++)for(int j=0;j<revealMaterials[i].Length;j++)if(revealMaterials[i][j])Destroy(revealMaterials[i][j]);
            revealMaterials=null;
        }
        void Present()
        {
            if(!ring)return;
            bool show=Active&&owner&&owner.IsAlive&&owner.IsVisibleToPlayer;
            ring.enabled=frame.enabled=show;
            scan.enabled=show&&RevealProgress>0&&RevealProgress<1;
            if(!show)return;
            float t=Elapsed/Duration;
            float radius=.8f+Mathf.Clamp01((Elapsed-.46f)/.19f)*.35f;
            Vector3 origin=transform.position+Vector3.up*.065f;
            for(int i=0;i<33;i++)
            {float angle=i*Mathf.PI/16;ring.SetPosition(i,origin+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*radius);}
            float h=owner.Elite.IsElite?3.5f:2.15f;
            for(int side=0;side<4;side++)
            {
                float a=side*Mathf.PI*.5f+Mathf.PI*.25f;
                Vector3 corner=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*.7f;
                // Four short vertical brackets give volume without an opaque shell hiding the model.
                int index=side*4;
                frame.SetPosition(index,origin+corner*.82f+Vector3.up*h);
                frame.SetPosition(index+1,origin+corner+Vector3.up*h);
                frame.SetPosition(index+2,origin+corner+Vector3.up*(h-.35f));
                frame.SetPosition(index+3,origin+corner*.82f+Vector3.up*(h-.35f));
            }
            frame.enabled=show&&Elapsed<.46f;
            ring.widthMultiplier=Mathf.Lerp(.055f,.015f,t);
            frame.widthMultiplier=Mathf.Lerp(.035f,.008f,t);
            if(scan.enabled)
            {
                Bounds bounds=BodyBounds();float y=Mathf.Lerp(bounds.min.y-.05f,bounds.max.y+.08f,RevealProgress);
                for(int i=0;i<33;i++)
                {
                    float a=i*Mathf.PI/16;
                    scan.SetPosition(i,new Vector3(bounds.center.x+Mathf.Cos(a)*(bounds.extents.x+.08f),y,bounds.center.z+Mathf.Sin(a)*(bounds.extents.z+.08f)));
                }
            }
        }
        public void ResetPresentation()
        {
            Active=false;Elapsed=0;
            RestoreMaterials();
            if(owner&&owner.visual)owner.visual.localPosition=rest;
            if(ring)ring.enabled=false;if(frame)frame.enabled=false;if(scan)scan.enabled=false;
        }
        void OnDisable(){ResetPresentation();}
        void OnDestroy(){ReleaseMaterials();if(material)Destroy(material);}
    }
}
