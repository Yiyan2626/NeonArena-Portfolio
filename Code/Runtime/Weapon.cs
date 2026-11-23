using UnityEngine;

namespace NeonArena
{
    public sealed class Weapon : MonoBehaviour
    {
        public Transform muzzle;
        public ProjectilePool pool;
        public ParticleSystem muzzleFlash;
        public LaserBeam beam;
        public WeaponVisual visuals;
        public WeaponLoadout Loadout { get; } = new WeaponLoadout();
        public BurstCycle StandardCycle { get; } = new BurstCycle(3, 0.9f);
        public BurstCycle PierceCycle { get; } = new BurstCycle(1.6f, 1.3f);
        public BurstCycle RicochetCycle { get; } = new BurstCycle(2, 1.6f);
        public BurstCycle LaserCycle { get; } = new BurstCycle(1.25f, 2.2f);
        [Min(0.02f)] public float fireInterval = 0.18f;
        [Min(0)] public float damage = 25;
        [Min(1)] public float projectileSpeed = 32;
        public int ShotsFired { get; private set; }
        public Vector3 LastShotDirection { get; private set; }
        public Vector3 LastShotOrigin { get; private set; }
        public bool AimBlocked { get; private set; }
        public bool PierceActive { get; private set; }
        public bool RicochetActive { get; private set; }
        float shotWait;
        int lastTriggerFrame = -1;

        public bool TryFire(Vector3? aimPoint = null)
        {
            if (lastTriggerFrame == Time.frameCount) return false;
            lastTriggerFrame = Time.frameCount;
            return SimulateFire(Time.deltaTime, true, aimPoint ?? muzzle.position + muzzle.forward * 60);
        }
        public bool SimulateFire(float dt, bool trigger, Vector3 aimPoint)
        {
            if (ArenaGame.Instance && (!ArenaGame.Instance.IsPlaying || ArenaGame.Instance.player.Overheat.Active || ArenaGame.Instance.Cinema.FinaleActive)) { StopFiring(); return false; }
            if (dt <= 0 || float.IsNaN(dt) || float.IsInfinity(dt)) return false;
            shotWait = Mathf.Max(0, shotWait - dt);
            if(Loadout.FrontierRules)return FireFrontier(trigger,aimPoint);
            float standardTime = StandardCycle.Advance(trigger, dt);
            float laserTime = LaserCycle.Advance(trigger && Loadout.Laser > 0, dt);
            bool weaponEmitting = trigger && (standardTime > 0 || laserTime > 0);
            PierceActive = PierceCycle.Advance(weaponEmitting && Loadout.Piercing > 0, dt) > 0;
            RicochetActive = RicochetCycle.Advance(weaponEmitting && Loadout.Ricochet > 0, dt) > 0;
            ShotProfile profile = Loadout.CreateProfile(PierceActive, RicochetActive);
            if (beam)
            {
                if (laserTime > 0) beam.Fire(aimPoint, laserTime, Loadout, profile);
                else beam.Stop();
                ArenaAudio.Instance?.SetLaser(laserTime > 0);
            }
            if (!trigger || standardTime <= 0 || shotWait > 0 || !pool) return false;
            // A long muzzle must never fire from the far side of a wall.
            Vector3 shoulder = transform.position + Vector3.up * .85f;
            AimBlocked=Physics.Linecast(shoulder, muzzle.position, 1 << 9, QueryTriggerInteraction.Ignore);
            if (AimBlocked) return false;
            Vector3 direction = (aimPoint - muzzle.position).normalized;
            if (!pool.TryLaunch(muzzle.position, direction, projectileSpeed, damage * Loadout.DamageMultiplier, profile)) return false;
            shotWait = fireInterval + (PierceActive ? 0.025f : 0) + (RicochetActive ? 0.025f : 0);
            ShotsFired++;
            LastShotDirection=direction;LastShotOrigin=muzzle.position;
            if (muzzleFlash) muzzleFlash.Play();
            ArenaAudio.Instance?.PlayShot(PierceActive, RicochetActive);
            if (visuals) visuals.Recoil();
            GetComponent<PlayerController>()?.viewCamera.GetComponent<ArenaCamera>()?.Kick(.018f);
            return true;
        }
        bool FireFrontier(bool trigger,Vector3 aimPoint)
        {
            PierceActive=Loadout.Piercing>0;RicochetActive=Loadout.Ricochet>0;
            if(beam)beam.Stop();
            if(!trigger||shotWait>0||!pool)return false;
            AimBlocked=Physics.Linecast(transform.position+Vector3.up*.85f,muzzle.position,1<<9,QueryTriggerInteraction.Ignore);
            if(AimBlocked)return false;
            Vector3 direction=(aimPoint-muzzle.position).normalized;if(direction.sqrMagnitude<.5f)return false;
            int ammo=Loadout.SelectedAmmo;float hitDamage=ammo==2?82:ammo==0?34:ammo==1?18:25;
            float interval=ammo==2?.60f:ammo==0?.30f:ammo==1?.12f:.18f;
            if(ammo==2)
            {
                Vector3 end=muzzle.position+direction*240;
                if(Physics.Raycast(muzzle.position,direction,out var hit,240,(1<<8)|(1<<9),QueryTriggerInteraction.Ignore))
                {
                    end=hit.point;var shield=hit.collider.GetComponent<EnemyShield>();
                    if(shield&&shield.IsIntact){shield.BreakWithLaser(hit.point);ArenaGame.Instance?.RegisterHit();}
                    else {var health=hit.collider.GetComponentInParent<Health>();if(health){var boss=health.GetComponent<FrontierBoss>();if(health.TakeDamage(boss&&boss.enabled?boss.AmmoDamage(hitDamage,2):hitDamage))ArenaGame.Instance?.RegisterHit();}}
                    if(CombatVfx027.Instance)CombatVfx027.Instance.Impact(end,hit.normal,direction,WeaponLoadout.UpgradeColor(AmmoUpgrade.Laser),2,false);
                    else ImpactPool.Instance?.Emit(end,WeaponLoadout.UpgradeColor(AmmoUpgrade.Laser),14);
                }
                PulseLaser.Draw(muzzle.position,end);
                ArenaAudio.Instance?.Play(SoundCue.LaserStart);
            }
            else if(!pool.TryLaunch(muzzle.position,direction,ammo==0?70:48,hitDamage,Loadout.Profile))return false;
            shotWait=interval;ShotsFired++;LastShotDirection=direction;LastShotOrigin=muzzle.position;
            if(FrontierCombatFx.Instance)FrontierCombatFx.Instance.Shot(muzzle,ammo);else if(muzzleFlash)muzzleFlash.Play();visuals?.Recoil();
            if(ammo!=2)ArenaAudio.Instance?.PlayShot(PierceActive,RicochetActive);
            GetComponent<PlayerController>()?.viewCamera.GetComponent<ArenaCamera>()?.Kick(ammo==2?.035f:.018f);
            return true;
        }
        void LateUpdate()
        {
            var player=GetComponent<PlayerController>();
            if(player){Vector3 aim=player.AimPoint-muzzle.position;AimBlocked=Physics.Linecast(transform.position+Vector3.up*.85f,muzzle.position,1<<9,QueryTriggerInteraction.Ignore)||(aim.magnitude>.1f&&Physics.Raycast(muzzle.position,aim.normalized,Mathf.Max(0,aim.magnitude-.1f),1<<9,QueryTriggerInteraction.Ignore));}
            if (lastTriggerFrame != Time.frameCount) SimulateFire(Time.deltaTime, false, muzzle.position + muzzle.forward);
            if (ArenaGame.Instance && !ArenaGame.Instance.IsPlaying) StopFiring();
        }
        public void Upgrade(AmmoUpgrade type)
        {
            if (!Loadout.CanUpgrade(type)) return;
            Loadout.Upgrade(type);
            if (visuals) visuals.Apply(Loadout);
            var player = GetComponent<PlayerController>();
            if (player)
            {
                if (type == AmmoUpgrade.Vitality) { player.Health.maxHealth += 20; player.Health.Heal(20); }
                if (type == AmmoUpgrade.DashCapacity || type == AmmoUpgrade.DashRecovery)
                    player.Dash.Configure(1 + Loadout.DashCapacity, 2 - 0.15f * Loadout.DashRecovery);
            }
        }
        public void StopFiring() { if (beam) beam.Stop(); ArenaAudio.Instance?.SetLaser(false, false); }
        public void ResetWeapon()
        {
            shotWait = 0; ShotsFired = 0; lastTriggerFrame = -1;
            LastShotDirection=LastShotOrigin=Vector3.zero;AimBlocked=false;
            StandardCycle.Reset(); PierceCycle.Reset(); RicochetCycle.Reset(); LaserCycle.Reset();
            PierceActive = RicochetActive = false;
            Loadout.Reset(); StopFiring();
            if (visuals) visuals.Apply(Loadout);
        }
    }
}
