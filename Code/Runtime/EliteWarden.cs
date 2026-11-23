using UnityEngine;

namespace NeonArena
{
    public enum ElitePhase { Pursuit,Windup,Attack,Recovery }
    public enum EliteMove { HammerSweep,Ram,ShockRing }
    public sealed class EliteWarden : MonoBehaviour
    {
        public bool IsElite { get; private set; }
        public ElitePhase Phase { get; private set; }
        public EliteMove Move { get; private set; }
        public float Remaining { get; private set; }
        public int Attacks { get; private set; }
        public int MoveMask { get; private set; }
        public Vector3 Direction { get; private set; }
        public float RingRadius { get; private set; }
        public const float RingRange=7, RingHeight=.7f;
        EnemyController enemy;PlayerController target;CharacterController motor;
        LineRenderer warning,impact;Material material;
        float cooldown,elapsed;int sequence;bool hit;Vector3 origin;
        public string MoveName=>Move==EliteMove.HammerSweep?"裂甲横扫 · 离开扇形":Move==EliteMove.Ram?"重装突进 · 横向闪避":"震荡环 · 跳跃 / 喷气越过";
        void Awake()
        {
            enemy=GetComponent<EnemyController>();motor=GetComponent<CharacterController>();
            material=new Material(Shader.Find("NeonArena/BladeGlow"));material.color=new Color(1,.27f,.035f,.9f);
            warning=RuntimeArt.Line(transform,"Warden Attack Telegraph",material,.10f);impact=RuntimeArt.Line(transform,"Warden Shock",material,.23f);
        }
        public void Initialize(bool elite,PlayerController player)
        {IsElite=elite;target=player;Phase=ElitePhase.Pursuit;Remaining=elapsed=RingRadius=0;cooldown=1.5f;sequence=Attacks=MoveMask=0;hit=false;warning.enabled=impact.enabled=false;}
        public bool BeginAttack(EliteMove move)
        {
            if(!IsElite||!enemy.IsAlive||!target||!ArenaGame.Instance.IsPlaying)return false;
            Vector3 delta=target.transform.position-transform.position;
            if(delta.magnitude>12||Mathf.Abs(delta.y)>1.2f || Physics.Linecast(transform.position+Vector3.up*.8f,target.transform.position+Vector3.up*.8f,1<<9,QueryTriggerInteraction.Ignore))return false;
            Move=move;Direction=Vector3.ProjectOnPlane(delta,Vector3.up).normalized;if(Direction.sqrMagnitude<.1f)Direction=Vector3.forward;
            origin=transform.position;Phase=ElitePhase.Windup;Remaining=move==EliteMove.ShockRing?1.2f:move==EliteMove.Ram?1.05f:.85f;elapsed=0;hit=false;RingRadius=0;
            ArenaAudio.Instance?.Play(move==EliteMove.ShockRing?SoundCue.EliteRing:move==EliteMove.Ram?SoundCue.EliteRam:SoundCue.EliteHammer,transform.position);
            DrawWarning();return true;
        }
        public bool Tick(float dt)
        {
            if(!IsElite||!target||!enemy.IsAlive||!ArenaGame.Instance.IsPlaying||ArenaGame.Instance.Cinema.FinaleActive||dt<=0)return false;
            if(Phase==ElitePhase.Pursuit)
            {
                cooldown-=dt;
                if(cooldown<=0 && BeginAttack((EliteMove)(sequence%3)))return true;
                return false;
            }
            enemy.visual.rotation=Quaternion.LookRotation(Direction);
            motor.Move(Vector3.down*5*dt);
            if(Phase==ElitePhase.Windup)
            {
                Remaining-=dt;DrawWarning();
                if(Remaining<=0){Phase=ElitePhase.Attack;elapsed=0;Remaining=Move==EliteMove.Ram?.6f:Move==EliteMove.ShockRing?.9f:.25f;warning.enabled=false;Attacks++;MoveMask|=1<<(int)Move;ArenaAudio.Instance?.Play(SoundCue.EliteImpact,transform.position);}
                return true;
            }
            if(Phase==ElitePhase.Attack)
            {
                float step=Mathf.Min(dt,Remaining);elapsed+=step;
                if(Move==EliteMove.Ram)
                {
                    Vector3 before=transform.position;Vector3 next=before+Direction*(11*step);
                    bool ground=ArenaTerrain.TryGround(next,out var landing,.65f)&&Mathf.Abs(landing.y-before.y)<.65f;
                    if(ground)motor.Move(Direction*(11*step));
                    else Remaining=0;
                    Vector3 segment=transform.position-before,offset=target.transform.position-before;
                    float t=segment.sqrMagnitude>.0001f?Mathf.Clamp01(Vector3.Dot(offset,segment)/segment.sqrMagnitude):0;
                    if((offset-segment*t).sqrMagnitude<1.5f*1.5f)Damage(1.65f);
                    if((transform.position-before).sqrMagnitude<.001f)Remaining=0;
                }
                else if(Move==EliteMove.HammerSweep)
                {
                    Vector3 to=target.transform.position-origin;
                    if(to.magnitude<4.2f && Mathf.Abs(to.y)<1.3f && Vector3.Dot(Direction,Vector3.ProjectOnPlane(to,Vector3.up).normalized)>.35f)Damage(1.5f);
                    DrawArc(impact,origin,4.1f,140);impact.enabled=enemy.IsVisibleToPlayer;
                }
                else
                {
                    float previous=RingRadius;RingRadius=RingRange*Mathf.Clamp01(elapsed/.9f);
                    Vector3 to=target.transform.position-origin;float distance=Vector3.ProjectOnPlane(to,Vector3.up).magnitude;
                    if(to.y<RingHeight && to.y>-.6f && distance>=previous-.45f && distance<=RingRadius+.45f)Damage(1.3f);
                    DrawArc(impact,origin,RingRadius,360);impact.enabled=enemy.IsVisibleToPlayer;
                }
                Remaining-=step;
                if(Remaining<=0)Recover(1.5f);
                return true;
            }
            Remaining-=dt;
            if(Remaining<=0){Phase=ElitePhase.Pursuit;cooldown=.8f;sequence++;}
            return true;
        }
        void Damage(float factor)
        {
            if(hit || Physics.Linecast(transform.position+Vector3.up*.7f,target.transform.position+Vector3.up*.7f,1<<9,QueryTriggerInteraction.Ignore))return;
            hit=true;if(target.Health.TakeDamage(enemy.damage*factor))ArenaAudio.Instance?.PlayHurt();
        }
        public void BladeStagger(){if(IsElite)Recover(.6f);}
        void Recover(float seconds){Phase=ElitePhase.Recovery;Remaining=seconds;warning.enabled=impact.enabled=false;}
        void DrawWarning()
        {
            warning.enabled=enemy.IsVisibleToPlayer;
            if(Move==EliteMove.Ram)
            {
                warning.positionCount=5;Vector3 side=Vector3.Cross(Vector3.up,Direction)*.8f;Vector3 p=origin+Vector3.up*.07f;
                float length=6.6f;if(Physics.Raycast(p+Vector3.up*.6f,Direction,out var hitWall,length,1<<9))length=hitWall.distance;
                warning.SetPositions(new[]{p-side,p+side,p+side+Direction*length,p-side+Direction*length,p-side});
            }
            else DrawArc(warning,origin,Move==EliteMove.ShockRing?RingRange:4.1f,Move==EliteMove.ShockRing?360:140);
        }
        void DrawArc(LineRenderer line,Vector3 center,float radius,float angle)
        {
            int count=48;line.positionCount=count+3;
            for(int i=0;i<=count;i++)
            {
                Vector3 p=center+Quaternion.Euler(0,Mathf.Lerp(-angle*.5f,angle*.5f,i/(float)count),0)*Direction*radius;
                if(ArenaTerrain.TryGround(p,out var ground,1))p.y=ground.y;
                line.SetPosition(i,p+Vector3.up*.07f);
            }
            line.SetPosition(count+1,angle<360?center+Vector3.up*.07f:line.GetPosition(0));line.SetPosition(count+2,line.GetPosition(0));
        }
        void LateUpdate(){if(!IsElite||!enemy.IsVisibleToPlayer){warning.enabled=impact.enabled=false;}}
        void OnDisable(){if(warning)warning.enabled=false;if(impact)impact.enabled=false;}
        void OnDestroy(){if(material)Destroy(material);}
    }
}
