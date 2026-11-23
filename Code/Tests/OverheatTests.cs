using System.Collections;
using NeonArena;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class OverheatTests
{
    ArenaGame game;
    [UnitySetUp] public IEnumerator Setup()
    {
        Time.timeScale=1; yield return SceneManager.LoadSceneAsync("Arena"); yield return null;
        game=ArenaGame.Instance;game.autoSpawn=false;game.StartSession();game.player.enabled=false;
        game.player.Overheat.enabled=false; game.player.Teleport(new Vector3(0,.08f,0));
    }
    [UnityTearDown] public IEnumerator Cleanup(){Time.timeScale=1;yield return null;}
    EnemyController Enemy(Vector3 p,bool shield=false)
    { var e=game.SpawnEnemy();Assert.That(e,Is.Not.Null);e.enabled=false;e.Spawn(p,game.player,1,false,shield);Physics.SyncTransforms();return e; }
    void Charge()
    {for(int i=0;i<PlayerOverheat.RequiredKills;i++)game.RegisterKill();game.player.Overheat.Tick(.01f);}
    [Test] public void PickupChanceCapAndFullHealthRetention()
    {
        var d=game.Drops; Assert.That(d.TryDrop(Vector3.zero,.25f),Is.False);
        Assert.That(d.TryDrop(Vector3.zero,.249f),Is.True);d.Tick(.1f);Assert.That(d.ActiveCount,Is.EqualTo(1));
        game.player.Health.TakeDamage(10);d.Tick(.1f);Assert.That(game.player.Health.Current,Is.EqualTo(100));Assert.That(d.Collected,Is.EqualTo(1));
        Assert.That(d.ActiveCount,Is.Zero);d.Tick(.1f);Assert.That(d.Collected,Is.EqualTo(1));
        for(int i=0;i<30;i++)d.TryDrop(new Vector3(5,0,0),0);
        Assert.That(d.ActiveCount,Is.EqualTo(20));d.Tick(36);Assert.That(d.ActiveCount,Is.Zero);
    }
    [Test] public void PickupsCannotHealThroughBridgeAndResetOnNewRun()
    {
        game.terrain.Apply(3);
        game.player.Teleport(new Vector3(0,.05f,9));game.player.Health.TakeDamage(40);
        Assert.That(game.Drops.TryDrop(new Vector3(0,3.05f,9),0),Is.True);
        game.Drops.Tick(.1f);Assert.That(game.player.Health.Current,Is.EqualTo(60));
        game.player.Teleport(new Vector3(0,3.05f,9));game.Drops.Tick(.1f);Assert.That(game.player.Health.Current,Is.EqualTo(85));
        game.Drops.TryDrop(new Vector3(5,0,0),0);game.StartSession();Assert.That(game.Drops.ActiveCount,Is.Zero);Assert.That(game.Drops.Collected,Is.Zero);
    }
    [Test] public void OverheatThresholdInvulnerabilityDashCancellationAndExpiry()
    {
        Enemy(new Vector3(0,.05f,5));var h=game.player.Overheat;
        for(int i=0;i<7;i++)game.RegisterKill();h.Tick(.01f);Assert.That(h.Active,Is.False);
        game.RegisterKill();h.Tick(.01f);Assert.That(h.Active,Is.True);
        Assert.That(game.player.Health.TakeDamage(1000),Is.False);
        game.player.Dash.TryStart(Vector2.up);game.player.Dash.Cancel();Assert.That(game.player.Health.TakeDamage(1000),Is.False);
        Assert.That(game.player.weapon.SimulateFire(.1f,true,Vector3.forward*5),Is.False);
        game.RegisterKill();Assert.That(h.Charge,Is.Zero);
        game.TogglePause();float remaining=h.Remaining;h.Tick(3);Assert.That(h.Remaining,Is.EqualTo(remaining));game.TogglePause();
        h.Tick(10);Assert.That(h.Active,Is.False);Assert.That(game.player.Health.TakeDamage(10),Is.True);
        Assert.That(game.player.weapon.SimulateFire(.2f,true,Vector3.forward*5),Is.True);
    }
    [Test] public void ChargedMeterWaitsForNextEnemyAndRestartClearsIt()
    {
        Charge();Assert.That(game.player.Overheat.Active,Is.False);Assert.That(game.player.Overheat.Charge,Is.EqualTo(8));
        Enemy(new Vector3(0,.05f,5));game.player.Overheat.Tick(.01f);Assert.That(game.player.Overheat.Active,Is.True);
        game.StartSession();Assert.That(game.player.Overheat.Active,Is.False);Assert.That(game.player.Overheat.Charge,Is.Zero);Assert.That(Time.timeScale,Is.EqualTo(1));
    }
    [Test] public void BladeKillsShieldEnemyOnceAndCyclesThreeStyles()
    {
        var e=Enemy(new Vector3(0,.05f,5),true);Charge();var heat=game.player.Overheat;
        for(int i=0;i<3;i++)
        {
            if(i>0){game.player.Teleport(new Vector3(0,.08f,0));e.Spawn(new Vector3(0,.05f,5),game.player,10,false,true);Physics.SyncTransforms();}
            Assert.That(heat.TryExecute(e),Is.True);Assert.That(heat.Style,Is.EqualTo(i));Assert.That(heat.TryExecute(e),Is.False);
            Assert.That(e.IsAlive,Is.True);heat.Tick(.16f);Assert.That(e.IsAlive,Is.False);
            Assert.That(heat.Executions,Is.EqualTo(i+1));heat.Tick(.6f);Assert.That(heat.Executions,Is.EqualTo(i+1));
        }
        Assert.That(heat.Remaining,Is.LessThan(10));Assert.That(heat.Charge,Is.Zero);
    }
    [Test] public void BlinkRejectsHiddenTargetsAndDoesNotConsumeCooldown()
    {
        var e=Enemy(new Vector3(0,.05f,5));Charge();
        var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.layer=9;wall.transform.position=new Vector3(0,1,2.5f);wall.transform.localScale=new Vector3(4,2,.5f);
        try{Physics.SyncTransforms();Assert.That(game.player.Overheat.TryExecute(e),Is.False);Assert.That(game.player.Overheat.Cooldown,Is.Zero);Assert.That(e.IsAlive,Is.True);}
        finally{Object.DestroyImmediate(wall);}
    }
    [Test] public void FinaleDelaysRewardPausesAndRestoresClock()
    {
        game.autoSpawn=true;game.StartSession();
        for(int i=0;i<5;i++){game.TickWave(1.1f);foreach(var e in game.Enemies)if(e.IsAlive)e.Health.TakeDamage(100000);}
        Assert.That(game.Cinema.FinaleActive,Is.True);Assert.That(game.State,Is.EqualTo(ArenaState.Playing));Assert.That(Time.timeScale,Is.EqualTo(.2f));
        game.TickWave(0);Assert.That(game.State,Is.EqualTo(ArenaState.Playing));
        game.TogglePause();game.TickWave(2);Assert.That(game.Cinema.FinaleActive,Is.True);Assert.That(Time.timeScale,Is.Zero);
        game.TogglePause();Assert.That(Time.timeScale,Is.EqualTo(.2f));game.TickWave(.22f);
        Assert.That(game.State,Is.EqualTo(ArenaState.Reward));Assert.That(Time.timeScale,Is.EqualTo(1));Assert.That(game.Cinema.Finales,Is.EqualTo(1));
        game.StartSession();Assert.That(game.Cinema.Finales,Is.Zero);Assert.That(Time.timeScale,Is.EqualTo(1));
    }
    [Test] public void JumpHasGravityAndCannotBeRepeatedInAir()
    {
        var p=game.player;p.Move(Vector2.zero,.04f);p.RequestJump();p.Move(Vector2.zero,.04f);float speed=p.VerticalSpeed;
        Assert.That(speed,Is.GreaterThan(0));p.RequestJump();p.Move(Vector2.zero,.04f);Assert.That(p.VerticalSpeed,Is.LessThan(speed));
        float peak=0;for(int i=0;i<80;i++){p.Move(Vector2.zero,.02f);peak=Mathf.Max(peak,p.transform.position.y);}
        Assert.That(peak,Is.GreaterThan(1.3f));Assert.That(peak,Is.LessThan(1.9f));Assert.That(p.transform.position.y,Is.LessThan(.15f));
    }
    [Test] public void DistantFinaleFramesImpactAndPreservesUserOrbit()
    {
        var camera=game.player.viewCamera.GetComponent<ArenaCamera>();camera.yaw=35;camera.pitch=45;camera.RenderView(true);
        Vector3 point=new Vector3(5,.05f,-16);game.Cinema.Finish(point);game.Cinema.Tick(.07f);
        camera.RenderView(true);game.Cinema.Render(game.player.viewCamera);
        Vector3 screen=game.player.viewCamera.WorldToViewportPoint(point+Vector3.up*.85f);
        Assert.That(screen.x,Is.EqualTo(.5f).Within(.08f));Assert.That(screen.y,Is.EqualTo(.5f).Within(.08f));
        Assert.That(camera.yaw,Is.EqualTo(35));Assert.That(camera.pitch,Is.EqualTo(45));
        game.StartSession();Assert.That(Time.timeScale,Is.EqualTo(1));Assert.That(game.Cinema.ShotActive,Is.False);
    }
    [UnityTest] public IEnumerator EnemiesReachUpperDeckViaRamp()
    {
        game.terrain.Apply(3);
        game.player.Teleport(new Vector3(-17,3.05f,3));var e=Enemy(new Vector3(-17,.05f,-13));
        Assert.That(game.terrain.HasRoute(e.transform.position,game.player.transform.position),Is.True);
        yield return new WaitForSeconds(.7f);
        for(int i=0;i<650;i++)e.Tick(.04f);
        Assert.That(e.transform.position.y,Is.GreaterThan(2.8f));Assert.That(Vector3.Distance(e.transform.position,game.player.transform.position),Is.LessThan(1.6f));
    }
    [Test] public void EveryLayoutHasConnectedUpperAndLowerSpawnRoutes()
    {
        for(int wave=3;wave<=8;wave++)
        {
            game.terrain.Apply(wave);
            foreach(var spawn in game.spawnPoints)
            {
                Assert.That(game.terrain.HasRoute(spawn,new Vector3(0,.05f,-2)),Is.True,"ground route "+wave+" "+spawn);
                Assert.That(game.terrain.HasRoute(spawn,new Vector3(0,3.05f,9)),Is.True,"bridge route "+wave+" "+spawn);
            }
        }
    }
    [Test] public void JumpHitsBridgeUndersideAndCannotPassThroughIt()
    {
        game.terrain.Apply(3);
        var p=game.player;p.Teleport(new Vector3(0,.05f,9));p.Move(Vector2.zero,.04f);p.RequestJump();
        float peak=0;
        for(int i=0;i<60;i++){p.Move(Vector2.zero,.02f);peak=Mathf.Max(peak,p.transform.position.y);}
        Assert.That(peak,Is.LessThan(1));Assert.That(p.transform.position.y,Is.LessThan(.15f));
    }
    [UnityTest] public IEnumerator UpperEnemyCannotMeleeOrChargeThroughFloor()
    {
        game.terrain.Apply(3);
        game.player.Teleport(new Vector3(0,.05f,9));var e=Enemy(new Vector3(0,3.05f,9));
        yield return new WaitForSeconds(.85f);e.Tick(.1f);Assert.That(game.player.Health.Current,Is.EqualTo(100));
        e.Spawn(new Vector3(0,3.05f,9),game.player,2,true);e.Charge.Tick(1);Assert.That(e.Charge.Phase,Is.EqualTo(ChargePhase.Pursuing));
    }
    [Test] public void LaserCanAimUpAndBridgeStopsVerticalShots()
    {
        game.terrain.Apply(3);
        var e=Enemy(new Vector3(0,2,5));game.player.weapon.Upgrade(AmmoUpgrade.Laser);
        game.player.weapon.beam.Fire(e.transform.position+Vector3.up*.8f,.1f,game.player.weapon.Loadout);
        Assert.That(e.Health.Current,Is.LessThan(e.Health.maxHealth));
        e.Spawn(new Vector3(0,3.05f,9),game.player);Physics.SyncTransforms();
        game.player.weapon.beam.Evaluate(new Vector3(0,.85f,9),Vector3.up,.5f,game.player.weapon.Loadout);
        Assert.That(e.Health.Current,Is.EqualTo(e.Health.maxHealth));
    }
}
