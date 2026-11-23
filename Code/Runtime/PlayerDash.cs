using UnityEngine;

namespace NeonArena
{
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerDash : MonoBehaviour
    {
        public float duration = 0.18f, speed = 24, cooldown = 2;
        public TrailRenderer trail;
        public bool IsActive => remaining > 0;
        public float Progress => 1 - remaining / duration;
        public float CooldownLeft { get; private set; }
        public int Uses { get; private set; }
        public int MaxCharges { get; private set; } = 1;
        public int Charges { get; private set; } = 1;
        public Vector3 Direction { get; private set; }
        float remaining;
        PlayerController player;
        Health health;
        void Awake() { player = GetComponent<PlayerController>(); health = GetComponent<Health>(); }
        public bool TryStart(Vector2 input)
        {
            if (!ArenaGame.Instance || !ArenaGame.Instance.IsPlaying || health.IsDead || Charges <= 0 || IsActive || player.Overheat.Striking || ArenaGame.Instance.Cinema.FinaleActive) return false;
            Direction = input.sqrMagnitude > 0.01f ? player.GetMoveDirection(input).normalized : player.visual.forward;
            Direction = Vector3.ProjectOnPlane(Direction, Vector3.up).normalized;
            remaining = duration; Charges--; if (CooldownLeft <= 0) CooldownLeft = cooldown; Uses++;
            health.DamageBlocked = true;
            if (trail) { trail.Clear(); trail.emitting = true; }
            if(FrontierCombatFx.Instance)FrontierCombatFx.Instance.DashStart(transform.position,Direction);
            else ImpactPool.Instance?.Emit(transform.position + Vector3.up * 0.4f, Color.cyan, 18);
            ArenaAudio.Instance?.PlayDash();
            return true;
        }
        // Called by player movement, so cooldown and displacement share paused game time.
        public Vector3 Step(float dt)
        {
            if (dt <= 0 || !ArenaGame.Instance || !ArenaGame.Instance.IsPlaying) return Vector3.zero;
            if (Charges < MaxCharges)
            {
                CooldownLeft -= dt;
                while (CooldownLeft <= 0 && Charges < MaxCharges)
                { Charges++; if (Charges < MaxCharges) CooldownLeft += cooldown; }
                if (Charges == MaxCharges) CooldownLeft = 0;
            }
            float activeTime = Mathf.Min(dt, remaining);
            remaining = Mathf.Max(0, remaining - dt);
            if (!IsActive) Cancel();
            return Direction * (speed * activeTime);
        }
        public void Cancel()
        {
            remaining = 0;
            if (health) health.DamageBlocked = false;
            if (trail) trail.emitting = false;
        }
        public void ResetDash()
        {
            Cancel(); CooldownLeft = 0; Uses = 0; MaxCharges = Charges = 1; cooldown = 2;
            if (trail) trail.Clear();
        }
        public void Configure(int capacity, float recovery)
        {
            capacity = Mathf.Clamp(capacity, 1, 4); recovery = Mathf.Clamp(recovery, 0.8f, 2);
            Charges = Mathf.Clamp(Charges + capacity - MaxCharges, 0, capacity);
            MaxCharges = capacity;
            CooldownLeft = cooldown > 0 ? CooldownLeft * recovery / cooldown : 0;
            cooldown = recovery;
            if (Charges == MaxCharges) CooldownLeft = 0;
        }
        void OnDisable() => Cancel();
    }
}
