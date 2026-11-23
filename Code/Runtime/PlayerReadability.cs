using UnityEngine;

namespace NeonArena
{
    [DefaultExecutionOrder(120)]
    public sealed class PlayerReadability : MonoBehaviour
    {
        PlayerController player;
        SkinnedMeshRenderer silhouette;
        Material outline,marker;
        LineRenderer landing,pulse;
        Renderer[] legacyMarker;bool[] legacyMarkerStates;
        public bool Occluded { get; private set; }
        public bool LandingVisible=>landing&&landing.enabled;
        public Vector3 LandingPoint { get; private set; }
        void Start()
        {
            player=GetComponent<PlayerController>();var skin=player.Motion.Model.skin;
            var markerRoot=player.visual.Find("Player Marker");
            if(markerRoot){legacyMarker=markerRoot.GetComponentsInChildren<Renderer>();legacyMarkerStates=new bool[legacyMarker.Length];for(int i=0;i<legacyMarker.Length;i++){legacyMarkerStates[i]=legacyMarker[i].enabled;legacyMarker[i].enabled=false;}}
            outline=new Material(Shader.Find("NeonArena/PlayerSilhouette"));outline.color=new Color(.08f,.75f,1,.62f);
            silhouette=new GameObject("Player Occlusion Silhouette").AddComponent<SkinnedMeshRenderer>();silhouette.transform.SetParent(skin.transform,false);
            silhouette.sharedMesh=skin.sharedMesh;silhouette.bones=skin.bones;silhouette.rootBone=skin.rootBone;silhouette.localBounds=skin.localBounds;silhouette.updateWhenOffscreen=true;
            var mats=new Material[skin.sharedMesh.subMeshCount];for(int i=0;i<mats.Length;i++)mats[i]=outline;silhouette.sharedMaterials=mats;silhouette.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            marker=new Material(Shader.Find("NeonArena/BladeGlow"));marker.color=new Color(.05f,.85f,1,.8f);
            landing=RuntimeArt.Line(transform,"Projected Landing",marker,.07f);pulse=RuntimeArt.Line(transform,"Overheat Ignition Ring",marker,.10f);
        }
        void LateUpdate()
        {
            if(!player||!ArenaGame.Instance)return;
            bool active=ArenaGame.Instance.IsPlaying;
            Vector3 point=transform.position+Vector3.up*.9f;
            Occluded=ArenaGame.Instance.Vision.PlayerInBrush;
            Vector3 ray=point-player.viewCamera.transform.position;
            foreach(var occlusionHit in Physics.RaycastAll(player.viewCamera.transform.position,ray.normalized,ray.magnitude,1<<9,QueryTriggerInteraction.Ignore))
            {
                var cut=occlusionHit.collider.GetComponent<CameraOccluder>();if(cut&&cut.Fade<.5f)continue;
                var roof=occlusionHit.collider.GetComponent<CutawayRoof>();if(roof&&roof.IsCutaway)continue;
                Occluded=true;break;
            }
            silhouette.enabled=active&&Occluded;
            landing.enabled=active&&!player.Grounded && Physics.Raycast(transform.position+Vector3.up*.1f,Vector3.down,out _,14,1<<9,QueryTriggerInteraction.Ignore);
            if(landing.enabled && Physics.Raycast(transform.position+Vector3.up*.1f,Vector3.down,out var hit,14,1<<9,QueryTriggerInteraction.Ignore))
            {LandingPoint=hit.point;Ring(landing,hit.point+Vector3.up*.045f,.6f);}
            float t=Time.unscaledTime-player.Overheat.ActivatedAt;pulse.enabled=active&&t<.7f&&t>=0;
            if(pulse.enabled){Ring(pulse,transform.position+Vector3.up*.08f,1+t*5);pulse.widthMultiplier=.16f*(1-t/.7f);}
        }
        static void Ring(LineRenderer line,Vector3 p,float radius)
        {line.positionCount=49;for(int i=0;i<=48;i++){float a=i*Mathf.PI*2/48;line.SetPosition(i,p+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*radius);}}
        void OnDestroy(){if(legacyMarker!=null)for(int i=0;i<legacyMarker.Length;i++)if(legacyMarker[i])legacyMarker[i].enabled=legacyMarkerStates[i];if(outline)Destroy(outline);if(marker)Destroy(marker);}
    }
}
