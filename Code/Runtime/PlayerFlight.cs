using UnityEngine;

namespace NeonArena
{
    public sealed class PlayerFlight : MonoBehaviour
    {
        public const float Capacity=2.6f;
        public float Fuel { get; private set; }=Capacity;
        public bool Thrusting { get; private set; }
        public bool Held { get; set; }
        public int Rank=>player.weapon.Loadout.Jump;
        public int Launches { get; private set; }
        PlayerController player;
        Transform pack;
        readonly Transform[] flames=new Transform[2];
        Material armor,glow;
        bool exhausted;
        float groundedFor,launchHeight;
        public void Initialize(PlayerController owner)
        {
            player=owner;armor=RuntimeArt.Material(new Color(.14f,.19f,.25f));glow=RuntimeArt.Material(new Color(.08f,.6f,1),2);
            pack=new GameObject("Jump Module / Jetpack").transform;pack.SetParent(player.visual,false);pack.localPosition=new Vector3(0,1,-.35f);
            var source=Resources.Load<GameObject>("Frontier/Jetpack");bool detailed=ArenaGame.Instance&&ArenaGame.Instance.Frontier&&source;
            if(detailed)Instantiate(source,pack,false);else RuntimeArt.Box(pack,"Battery",Vector3.zero,new Vector3(.48f,.5f,.28f),armor);
            for(int i=0;i<2;i++)
            {
                float x=i==0?-.32f:.32f;
                if(!detailed)RuntimeArt.Box(pack,"Jet Nozzle",new Vector3(x,-.1f,0),new Vector3(.2f,.52f,.27f),armor);
                var exhaust=new GameObject("Jet exhaust").AddComponent<LineRenderer>();exhaust.transform.SetParent(pack,false);exhaust.useWorldSpace=false;exhaust.positionCount=2;exhaust.SetPositions(new[]{new Vector3(x,-.4f,0),new Vector3(x,-.9f,0)});exhaust.startWidth=.15f;exhaust.endWidth=0;exhaust.sharedMaterial=glow;flames[i]=exhaust.transform;
            }
            pack.gameObject.SetActive(false);
        }
        public float Step(float velocity,bool grounded,float dt,float height)
        {
            if(!ArenaGame.Instance || !ArenaGame.Instance.IsPlaying || dt<=0)return velocity;
            bool before=Thrusting;Thrusting=false;
            if(grounded)
            {
                groundedFor+=dt;launchHeight=height;
                if(groundedFor>.35f)Fuel=Mathf.Min(Capacity,Fuel+dt*1.3f);
                if(Fuel>=.5f)exhausted=false;
            }
            else groundedFor=0;
            if(Rank>=2 && Held && !grounded && !exhausted && Fuel>0)
            {
                Thrusting=true;Fuel=Mathf.Max(0,Fuel-dt);
                velocity=Mathf.MoveTowards(velocity+25*dt,height<launchHeight+9?5.5f:0,18*dt);
                velocity=Mathf.Min(velocity,Mathf.Max(0,(launchHeight+9-height)/dt));
                if(!before)Launches++;
                if(Fuel<=0){exhausted=true;Thrusting=false;}
            }
            return velocity;
        }
        void LateUpdate()
        {
            if(!pack)return;pack.gameObject.SetActive(Rank>=2);
            foreach(var flame in flames)
            {
                flame.gameObject.SetActive(Thrusting && ArenaGame.Instance.IsPlaying && !player.Overheat.Striking);
                flame.localScale=new Vector3(.12f,.55f+Mathf.Sin(Time.time*70)*.12f,.12f);
            }
        }
        public void ResetFlight(){Fuel=Capacity;Held=Thrusting=exhausted=false;groundedFor=0;launchHeight=0;Launches=0;}
        public void Stop(){Held=Thrusting=false;}
        void OnDestroy(){if(armor)Destroy(armor);if(glow)Destroy(glow);}
    }
}
