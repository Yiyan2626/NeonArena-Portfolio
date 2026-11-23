using System.Collections;
using NeonArena;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class Day35Tests
{
    ArenaGame game;
    [UnitySetUp] public IEnumerator Setup()
    {
        Time.timeScale = 1;
        yield return SceneManager.LoadSceneAsync("Arena"); yield return null;
        game = ArenaGame.Instance; game.autoSpawn = false; game.StartSession();
        game.player.enabled = false; game.player.Teleport(new Vector3(0, 0.08f, 0));
    }
    [UnityTearDown] public IEnumerator Cleanup() { Time.timeScale = 1; yield return null; }
    [Test] public void DashUsesCameraDirectionAndEnforcesCooldown()
    {
        var p = game.player; var dash = p.Dash;
        Assert.That(dash, Is.Not.Null);
        var orbit = p.viewCamera.GetComponent<ArenaCamera>(); orbit.yaw = 90; orbit.RenderView(true);
        Assert.That(dash.TryStart(Vector2.up), Is.True); Assert.That(dash.TryStart(Vector2.up), Is.False);
        Vector3 before = p.transform.position;
        p.Move(Vector2.left, 0.5f);
        Assert.That(p.transform.position.x - before.x, Is.EqualTo(dash.speed * dash.duration).Within(0.05));
        Assert.That(Mathf.Abs(p.transform.position.z - before.z), Is.LessThan(0.05));
        Assert.That(dash.IsActive, Is.False); Assert.That(dash.TryStart(Vector2.up), Is.False);
        p.Move(Vector2.zero, 1.6f); Assert.That(dash.TryStart(Vector2.up), Is.True);
    }
    [Test] public void DashInvulnerabilityExpiresAndResetClearsIt()
    {
        var p = game.player;
        p.Dash.TryStart(Vector2.up); Assert.That(p.Health.TakeDamage(12), Is.False);
        p.Move(Vector2.zero, 0.2f); Assert.That(p.Health.TakeDamage(12), Is.True);
        game.StartSession(); Assert.That(p.Dash.CooldownLeft, Is.Zero);
        Assert.That(p.Dash.Uses, Is.Zero); Assert.That(p.Health.DamageBlocked, Is.False);
    }
    [Test] public void DashStopsAtWorldWall()
    {
        var p = game.player; p.Teleport(new Vector3(ArenaTerrain.HalfWidth-1.5f, 0.08f, 30));
        p.visual.rotation = Quaternion.LookRotation(Vector3.right);
        p.Dash.TryStart(Vector2.zero); p.Move(Vector2.zero, 0.3f);
        Assert.That(p.transform.position.x, Is.LessThan(ArenaTerrain.HalfWidth-.15f));
        Assert.That(p.transform.position.x, Is.GreaterThan(ArenaTerrain.HalfWidth-2));
    }
    [Test] public void PauseFreezesDashPositionAndCooldown()
    {
        var p = game.player; p.Dash.TryStart(Vector2.up); p.Move(Vector2.up, 0.05f);
        Vector3 before = p.transform.position; float cooldown = p.Dash.CooldownLeft;
        game.TogglePause(); p.Move(Vector2.up, 2); p.Dash.Step(2);
        Assert.That(p.transform.position, Is.EqualTo(before)); Assert.That(p.Dash.CooldownLeft, Is.EqualTo(cooldown));
        Assert.That(p.Dash.TryStart(Vector2.up), Is.False);
        game.TogglePause(); p.Move(Vector2.zero, 0.2f); Assert.That(p.Dash.IsActive, Is.False);
    }
    EnemyController Charger()
    {
        var e = game.SpawnEnemy(); e.enabled = false;
        e.Spawn(new Vector3(0, 0.08f, -5), game.player, 2, true);
        // Charge behavior starts only after the production arrival presentation.
        e.Arrival.Step(EnemySpawnPresentation.Duration + .01f);
        Physics.SyncTransforms(); e.Charge.Tick(0.31f); return e;
    }
    [Test] public void ChargeTelegraphsLocksDirectionAndHasRecovery()
    {
        var e = Charger(); var c = e.Charge;
        Assert.That(c.Phase, Is.EqualTo(ChargePhase.Telegraph)); Assert.That(c.warning.enabled, Is.True);
        Vector3 before = e.transform.position;
        game.player.Teleport(new Vector3(4, 0.08f, 0));
        c.Tick(c.windup); Assert.That(e.transform.position, Is.EqualTo(before));
        Assert.That(c.Phase, Is.EqualTo(ChargePhase.Charging)); Assert.That(c.warning.enabled, Is.False);
        c.Tick(c.rushDuration);
        Assert.That(Mathf.Abs(e.transform.position.x), Is.LessThan(0.05));
        Assert.That(game.player.Health.Current, Is.EqualTo(100));
        Assert.That(c.Phase, Is.EqualTo(ChargePhase.Recovering));
        Vector3 after = e.transform.position; c.Tick(0.5f); Assert.That(e.transform.position, Is.EqualTo(after));
    }
    [Test] public void ChargeDealsOneHitAndDashCanAvoidIt()
    {
        var e = Charger(); var c = e.Charge;
        c.Tick(c.windup); c.Tick(c.rushDuration);
        Assert.That(game.player.Health.Current, Is.EqualTo(100 - e.damage * 1.5f).Within(0.01));
        float hp = game.player.Health.Current; c.Tick(0.2f); Assert.That(game.player.Health.Current, Is.EqualTo(hp));
        game.player.Health.ResetHealth(); e.Spawn(new Vector3(0, 0.08f, -5), game.player, 2, true);
        e.Arrival.Step(EnemySpawnPresentation.Duration + .01f);
        Physics.SyncTransforms(); c.Tick(0.31f); c.Tick(c.windup);
        game.player.Dash.TryStart(Vector2.right); c.Tick(c.rushDuration);
        Assert.That(game.player.Health.Current, Is.EqualTo(100));
    }
    [Test] public void ReusedChargerLosesAttackAndExtraArmor()
    {
        var e = Charger(); Assert.That(e.Health.maxHealth, Is.GreaterThan(100));
        e.gameObject.SetActive(false); Assert.That(e.Charge.warning.enabled, Is.False);
        e.Spawn(new Vector3(5, 0.08f, 0), game.player, 1, false);
        Assert.That(e.Charge.IsCharger, Is.False); Assert.That(e.Charge.armor.gameObject.activeSelf, Is.False);
        Assert.That(e.Charge.Phase, Is.EqualTo(ChargePhase.Pursuing)); Assert.That(e.Health.maxHealth, Is.EqualTo(75));
    }
    [Test] public void ChargeCannotCrossWallAndPauseFreezesTelegraph()
    {
        var e = Charger(); float remaining = e.Charge.PhaseRemaining;
        game.TogglePause(); e.Charge.Tick(3); Assert.That(e.Charge.PhaseRemaining, Is.EqualTo(remaining)); game.TogglePause();
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.layer = 9;
        wall.transform.position = new Vector3(0, 1, -3); wall.transform.localScale = new Vector3(6, 2, 0.4f);
        Physics.SyncTransforms();
        try
        {
            e.Charge.Tick(e.Charge.windup); e.Charge.Tick(e.Charge.rushDuration);
            Assert.That(e.transform.position.z, Is.LessThan(-3)); Assert.That(game.player.Health.Current, Is.EqualTo(100));
        }
        finally { Object.DestroyImmediate(wall); }
    }
    void ClearWave()
    {
        for (int i = 0; i < 70 && game.IsPlaying; i++)
        {
            game.TickWave(1.1f);
            foreach (var e in game.Enemies) if (e.IsAlive) e.Health.TakeDamage(100000);
        }
        game.TickWave(0);
    }
    [UnityTest] public IEnumerator ThreeWavesWinContinueKeepsLoadoutAndRestartResets()
    {
        game.autoSpawn = true; game.StartSession();
        for (int wave = 0; wave < 3; wave++)
        {
            ClearWave(); Assert.That(game.State, Is.EqualTo(ArenaState.Reward));
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(game.ChooseUpgrade((AmmoUpgrade)wave), Is.True);
            if (wave < 2) game.TickWave(2);
        }
        Assert.That(game.State, Is.EqualTo(ArenaState.Victory));
        Assert.That(game.player.weapon.Loadout.TotalRanks, Is.EqualTo(3));
        Assert.That(game.player.Dash.TryStart(Vector2.up), Is.False);
        Assert.That(game.player.weapon.TryFire(), Is.False);
        game.TickWave(10); Assert.That(game.ActiveEnemies, Is.Zero);
        game.hud.primaryButton.onClick.Invoke();
        Assert.That(game.Endless, Is.True); Assert.That(game.ContinueEndless(), Is.False);
        game.TickWave(2); Assert.That(game.Wave, Is.EqualTo(4)); Assert.That(game.IsPlaying, Is.True);
        Assert.That(game.player.weapon.Loadout.TotalRanks, Is.EqualTo(3));
        game.StartSession(); Assert.That(game.Endless, Is.False); Assert.That(game.Wave, Is.EqualTo(1));
        Assert.That(game.player.weapon.Loadout.TotalRanks, Is.Zero);
    }
    [Test] public void RewardCancelsActiveDashAndBlocksStartingAnother()
    {
        game.autoSpawn = true; game.StartSession(); game.player.Dash.TryStart(Vector2.up); ClearWave();
        Assert.That(game.player.Dash.IsActive, Is.False); Assert.That(game.player.Health.DamageBlocked, Is.False);
        Assert.That(game.player.Dash.TryStart(Vector2.up), Is.False);
    }
}
