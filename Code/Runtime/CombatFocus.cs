using UnityEngine;

namespace NeonArena
{
    [RequireComponent(typeof(PlayerController))]
    public sealed class CombatFocus : MonoBehaviour
    {
        public EnemyController Target { get; private set; }
        public bool Held { get; private set; }
        public bool HasTarget => Held && Target && Target.IsAlive && ArenaGame.Instance && ArenaGame.Instance.IsPlaying;
        public int Acquisitions { get; private set; }
        public Vector3 AimPoint => HasTarget ? Target.transform.position + Vector3.up * .85f : transform.position + transform.forward * 10;
        PlayerController player;
        void Awake() => player = GetComponent<PlayerController>();
        public bool CanTarget(EnemyController enemy)
        {
            if (!enemy || !enemy.IsAlive || (ArenaGame.Instance.Vision && !ArenaGame.Instance.Vision.CanObserve(enemy.transform.position))) return false;
            Vector3 from = transform.position + Vector3.up * .85f, delta = enemy.transform.position + Vector3.up * .85f - from;
            if (delta.sqrMagnitude > 22 * 22 || !ArenaGame.Instance.terrain.CanSee(transform.position, enemy.transform.position)) return false;
            return !Physics.Raycast(from, delta.normalized, delta.magnitude, 1 << 9, QueryTriggerInteraction.Ignore);
        }
        public void SetHeld(bool held, Vector2 screenPoint, bool forceSearch = false)
        {
            // Compatibility entry point for older replays. RMB no longer acquires targets.
            Held = false; Target = null;
        }
        public void ResetFocus() { Held = false; Target = null; Acquisitions = 0; }
        void OnDisable() { Held = false; Target = null; }
    }
}
