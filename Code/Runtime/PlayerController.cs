using UnityEngine;

namespace NeonArena
{
    [RequireComponent(typeof(CharacterController), typeof(Health))]
    public sealed class PlayerController : MonoBehaviour
    {
        [Min(0)] public float moveSpeed = 7;
        public Transform visual;
        public Weapon weapon;
        public Camera viewCamera;
        public Health Health { get; private set; }
        public PlayerDash Dash { get; private set; }
        public CombatFocus Focus { get; private set; }
        public PlayerOverheat Overheat { get; private set; }
        public PlayerFlight Flight { get; private set; }
        public CharacterMotion Motion { get; private set; }
        public float VerticalSpeed { get; private set; }
        public bool Grounded => motor && (motor.isGrounded || (VerticalSpeed <= 0 && Physics.CheckSphere(transform.position + Vector3.up * .12f, .19f, 1 << 9, QueryTriggerInteraction.Ignore)));
        float jumpBuffer, coyote;
        public Vector3 AimPoint { get; private set; }
        public float DistanceTravelled { get; private set; }
        CharacterController motor;
        Vector3 startPosition;
        Quaternion startRotation;
        bool interactiveAim;
        public int LookFrame { get; private set; }=-1;
        float baseMaximumHealth;

        void Awake()
        {
            motor = GetComponent<CharacterController>();
            Health = GetComponent<Health>();
            baseMaximumHealth = Health.maxHealth;
            Dash = GetComponent<PlayerDash>();
            Focus = GetComponent<CombatFocus>();
            if (!Focus) Focus = gameObject.AddComponent<CombatFocus>();
            Overheat = gameObject.AddComponent<PlayerOverheat>(); Overheat.Initialize(this);
            Flight = gameObject.AddComponent<PlayerFlight>();Flight.Initialize(this);
            Motion=gameObject.AddComponent<CharacterMotion>();Motion.Initialize(this,null);
            gameObject.AddComponent<PlayerReadability>();
            if(ArenaGame.Instance&&ArenaGame.Instance.Frontier){gameObject.AddComponent<RicochetGuide>();gameObject.AddComponent<FrontierCombatFx>();}
            Health.Damaged += OnDamage;
            startPosition = transform.position;
            startRotation = transform.rotation;
        }

        void Update()
        {
            if (ArenaGame.Instance == null || !ArenaGame.Instance.IsPlaying || Health.IsDead || ArenaGame.Instance.Cinema.FinaleActive) return;
            var camera=viewCamera.GetComponent<ArenaCamera>();
            ApplyLookInput(new Vector2(Input.GetAxisRaw("Mouse X"),Input.GetAxisRaw("Mouse Y")),camera.IsOrbiting,Time.deltaTime);
            LookFrame=Time.frameCount;
            var input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            Flight.Held=Input.GetKey(KeyCode.Space);
            if (Input.GetKeyDown(KeyCode.Space)) RequestJump();
            if (Input.GetKeyDown(KeyCode.LeftShift)) Dash.TryStart(input);
            Move(input, Time.deltaTime);
            if(camera.UsesFollowControls&&!camera.IsOrbiting)camera.FollowHeading(visual.eulerAngles.y,Time.deltaTime);
            camera.RenderView(false);
            interactiveAim=camera.UsesFollowControls;
            if(interactiveAim&&camera.IsOrbiting)
            {
                // Free look observes independently; firing retains the character's heading.
                AimPoint=transform.position+Vector3.up*.85f+visual.forward*30;
                weapon.visuals?.AimAt(AimPoint);
                if(Input.GetMouseButton(0)){if(Overheat.Active)Overheat.RequestAttack();else weapon.TryFire(AimPoint);}
            }
            else AimAndFire(Input.GetMouseButton(0),interactiveAim?new Vector2(Screen.width*.5f,Screen.height*.5f):(Vector2)Input.mousePosition);
            interactiveAim=false;
        }

        public void ApplyLookInput(Vector2 mouse,bool freeLook,float dt)
        {
            var camera=viewCamera.GetComponent<ArenaCamera>();
            if(!camera.UsesFollowControls||freeLook||dt<=0||!ArenaGame.Instance.IsPlaying||Overheat.Striking)return;
            float keyboard=(Input.GetKey(KeyCode.E)?1:0)-(Input.GetKey(KeyCode.Q)?1:0);
            visual.rotation=Quaternion.Euler(0,visual.eulerAngles.y+mouse.x*camera.mouseSensitivity+keyboard*95*dt,0);
            camera.Orbit(0,mouse.y*camera.mouseSensitivity);
        }
        public void AimAndFire(bool trigger, Vector2 screenPoint)
        {
            if (!ArenaGame.Instance || !ArenaGame.Instance.IsPlaying || Health.IsDead) return;
            if (ArenaGame.Instance.Cinema.FinaleActive) return;
            if (Overheat.Active) { weapon.StopFiring(); if (trigger) Overheat.RequestAttack(); return; }
            var ray = viewCamera.ScreenPointToRay(viewCamera.GetComponent<ArenaCamera>().IsOrbiting ? new Vector3(Screen.width * .5f, Screen.height * .5f) : (Vector3)screenPoint);
            // Foreground cutaway geometry between the camera and the player is not an aim target.
            // Start at torso depth, keeping the same exact screen ray for everything ahead.
            float torsoDepth=Mathf.Max(0,Vector3.Dot(transform.position+Vector3.up*.85f-ray.origin,ray.direction));
            ray.origin=ray.GetPoint(torsoDepth);
            float nearest=100;RaycastHit aimHit=default;bool found=false;
            foreach(var hit in Physics.RaycastAll(ray,100,(1<<8)|(1<<9),QueryTriggerInteraction.Ignore))
            {
                if(hit.distance>=nearest)continue;
                var candidate=hit.collider.GetComponentInParent<EnemyController>();if(candidate&&!candidate.IsVisibleToPlayer)continue;
                nearest=hit.distance;aimHit=hit;found=true;
            }
            if(found)
            {
                AimAt(aimHit.point);
            }
            else AimAt(ray.GetPoint(100));
            if (trigger) weapon.TryFire(AimPoint);
        }

        public void RequestJump() { if (ArenaGame.Instance && ArenaGame.Instance.IsPlaying) jumpBuffer = .12f; }

        public void Move(Vector2 input, float dt)
        {
            if (Health.IsDead || dt <= 0 || (ArenaGame.Instance && (!ArenaGame.Instance.IsPlaying || ArenaGame.Instance.Cinema.FinaleActive)) || Overheat.Striking) return;
            Vector3 before = transform.position;
            bool dashing = Dash && Dash.IsActive;
            Vector3 dashStep = Dash ? Dash.Step(dt) : Vector3.zero;
            bool grounded = Grounded;
            coyote = grounded ? .1f : Mathf.Max(0,coyote-dt);
            if (grounded && VerticalSpeed < 0) VerticalSpeed = -2;
            if (jumpBuffer > 0 && coyote > 0) { VerticalSpeed = weapon.Loadout.FrontierRules?9:weapon.Loadout.Jump>0?11.5f:9; jumpBuffer = coyote = 0; ArenaAudio.Instance?.Play(SoundCue.Jump); }
            jumpBuffer = Mathf.Max(0,jumpBuffer-dt);
            VerticalSpeed = Mathf.Max(-30,VerticalSpeed-25*dt);
            VerticalSpeed = Flight.Step(VerticalSpeed,grounded,dt,transform.position.y);
            var collision = motor.Move((dashing ? dashStep : GetMoveDirection(input)*moveSpeed*dt)+Vector3.up*VerticalSpeed*dt);
            if ((collision & CollisionFlags.Above) != 0 && VerticalSpeed > 0) VerticalSpeed = 0;
            if (!grounded && motor.isGrounded) ArenaAudio.Instance?.Play(SoundCue.Land);
            Vector3 bounded=transform.position;bounded.x=Mathf.Clamp(bounded.x,-ArenaTerrain.WorldHalfWidth+.65f,ArenaTerrain.WorldHalfWidth-.65f);bounded.z=Mathf.Clamp(bounded.z,-ArenaTerrain.WorldHalfDepth+.65f,ArenaTerrain.WorldHalfDepth-.65f);motor.Move(bounded-transform.position);
            if (transform.position.y < -6 || Mathf.Abs(transform.position.x) > ArenaTerrain.WorldHalfWidth+1 || Mathf.Abs(transform.position.z) > ArenaTerrain.WorldHalfDepth+1) Teleport(ArenaGame.Instance.terrain.entry);
            DistanceTravelled += Vector3.Distance(new Vector3(before.x, 0, before.z),
                new Vector3(transform.position.x, 0, transform.position.z));
        }

        public Vector3 GetMoveDirection(Vector2 input)
        {
            Vector2 clamped = Vector2.ClampMagnitude(input, 1);
            // Camera right stays stable even when obstacle avoidance looks straight down.
            // Lock-on changes aiming and framing, never the movement basis.
            Vector3 right = viewCamera ? Vector3.ProjectOnPlane(viewCamera.transform.right, Vector3.up).normalized : Vector3.right;
            Vector3 forward = Vector3.Cross(right, Vector3.up);
            return forward * clamped.y + right * clamped.x;
        }

        public void AimAt(Vector3 point)
        {
            AimPoint = point;
            Vector3 direction = point - transform.position;
            direction.y = 0;
            if (!interactiveAim && direction.sqrMagnitude > 0.01f) visual.rotation = Quaternion.LookRotation(direction);
            weapon.visuals?.AimAt(point);
        }

        public void ResetPlayer()
        {
            motor.enabled = false;
            transform.SetPositionAndRotation(startPosition, startRotation);
            motor.enabled = true;
            visual.localRotation = Quaternion.identity;
            if(viewCamera)viewCamera.GetComponent<ArenaCamera>().yaw=visual.eulerAngles.y;
            Health.maxHealth = baseMaximumHealth;
            Health.ResetHealth();
            DistanceTravelled = 0;
            weapon.ResetWeapon();
            if (Dash) Dash.ResetDash();
            if (Focus) Focus.ResetFocus();
            if (Overheat) Overheat.ResetOverheat();
            if(Flight)Flight.ResetFlight();
            if(Motion)Motion.ResetMotion();
            VerticalSpeed = jumpBuffer = coyote = 0;
        }

        void OnDamage(float amount) => viewCamera.GetComponent<ArenaCamera>()?.Kick(0.12f);
        void OnDestroy() { if (Health) Health.Damaged -= OnDamage; }

        public void Teleport(Vector3 point)
        {
            VerticalSpeed = jumpBuffer = coyote = 0;
            if(Flight)Flight.Stop();
            motor.enabled = false;
            transform.position = point;
            motor.enabled = true;
            // A blink is discontinuous: do not drag a camera anchor from the previous room.
            if(viewCamera)viewCamera.GetComponent<ArenaCamera>()?.RenderView(true);
        }
    }
}
