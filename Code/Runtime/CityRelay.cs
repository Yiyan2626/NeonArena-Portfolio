using UnityEngine;
using UnityEngine.UI;
namespace NeonArena
{
    // Optional city-specific trap: commit at the cabinet, then retreat up a ramp
    // to avoid the short-range feedback while the street-level pulse hits pursuers.
    public sealed class CityRelay:MonoBehaviour
    {
        public int index;
        public const float ArmDuration=1.1f,Delay=2,Radius=11;
        public float Charge {get;private set;}
        public float Fuse {get;private set;}
        public bool Spent {get;private set;}
        public bool Armed {get;private set;}
        public int Hits {get;private set;}
        int wave=-1;float elapsed;LineRenderer ring;Material material;Text prompt;
        void Start()
        {
            material=RuntimeArt.Material(new Color(.2f,.7f,.75f),.7f);
            ring=RuntimeArt.Line(transform,"Capacitor warning radius",material,.055f);ring.positionCount=65;
            var canvas=ArenaGame.Instance.hud.transform;
            prompt=ArenaTheme.Text("Relay prompt "+index,canvas,new Vector2(0,110),new Vector2(780,48),"",17,ArenaTheme.Paper,TextAnchor.MiddleCenter,new Vector2(.5f,0));
        }
        void Update(){Step(Time.deltaTime,Input.GetKey(KeyCode.F));}
        public void Step(float dt,bool held)
        {
            var game=ArenaGame.Instance;if(!game)return;
            if(wave!=game.Wave||game.Elapsed<elapsed){ResetRelay();wave=game.Wave;}
            elapsed=game.Elapsed;
            if(!game.IsPlaying||game.Cinema.FinaleActive){if(prompt)prompt.text="";if(ring)ring.enabled=false;return;}
            var player=game.player;var delta=player.transform.position-transform.position;
            bool near=Vector3.ProjectOnPlane(delta,Vector3.up).magnitude<2.6f&&Mathf.Abs(delta.y)<1.4f;
            bool clear=near&&!Physics.Linecast(player.transform.position+Vector3.up*1.4f,transform.position+Vector3.up*1.5f,1<<9,QueryTriggerInteraction.Ignore);
            if(dt>0&&!Spent&&!Armed){Charge=clear&&held?Mathf.Min(ArmDuration,Charge+dt):Mathf.Max(0,Charge-dt*3);if(Charge>=ArmDuration){Armed=true;Fuse=Delay;ArenaAudio.Instance?.Play(SoundCue.Select,transform.position);}}
            else if(dt>0&&Armed){Fuse=Mathf.Max(0,Fuse-dt);if(Fuse<=0)Discharge();}
            if(prompt)prompt.text=Armed&&delta.magnitude<18?"中继过载 "+Fuse.ToString("0.0")+" 秒 · 退到 4 米外或上层，避开回流":clear&&!Spent?"长按 F  中继过载 "+Mathf.RoundToInt(Charge/ArmDuration*100)+"%  ·  延迟脉冲打击追兵":clear&&Spent?"中继电容已耗尽 · 下一波恢复":"";
            if(ring)
            {
                ring.enabled=Armed||(near&&!Spent);ring.startColor=ring.endColor=Armed?new Color(1,.43f,.15f):new Color(.2f,.7f,.75f);
                float radius=Armed?4:2.6f;
                for(int i=0;i<=64;i++){float a=i*Mathf.PI/32;ring.SetPosition(i,transform.position+new Vector3(Mathf.Cos(a)*radius,.08f,Mathf.Sin(a)*radius));}
            }
        }
        public bool CanAffect(Vector3 feet,float radius)
        {
            Vector3 origin=transform.position+Vector3.up*1.5f,target=feet+Vector3.up*.8f;
            return Vector3.Distance(transform.position,feet)<radius&&Mathf.Abs(feet.y-transform.position.y)<2.4f&&!Physics.Linecast(origin,target,1<<9,QueryTriggerInteraction.Ignore);
        }
        void Discharge()
        {
            Armed=false;Spent=true;var game=ArenaGame.Instance;
            foreach(var enemy in game.Enemies)if(enemy.IsAlive&&CanAffect(enemy.transform.position,Radius)){enemy.Health.TakeDamage(100);Hits++;}
            if(CanAffect(game.player.transform.position,4))game.player.Health.TakeDamage(18);
            ImpactPool.Instance?.Emit(transform.position+Vector3.up*1.6f,Color.cyan,45);
            ArenaAudio.Instance?.Play(SoundCue.ShieldBreak,transform.position);
        }
        public void ResetRelay(){Charge=Fuse=0;Spent=Armed=false;Hits=0;}
        void OnDestroy(){if(material)Destroy(material);if(prompt)Destroy(prompt.gameObject);}
    }
}
