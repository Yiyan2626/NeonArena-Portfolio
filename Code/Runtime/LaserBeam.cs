using UnityEngine;

namespace NeonArena
{
    public sealed class LaserBeam : MonoBehaviour
    {
        public Transform emitter;
        public LineRenderer core, glow;
        public float range = 60;
        public LayerMask collisionMask = (1 << 8) | (1 << 9);
        public bool IsActive => core && core.enabled;
        public int LastTargetCount { get; private set; }
        public int LastBounces { get; private set; }
        public float LastDamage { get; private set; }
        readonly Health[] damaged = new Health[32];
        readonly RaycastHit[] buffer = new RaycastHit[64];
        readonly Collider[] overlaps = new Collider[40];
        readonly Vector3[] points = new Vector3[40];
        float nextEffect;

        void Awake() => Stop();
        public void Fire(Vector3 aimPoint, float dt, WeaponLoadout loadout, ShotProfile? profile = null)
        {
            if (Physics.Linecast(transform.root.position + Vector3.up * .85f, emitter.position, 1 << 9, QueryTriggerInteraction.Ignore)) { Stop(); return; }
            Evaluate(emitter.position, (aimPoint - emitter.position).normalized, dt, loadout, profile);
        }
        public void Evaluate(Vector3 origin, Vector3 direction, float dt, WeaponLoadout loadout, ShotProfile? profile = null)
        {
            if (loadout.Laser == 0 || dt <= 0 || direction.sqrMagnitude < 0.01f ||
                (ArenaGame.Instance && !ArenaGame.Instance.IsPlaying)) { Stop(); return; }
            direction.Normalize();
            var shot = profile ?? loadout.Profile;
            int pointCount = 1, extra = shot.ExtraTargets, bounces = shot.Bounces;
            LastTargetCount = 0; LastBounces = 0; LastDamage = 0;
            float remaining = range;
            float radius = loadout.BeamWidth * 0.5f;
            bool effects = Time.time >= nextEffect;
            points[0] = origin;
            for (int step = 0; step < 32 && remaining > 0.01f && pointCount < points.Length - 1; step++)
            {
                Collider overlap = CombatTrace.Overlap(origin, radius, collisionMask, damaged, LastTargetCount, overlaps);
                if (overlap)
                {
                    var shield = overlap.GetComponent<EnemyShield>();
                    if (shield && shield.IsIntact) { ArenaAudio.Instance?.Play(SoundCue.LaserBlock, origin, .65f); points[pointCount++] = origin; break; }
                    var health = overlap.GetComponentInParent<Health>();
                    if (!health) { points[pointCount++] = origin; break; }
                    Damage(health, origin, loadout.BeamDps * dt, effects);
                    if (extra-- <= 0) { points[pointCount++] = origin; break; }
                }
                if (!CombatTrace.Nearest(origin, radius, direction, remaining, collisionMask, damaged, LastTargetCount, buffer, out RaycastHit hit))
                {
                    points[pointCount++] = origin + direction * remaining; break;
                }
                origin += direction * hit.distance;
                remaining -= hit.distance;
                points[pointCount++] = origin;
                var surface = hit.collider.GetComponent<EnemyShield>();
                if (surface && surface.IsIntact) { surface.Pulse(hit.point);ArenaAudio.Instance?.Play(SoundCue.LaserBlock, origin, .65f); break; }
                var target = hit.collider.GetComponentInParent<Health>();
                if (target)
                {
                    Damage(target, hit.point, loadout.BeamDps * dt, effects);
                    if (extra-- <= 0) break;
                    origin += direction * 0.015f; remaining -= 0.015f;
                }
                else
                {
                    if (effects) ImpactPool.Instance?.Emit(hit.point, WeaponLoadout.UpgradeColor(AmmoUpgrade.Laser), 4);
                    if (bounces-- <= 0) break;
                    LastBounces++;
                    direction = Vector3.Reflect(direction, hit.normal).normalized;
                    origin += hit.normal * 0.02f;
                }
            }
            if (effects) nextEffect = Time.time + 0.07f;
            Draw(core, pointCount, loadout.BeamWidth, new Color(1, 0.95f, 0.66f, 1));
            Draw(glow, pointCount, loadout.BeamWidth * 3.2f, new Color(1, 0.42f, 0.03f, 0.35f));
        }
        void Damage(Health health, Vector3 point, float amount, bool effects)
        {
            if (LastTargetCount >= damaged.Length) return;
            damaged[LastTargetCount++] = health;
            float before = health.Current;
            if (health.TakeDamage(amount)) { LastDamage += before - health.Current; ArenaGame.Instance?.RegisterDamageFeedback(); }
            if (effects && CombatTrace.Visible(health)) ImpactPool.Instance?.Emit(point, WeaponLoadout.UpgradeColor(AmmoUpgrade.Laser), 3);
        }
        void Draw(LineRenderer line, int count, float width, Color color)
        {
            if (!line) return;
            line.enabled = true; line.positionCount = count; line.widthMultiplier = width;
            line.startColor = line.endColor = color;
            for (int i = 0; i < count; i++) line.SetPosition(i, points[i]);
        }
        public void Stop()
        {
            if (core) core.enabled = false;
            if (glow) glow.enabled = false;
        }
        void OnDisable() => Stop();
    }
}
