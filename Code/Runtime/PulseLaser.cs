using UnityEngine;
namespace NeonArena
{
    public sealed class PulseLaser:MonoBehaviour
    {
        LineRenderer line;Material glow;float left=.12f;
        public static void Draw(Vector3 from,Vector3 to)
        {
            if(CombatVfx027.Instance){CombatVfx027.Instance.Laser(from,to);return;}
            var pulse=new GameObject("Laser single pulse").AddComponent<PulseLaser>();
            pulse.glow=new Material(Shader.Find("NeonArena/Beam"));pulse.glow.color=new Color(1,.66f,.16f);
            pulse.line=RuntimeArt.Line(pulse.transform,"Pulse",pulse.glow,.095f);pulse.line.positionCount=2;
            pulse.line.SetPositions(new[]{from,to});pulse.line.enabled=true;
        }
        void Update(){if(ArenaGame.Instance&&ArenaGame.Instance.State==ArenaState.Paused)return;left-=Time.deltaTime;line.widthMultiplier=Mathf.Max(0,left/.12f)*.095f;if(left<=0)Destroy(gameObject);}
        void OnDestroy(){if(glow)Destroy(glow);}
    }
}
