using UnityEngine;
using UnityEngine.Rendering;

namespace NeonArena
{
    // Scene-only presentation. No geometry, collider, navigation or gameplay changes.
    [DisallowMultipleComponent]
    public sealed class FrontierScenePolish028 : MonoBehaviour
    {
        Light key;
        Color keyColor,sky,equator,ground,fogColor;
        float keyIntensity,keyShadowStrength,fogStart,fogEnd;
        Quaternion keyRotation;
        AmbientMode ambientMode;
        FogMode fogMode;
        bool fogEnabled,applied;
        public bool Applied=>applied;
        public int AdjustedFoliageCount=>0;
        public int HiddenLegacySigns{get;private set;}
        Renderer[] legacySigns;
        bool[] signStates;
        CameraOccluder[] entryCherry;
        Renderer[] entryCherryRenderers;
        public int EntryCherryRenderers=>entryCherry==null?0:entryCherry.Length;

        void Start()
        {
            if(gameObject.scene.name!="BorderCity")return;
            foreach(var light in FindObjectsOfType<Light>())
                if(light.gameObject.scene==gameObject.scene&&light.type==LightType.Directional&&light.name=="Key Light"){key=light;break;}
            if(!key)return;
            keyColor=key.color;keyIntensity=key.intensity;keyShadowStrength=key.shadowStrength;keyRotation=key.transform.rotation;
            ambientMode=RenderSettings.ambientMode;sky=RenderSettings.ambientSkyColor;equator=RenderSettings.ambientEquatorColor;ground=RenderSettings.ambientGroundColor;
            fogEnabled=RenderSettings.fog;fogMode=RenderSettings.fogMode;fogColor=RenderSettings.fogColor;fogStart=RenderSettings.fogStartDistance;fogEnd=RenderSettings.fogEndDistance;
            key.color=new Color(1f,.9f,.78f);key.intensity=1.24f;key.shadowStrength=.82f;
            key.transform.rotation=Quaternion.Euler(43,-32,0);
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.46f,.54f,.64f);
            RenderSettings.ambientEquatorColor=new Color(.34f,.40f,.48f);
            RenderSettings.ambientGroundColor=new Color(.23f,.25f,.28f);
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;
            RenderSettings.fogColor=new Color(.42f,.49f,.56f);RenderSettings.fogStartDistance=78;RenderSettings.fogEndDistance=310;
            // These old explanatory TextMeshes face the wrong way from several routes.
            // HUD wayfinding and terminal beacons remain the actual navigation system.
            var signs=new System.Collections.Generic.List<Renderer>();
            foreach(var label in FindObjectsOfType<TextMesh>())
                if(label.gameObject.scene==gameObject.scene&&label.name.StartsWith("Wayfinding / "))
                {var r=label.GetComponent<Renderer>();if(r)signs.Add(r);}
            legacySigns=signs.ToArray();signStates=new bool[legacySigns.Length];
            for(int i=0;i<legacySigns.Length;i++){signStates[i]=legacySigns[i].enabled;legacySigns[i].enabled=false;}
            var cherries=new System.Collections.Generic.List<CameraOccluder>();
            var cherryRenderers=new System.Collections.Generic.List<Renderer>();
            var entryBounds=new Bounds(new Vector3(0,5,-13),new Vector3(12,14,18));
            foreach(var renderer in FindObjectsOfType<MeshRenderer>())
            {
                if(renderer.gameObject.scene!=gameObject.scene||!renderer.bounds.Intersects(entryBounds)||renderer.bounds.size.x>30||renderer.bounds.size.z>30)continue;
                bool cherry=false;foreach(var material in renderer.sharedMaterials)if(material&&material.name=="CityMat_151")cherry=true;
                var occluder=renderer.GetComponent<CameraOccluder>();
                if(!cherry||!occluder)continue;
                cherries.Add(occluder);cherryRenderers.Add(renderer);
                Debug.Log("FRONTIER_PRESENTATION_028 entryCherry="+renderer.name+" bounds="+renderer.bounds+" material=CityMat_151 collisionUnchanged=true");
            }
            entryCherry=cherries.ToArray();entryCherryRenderers=cherryRenderers.ToArray();
            HiddenLegacySigns=legacySigns.Length;applied=true;
            Debug.Log("FRONTIER_PRESENTATION_028 applied=true key=warm ambient=trilight fog=78..310 hiddenLegacySigns="+HiddenLegacySigns+" movedFoliage=0 colliderChanges=0");
        }
        void LateUpdate()
        {
            var game=ArenaGame.Instance;
            if(!applied||!game||!game.IsPlaying||!game.player)return;
            // Frozen capture cameras and live cameras share the same conservative local cutaway.
            var eye=game.player.viewCamera.transform.position;
            var delta=game.player.transform.position+Vector3.up-eye;
            var ray=new Ray(eye,delta.normalized);
            for(int i=0;i<entryCherry.Length;i++)
                if(entryCherry[i]&&entryCherryRenderers[i]&&entryCherryRenderers[i].bounds.IntersectRay(ray,out float distance)&&distance<delta.magnitude)
                    entryCherry[i].Reveal();
        }
        void OnDestroy()
        {
            if(!applied)return;
            if(key){key.color=keyColor;key.intensity=keyIntensity;key.shadowStrength=keyShadowStrength;key.transform.rotation=keyRotation;}
            // Do not replace a newly loaded scene's environment during old-scene teardown.
            if(UnityEngine.SceneManagement.SceneManager.GetActiveScene()==gameObject.scene)
            {
                RenderSettings.ambientMode=ambientMode;RenderSettings.ambientSkyColor=sky;RenderSettings.ambientEquatorColor=equator;RenderSettings.ambientGroundColor=ground;
                RenderSettings.fog=fogEnabled;RenderSettings.fogMode=fogMode;RenderSettings.fogColor=fogColor;RenderSettings.fogStartDistance=fogStart;RenderSettings.fogEndDistance=fogEnd;
            }
            for(int i=0;i<legacySigns.Length;i++)if(legacySigns[i])legacySigns[i].enabled=signStates[i];
            applied=false;
        }
    }
}
