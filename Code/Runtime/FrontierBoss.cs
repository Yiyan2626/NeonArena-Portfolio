using UnityEngine;
namespace NeonArena
{
    public sealed class FrontierBoss:MonoBehaviour
    {
        public int Stage{get;private set;}=1;
        public bool TransitionPending{get;private set;}
        public bool Defeated=>owner&&owner.Health.IsDead;
        public int ArtilleryShots{get;private set;}
        public int Summoned{get;private set;}
        public bool Charging=>charging&&!TransitionPending;
        public float WarningRemaining=>Charging?Mathf.Max(0,timer):0;
        public string Instruction=>TransitionPending?"阶段间歇 · 控制室两侧安全终端 [F] 更换弹种":Stage==1?"01 能量盾 · 激光单发瞬破护盾":Stage==2?"02 集群增援 · 穿透弹清理纵队":"03 掩体炮击 · 反弹弹沿标记反射面折射";
        FrontierCampaign campaign;EnemyController owner;PlayerController player;CharacterController motor;
        Vector3[] cover;Vector3 lockedAim;float timer=2,summonTimer=4;bool charging;
        LineRenderer warning;Material material;
        public void Configure(FrontierCampaign flow,PlayerController target,Vector3[] positions)
        {
            if(owner)owner.Health.Damaged-=OnDamage;if(warning)Destroy(warning.gameObject);if(material)Destroy(material);enabled=true;
            campaign=flow;owner=GetComponent<EnemyController>();player=target;motor=GetComponent<CharacterController>();cover=positions;
            Stage=1;TransitionPending=false;timer=2;charging=false;ArtilleryShots=Summoned=0;summonTimer=4;
            owner.Health.maxHealth=3000;owner.Health.ResetHealth();owner.Shield.Initialize(true,4);owner.Shield.ConfigureToModel(owner.Motion.Model);
            owner.Health.Damaged+=OnDamage;
            material=new Material(Shader.Find("NeonArena/Beam"));material.color=new Color(1,.25f,.08f);
            warning=RuntimeArt.Line(transform,"Boss artillery preview",material,.065f);warning.positionCount=2;warning.enabled=false;
        }
        public float LimitDamage(float amount)
        {
            if(TransitionPending)return 0;
            float floor=Stage==1?owner.Health.maxHealth*2/3:Stage==2?owner.Health.maxHealth/3:0;
            return Mathf.Min(amount,Mathf.Max(0,owner.Health.Current-floor));
        }
        public float AmmoDamage(float damage,int ammo)=>Stage==2&&ammo!=0?damage*.38f:Stage==3&&ammo!=1?damage*.32f:damage;
        void OnDamage(float damage)
        {
            float threshold=Stage==1?owner.Health.maxHealth*2/3:owner.Health.maxHealth/3;
            if(Stage<3&&owner.Health.Current<=threshold+.01f&&!TransitionPending)
            {
                TransitionPending=true;owner.Shield.Initialize(false,4);charging=false;warning.enabled=false;campaign.BossPhaseBreak();
                ArenaAudio.Instance?.Play(SoundCue.ShieldBreak,transform.position);
            }
        }
        public void ResumePhase()
        {
            if(!TransitionPending)return;Stage++;TransitionPending=false;timer=1.6f;summonTimer=.8f;
            if(Stage==3&&cover!=null&&cover.Length>0){motor.enabled=false;transform.position=cover[0];motor.enabled=true;}
        }
        public void Tick(float dt)
        {
            if(TransitionPending||Defeated||dt<=0)return;
            Vector3 aim=player.transform.position+Vector3.up*.85f;Vector3 delta=Vector3.ProjectOnPlane(aim-transform.position,Vector3.up);
            if(delta.sqrMagnitude>.01f)owner.visual.rotation=Quaternion.RotateTowards(owner.visual.rotation,Quaternion.LookRotation(delta),dt*70);
            if(Stage==2)
            {
                summonTimer-=dt;
                if(summonTimer<=0&&ArenaGame.Instance.ActiveEnemies<9)
                {
                    for(int i=0;i<4;i++)
                    {
                        Vector3 slot=campaign.bossPoint+new Vector3((i-1.5f)*1.3f,0,5);
                        if(ArenaTerrain.TryGround(slot,out var p,2))slot=p;
                        if(ArenaGame.Instance.SpawnAuthored(slot,4,true,false,false,false))Summoned++;
                    }
                    summonTimer=10;
                }
            }
            timer-=dt;Vector3 origin=transform.position+Vector3.up*2.3f;
            if(charging)
            {
                warning.enabled=owner.IsVisibleToPlayer;warning.SetPosition(0,origin);warning.SetPosition(1,lockedAim);
                warning.widthMultiplier=Mathf.Lerp(.055f,.16f,1-Mathf.Clamp01(timer/1.2f));
                if(timer<=0)
                {
                    Vector3 direction=(lockedAim-origin).normalized;
                    if(Stage==3)
                    {
                        Vector3 floor=lockedAim-Vector3.up*.85f;
                        for(int i=0;i<3;i++){Vector3 landing=floor+player.visual.right*((i-1)*1.9f);if(ArenaTerrain.TryGround(landing,out var ground,3))landing=ground;FrontierMortar.Fire(origin,landing,player);}
                    }
                    else SentryBolt.Fire(origin,direction,15,player);
                    charging=false;warning.enabled=false;timer=Stage==3?2.4f:3.2f;ArtilleryShots++;owner.Motion.Melee();
                    ArenaAudio.Instance?.Play(SoundCue.EliteImpact,origin,.85f);
                }
            }
            else if(timer<=0){charging=true;lockedAim=aim;timer=Stage==3?.95f:1.2f;ArenaAudio.Instance?.Play(SoundCue.ChargeWarn,origin);}
            if(Stage<3 && delta.magnitude>9)
            {
                Vector3 step=ArenaGame.Instance.terrain.Steer(transform.position,player.transform.position)*1.6f;
                motor.Move((step+Vector3.down*5)*dt);
            }
            else motor.Move(Vector3.down*5*dt);
        }
        void OnDisable(){if(warning)warning.enabled=false;}
        public void Retire(){enabled=false;if(owner)owner.Health.Damaged-=OnDamage;if(warning)warning.enabled=false;}
        void OnDestroy(){if(owner)owner.Health.Damaged-=OnDamage;if(material)Destroy(material);}
    }
}
