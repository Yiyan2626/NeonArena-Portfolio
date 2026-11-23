using System.Collections;
using NeonArena;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class ProgressionTests
{
    ArenaGame game;
    [UnitySetUp] public IEnumerator Setup()
    {
        Time.timeScale = 1;
        yield return SceneManager.LoadSceneAsync("Arena");
        yield return null;
        game = ArenaGame.Instance; game.autoSpawn = false; game.StartSession();
        game.player.enabled = false;
        // Fixed collision corridor isolates weapon bounce budgets from changing level dimensions.
        for(int side=-1;side<=1;side+=2){var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Test reflection boundary";wall.layer=9;wall.transform.position=new Vector3(0,2,side*20.1f);wall.transform.localScale=new Vector3(10,4,.5f);}Physics.SyncTransforms();
    }
    [UnityTearDown] public IEnumerator Cleanup() { Time.timeScale = 1; yield return null; }
    EnemyController Target(float z)
    {
        var e = game.SpawnEnemy(); e.enabled = false;
        e.Spawn(new Vector3(0, 0.05f, z), game.player);
        Physics.SyncTransforms(); return e;
    }
    Projectile Shot(ShotProfile profile, float dt = 0.1f)
    {
        game.projectiles.TryLaunch(new Vector3(0, 0.85f, 0), Vector3.forward, 100, 10, profile);
        var p = game.projectiles.GetComponentInChildren<Projectile>(); p.Simulate(dt); return p;
    }
    [Test] public void StandardStopsAtFirstEnemyAndPiercingHonorsBudget()
    {
        var a = Target(2); var b = Target(4); var c = Target(6); var d = Target(8);
        Shot(ShotProfile.Standard);
        Assert.That(a.Health.Current, Is.EqualTo(65)); Assert.That(b.Health.Current, Is.EqualTo(75));
        var p = Shot(new ShotProfile(2, 0, Color.cyan));
        Assert.That(a.Health.Current, Is.EqualTo(55)); Assert.That(b.Health.Current, Is.EqualTo(65));
        Assert.That(c.Health.Current, Is.EqualTo(65)); Assert.That(d.Health.Current, Is.EqualTo(75));
        Assert.That(p.UniqueHits, Is.EqualTo(3)); Assert.That(p.InFlight, Is.False);
        Assert.That(game.Hits, Is.EqualTo(2));
    }
    [Test] public void CombinedShotPiercesReflectsAndDoesNotDamageSameEnemyTwice()
    {
        var a = Target(3); var b = Target(-3);
        var p = Shot(new ShotProfile(4, 1, Color.magenta), 0.47f);
        Assert.That(a.Health.Current, Is.EqualTo(65)); Assert.That(b.Health.Current, Is.EqualTo(65));
        Assert.That(p.UniqueHits, Is.EqualTo(2)); Assert.That(p.BouncesUsed, Is.EqualTo(1));
        Assert.That(p.Direction.z, Is.LessThan(-0.99f));
        p.Simulate(0.4f); Assert.That(p.InFlight, Is.False);
    }
    [Test] public void ObstacleReflectsAndPoolReuseResetsShotState()
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.layer = 9;
        wall.transform.position = new Vector3(0, 1, 3); wall.transform.localScale = new Vector3(5, 2, 0.4f);
        Physics.SyncTransforms();
        try
        {
            var p = Shot(new ShotProfile(2, 2, Color.magenta), 0.06f);
            Assert.That(p.BouncesUsed, Is.EqualTo(1)); Assert.That(p.Direction.z, Is.LessThan(0));
            p.Retire();
            p.Launch(Vector3.up, Vector3.forward, 100, 10);
            Assert.That(p.UniqueHits, Is.Zero); Assert.That(p.BouncesUsed, Is.Zero);
            p.Simulate(0.06f); Assert.That(p.InFlight, Is.False); Assert.That(p.BouncesUsed, Is.Zero);
        }
        finally { Object.DestroyImmediate(wall); }
    }
    [Test] public void HighDeltaRicochetHasFiniteBounceBudget()
    {
        var p = Shot(new ShotProfile(12, 8, Color.magenta), .01f);
        p.Retire();p.Launch(Vector3.up,Vector3.forward,200,10,new ShotProfile(12,8,Color.magenta));p.Simulate(2f);
        Assert.That(p.BouncesUsed, Is.EqualTo(8)); Assert.That(p.InFlight, Is.False);
    }
    [Test] public void BeamContinuousDamageIsIndependentOfStepSize()
    {
        var a = Target(3); var loadout = game.player.weapon.Loadout; loadout.Upgrade(AmmoUpgrade.Laser);
        var beam = game.player.weapon.beam;
        beam.Evaluate(new Vector3(0, 0.85f, 0), Vector3.forward, 0.25f, loadout);
        float first = 75 - a.Health.Current; a.Health.ResetHealth();
        for (int i = 0; i < 5; i++) beam.Evaluate(new Vector3(0, 0.85f, 0), Vector3.forward, 0.05f, loadout);
        Assert.That(75 - a.Health.Current, Is.EqualTo(first).Within(0.001));
        Assert.That(first, Is.EqualTo(loadout.BeamDps * 0.25f).Within(0.001));
    }
    [Test] public void BeamInheritsBothModifiersAndPauseStopsDamage()
    {
        var a = Target(3); var b = Target(-3); var w = game.player.weapon;
        w.Upgrade(AmmoUpgrade.Laser); w.Upgrade(AmmoUpgrade.Piercing); w.Upgrade(AmmoUpgrade.Ricochet);
        w.beam.Evaluate(new Vector3(0, 0.85f, 0), Vector3.forward, 0.1f, w.Loadout);
        Assert.That(w.beam.LastTargetCount, Is.EqualTo(2)); Assert.That(w.beam.LastBounces, Is.EqualTo(2));
        Assert.That(a.Health.Current, Is.EqualTo(b.Health.Current).Within(0.001));
        float before = a.Health.Current;
        game.TogglePause(); Assert.That(w.beam.IsActive, Is.False);
        w.beam.Evaluate(new Vector3(0, 0.85f, 0), Vector3.forward, 1, w.Loadout);
        Assert.That(a.Health.Current, Is.EqualTo(before));
    }
    [UnityTest] public IEnumerator ReleasingTriggerDisablesBeam()
    {
        game.player.weapon.Upgrade(AmmoUpgrade.Laser);
        game.player.weapon.TryFire(new Vector3(0, 0.85f, 5));
        Assert.That(game.player.weapon.beam.IsActive, Is.True);
        yield return null; yield return null;
        Assert.That(game.player.weapon.beam.IsActive, Is.False);
    }
    [Test] public void UnmodifiedBeamStopsAtFirstEnemyAndWorldObstacles()
    {
        var a = Target(3); var b = Target(6); var w = game.player.weapon;
        w.Upgrade(AmmoUpgrade.Laser);
        w.beam.Evaluate(new Vector3(0, 0.85f, 0), Vector3.forward, 0.1f, w.Loadout);
        Assert.That(a.Health.Current, Is.LessThan(75)); Assert.That(b.Health.Current, Is.EqualTo(75));
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.layer = 9;
        wall.transform.position = new Vector3(0, 1, 1.5f); wall.transform.localScale = new Vector3(5, 2, 0.4f);
        Physics.SyncTransforms(); float before = a.Health.Current;
        try
        {
            w.beam.Evaluate(new Vector3(0, 0.85f, 0), Vector3.forward, 0.1f, w.Loadout);
            Assert.That(w.beam.LastTargetCount, Is.Zero); Assert.That(w.beam.LastBounces, Is.Zero);
            Assert.That(a.Health.Current, Is.EqualTo(before));
        }
        finally { Object.DestroyImmediate(wall); }
    }
    [UnityTest] public IEnumerator RewardClearsFireAndButtonAppliesSingleUpgrade()
    {
        game.autoSpawn = true; game.StartSession();
        game.player.weapon.Upgrade(AmmoUpgrade.Laser);
        game.player.weapon.TryFire(new Vector3(0, 0.85f, 6));
        ClearWave();
        Assert.That(game.projectiles.ActiveCount, Is.Zero);
        Assert.That(game.player.weapon.beam.IsActive, Is.False);
        Assert.That(game.player.weapon.TryFire(), Is.False);
        yield return new WaitForSecondsRealtime(0.45f);
        var hud = Object.FindObjectOfType<UpgradeHUD>();
        var buttons = hud.GetComponentsInChildren<UnityEngine.UI.Button>();
        UnityEngine.UI.Button choice = null;
        foreach (var button in buttons) if (button.name == "Choose") { choice = button; break; }
        Assert.That(choice, Is.Not.Null); Assert.That(choice.interactable, Is.True);
        choice.onClick.Invoke(); choice.onClick.Invoke();
        Assert.That(game.player.weapon.Loadout.Piercing, Is.EqualTo(1));
        Assert.That(game.player.weapon.Loadout.Laser, Is.EqualTo(1));
        Assert.That(game.State, Is.EqualTo(ArenaState.Intermission));
    }
    void ClearWave()
    {
        for (int i = 0; i < 70 && game.IsPlaying; i++)
        {
            game.TickWave(1.1f);
            foreach (var e in game.Enemies) if (e.IsAlive) e.Health.TakeDamage(100000);
        }
        game.TickWave(0);
        Assert.That(game.State, Is.EqualTo(ArenaState.Reward));
    }
    [UnityTest] public IEnumerator RewardsStackUpgradeAndRejectDuplicateSelection()
    {
        game.autoSpawn = true; game.StartSession();
        ClearWave(); Assert.That(game.ChooseUpgrade(AmmoUpgrade.Piercing), Is.False);
        yield return new WaitForSecondsRealtime(0.4f);
        Assert.That(game.ChooseUpgrade(AmmoUpgrade.Piercing), Is.True);
        Assert.That(game.ChooseUpgrade(AmmoUpgrade.Laser), Is.False);
        Assert.That(game.Wave, Is.EqualTo(2)); game.TickWave(2);
        ClearWave(); yield return new WaitForSecondsRealtime(0.4f);
        Assert.That(game.ChooseUpgrade(AmmoUpgrade.Ricochet), Is.True);
        Assert.That(game.player.weapon.Loadout.Piercing, Is.EqualTo(1));
        Assert.That(game.player.weapon.Loadout.Ricochet, Is.EqualTo(1)); game.TickWave(2);
        ClearWave(); yield return new WaitForSecondsRealtime(0.4f);
        game.ChooseUpgrade(AmmoUpgrade.Piercing);
        Assert.That(game.player.weapon.Loadout.Piercing, Is.EqualTo(2));
        Assert.That(game.player.weapon.Loadout.Ricochet, Is.EqualTo(1));
        game.StartSession(); Assert.That(game.player.weapon.Loadout.TotalRanks, Is.Zero);
        Assert.That(game.Wave, Is.EqualTo(1)); Assert.That(game.ClearedWaves, Is.Zero);
    }
    [Test] public void PendingEnemiesPreventPrematureReward()
    {
        game.autoSpawn = true; game.StartSession(); game.TickWave(0.1f);
        Assert.That(game.PendingSpawns, Is.EqualTo(4));
        foreach (var e in game.Enemies) if (e.IsAlive) e.Health.TakeDamage(1000);
        game.TickWave(0); Assert.That(game.State, Is.EqualTo(ArenaState.Playing));
        Assert.That(game.ClearedWaves, Is.Zero);
    }
    [Test] public void ReusedEnemiesScaleFromBaseAndResetForNewRun()
    {
        var e = Target(3); float speed = e.moveSpeed;
        e.Spawn(Vector3.zero, game.player, 5); float health = e.Health.maxHealth;
        Assert.That(health, Is.GreaterThan(75)); Assert.That(e.moveSpeed, Is.GreaterThan(speed));
        e.Spawn(Vector3.zero, game.player, 5); Assert.That(e.Health.maxHealth, Is.EqualTo(health));
        e.Spawn(Vector3.zero, game.player, 1); Assert.That(e.Health.maxHealth, Is.EqualTo(75));
        Assert.That(e.moveSpeed, Is.EqualTo(speed));
    }
    [Test] public void InstalledModulesAreDistinctAndUpgradeEffectsIncrease()
    {
        var w = game.player.weapon; var v = w.visuals;
        Assert.That(v.piercingModule.gameObject.activeSelf, Is.False);
        w.Upgrade(AmmoUpgrade.Piercing); Assert.That(v.piercingModule.gameObject.activeSelf, Is.True);
        Assert.That(v.ricochetModule.gameObject.activeSelf, Is.False);
        w.Upgrade(AmmoUpgrade.Ricochet); w.Upgrade(AmmoUpgrade.Laser);
        Assert.That(v.ricochetModule.gameObject.activeSelf && v.laserModule.gameObject.activeSelf, Is.True);
        float damage = w.Loadout.BeamDps, width = w.Loadout.BeamWidth;
        w.Upgrade(AmmoUpgrade.Laser); Assert.That(w.Loadout.BeamDps, Is.GreaterThan(damage));
        Assert.That(w.Loadout.BeamWidth, Is.GreaterThan(width));
        w.ResetWeapon(); Assert.That(v.laserModule.gameObject.activeSelf, Is.False);
    }
}
