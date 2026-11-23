using UnityEngine;
namespace NeonArena
{
    public sealed class SpawnTelegraph : MonoBehaviour
    {
        public const float Duration=1.05f;
        public bool Active {get;private set;}
        public float Remaining {get;private set;}
        public Vector3 Point {get;private set;}
        Material material;LineRenderer ring,column;
        void Awake()
        {
            material=RuntimeArt.Material(new Color(1,.35f,.12f),1.5f);
            ring=RuntimeArt.Line(transform,"Incoming enemy ring",material,.1f);
            column=RuntimeArt.Line(transform,"Incoming enemy signal",material,.065f);Clear();
        }
        public void Begin(Vector3 point)
        {
            Point=point;Remaining=Duration;Active=true;
            ArenaAudio.Instance?.Play(SoundCue.ChargeWarn,point,.65f);Render();
        }
        public bool Step(float dt)
        {
            if(!Active||dt<=0||!ArenaGame.Instance||!ArenaGame.Instance.IsPlaying||ArenaGame.Instance.Cinema.FinaleActive)return false;
            Remaining=Mathf.Max(0,Remaining-dt);Render();return Remaining<=0;
        }
        void Render()
        {
            var game=ArenaGame.Instance;bool shown=Active&&game&&(game.IsPlaying||game.State==ArenaState.Paused)&&(!game.Vision||game.Vision.CanObserve(Point));
            ring.enabled=column.enabled=shown;if(!shown)return;
            float t=1-Remaining/Duration;ring.positionCount=49;
            for(int i=0;i<=48;i++){float a=i*Mathf.PI/24;ring.SetPosition(i,Point+Vector3.up*.06f+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*(1.35f-t*.6f));}
            column.positionCount=2;column.SetPosition(0,Point+Vector3.up*.1f);column.SetPosition(1,Point+Vector3.up*(.8f+t*1.4f));
        }
        void LateUpdate(){if(Active)Render();}
        public void Clear(){Active=false;Remaining=0;if(ring)ring.enabled=false;if(column)column.enabled=false;}
        void OnDisable(){Clear();}
        void OnDestroy(){if(material)Destroy(material);}
    }
}
