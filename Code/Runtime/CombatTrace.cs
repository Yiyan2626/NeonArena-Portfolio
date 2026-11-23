using UnityEngine;

namespace NeonArena
{
    // NonAlloc results are unsorted. Select the nearest legal collider explicitly.
    public static class CombatTrace
    {
        public static bool Visible(Health health)
        { var enemy = health.GetComponent<EnemyController>(); return !enemy || enemy.IsVisibleToPlayer; }
        public static bool WasHit(Health target, Health[] damaged, int count)
        {
            for (int i = 0; i < count; i++) if (damaged[i] == target) return true;
            return false;
        }
        public static bool Nearest(Vector3 origin, float radius, Vector3 direction, float distance,
            LayerMask mask, Health[] damaged, int damagedCount, RaycastHit[] buffer, out RaycastHit nearest)
        {
            int count = Physics.SphereCastNonAlloc(origin, radius, direction, buffer, distance, mask, QueryTriggerInteraction.Ignore);
            nearest = default;
            float best = float.PositiveInfinity;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                var candidate = buffer[i];
                var health = candidate.collider.GetComponentInParent<Health>();
                if (health && (health.IsDead || WasHit(health, damaged, damagedCount))) continue;
                if (candidate.distance > best + 0.001f) continue;
                if (found && Mathf.Abs(candidate.distance - best) < 0.001f && candidate.collider.gameObject.layer != 9 && !candidate.collider.GetComponent<EnemyShield>()) continue;
                nearest = candidate; best = candidate.distance; found = true;
            }
            return found;
        }
        public static Collider Overlap(Vector3 point, float radius, LayerMask mask, Health[] damaged, int damagedCount, Collider[] buffer)
        {
            int count = Physics.OverlapSphereNonAlloc(point, radius, buffer, mask, QueryTriggerInteraction.Ignore);
            Collider selected = null;
            for (int i = 0; i < count; i++)
            {
                var collider = buffer[i];
                if (collider.gameObject.layer == 9) return collider;
                var health = collider.GetComponentInParent<Health>();
                if (health && (health.IsDead || WasHit(health, damaged, damagedCount))) continue;
                var shield = collider.GetComponent<EnemyShield>();
                if (shield && shield.IsIntact) { selected = collider; continue; }
                if (selected && selected.GetComponent<EnemyShield>()) continue;
                if (health && !health.IsDead && !WasHit(health, damaged, damagedCount)) selected = collider;
            }
            return selected;
        }
    }
}
