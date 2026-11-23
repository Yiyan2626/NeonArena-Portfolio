using UnityEngine;

namespace NeonArena
{
    // A grounded sentry with true three-dimensional aim. The charge freezes its aim before firing.
    public sealed class EnemyRanged : MonoBehaviour
    {
        public bool IsRanged { get; private set; }
        public bool Charging { get; private set; }
        public int Shots { get; private set; }
        public bool HoldPosition {get;set;}
        public Vector3 LockedAim { get; private set; }
        public float WarningRemaining=>Charging?Mathf.Max(0,timer):0;
        public bool Retreating {get;private set;}
        public const float RetreatDistance=9, SettleDistance=12, ApproachDistance=17;
        EnemyController owner; PlayerController target; CharacterController motor;
        LineRenderer warning; Material material; float timer, gravity;
        int strafeSide;
        public void Initialize(bool ranged, PlayerController player)
        {
            owner=GetComponent<EnemyController>();motor=GetComponent<CharacterController>();target=player;
            IsRanged=ranged;Charging=false;Shots=0;timer=1.4f;gravity=0;HoldPosition=false;
            Retreating=false;LockedAim=Vector3.zero;strafeSide=(GetInstanceID()&1)==0?1:-1;
            if(!warning){material=RuntimeArt.Material(new Color(1,.32f,.06f),2);warning=RuntimeArt.Line(transform,"Sentry aim warning",material,.035f);}
            warning.enabled=false;
        }
        public bool Tick(float dt)
        {
            if(!IsRanged)return false;
            if(!target||!owner.IsAlive||owner.Spawning||dt<=0||!ArenaGame.Instance||!ArenaGame.Instance.IsPlaying)return true;
            Vector3 origin=transform.position+Vector3.up*1.25f, aim=target.transform.position+Vector3.up*.85f;
            Vector3 delta=aim-origin, flat=Vector3.ProjectOnPlane(delta,Vector3.up);
            Vector3 facing=Charging?Vector3.ProjectOnPlane(LockedAim-origin,Vector3.up):flat;
            if(facing.sqrMagnitude>.01f)owner.visual.rotation=Quaternion.RotateTowards(owner.visual.rotation,Quaternion.LookRotation(facing),dt*260);
            bool clear=delta.magnitude<(HoldPosition?65:30) && !Physics.Linecast(origin,aim,1<<9,QueryTriggerInteraction.Ignore);
            if(Charging)
            {
                timer-=dt;warning.enabled=owner.IsVisibleToPlayer;
                warning.positionCount=2;warning.SetPosition(0,origin);warning.SetPosition(1,LockedAim);
                warning.widthMultiplier=Mathf.Lerp(.035f,.10f,1-Mathf.Clamp01(timer/.85f));
                warning.startColor=warning.endColor=Color.Lerp(new Color(1,.62f,.12f),new Color(1,.10f,.05f),1-Mathf.Clamp01(timer/.85f));
                if(timer<=0)
                {
                    if(!Physics.Linecast(origin,LockedAim,1<<9,QueryTriggerInteraction.Ignore))
                    {SentryBolt.Fire(origin,(LockedAim-origin).normalized,owner.damage*.8f,target);Shots++;ArenaAudio.Instance?.Play(SoundCue.RicochetShot,origin,.8f);}
                    Charging=false;warning.enabled=false;timer=2.4f;
                }
            }
            else
            {
                timer-=dt;
                if(clear&&timer<=0){Charging=true;timer=.85f;LockedAim=aim;ArenaAudio.Instance?.Play(SoundCue.ChargeWarn,origin,.6f);}
            }
            Vector3 step=Vector3.zero;
            // Elevated authored sentries keep their perch; same-level guards can still make space.
            bool perch=HoldPosition&&Mathf.Abs(target.transform.position.y-transform.position.y)>2;
            if(!Charging&&!perch)
            {
                float distance=flat.magnitude;
                if(distance<RetreatDistance)Retreating=true;
                else if(distance>SettleDistance)Retreating=false;
                if(Retreating&&clear)
                {
                    Vector3 away=-flat.normalized;
                    Vector3 side=Vector3.Cross(Vector3.up,away)*strafeSide;
                    Vector3 escape=(away+side*.35f).normalized;
                    if(!owner.CanStep(escape))
                    {
                        escape=side;
                        if(!owner.CanStep(escape)){strafeSide=-strafeSide;escape=-side;}
                    }
                    if(owner.CanStep(escape))step=owner.Separate(escape)*owner.moveSpeed*.85f;
                }
                else if(!clear||distance>ApproachDistance)step=owner.Navigate(target.transform.position,dt)*owner.moveSpeed;
                else step=owner.Separate(Vector3.zero)*owner.moveSpeed*.5f;
            }
            if(motor.isGrounded&&gravity<0)gravity=-2;gravity=Mathf.Max(-25,gravity-dt*25);
            // A locked attack must not be displaced by the controller's stuck-recovery side step.
            if(Charging)motor.Move(Vector3.up*gravity*dt);
            else owner.MoveGrounded(step,gravity,dt);
            return true;
        }
        void OnDisable(){Charging=false;Retreating=false;timer=0;LockedAim=Vector3.zero;if(warning)warning.enabled=false;}
        void OnDestroy(){if(material)Destroy(material);}
    }
    public sealed class SentryBolt : MonoBehaviour
    {
        Vector3 direction;float damage,life;PlayerController target;Material material;
        public static void Fire(Vector3 origin,Vector3 direction,float damage,PlayerController target)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);go.name="Guardian plasma bolt";
            Object.Destroy(go.GetComponent<Collider>());go.transform.position=origin;go.transform.localScale=Vector3.one*.26f;
            var bolt=go.AddComponent<SentryBolt>();bolt.direction=direction;bolt.damage=damage;bolt.target=target;bolt.life=4;
            bolt.material=RuntimeArt.Material(new Color(1,.24f,.04f),3);go.GetComponent<Renderer>().sharedMaterial=bolt.material;
            var trail=go.AddComponent<TrailRenderer>();trail.sharedMaterial=bolt.material;trail.time=.12f;trail.startWidth=.18f;trail.endWidth=0;
        }
        void Update()
        {
            var game=ArenaGame.Instance;
            if(!game||!target||target.Health.IsDead||game.State==ArenaState.Ready||game.State==ArenaState.Reward||game.State==ArenaState.Intermission||game.State==ArenaState.Dead){Destroy(gameObject);return;}
            if(!game.IsPlaying||game.Cinema.FinaleActive)return;
            float dt=Time.deltaTime,step=17*dt;life-=dt;
            if(Physics.SphereCast(transform.position,.13f,direction,out var hit,step,(1<<9)|(1<<10),QueryTriggerInteraction.Ignore))
            {
                if(hit.collider.GetComponentInParent<PlayerController>()==target && target.Health.TakeDamage(damage))ArenaAudio.Instance?.PlayHurt();
                ImpactPool.Instance?.Emit(hit.point,new Color(1,.45f,.08f),8);Destroy(gameObject);return;
            }
            transform.position+=direction*step;if(life<=0)Destroy(gameObject);
        }
        public static void Clear(){foreach(var bolt in FindObjectsOfType<SentryBolt>())Destroy(bolt.gameObject);}
        void OnDestroy(){if(material)Destroy(material);}
    }
}
