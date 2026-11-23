using System.Collections;
using NeonArena;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class ArenaPlayModeTests
{
    ArenaGame game;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        Time.timeScale = 1;
        yield return SceneManager.LoadSceneAsync("Arena");
        yield return null;
        game = ArenaGame.Instance;
        game.autoSpawn = false;
        game.StartSession();
        game.player.enabled = false; // Deterministic commands below, independent of mouse position.
        yield return null;
    }
    [UnityTearDown]
    public IEnumerator TearDown() { Time.timeScale = 1; yield return null; }

    [Test]
    public void SceneHasRequiredReferencesAndInitialState()
    {
        Assert.That(game.player.viewCamera, Is.Not.Null);
        Assert.That(game.player.weapon.pool, Is.SameAs(game.projectiles));
        Assert.That(game.player.Health.Current, Is.EqualTo(100));
        Assert.That(game.Enemies.Count, Is.EqualTo(32));
        Assert.That(game.projectiles.ActiveCount, Is.Zero);
    }
    [Test]
    public void FreeCameraUsesPerspectiveAndPreservesOrbitSettingsAcrossToggle()
    {
        var orbit = game.player.viewCamera.GetComponent<ArenaCamera>();
        orbit.SetTactical(false);
        Assert.That(game.player.viewCamera.orthographic, Is.False);
        Vector3 before = orbit.transform.position;
        orbit.Orbit(90, 0); orbit.RenderView(true);
        Assert.That(Vector3.Distance(before, orbit.transform.position), Is.GreaterThan(5));
        float yaw = orbit.yaw;
        orbit.SetTactical(true);
        Assert.That(game.player.viewCamera.orthographic, Is.True);
        orbit.SetTactical(false);
        Assert.That(game.player.viewCamera.orthographic, Is.False);
        Assert.That(orbit.yaw, Is.EqualTo(yaw));
    }
    [Test]
    public void CameraZoomAndPitchStayWithinUsableLimits()
    {
        var orbit = game.player.viewCamera.GetComponent<ArenaCamera>();
        orbit.Zoom(1000); Assert.That(orbit.distance, Is.EqualTo(7));
        orbit.Zoom(-1000); Assert.That(orbit.distance, Is.EqualTo(26));
        orbit.Orbit(0, 1000); Assert.That(orbit.pitch, Is.EqualTo(-65));
        orbit.Orbit(0, -1000); Assert.That(orbit.pitch, Is.EqualTo(75));
    }
    [Test]
    public void MovementFollowsCameraAfterNinetyDegreeOrbit()
    {
        var orbit = game.player.viewCamera.GetComponent<ArenaCamera>();
        orbit.yaw = 90; orbit.RenderView(true);
        Assert.That(Vector3.Dot(game.player.GetMoveDirection(Vector2.up), Vector3.right), Is.GreaterThan(0.99f));
        Assert.That(Vector3.Dot(game.player.GetMoveDirection(Vector2.right), Vector3.back), Is.GreaterThan(0.99f));
        Assert.That(game.player.GetMoveDirection(Vector2.one).magnitude, Is.EqualTo(1).Within(0.001f));
    }
    [UnityTest]
    public IEnumerator CameraRetractsBeforeWallAndRecoversAfterObstacleRemoved()
    {
        var orbit = game.player.viewCamera.GetComponent<ArenaCamera>();
        orbit.followCharacter=false; // This regression covers the retained centred camera mode.
        orbit.SetTactical(false);
        Vector3 focus = game.player.transform.position + Vector3.up * 0.8f;
        Vector3 backwards = Quaternion.Euler(orbit.pitch, orbit.yaw, 0) * Vector3.back;
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.layer = 9;
        wall.transform.SetPositionAndRotation(focus + backwards * 5, Quaternion.Euler(orbit.pitch, orbit.yaw, 0));
        wall.transform.localScale = new Vector3(4, 4, 0.5f);
        Physics.SyncTransforms();
        orbit.RenderView(true);
        Assert.That(Vector3.Distance(orbit.transform.position, focus), Is.LessThan(5));
        Object.Destroy(wall);
        yield return null;
        orbit.RenderView(true);
        Assert.That(Vector3.Distance(orbit.transform.position, focus), Is.EqualTo(orbit.distance).Within(0.1f));
    }
    [Test]
    public void FatalDamageClampsAndDeathFiresExactlyOnce()
    {
        int deaths = 0;
        game.player.Health.Died += () => deaths++;
        Assert.That(game.player.Health.TakeDamage(1000), Is.True);
        Assert.That(game.player.Health.TakeDamage(1000), Is.False);
        Assert.That(game.player.Health.Current, Is.Zero);
        Assert.That(deaths, Is.EqualTo(1));
        Assert.That(game.State, Is.EqualTo(ArenaState.Dead));
    }
    [Test]
    public void DamageRejectsInvalidValues()
    {
        foreach (float value in new[] { -1f, 0, float.NaN, float.PositiveInfinity })
            Assert.That(game.player.Health.TakeDamage(value), Is.False);
        Assert.That(game.player.Health.Current, Is.EqualTo(100));
    }
    [UnityTest]
    public IEnumerator ContactInvulnerabilityExpires()
    {
        Assert.That(game.player.Health.TakeDamage(12), Is.True);
        Assert.That(game.player.Health.TakeDamage(12), Is.False);
        yield return new WaitForSeconds(0.7f);
        Assert.That(game.player.Health.TakeDamage(12), Is.True);
        Assert.That(game.player.Health.Current, Is.EqualTo(76));
    }
    [Test]
    public void DiagonalMovementIsNormalized()
    {
        game.player.Teleport(new Vector3(0, 0.08f, 0));
        game.player.Move(Vector2.up, 0.1f);
        float straight = new Vector2(game.player.transform.position.x, game.player.transform.position.z).magnitude;
        game.player.Teleport(new Vector3(0, 0.08f, 0));
        game.player.Move(Vector2.one, 0.1f);
        float diagonal = new Vector2(game.player.transform.position.x, game.player.transform.position.z).magnitude;
        Assert.That(diagonal, Is.EqualTo(straight).Within(0.03f));
        Assert.That(straight, Is.EqualTo(0.7f).Within(0.03f));
    }
    [Test]
    public void WallsContainPlayerOnAllFourSides()
    {
        foreach (Vector2 direction in new[] { Vector2.right, Vector2.left, Vector2.up, Vector2.down })
        {
            game.player.Teleport(new Vector3(0, 0.08f, 0));
            for (int i = 0; i < 400; i++) game.player.Move(direction, 0.05f);
            Assert.That(Mathf.Abs(game.player.transform.position.x), Is.LessThan(ArenaTerrain.HalfWidth-.15f));
            Assert.That(Mathf.Abs(game.player.transform.position.z), Is.LessThan(ArenaTerrain.HalfDepth-.15f));
            Assert.That(game.player.transform.position.y, Is.GreaterThan(-0.1f));
        }
    }
    [Test]
    public void WeaponRateLimitAndPoolCapacityAreEnforced()
    {
        Assert.That(game.player.weapon.TryFire(), Is.True);
        Assert.That(game.player.weapon.TryFire(), Is.False);
        Assert.That(game.player.weapon.ShotsFired, Is.EqualTo(1));
        game.projectiles.Clear();
        for (int i = 0; i < game.projectiles.capacity; i++)
            Assert.That(game.projectiles.TryLaunch(Vector3.up, Vector3.forward, 10, 25), Is.True);
        Assert.That(game.projectiles.TryLaunch(Vector3.up, Vector3.forward, 10, 25), Is.False);
        Assert.That(game.projectiles.ActiveCount, Is.EqualTo(game.projectiles.capacity));
        game.projectiles.Clear();
        Assert.That(game.projectiles.ActiveCount, Is.Zero);
        Assert.That(game.projectiles.TryLaunch(Vector3.up, Vector3.forward, 10, 25), Is.True);
    }
    [UnityTest]
    public IEnumerator SweptProjectileHitsEnemyAtLowFrameRate()
    {
        var enemy = game.SpawnEnemy();
        enemy.enabled = false;
        enemy.Spawn(new Vector3(0, 0.05f, 3), game.player);
        Physics.SyncTransforms();
        game.projectiles.TryLaunch(new Vector3(0, 0.85f, 0), Vector3.forward, 100, 25);
        foreach (var p in game.projectiles.GetComponentsInChildren<Projectile>()) p.Simulate(0.1f);
        Assert.That(enemy.Health.Current, Is.EqualTo(50));
        Assert.That(game.projectiles.ActiveCount, Is.Zero);
        Assert.That(game.Hits, Is.EqualTo(1));
        yield return null;
        Assert.That(enemy.Health.Current, Is.EqualTo(50));
    }
    [UnityTest]
    public IEnumerator WallStopsProjectileBeforeEnemy()
    {
        var enemy = game.SpawnEnemy(); enemy.enabled = false;
        enemy.Spawn(new Vector3(0, 0.05f, ArenaTerrain.HalfDepth+2), game.player);
        Physics.SyncTransforms();
        game.projectiles.TryLaunch(new Vector3(0, 0.85f, ArenaTerrain.HalfDepth-2), Vector3.forward, 100, 25);
        foreach (var p in game.projectiles.GetComponentsInChildren<Projectile>()) p.Simulate(0.1f);
        Assert.That(enemy.Health.Current, Is.EqualTo(75));
        Assert.That(game.projectiles.ActiveCount, Is.Zero);
        yield return null;
    }
    [Test]
    public void PointBlankShotStartingInsideEnemyStillHits()
    {
        var enemy = game.SpawnEnemy(); enemy.enabled = false;
        enemy.Spawn(new Vector3(0, 0.05f, 3), game.player);
        Physics.SyncTransforms();
        game.projectiles.TryLaunch(new Vector3(0, 0.85f, 3), Vector3.forward, 32, 25);
        foreach (var p in game.projectiles.GetComponentsInChildren<Projectile>()) p.Simulate(0.02f);
        Assert.That(enemy.Health.Current, Is.EqualTo(50));
        Assert.That(game.projectiles.ActiveCount, Is.Zero);
    }
    [UnityTest]
    public IEnumerator ProjectilesExpireAndReturnToPool()
    {
        // Vertical flight avoids all walls so this specifically checks lifetime.
        game.projectiles.TryLaunch(new Vector3(0, 3, 0), Vector3.up, 10, 25);
        yield return new WaitForSeconds(3f);
        Assert.That(game.projectiles.ActiveCount, Is.Zero);
    }
    [UnityTest]
    public IEnumerator EnemyPursuesAndDamagesPlayer()
    {
        var enemy = game.SpawnEnemy();
        enemy.Spawn(new Vector3(0, 0.05f, 2), game.player);
        float before = Vector3.Distance(enemy.transform.position, game.player.transform.position);
        yield return new WaitForSeconds(1.2f);
        Assert.That(Vector3.Distance(enemy.transform.position, game.player.transform.position), Is.LessThan(before));
        yield return new WaitForSeconds(1.8f);
        Assert.That(game.player.Health.Current, Is.LessThan(100));
    }
    [Test]
    public void EnemyDeathScoresOnceAndReusedEnemyHasFullHealth()
    {
        var enemy = game.SpawnEnemy();
        enemy.Health.TakeDamage(75);
        enemy.Health.TakeDamage(75);
        Assert.That(game.Kills, Is.EqualTo(1));
        Assert.That(enemy.gameObject.activeSelf, Is.False);
        var reused = game.SpawnEnemy();
        Assert.That(reused, Is.SameAs(enemy));
        Assert.That(reused.Health.Current, Is.EqualTo(75));
    }
    [UnityTest]
    public IEnumerator PauseFreezesTimeAndCanResume()
    {
        game.TogglePause();
        float elapsed = game.Elapsed;
        float scaledTime = Time.time;
        yield return new WaitForSecondsRealtime(0.25f);
        Assert.That(Time.time, Is.EqualTo(scaledTime).Within(0.01f));
        Assert.That(game.Elapsed, Is.EqualTo(elapsed));
        Assert.That(game.State, Is.EqualTo(ArenaState.Paused));
        game.TogglePause();
        yield return new WaitForSeconds(0.1f);
        Assert.That(game.Elapsed, Is.GreaterThan(elapsed));
    }
    [Test]
    public void RestartClearsHealthCountersEnemiesAndBullets()
    {
        game.SpawnEnemy();
        game.player.weapon.TryFire();
        game.player.Health.TakeDamage(100);
        game.StartSession();
        Assert.That(game.State, Is.EqualTo(ArenaState.Playing));
        Assert.That(Time.timeScale, Is.EqualTo(1));
        Assert.That(game.player.Health.Current, Is.EqualTo(100));
        Assert.That(game.ActiveEnemies, Is.Zero);
        Assert.That(game.projectiles.ActiveCount, Is.Zero);
        Assert.That(game.player.weapon.ShotsFired, Is.Zero);
        Assert.That(game.Kills, Is.Zero);
        Assert.That(game.Elapsed, Is.Zero);
    }
}
