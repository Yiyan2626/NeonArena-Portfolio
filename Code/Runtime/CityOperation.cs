using UnityEngine;
namespace NeonArena
{
    // Wave objectives advance through the real city without relocating the player.
    public sealed class CityOperation : MonoBehaviour
    {
        public bool Enabled => ArenaGame.Instance && !ArenaGame.Instance.Frontier && ArenaGame.Instance.terrain.satelliteCity && ArenaGame.Instance.autoSpawn;
        public int Sector {get;private set;}
        public bool Travelling {get;private set;}
        public float HoldProgress {get;private set;}
        public const float HoldDuration=18;
        public Vector3 Destination {get;private set;}
        public bool Holding=>Enabled&&Sector==2&&!Travelling&&HoldProgress<HoldDuration;
        public bool CanComplete=>!Enabled||(!Travelling&&(!Holding));
        public bool CanSpawn=>!Enabled||!Travelling;
        public string SectorName=>new[]{"01 公园接触","02 街道推进","03 交换所守点","04 上层精英战"}[Sector];
        public bool PlayerInZone=>Vector3.Distance(Vector3.ProjectOnPlane(ArenaGame.Instance.player.transform.position-Destination,Vector3.up),Vector3.zero)<(Sector==2?7:6)&&Mathf.Abs(ArenaGame.Instance.player.transform.position.y-Destination.y)<2;
        public string Objective
        {
            get {
                if(Travelling)return "前往 "+SectorName+"  ·  "+Vector3.Distance(ArenaGame.Instance.player.transform.position,Destination).ToString("0")+" m";
                if(Holding)return "守住楼内信标  "+Mathf.FloorToInt(HoldProgress)+" / "+HoldDuration+" 秒"+(PlayerInZone?"":" · 返回信标范围");
                return Sector==3?"击败上层守卫与增援":"清除当前区域敌人";
            }
        }
        LineRenderer boundary,route;Material material;float routeAt;
        public void Begin(int wave)
        {
            Sector=(wave-1)%4;HoldProgress=0;
            Destination=new[]{new Vector3(0,1.8f,-10),new Vector3(0,.08f,-33),new Vector3(-24,.3f,-54),new Vector3(-28,5.25f,-49)}[Sector];
            Travelling=Enabled&&wave>1&&!PlayerInZone;routeAt=0;
        }
        public void Tick(float dt)
        {
            if(!Enabled||!ArenaGame.Instance.IsPlaying||dt<=0)return;
            if(Travelling&&PlayerInZone){Travelling=false;ArenaAudio.Instance?.Play(SoundCue.Select,Destination);}
            if(Holding&&PlayerInZone)HoldProgress=Mathf.Min(HoldDuration,HoldProgress+dt);
        }
        void Start()
        {
            material=RuntimeArt.Material(new Color(.2f,.8f,.85f),1);
            boundary=RuntimeArt.Line(transform,"Operation objective boundary",material,.065f);
            route=RuntimeArt.Line(transform,"Operation route",material,.1f);
        }
        void LateUpdate()
        {
            if(!boundary)return;var game=ArenaGame.Instance;
            bool active=Enabled&&game.IsPlaying&&!game.Cinema.FinaleActive;
            boundary.enabled=active&&(Travelling||Holding);route.enabled=active&&Travelling;
            if(boundary.enabled)
            {
                boundary.positionCount=49;float radius=Holding?7:2;
                for(int i=0;i<=48;i++){float a=i*Mathf.PI/24;boundary.SetPosition(i,Destination+Vector3.up*.07f+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*radius);}
            }
            if(route.enabled&&Time.unscaledTime>routeAt)
            {
                routeAt=Time.unscaledTime+.15f;
                var corners=game.terrain.RouteCorners(game.player.transform.position,Destination);
                route.positionCount=corners.Length;for(int i=0;i<corners.Length;i++)route.SetPosition(i,corners[i]+Vector3.up*.10f);
            }
        }
        public void ResetOperation(){Travelling=false;HoldProgress=0;Sector=0;}
        void OnDestroy(){if(material)Destroy(material);}
    }
}
