using System.Collections;
using NeonArena;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class TacticsTests
{
    ArenaGame game;
    [UnitySetUp] public IEnumerator Setup()
    {
        Time.timeScale = 1;
        yield return SceneManager.LoadSceneAsync("Arena"); yield return null;
        game = ArenaGame.Instance; game.autoSpawn = false; game.StartSession(); game.player.enabled = false;
    }
    [UnityTearDown] public IEnumerator Cleanup() { Time.timeScale = 1; yield return null; }
    EnemyController ShieldTarget()
    {
        var e = game.SpawnEnemy(); e.enabled = false;
        e.Spawn(new Vector3(0, 0.05f, 4), game.player, 1, false, true);
        e.visual.rotation = Quaternion.LookRotation(Vector3.back);
        Physics.SyncTransforms(); return e;
    }
    Projectile Shoot(ShotProfile profile, Vector3? origin = null, Vector3? direction = null)
    {
        game.projectiles.TryLaunch(origin ?? new Vector3(0, 0.85f, 0), direction ?? Vector3.forward, 100, 25, profile);
        var p = game.projectiles.GetComponentInChildren<Projectile>(); p.Simulate(0.08f); return p;
    }
    [Test] public void OrdinaryShotsDamageShieldSlowlyWithoutHurtingBody()
    {
        var e = ShieldTarget(); float hp = e.Health.Current;
        Shoot(ShotProfile.Standard);
        Assert.That(e.Shield.Current, Is.EqualTo(85).Within(0.001)); Assert.That(e.Health.Current, Is.EqualTo(hp));
        Assert.That(game.Hits, Is.EqualTo(1));
    }
    [Test] public void PiercingBreaksShieldQuicklyAndContinuesIntoBody()
    {
        var e = ShieldTarget(); float hp = e.Health.Current; var profile = new ShotProfile(2, 0, Color.cyan);
        Shoot(profile); Assert.That(e.Shield.Current, Is.EqualTo(15).Within(0.001)); Assert.That(e.Health.Current, Is.EqualTo(hp));
        Shoot(profile); Assert.That(e.Shield.IsIntact, Is.False); Assert.That(e.Health.Current, Is.EqualTo(hp - 25));
        Assert.That(game.Hits, Is.EqualTo(2));
    }
    [Test] public void RicochetReflectsOffShieldWithoutDamagingIt()
    {
        var e = ShieldTarget(); float hp = e.Health.Current;
        var p = Shoot(new ShotProfile(0, 2, Color.magenta));
        Assert.That(e.Shield.Current, Is.EqualTo(90)); Assert.That(e.Health.Current, Is.EqualTo(hp));
        Assert.That(p.Direction.z, Is.LessThan(-0.99f)); Assert.That(p.BouncesUsed, Is.EqualTo(1));
        p.Simulate(2); Assert.That(p.InFlight, Is.False); Assert.That(p.BouncesUsed, Is.LessThanOrEqualTo(2));
    }
    [Test] public void CombinedProjectileBreaksFirstAndReflectsOnlyIfShieldSurvives()
    {
        var e = ShieldTarget(); float hp = e.Health.Current; var profile = new ShotProfile(2, 2, Color.magenta);
        var p = Shoot(profile);
        Assert.That(e.Shield.Current, Is.EqualTo(15)); Assert.That(p.BouncesUsed, Is.EqualTo(1)); Assert.That(e.Health.Current, Is.EqualTo(hp));
        p.Retire(); p = Shoot(profile);
        Assert.That(e.Shield.IsIntact, Is.False); Assert.That(e.Health.Current, Is.EqualTo(hp - 25)); Assert.That(p.UniqueHits, Is.EqualTo(1));
    }
    [Test] public void LaserWithAllModifiersCannotDamageOrCrossIntactShield()
    {
        var e = ShieldTarget(); float hp = e.Health.Current; var w = game.player.weapon;
        w.Upgrade(AmmoUpgrade.Piercing); w.Upgrade(AmmoUpgrade.Ricochet); w.Upgrade(AmmoUpgrade.Laser);
        w.beam.Evaluate(new Vector3(0, 0.85f, 0), Vector3.forward, 2, w.Loadout);
        Assert.That(e.Health.Current, Is.EqualTo(hp)); Assert.That(e.Shield.Current, Is.EqualTo(90));
        Assert.That(w.beam.LastTargetCount, Is.Zero); Assert.That(w.beam.LastDamage, Is.Zero); Assert.That(w.beam.LastBounces, Is.Zero);
        Assert.That(w.beam.core.GetPosition(w.beam.core.positionCount - 1).z, Is.LessThan(4));
    }
    [Test] public void RearShotsBypassFrontShieldAndShieldResetsOnPoolReuse()
    {
        var e = ShieldTarget(); float hp = e.Health.Current;
        Shoot(ShotProfile.Standard, new Vector3(0, 0.85f, 7), Vector3.back);
        Assert.That(e.Health.Current, Is.EqualTo(hp - 25)); Assert.That(e.Shield.Current, Is.EqualTo(90));
        e.Shield.Hit(100, true, false); Assert.That(e.Shield.IsIntact, Is.False);
        e.Spawn(new Vector3(0, 0.05f, 4), game.player, 2, false, true);
        Assert.That(e.Shield.Current, Is.EqualTo(95.4f).Within(0.01)); Assert.That(e.Shield.surface.enabled, Is.True);
        e.Spawn(Vector3.zero, game.player); Assert.That(e.Shield.IsIntact, Is.False);
    }
    [Test] public void ShotsStartingInsideShieldCannotBypassItsProtection()
    {
        var e = ShieldTarget(); float hp = e.Health.Current;
        Shoot(ShotProfile.Standard, e.Shield.transform.position);
        Assert.That(e.Health.Current, Is.EqualTo(hp)); Assert.That(e.Shield.Current, Is.EqualTo(85));
    }
    [Test] public void BurstDurationAndRecoveryHaveFiniteDutyCycle()
    {
        var cycle = new BurstCycle(1.25f, 2.2f);
        Assert.That(cycle.Advance(true, 2), Is.EqualTo(1.25f)); Assert.That(cycle.CooldownLeft, Is.EqualTo(2.2f));
        Assert.That(cycle.Advance(true, 1), Is.Zero); Assert.That(cycle.Advance(true, 1.2f), Is.Zero);
        Assert.That(cycle.Advance(true, 0.1f), Is.EqualTo(0.1f));
        cycle.Advance(false, 0.01f); Assert.That(cycle.CooldownLeft, Is.EqualTo(2.2f));
        Assert.That(cycle.Advance(true, 0.01f), Is.Zero);
    }
    [Test] public void LaserCoolsIndependentlyWhileStandardShotsContinueAndPauseFreezesAll()
    {
        var w = game.player.weapon; w.Upgrade(AmmoUpgrade.Laser);
        for (int i = 0; i < 15; i++) w.SimulateFire(0.1f, true, new Vector3(0, 0.85f, 8));
        Assert.That(w.LaserCycle.CooldownLeft, Is.GreaterThan(0)); Assert.That(w.beam.IsActive, Is.False);
        Assert.That(w.StandardCycle.CooldownLeft, Is.Zero);
        int shots = w.ShotsFired; w.SimulateFire(0.2f, true, new Vector3(0, 0.85f, 8)); Assert.That(w.ShotsFired, Is.GreaterThan(shots));
        float remaining = w.LaserCycle.CooldownLeft; game.TogglePause(); w.SimulateFire(10, true, Vector3.forward);
        Assert.That(w.LaserCycle.CooldownLeft, Is.EqualTo(remaining));
        game.StartSession(); Assert.That(w.LaserCycle.CooldownLeft, Is.Zero); Assert.That(w.LaserCycle.Remaining, Is.EqualTo(1.25f));
    }
    [Test] public void LifeUpgradeRaisesMaximumAndRestartRestoresBase()
    {
        game.player.Health.TakeDamage(40); game.player.weapon.Upgrade(AmmoUpgrade.Vitality);
        Assert.That(game.player.Health.maxHealth, Is.EqualTo(120)); Assert.That(game.player.Health.Current, Is.EqualTo(80));
        Assert.That(game.player.weapon.Loadout.DamageMultiplier, Is.EqualTo(1));
        game.StartSession(); Assert.That(game.player.Health.maxHealth, Is.EqualTo(100)); Assert.That(game.player.Health.Current, Is.EqualTo(100));
    }
    [Test] public void DashChargesChainAndRefillOneAtATimeWithUpgradeCaps()
    {
        var w = game.player.weapon; var dash = game.player.Dash;
        w.Upgrade(AmmoUpgrade.DashCapacity);
        Assert.That(dash.Charges, Is.EqualTo(2)); Assert.That(dash.TryStart(Vector2.up), Is.True);
        dash.Step(0.2f); Assert.That(dash.TryStart(Vector2.up), Is.True); dash.Step(0.2f);
        Assert.That(dash.Charges, Is.Zero); Assert.That(dash.TryStart(Vector2.up), Is.False);
        dash.Step(1.6f); Assert.That(dash.Charges, Is.EqualTo(1));
        dash.Step(2); Assert.That(dash.Charges, Is.EqualTo(2)); Assert.That(dash.CooldownLeft, Is.Zero);
        for (int i = 0; i < 12; i++) { w.Upgrade(AmmoUpgrade.DashCapacity); w.Upgrade(AmmoUpgrade.DashRecovery); }
        Assert.That(dash.MaxCharges, Is.EqualTo(4)); Assert.That(dash.cooldown, Is.EqualTo(0.8f).Within(0.001));
        Assert.That(w.Loadout.CanUpgrade(AmmoUpgrade.DashCapacity), Is.False); Assert.That(w.Loadout.CanUpgrade(AmmoUpgrade.DashRecovery), Is.False);
        game.StartSession(); Assert.That(dash.Charges, Is.EqualTo(1)); Assert.That(dash.MaxCharges, Is.EqualTo(1)); Assert.That(dash.cooldown, Is.EqualTo(2));
    }
    [Test] public void RecoveryUpgradePreservesFractionAndDoesNotGrantFreeCharge()
    {
        var dash = game.player.Dash; dash.TryStart(Vector2.up); dash.Step(1);
        game.player.weapon.Upgrade(AmmoUpgrade.DashRecovery);
        Assert.That(dash.Charges, Is.Zero); Assert.That(dash.CooldownLeft, Is.EqualTo(0.925f).Within(0.001));
    }
    [Test] public void TerrainCyclesWithoutBlockingSpawnAndBrushBlocksPlayerSightNotBullets()
    {
        for (int wave = 1; wave <= 8; wave++)
        {
            game.terrain.Apply(wave); Assert.That(game.terrain.LayoutIndex, Is.EqualTo(Mathf.Min(wave-1,3)));
            foreach (var point in game.spawnPoints) Assert.That(Physics.CheckSphere(point + Vector3.up * 0.8f, 0.55f, 1 << 9), Is.False);
            Assert.That(Physics.CheckSphere(new Vector3(0, 0.88f, -2), 0.55f, 1 << 9), Is.False);
        }
        game.terrain.Apply(3); game.player.Teleport(new Vector3(0, 0.08f, 1));
        var e = game.SpawnEnemy(); e.enabled = false; e.Spawn(new Vector3(0, 0.05f, 6), game.player); e.RefreshVisibility();
        Assert.That(e.IsVisibleToPlayer, Is.False);
        game.player.viewCamera.GetComponent<ArenaCamera>().Orbit(180, 0); e.RefreshVisibility(); Assert.That(e.IsVisibleToPlayer, Is.False);
        foreach (var renderer in e.GetComponentsInChildren<Renderer>()) Assert.That(renderer.forceRenderingOff, Is.True);
        Physics.SyncTransforms(); Shoot(ShotProfile.Standard, new Vector3(0, 0.85f, 1)); Assert.That(e.Health.Current, Is.EqualTo(50));
        game.player.Teleport(new Vector3(7, 0.08f, 6)); e.RefreshVisibility(); Assert.That(e.IsVisibleToPlayer, Is.True);
        game.terrain.Apply(1); Assert.That(game.terrain.CanSee(new Vector3(0, 0, 1), new Vector3(0, 0, 6)), Is.True);
    }
    [UnityTest] public IEnumerator EnemiesNavigateAroundInteriorCover()
    {
        game.terrain.Apply(2); game.player.Teleport(new Vector3(0, 0.08f, 2.5f));
        var e = game.SpawnEnemy(); e.enabled = false; e.Spawn(new Vector3(-8, 0.08f, 2.5f), game.player);
        yield return new WaitForSeconds(0.7f);
        for (int i = 0; i < 500; i++) e.Tick(0.04f);
        Assert.That(Vector3.Distance(e.transform.position, game.player.transform.position), Is.LessThan(1.6f));
    }
    [Test] public void EnemiesStrengthenGraduallyAndReuseDoesNotMultiplyStats()
    {
        var e = game.SpawnEnemy(); e.Spawn(Vector3.zero, game.player, 2);
        Assert.That(e.Health.maxHealth, Is.EqualTo(82.5f).Within(0.001)); Assert.That(e.damage, Is.EqualTo(12.6f).Within(0.001));
        e.Spawn(Vector3.zero, game.player, 2); Assert.That(e.Health.maxHealth, Is.EqualTo(82.5f).Within(0.001));
    }
    [UnityTest] public IEnumerator SevenRewardChoicesIncludeCharacterUpgrades()
    {
        game.autoSpawn = true; game.StartSession();
        for (int i = 0; i < 20 && game.IsPlaying; i++)
        { game.TickWave(1.1f); foreach (var e in game.Enemies) if (e.IsAlive) e.Health.TakeDamage(10000); }
        yield return new WaitForSecondsRealtime(0.45f);
        int choices = 0; foreach (var b in Object.FindObjectOfType<UpgradeHUD>().GetComponentsInChildren<UnityEngine.UI.Button>()) if (b.name == "Choose") choices++;
        Assert.That(choices, Is.EqualTo(7));
        Assert.That(game.ChooseUpgrade(AmmoUpgrade.DashCapacity), Is.True);
        Assert.That(game.player.Dash.MaxCharges, Is.EqualTo(2)); Assert.That(game.ChooseUpgrade(AmmoUpgrade.Vitality), Is.False);
        game.TickWave(2); Assert.That(game.terrain.LayoutIndex, Is.EqualTo(1)); Assert.That(game.player.Dash.MaxCharges, Is.EqualTo(2));
    }
}
