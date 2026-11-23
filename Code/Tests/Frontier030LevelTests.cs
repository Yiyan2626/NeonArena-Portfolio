using System.Collections;
using System.Linq;
using NeonArena;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class Frontier030LevelTests
{
    ArenaGame game;
    FrontierTactics030 tactics;
    [UnitySetUp] public IEnumerator Setup()
    {
        Time.timeScale = 1;
        yield return SceneManager.LoadSceneAsync("BorderCity");
        yield return null;
        game = ArenaGame.Instance;
        game.StartSession();
        game.enabled = game.Frontier.enabled = game.player.enabled = false;
        game.player.viewCamera.GetComponent<ArenaCamera>().enabled = false;
        tactics = Object.FindObjectOfType<FrontierTactics030>();
        Assert.That(tactics, Is.Not.Null, "Apply FrontierTacticsBuilder030.Apply before running scene tests.");
        tactics.enabled = false;
    }
    void Open()
    {
        game.player.Teleport(FrontierTactics030.ControlStand);
        Assert.That(tactics.TryInteract(), Is.True, tactics.BlockReason());
        tactics.Simulate(FrontierTactics030.Duration + .01f);
        Assert.That(tactics.IsOpen, Is.True);
    }
    bool WindowBlocked()
    {
        var from = FrontierTactics030.FiringStand; from.y = FrontierTactics030.WindowAimHeight;
        var to = FrontierTactics030.NorthTarget; to.y = from.y;
        return Physics.Linecast(from, to, 1 << 9, QueryTriggerInteraction.Ignore);
    }
    EnemyController Spawn(Vector3 point)
    {
        var enemy = game.SpawnAuthored(point, 3, false, false, false, true);
        Assert.That(enemy, Is.Not.Null);
        enemy.enabled = false; enemy.Motion.enabled = false;
        enemy.visual.rotation = Quaternion.Euler(0, 180, 0);
        return enemy;
    }
    void Fire(AmmoUpgrade? ammo, Vector3 aim)
    {
        var weapon = game.player.weapon;
        weapon.ResetWeapon();
        weapon.Loadout.UseFrontierRules();
        if (ammo.HasValue) weapon.Upgrade(ammo.Value);
        game.player.Teleport(FrontierTactics030.FiringStand);
        game.player.AimAt(aim);
        Physics.SyncTransforms();
        Assert.That(weapon.SimulateFire(1, true, aim), Is.True, "Real weapon must launch from the firing bay.");
        foreach (var projectile in game.projectiles.GetComponentsInChildren<Projectile>()) projectile.Simulate(.35f);
    }
    [Test] public void InteractionChecksRangeSightPauseAndSupportsCombat()
    {
        game.player.Teleport(FrontierAnnex029.Entrance);
        Assert.That(tactics.TryInteract(), Is.False, "No remote activation.");
        game.player.Teleport(FrontierTactics030.ControlStand);
        Assert.That(tactics.BlockReason(), Is.Empty);
        // A real physical blocker between the player and the nearby console.
        var blocker = new GameObject("Interaction LOS fixture"); blocker.layer = 9;
        blocker.transform.position = new Vector3(-.7f, 1, -101.9f);
        blocker.AddComponent<BoxCollider>().size = new Vector3(2, 2, .15f);
        Physics.SyncTransforms();
        Assert.That(tactics.TryInteract(), Is.False, "F cannot reach through solid machinery.");
        Object.DestroyImmediate(blocker); Physics.SyncTransforms();
        game.TogglePause();
        Assert.That(tactics.TryInteract(), Is.False);
        game.TogglePause();
        Spawn(FrontierTactics030.NorthTarget);
        Assert.That(game.ActiveEnemies, Is.GreaterThan(0));
        Assert.That(tactics.TryInteract(), Is.True, "Tactical cover controls remain usable during combat.");
        Assert.That(tactics.TryInteract(), Is.False, "Animation cannot be retriggered.");
        tactics.Simulate(.2f);
        var stopped = tactics.shutter.position;
        game.TogglePause(); tactics.Simulate(2);
        Assert.That(tactics.shutter.position, Is.EqualTo(stopped), "Pause must freeze actual collision geometry.");
        game.TogglePause(); tactics.Simulate(1);
        Assert.That(tactics.IsOpen, Is.True);
        Assert.That(WindowBlocked(), Is.False);
        game.EnterFrontierTerminal();
        Assert.That(tactics.TryInteract(), Is.False, "Reward state cannot operate world mechanisms.");
    }
    [Test] public void RealWeaponPiercesClosedShutterButOrdinaryAndLaserNeedOpenSightline()
    {
        var front = Spawn(FrontierTactics030.NorthTarget);
        var rear = Spawn(FrontierTactics030.NorthRearTarget);
        var aim = FrontierTactics030.NorthRearTarget; aim.y = FrontierTactics030.WindowAimHeight;
        float firstHealth = front.Health.Current, rearHealth = rear.Health.Current;
        Assert.That(WindowBlocked(), Is.True);
        Fire(null, aim);
        Assert.That(front.Health.Current, Is.EqualTo(firstHealth));
        Fire(AmmoUpgrade.Laser, aim);
        Assert.That(front.Health.Current, Is.EqualTo(firstHealth));
        Fire(AmmoUpgrade.Piercing, aim);
        Assert.That(front.Health.Current, Is.EqualTo(firstHealth - 34));
        Assert.That(rear.Health.Current, Is.EqualTo(rearHealth - 34));
        game.projectiles.Clear();
        front.Health.ResetHealth(); rear.Health.ResetHealth();
        Open();
        Fire(null, aim);
        Assert.That(front.Health.Current, Is.EqualTo(firstHealth - 25));
        Assert.That(rear.Health.Current, Is.EqualTo(rearHealth), "Ordinary ammunition must still stop on its first target.");
    }
    [Test] public void RestartRestoresTheActualShutterAndBothOldRoutesStayConnected()
    {
        Assert.That(game.Frontier.encounters.Sum(e => e.count), Is.EqualTo(90));
        Assert.That(game.Frontier.encounters[tactics.encounterIndex].count, Is.EqualTo(10));
        Assert.That(game.player.weapon.Loadout.SelectedAmmo, Is.EqualTo(-1));
        Assert.That(game.player.weapon.Loadout.Jump, Is.EqualTo(1));
        Assert.That(game.player.Dash.MaxCharges, Is.EqualTo(1));
        Open();
        foreach (var point in FrontierAnnex029.CoveredRoute)
            Assert.That(game.terrain.HasRoute(FrontierAnnex029.Entrance, point), Is.True);
        // The open aperture is intentionally too low for a standing navigation agent.
        var corners = game.terrain.RouteCorners(FrontierTactics030.FiringStand, FrontierTactics030.NorthTarget);
        Assert.That(corners.Length, Is.GreaterThan(2), "Navigation must route around the permanent machine sill, not into the firing aperture.");
        game.StartSession();
        Assert.That(tactics.Openness, Is.Zero);
        Assert.That(tactics.Interactions, Is.Zero);
        Assert.That(tactics.IsMoving, Is.False);
        Assert.That(tactics.shutter.position, Is.EqualTo(tactics.closedPosition));
        Assert.That(WindowBlocked(), Is.True);
        Assert.That(game.Frontier.Sector, Is.Zero);
        Assert.That(game.player.weapon.Loadout.SelectedAmmo, Is.EqualTo(-1));
    }
    [Test] public void OpenApertureRevealsExposedBodyAndShieldButClosedShutterAndBrushConcealThem()
    {
        var enemy = game.SpawnAuthored(FrontierTactics030.NorthTarget, 3, false, true, false, true);
        Assert.That(enemy, Is.Not.Null);
        enemy.enabled = enemy.Motion.enabled = false;
        enemy.Arrival.Step(EnemySpawnPresentation.Duration + .01f);
        game.player.Teleport(FrontierTactics030.FiringStand);
        Physics.SyncTransforms();
        enemy.RefreshVisibility();
        Assert.That(game.Vision.CanObserve(enemy.VisibilityPoint), Is.False, "Closed shutter must not reveal a target.");
        Assert.That(enemy.IsVisibleToPlayer, Is.False);
        Assert.That(enemy.Shield.plate.forceRenderingOff, Is.True);

        Open();
        game.player.Teleport(FrontierTactics030.FiringStand);
        Physics.SyncTransforms();
        Assert.That(Physics.Linecast(game.player.transform.position + Vector3.up * .85f,
            enemy.VisibilityPoint + Vector3.up * .65f, 1 << 9, QueryTriggerInteraction.Ignore), Is.True,
            "This regression fixture must retain the sill-obstructed lower-body ray.");
        enemy.RefreshVisibility();
        Assert.That(game.Vision.CanObserve(enemy.VisibilityPoint), Is.True, "Actual exposed upper body must be observable.");
        Assert.That(enemy.IsVisibleToPlayer, Is.True);
        Assert.That(enemy.Motion.Model.GetComponentsInChildren<Renderer>().Any(r => r.enabled && !r.forceRenderingOff), Is.True);
        Assert.That(enemy.Shield.plate.forceRenderingOff, Is.False, "Shield must be drawn before the player chooses a laser shot.");
        Assert.That(game.Vision.CanObserve(game.player.transform.position + Vector3.right * 66), Is.False, "Upper-body sampling cannot extend sight radius.");

        var brushObject = new GameObject("030 actual brush occlusion fixture");
        brushObject.transform.position = enemy.transform.position + Vector3.up;
        var volume = brushObject.AddComponent<BoxCollider>(); volume.isTrigger = true; volume.size = new Vector3(2, 3, 2);
        var brush = brushObject.AddComponent<BrushPatch>();
        var originalBrush = game.terrain.brush;
        game.terrain.brush = (originalBrush ?? new BrushPatch[0]).Concat(new[] { brush }).ToArray();
        Physics.SyncTransforms();
        try
        {
            enemy.RefreshVisibility();
            Assert.That(game.Vision.CanObserve(enemy.VisibilityPoint), Is.False, "Exposed torso does not bypass brush concealment.");
            Assert.That(enemy.IsVisibleToPlayer, Is.False);
        }
        finally
        {
            game.terrain.brush = originalBrush;
            Object.DestroyImmediate(brushObject);
        }
        game.player.Teleport(FrontierTactics030.ControlStand);
        Assert.That(tactics.TryInteract(), Is.True, tactics.BlockReason());
        tactics.Simulate(FrontierTactics030.Duration + .01f);
        game.player.Teleport(FrontierTactics030.FiringStand);
        enemy.RefreshVisibility();
        Assert.That(enemy.IsVisibleToPlayer, Is.False, "Closing the shutter must hide the exposed target again.");
        Assert.That(enemy.Shield.plate.forceRenderingOff, Is.True);
    }
}
