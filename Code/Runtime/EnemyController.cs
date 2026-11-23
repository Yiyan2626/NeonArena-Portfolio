using UnityEngine;

namespace NeonArena
{
    [RequireComponent(typeof(CharacterController), typeof(Health))]
    public sealed class EnemyController : MonoBehaviour
    {
        public float moveSpeed = 2.3f;
        public float attackRange = 1.22f;
        public float attackInterval = 1.1f;
        public float damage = 12;
        public Transform visual;
        public Health Health { get; private set; }
        public EnemyCharge Charge { get; private set; }
        public EnemyShield Shield { get; private set; }
        public EliteWarden Elite { get; private set; }
        public EnemyRanged Ranged { get; private set; }
        public CharacterMotion Motion { get; private set; }
        public EnemySpawnPresentation Arrival { get; private set; }
        public bool Spawning => Arrival && Arrival.Active;
        public bool IsVisibleToPlayer { get; private set; } = true;
        public bool IsAlive => gameObject.activeSelf && !Health.IsDead;
        public Vector3 VisibilityPoint=>transform.position+Vector3.up*(ArenaGame.Instance&&ArenaGame.Instance.Frontier&&Elite&&Elite.IsElite?1.8f:0);
        CharacterController motor;
        PlayerController target;
        float attackAt, activationAt;
        float baseSpeed, baseDamage, baseHealth;
        readonly Collider[] neighbors = new Collider[12];
        Renderer[] surfaces;
        Vector3 cachedSteering, navigationGoal, recoveryDirection;
        float navigationRemaining, stalledFor, recoveryRemaining;
        int avoidanceSide;
        float visibilityAt, verticalSpeed, flinchUntil, nextFlinch;
        float meleeWindup;bool meleePending;
        public bool MeleePreparing=>meleePending;
        public float MeleeRemaining=>meleeWindup;
        public bool Staggered=>Time.time<flinchUntil;
        public void ShieldStagger(){if(!Elite.IsElite){flinchUntil=Time.time+.45f;nextFlinch=Time.time+.8f;meleePending=false;Motion.Melee();}}

        void Awake()
        {
            motor = GetComponent<CharacterController>();
            Health = GetComponent<Health>();
            Charge = GetComponent<EnemyCharge>();
            Shield = GetComponentInChildren<EnemyShield>(true);
            Elite=gameObject.AddComponent<EliteWarden>();
            Ranged=gameObject.AddComponent<EnemyRanged>();
            Motion=gameObject.AddComponent<CharacterMotion>();Motion.Initialize(null,this);
            Arrival=gameObject.AddComponent<EnemySpawnPresentation>();
            surfaces = GetComponentsInChildren<Renderer>(true);
            Health.Died += OnDeath; Health.Damaged += OnDamage;
            baseSpeed = moveSpeed; baseDamage = damage; baseHealth = Health.maxHealth;
        }
        void OnDestroy() { if (Health != null) { Health.Died -= OnDeath; Health.Damaged -= OnDamage; } }

        public void Spawn(Vector3 position, PlayerController player, int wave = 1, bool charger = false, bool shielded = false, bool elite = false, bool ranged = false)
        {
            if(Arrival)Arrival.ResetPresentation();
            var priorBoss=GetComponent<FrontierBoss>();if(priorBoss)priorBoss.Retire();
            gameObject.SetActive(true);
            motor.enabled = false;
            transform.position = position;
            motor.enabled = true;
            target = player; verticalSpeed = 0; flinchUntil=nextFlinch=0;meleePending=false;meleeWindup=0;
            navigationRemaining=stalledFor=recoveryRemaining=0;cachedSteering=Vector3.zero;
            avoidanceSide=(GetInstanceID()&1)==0?1:-1;
            int tier = Mathf.Max(0, wave - 1);
            moveSpeed = baseSpeed * (1 + Mathf.Min(0.5f, tier * 0.02f));
            damage = baseDamage * (1 + tier * 0.05f);
            Health.maxHealth = baseHealth * (1 + tier * 0.10f);
            if (charger) { Health.maxHealth *= 1.35f; moveSpeed *= 0.9f; }
            if (shielded) { Health.maxHealth *= 1.15f; moveSpeed *= 0.72f; }
            if(elite){charger=shielded=false;Health.maxHealth=450*(1+.10f*Mathf.Max(0,wave-3));moveSpeed=2.4f;}
            if(elite)ranged=false;
            if(ranged){charger=false;if(!ArenaGame.Instance.Frontier)shielded=false;Health.maxHealth*=1.2f;}
            Ranged.Initialize(ranged,player);
            motor.height=elite?2.5f:ranged?2:1.4f;motor.center=Vector3.up*motor.height*.5f;
            if(elite&&ArenaGame.Instance.Frontier){motor.height=3.5f;motor.center=Vector3.up*1.75f;}
            if (Charge) Charge.Initialize(charger, player);
            if (Shield) Shield.Initialize(shielded, wave);
            Elite.Initialize(elite,player);Motion.SetKind(elite?4:ranged?5:charger?2:shielded?3:1);Motion.ResetMotion();
            if(Shield && shielded)Shield.ConfigureToModel(Motion.Model);
            if(Shield)Physics.IgnoreCollision(Shield.surface,player.GetComponent<CharacterController>(),true);
            Health.ResetHealth();
            attackAt = Time.time + 0.8f;
            activationAt = Time.time + 0.65f;
            visual.localScale = Vector3.one;
            Arrival.Begin(this);
            surfaces=GetComponentsInChildren<Renderer>(true);
            RefreshVisibility();
        }

        void Update()
        {
            if (Time.unscaledTime >= visibilityAt) { visibilityAt = Time.unscaledTime + 0.06f; RefreshVisibility(); }
            if (!ArenaGame.Instance || !ArenaGame.Instance.IsPlaying) return;
            Tick(Time.deltaTime);
        }

        public void Tick(float dt)
        {
            if (dt<=0 || !target || target.Health.IsDead || !IsAlive || Spawning || Time.time < activationAt || !ArenaGame.Instance.IsPlaying) return;
            if (ArenaGame.Instance.Cinema.FinaleActive) return;
            var frontierBoss=GetComponent<FrontierBoss>();if(frontierBoss&&frontierBoss.enabled){frontierBoss.Tick(dt);return;}
            if(Time.time<flinchUntil && motor.isGrounded)return;
            if (Elite && Elite.Tick(dt)) return;
            if (Charge && Charge.Tick(dt)) return;
            if (Ranged && Ranged.Tick(dt)) return;
            if(meleePending)
            {
                meleeWindup-=dt;
                if(meleeWindup<=0)
                {
                    meleePending=false;attackAt=Time.time+attackInterval;
                    if(Vector3.Distance(target.transform.position,transform.position)<=attackRange+.25f&&!Physics.Linecast(transform.position+Vector3.up*.8f,target.transform.position+Vector3.up*.8f,1<<9,QueryTriggerInteraction.Ignore))
                    {Motion.Melee();if(target.Health.TakeDamage(damage)){ArenaAudio.Instance?.PlayHurt();ImpactPool.Instance?.Emit(target.transform.position+Vector3.up,Color.cyan,12);}}
                }
                motor.Move(Vector3.down*2*dt);return;
            }
            Vector3 direction = target.transform.position - transform.position;
            direction.y = 0;
            float distance = Vector3.Distance(target.transform.position,transform.position);
            if (direction.sqrMagnitude > .0001f) visual.rotation = Quaternion.RotateTowards(visual.rotation,
                Quaternion.LookRotation(direction), dt * (Shield && Shield.IsIntact ? 105 : 540));
            Vector3 steering = Navigate(target.transform.position,dt);
            if (motor.isGrounded && verticalSpeed < 0) verticalSpeed = -2;
            verticalSpeed = Mathf.Max(-25,verticalSpeed-25*dt);
            MoveGrounded(distance > attackRange*.85f ? steering*moveSpeed : Vector3.zero,verticalSpeed,dt);
            if (distance <= attackRange && Time.time >= attackAt && !Physics.Linecast(transform.position+Vector3.up*.8f,target.transform.position+Vector3.up*.8f,1<<9,QueryTriggerInteraction.Ignore))
            {
                meleePending=true;meleeWindup=.42f;Motion.Melee();ArenaAudio.Instance?.Play(SoundCue.ChargeWarn,transform.position,.32f);
            }
        }

        // Reuse the terrain query and neighbor buffer; only retarget five times a second.
        public Vector3 Navigate(Vector3 goal,float dt)
        {
            navigationRemaining-=dt;
            if(navigationRemaining<=0 || (goal-navigationGoal).sqrMagnitude>4)
            {
                navigationRemaining=.2f;navigationGoal=goal;
                cachedSteering=ArenaGame.Instance.terrain?ArenaGame.Instance.terrain.Steer(transform.position,goal):Vector3.ProjectOnPlane(goal-transform.position,Vector3.up).normalized;
            }
            return Separate(cachedSteering);
        }
        public Vector3 Separate(Vector3 desired)
        {
            Vector3 separation=Vector3.zero;
            int count=Physics.OverlapSphereNonAlloc(transform.position+Vector3.up*.6f,1.45f,neighbors,1<<8,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)
            {
                var other=neighbors[i].GetComponent<EnemyController>();
                if(!other || other==this || !other.IsAlive)continue;
                Vector3 away=Vector3.ProjectOnPlane(transform.position-other.transform.position,Vector3.up);
                float distance=away.magnitude;
                if(distance<.01f)away=GetInstanceID()<other.GetInstanceID()?Vector3.right:Vector3.left;
                else away/=distance;
                separation+=away*Mathf.Clamp01((1.4f-distance)/1.0f);
            }
            return Vector3.ClampMagnitude(desired+separation*.9f,1);
        }
        public bool CanStep(Vector3 direction,float distance=.9f)
        {
            if(direction.sqrMagnitude<.001f)return false;
            direction.Normalize();
            if(Physics.SphereCast(transform.position+Vector3.up*.8f,motor.radius*.9f,direction,out _,distance,1<<9,QueryTriggerInteraction.Ignore))return false;
            return ArenaTerrain.TryGround(transform.position+direction*distance,out var ground,1.2f)&&Mathf.Abs(ground.y-transform.position.y)<.7f;
        }
        public void MoveGrounded(Vector3 velocity,float gravity,float dt)
        {
            if(recoveryRemaining>0 && velocity.sqrMagnitude>.25f)
            {
                recoveryRemaining-=dt;
                if(CanStep(recoveryDirection))velocity=recoveryDirection*moveSpeed*.8f;
            }
            Vector3 before=transform.position;
            motor.Move((velocity+Vector3.up*gravity)*dt);
            float travelled=Vector3.ProjectOnPlane(transform.position-before,Vector3.up).magnitude;
            if(velocity.sqrMagnitude>.25f && travelled<velocity.magnitude*dt*.2f)stalledFor+=dt;else stalledFor=0;
            if(stalledFor>.55f)
            {
                var side=Vector3.Cross(Vector3.up,velocity.normalized)*avoidanceSide;
                if(!CanStep(side))side=-side;
                if(CanStep(side)){recoveryDirection=side;recoveryRemaining=.45f;}
                avoidanceSide=-avoidanceSide;stalledFor=0;navigationRemaining=0;
            }
        }

        void OnDamage(float amount)
        {
            CombatFeedback.Instance?.Hit(this,amount);
            // A short local flinch cannot stun-lock a target or interrupt elite telegraphs.
            if(!Health.IsDead && !Elite.IsElite && !(Shield && Shield.IsIntact) && !(Charge && Charge.IsCharger) && Time.time>=nextFlinch)
            {flinchUntil=Time.time+(ArenaGame.Instance.Frontier?.12f:.065f);nextFlinch=Time.time+.45f;}
        }
        void OnDeath()
        {
            if(IsVisibleToPlayer && !Spawning)DefeatPose.Play(Motion);
            if (IsVisibleToPlayer && !CombatVfx027.Instance) ImpactPool.Instance?.Emit(transform.position + Vector3.up * 0.8f, new Color(1, 0.3f, 0.07f), 26);
            ArenaAudio.Instance?.PlayKill();
            gameObject.SetActive(false);
            ArenaGame.Instance?.RegisterKill(transform.position);
        }
        public void RefreshVisibility()
        {
            var terrain = ArenaGame.Instance ? ArenaGame.Instance.terrain : null;
            IsVisibleToPlayer = !terrain || !target || (ArenaGame.Instance.Vision ? ArenaGame.Instance.Vision.CanObserve(VisibilityPoint) : terrain.CanSee(target.transform.position, transform.position));
            if (surfaces != null) foreach (var renderer in surfaces) if (renderer) renderer.forceRenderingOff = !IsVisibleToPlayer || (Spawning && Arrival.HideBody && !renderer.transform.IsChildOf(Arrival.EffectRoot));
        }
    }
}
