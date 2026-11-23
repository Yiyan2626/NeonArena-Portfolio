using UnityEngine;
namespace NeonArena
{
    public sealed class FrontierMortar:MonoBehaviour
    {
        Vector3 start,end;float age;PlayerController target;Material material;LineRenderer warning;
        public static void Fire(Vector3 origin,Vector3 impact,PlayerController player)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);Object.Destroy(go.GetComponent<Collider>());go.name="Scorch arcing mortar";go.transform.position=origin;go.transform.localScale=Vector3.one*.25f;
            var shell=go.AddComponent<FrontierMortar>();shell.start=origin;shell.end=impact;shell.target=player;
            shell.material=new Material(Shader.Find("NeonArena/Beam"));shell.material.color=new Color(1,.32f,.03f);go.GetComponent<Renderer>().sharedMaterial=shell.material;
            var trail=go.AddComponent<TrailRenderer>();trail.sharedMaterial=shell.material;trail.time=.3f;trail.startWidth=.2f;trail.endWidth=0;
            shell.warning=RuntimeArt.Line(shell.transform,"Mortar impact warning",shell.material,.055f);shell.warning.positionCount=49;shell.warning.enabled=true;
            for(int i=0;i<=48;i++){float a=i*Mathf.PI/24;shell.warning.SetPosition(i,impact+Vector3.up*.08f+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*1.8f);}
        }
        void Update()
        {
            var g=ArenaGame.Instance;if(!g||!target||g.State==ArenaState.Ready||g.State==ArenaState.Dead||g.State==ArenaState.Reward||g.State==ArenaState.Victory){Destroy(gameObject);return;}
            if(!g.IsPlaying||g.Cinema.FinaleActive)return;age+=Time.deltaTime;float t=Mathf.Clamp01(age/1.4f);
            warning.widthMultiplier=Mathf.Lerp(.05f,.18f,t);warning.startColor=warning.endColor=Color.Lerp(new Color(1,.65f,.12f),new Color(1,.12f,.04f),t);
            transform.position=Vector3.Lerp(start,end,t)+Vector3.up*Mathf.Sin(t*Mathf.PI)*5;
            if(t<1)return;
            Vector3 delta=target.transform.position-end;
            if(Vector3.ProjectOnPlane(delta,Vector3.up).sqrMagnitude<1.8f*1.8f&&Mathf.Abs(delta.y)<1.6f&&!Physics.Linecast(end+Vector3.up*.5f,target.transform.position+Vector3.up*.5f,1<<9))target.Health.TakeDamage(22);
            ImpactPool.Instance?.Emit(end+Vector3.up*.3f,new Color(1,.32f,.04f),24);ArenaAudio.Instance?.Play(SoundCue.EliteImpact,end);Destroy(gameObject);
        }
        void OnDestroy(){if(material)Destroy(material);}
        public static void Clear(){foreach(var mortar in FindObjectsOfType<FrontierMortar>())Destroy(mortar.gameObject);}
    }
}
