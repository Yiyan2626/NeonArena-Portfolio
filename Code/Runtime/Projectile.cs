using UnityEngine;

namespace NeonArena
{
    public sealed class Projectile : MonoBehaviour
    {
        public LayerMask collisionMask;
        public float radius = 0.09f;
        public float lifetime = 2.8f;
        public TrailRenderer trail;
        public bool InFlight { get; private set; }
        public int BouncesUsed { get; private set; }
        public int UniqueHits => damagedCount;
        public Vector3 Direction => direction;
        Vector3 direction, baseScale;
        float speed, damage, age;
        int penetrations, bounces, damagedCount, visualAmmo;
        bool piercingShot, ricochetShot, countedHit;
        bool throughWalls;
        Color color;
        Renderer body;
        MaterialPropertyBlock block;
        readonly Collider[] overlaps = new Collider[40];
        readonly RaycastHit[] hits = new RaycastHit[64];
        readonly Health[] damaged = new Health[32];

        void Awake()
        {
            baseScale = transform.localScale;
            body = GetComponent<Renderer>();
            block = new MaterialPropertyBlock();
        }
        public void Launch(Vector3 position, Vector3 heading, float velocity, float hitDamage, ShotProfile? settings = null)
        {
            var profile = settings ?? ShotProfile.Standard;
            direction = heading.normalized;
            speed = velocity; damage = hitDamage; age = 0; damagedCount = 0;
            penetrations = profile.ExtraTargets; bounces = profile.Bounces; BouncesUsed = 0;
            piercingShot = penetrations > 0; ricochetShot = bounces > 0; countedHit = false;
            throughWalls=profile.ThroughWalls;
            visualAmmo=piercingShot?0:ricochetShot?1:-1;
            color = visualAmmo<0?new Color(1,.66f,.27f):profile.Color;
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction));
            transform.localScale = Vector3.Scale(baseScale * profile.Size, new Vector3(piercingShot?.38f:.27f,piercingShot?.38f:.27f,piercingShot?1.6f:ricochetShot?.75f:.60f));
            InFlight = true;
            gameObject.SetActive(true);
            block.Clear(); block.SetColor("_Color", color); block.SetColor("_EmissionColor", color * 1.8f);
            if (body) body.SetPropertyBlock(block);
            if (trail)
            {
                trail.Clear(); trail.SetPropertyBlock(block);
                trail.startWidth = (piercingShot?.065f:ricochetShot?.055f:.045f) * profile.Size;
                trail.startColor=color;trail.endColor=new Color(color.r,color.g,color.b,0);
                trail.time = bounces > 0 ? 0.2f : penetrations > 0 ? 0.14f : 0.045f;
            }
        }
        void Update()
        {
            if (ArenaGame.Instance != null && !ArenaGame.Instance.IsPlaying) return;
            Simulate(Time.deltaTime);
        }
        public void Simulate(float dt)
        {
            if (!InFlight || dt <= 0) return;
            Vector3 visualStart=transform.position;
            float remaining = speed * Mathf.Min(dt, Mathf.Max(0, lifetime - age));
            LayerMask mask=throughWalls ? collisionMask.value & ~(1<<9) : collisionMask.value;
            age += dt;
            // Retain the unused travel distance after each impact, even at a low frame rate.
            for (int step = 0; step < 32 && InFlight && remaining > 0.0001f; step++)
            {
                var overlap = CombatTrace.Overlap(transform.position, radius, mask, damaged, damagedCount, overlaps);
                if (overlap)
                {
                    var shield = overlap.GetComponent<EnemyShield>();
                    if (shield && shield.IsIntact)
                    {
                        Vector3 normal = Vector3.Dot(direction, shield.transform.forward) < 0 ? shield.transform.forward : -shield.transform.forward;
                        HitShield(shield, normal, true);
                        continue;
                    }
                    var health = overlap.GetComponentInParent<Health>();
                    if (!health) { Retire(); break; }
                    HitEnemy(health, transform.position);
                    if (!InFlight) break;
                }
                if (!CombatTrace.Nearest(transform.position, radius, direction, remaining, mask, damaged, damagedCount, hits, out RaycastHit hit))
                {
                    transform.position += direction * remaining; remaining = 0; break;
                }
                transform.position += direction * hit.distance;
                remaining -= hit.distance;
                var shieldSurface = hit.collider.GetComponent<EnemyShield>();
                if (shieldSurface && shieldSurface.IsIntact)
                {
                    HitShield(shieldSurface, hit.normal, false);
                    continue;
                }
                var target = hit.collider.GetComponentInParent<Health>();
                if (target)
                {
                    HitEnemy(target, hit.point);
                    float nudge = Mathf.Min(0.015f, remaining);
                    transform.position += direction * nudge; remaining -= nudge;
                }
                else
                {
                    if(CombatVfx027.Instance)CombatVfx027.Instance.Impact(hit.point,hit.normal,direction,color,visualAmmo,bounces>0);
                    else ImpactPool.Instance?.Emit(hit.point, color, 6);
                    ArenaAudio.Instance?.Play(bounces > 0 ? SoundCue.WallBounce : SoundCue.WallHit, hit.point, .65f);
                    if (bounces-- <= 0) { Retire(); break; }
                    BouncesUsed++;
                    direction = Vector3.Reflect(direction, hit.normal).normalized;
                    transform.position += hit.normal * 0.02f;
                    transform.rotation = Quaternion.LookRotation(direction);
                }
            }
            if(InFlight)CombatVfx027.Instance?.Travel(visualStart,transform.position,direction,color,visualAmmo);
            if (age >= lifetime) Retire();
        }
        void HitEnemy(Health health, Vector3 point)
        {
            if (damagedCount >= damaged.Length) { Retire(); return; }
            damaged[damagedCount++] = health;
            var boss=health.GetComponent<FrontierBoss>();float applied=boss&&boss.enabled?boss.AmmoDamage(damage,piercingShot?0:ricochetShot?1:-1):damage;
            if (health.TakeDamage(applied)) { CountHit(); ArenaAudio.Instance?.Play(SoundCue.FleshHit, point, .75f); }
            if (CombatTrace.Visible(health))
            {
                if(CombatVfx027.Instance)CombatVfx027.Instance.Impact(point,-direction,direction,color,visualAmmo,false);
                else ImpactPool.Instance?.Emit(point, color, penetrations > 0 ? 14 : 8);
            }
            if (penetrations-- <= 0) Retire();
        }
        void CountHit() { if (!countedHit) { countedHit = true; ArenaGame.Instance?.RegisterHit(); } }
        void HitShield(EnemyShield shield, Vector3 normal, bool inside)
        {
            CountHit();
            bool broken = shield.Hit(damage, piercingShot, ricochetShot,transform.position);
            if (broken && piercingShot)
            { transform.position += direction * 0.02f; return; }
            if (!broken && ricochetShot && bounces > 0)
            {
                CombatVfx027.Instance?.Impact(transform.position,normal,direction,color,visualAmmo,true);
                bounces--; BouncesUsed++;
                direction = Vector3.Reflect(direction, normal).normalized;
                transform.position = inside ? shield.Outside(normal, radius) : transform.position + normal * 0.025f;
                transform.rotation = Quaternion.LookRotation(direction);
                return;
            }
            Retire();
        }
        public void Retire()
        {
            InFlight = false;
            if (trail) trail.Clear();
            gameObject.SetActive(false);
        }
    }
}
