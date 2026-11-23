using UnityEngine;

namespace NeonArena
{
    public enum ChargePhase { Pursuing, Telegraph, Charging, Recovering }

    [RequireComponent(typeof(EnemyController))]
    public sealed class EnemyCharge : MonoBehaviour
    {
        public Transform armor;
        public LineRenderer warning;
        public float windup = 0.85f, rushDuration = 0.55f, rushSpeed = 12, recovery = 1.0f;
        public bool IsCharger { get; private set; }
        public ChargePhase Phase { get; private set; }
        public Vector3 LockedDirection { get; private set; }
        public float PhaseRemaining { get; private set; }
        EnemyController enemy;
        CharacterController motor;
        PlayerController target;
        float nextRush;
        bool struck;
        void Awake() { enemy = GetComponent<EnemyController>(); motor = GetComponent<CharacterController>(); }
        public void Initialize(bool charger, PlayerController player)
        {
            IsCharger = charger; target = player; Phase = ChargePhase.Pursuing;
            PhaseRemaining = 0; nextRush = 0.3f; struck = false;
            if (armor) armor.gameObject.SetActive(charger);
            if (warning) warning.enabled = false;
        }
        // Returns true while this attack owns movement. Direction is locked BEFORE the rush.
        public bool Tick(float dt)
        {
            if (!IsCharger || !target || !enemy.IsAlive || enemy.Spawning || !ArenaGame.Instance || !ArenaGame.Instance.IsPlaying || dt <= 0) return false;
            if (Phase == ChargePhase.Pursuing)
            {
                nextRush -= dt;
                float distance = Vector3.Distance(transform.position, target.transform.position);
                if (nextRush > 0 || distance < 2 || distance > 10 || Mathf.Abs(target.transform.position.y-transform.position.y) > .65f) return false;
                if (!ArenaTerrain.TryGround(transform.position + (target.transform.position-transform.position).normalized * Mathf.Min(distance, rushSpeed*rushDuration),out var landing, .7f) || Mathf.Abs(landing.y-transform.position.y) > .65f) return false;
                LockedDirection = Vector3.ProjectOnPlane(target.transform.position - transform.position, Vector3.up).normalized;
                if (Physics.SphereCast(transform.position + Vector3.up * 0.8f, 0.45f, LockedDirection, out _, distance, 1 << 9, QueryTriggerInteraction.Ignore)) return false;
                Phase = ChargePhase.Telegraph; PhaseRemaining = windup; struck = false;
                ArenaGame.Instance.RegisterCharge();
                ArenaAudio.Instance?.Play(SoundCue.ChargeWarn, transform.position);
                DrawWarning(); return true;
            }
            enemy.visual.rotation = Quaternion.LookRotation(LockedDirection);
            if (Phase == ChargePhase.Telegraph)
            {
                DrawWarning(); PhaseRemaining -= dt;
                if (PhaseRemaining <= 0)
                {
                    Phase = ChargePhase.Charging; PhaseRemaining = rushDuration;
                    ArenaAudio.Instance?.Play(SoundCue.ChargeRush, transform.position);
                    if (warning) warning.enabled = false;
                    if (enemy.IsVisibleToPlayer) ImpactPool.Instance?.Emit(transform.position + Vector3.up * 0.3f, new Color(1, 0.15f, 0.65f), 16);
                }
                return true;
            }
            if (Phase == ChargePhase.Charging)
            {
                float activeTime = Mathf.Min(dt, PhaseRemaining);
                Vector3 before = transform.position;
                motor.Move(LockedDirection * rushSpeed * activeTime + Vector3.down * 8 * activeTime);
                Vector3 segment = transform.position - before; segment.y = 0;
                Vector3 toPlayer = target.transform.position - before; toPlayer.y = 0;
                float along = segment.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector3.Dot(toPlayer, segment) / segment.sqrMagnitude) : 0;
                if (!struck && Mathf.Abs(target.transform.position.y-transform.position.y) < 1 && (toPlayer - segment * along).sqrMagnitude <= 1.05f * 1.05f && !Physics.Linecast(before+segment*along+Vector3.up*.8f,target.transform.position+Vector3.up*.8f,1<<9,QueryTriggerInteraction.Ignore))
                {
                    struck = true;
                    if (target.Health.TakeDamage(enemy.damage * 1.5f)) { ArenaAudio.Instance?.Play(SoundCue.ChargeImpact, transform.position); ArenaAudio.Instance?.PlayHurt(); }
                }
                PhaseRemaining -= dt;
                if (PhaseRemaining <= 0 || segment.magnitude < rushSpeed * activeTime * 0.3f)
                { if (segment.magnitude < rushSpeed * activeTime * .3f) ArenaAudio.Instance?.Play(SoundCue.ChargeImpact, transform.position); Phase = ChargePhase.Recovering; PhaseRemaining = recovery; }
                return true;
            }
            PhaseRemaining -= dt;
            if (PhaseRemaining <= 0) { Phase = ChargePhase.Pursuing; nextRush = 1.5f; }
            return true;
        }
        void DrawWarning()
        {
            if (!warning) return;
            Vector3 from = transform.position + Vector3.up * 0.08f;
            float distance = rushSpeed * rushDuration;
            if (Physics.Raycast(from + Vector3.up * 0.4f, LockedDirection, out var hit, distance, 1 << 9)) distance = hit.distance;
            warning.enabled = enemy.IsVisibleToPlayer; warning.positionCount = 2;
            warning.SetPosition(0, from); warning.SetPosition(1, from + LockedDirection * distance);
            warning.widthMultiplier = 0.48f + 0.12f * Mathf.Sin(Time.time * 22);
        }
        void OnDisable() { Phase=ChargePhase.Pursuing;PhaseRemaining=0;struck=false;if (warning) warning.enabled = false; }
    }
}
