using System.Collections;
using NeonArena;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class Polish09Tests
{
    ArenaGame game;
    [UnitySetUp] public IEnumerator Setup()
    {Time.timeScale=1;yield return SceneManager.LoadSceneAsync("Arena");yield return null;game=ArenaGame.Instance;game.autoSpawn=false;game.StartSession();game.player.enabled=false;game.player.Overheat.enabled=false;game.player.Teleport(new Vector3(0,.05f,0));}
    [UnityTearDown] public IEnumerator Cleanup(){Time.timeScale=1;yield return null;}
    EnemyController Enemy(Vector3 p,bool elite=true)
    {var e=game.SpawnEnemy();e.enabled=false;e.Spawn(p,game.player,3,false,false,elite);Physics.SyncTransforms();return e;}
    [Test] public void SavedModelsAreSkinnedAndEliteHasItsOwnMesh()
    {
        var normal=Enemy(new Vector3(4,.05f,5),false);var elite=Enemy(new Vector3(-4,.05f,5));
        Assert.That(game.player.Motion.Model.joints.Length,Is.EqualTo(15));
        Assert.That(normal.Motion.Model.skin.sharedMesh,Is.Not.SameAs(elite.Motion.Model.skin.sharedMesh));
        Assert.That(normal.Motion.Model.imported,Is.True);
        Assert.That(game.player.Motion.Model.skin.sharedMesh.bindposes.Length,Is.GreaterThanOrEqualTo(15));
        Assert.That(elite.Motion.Model.kind,Is.EqualTo(4));
        Assert.That(game.player.visual.Find("Torso").gameObject.activeSelf,Is.False);
    }
    [Test] public void MotionTransitionsThroughRunJumpFlightAndPause()
    {
        var p=game.player;var m=p.Motion;p.Move(Vector2.up,.05f);m.Tick(.05f);Assert.That(m.State,Is.EqualTo(MotionState.Run));
        p.weapon.Upgrade(AmmoUpgrade.Jump);p.weapon.Upgrade(AmmoUpgrade.Jump);p.RequestJump();p.Move(Vector2.zero,.04f);m.Tick(.04f);Assert.That(m.State,Is.EqualTo(MotionState.Jump));
        p.Flight.Held=true;p.Move(Vector2.zero,.04f);m.Tick(.04f);Assert.That(m.State,Is.EqualTo(MotionState.Jet));
        var rotation=m.Model.joints[9].localRotation;game.TogglePause();m.Tick(.3f);Assert.That(m.Model.joints[9].localRotation,Is.EqualTo(rotation));
    }
    [Test] public void EliteWindupCannotDamageAndPauseFreezesIt()
    {
        var e=Enemy(new Vector3(0,.05f,3));Assert.That(e.Elite.BeginAttack(EliteMove.HammerSweep),Is.True);
        float health=game.player.Health.Current;e.Elite.Tick(.4f);Assert.That(game.player.Health.Current,Is.EqualTo(health));
        float remaining=e.Elite.Remaining;game.TogglePause();e.Elite.Tick(5);Assert.That(e.Elite.Remaining,Is.EqualTo(remaining));
        game.TogglePause();e.Elite.Tick(.5f);e.Elite.Tick(.1f);Assert.That(game.player.Health.Current,Is.LessThan(health));
    }
    [Test] public void HammerLocksDirectionAndCanBeDodged()
    {
        var e=Enemy(new Vector3(0,.05f,3));e.Elite.BeginAttack(EliteMove.HammerSweep);var direction=e.Elite.Direction;
        game.player.Teleport(new Vector3(0,.05f,6));e.Elite.Tick(.9f);e.Elite.Tick(.15f);
        Assert.That(e.Elite.Direction,Is.EqualTo(direction));Assert.That(game.player.Health.Current,Is.EqualTo(100));
    }
    [Test] public void ShockRingCanBeJumpedAndHitsGroundedPlayer()
    {
        var e=Enemy(new Vector3(0,.05f,3));e.Elite.BeginAttack(EliteMove.ShockRing);e.Elite.Tick(1.25f);game.player.Teleport(new Vector3(0,1.4f,0));
        for(int i=0;i<10;i++)e.Elite.Tick(.1f);Assert.That(game.player.Health.Current,Is.EqualTo(100));
        game.player.Teleport(new Vector3(0,.05f,0));e.Elite.Initialize(true,game.player);e.Elite.BeginAttack(EliteMove.ShockRing);e.Elite.Tick(1.25f);
        for(int i=0;i<10;i++)e.Elite.Tick(.1f);Assert.That(game.player.Health.Current,Is.LessThan(100));Assert.That(e.Elite.Phase,Is.EqualTo(ElitePhase.Recovery));
    }
    [Test] public void EliteAttacksCannotStartThroughWallAndRamCannotCrossPit()
    {
        var e=Enemy(new Vector3(0,.05f,6));
        var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.layer=9;wall.transform.position=new Vector3(0,1,3);wall.transform.localScale=new Vector3(5,3,.5f);
        Physics.SyncTransforms();Assert.That(e.Elite.BeginAttack(EliteMove.Ram),Is.False);Object.DestroyImmediate(wall);
        game.terrain.Apply(4);e.Spawn(new Vector3(0,.05f,-8),game.player,6,false,false,true);game.player.Teleport(new Vector3(0,.05f,-18));Physics.SyncTransforms();
        Assert.That(e.Elite.BeginAttack(EliteMove.Ram),Is.True);e.Elite.Tick(1.1f);
        for(int i=0;i<15;i++)e.Elite.Tick(.05f);
        Assert.That(e.transform.position.z,Is.GreaterThan(-10.2f));
    }
    [Test] public void OverheatDamagesAndStaggersEliteButStillExecutesNormal()
    {
        var e=Enemy(new Vector3(0,.05f,4));var normal=Enemy(new Vector3(3,.05f,4),false);
        var h=game.player.Overheat;for(int i=0;i<8;i++)game.RegisterKill();h.Tick(.01f);
        Assert.That(h.TryExecute(e),Is.True);h.Tick(.07f);Assert.That(e.IsAlive,Is.True);Assert.That(e.Health.Current,Is.EqualTo(e.Health.maxHealth*.7f).Within(.1f));
        Assert.That(e.Elite.Phase,Is.EqualTo(ElitePhase.Recovery));h.Tick(.25f);Assert.That(h.TryExecute(normal),Is.True);h.Tick(.07f);Assert.That(normal.IsAlive,Is.False);
    }
    [Test] public void ElitePoolReuseClearsAttackModelAndHealth()
    {
        var e=Enemy(new Vector3(0,.05f,4));e.Elite.BeginAttack(EliteMove.HammerSweep);e.Elite.Tick(.9f);
        e.Spawn(new Vector3(0,.05f,4),game.player,1);Assert.That(e.Elite.IsElite,Is.False);Assert.That(e.Elite.Attacks,Is.Zero);Assert.That(e.Motion.Model.kind,Is.EqualTo(1));Assert.That(e.Health.maxHealth,Is.EqualTo(75));
    }
    [Test] public void EliteAutonomousRotationUsesAllThreeMoves()
    {
        var e=Enemy(new Vector3(0,.05f,4));game.player.Health.DamageBlocked=true;
        for(int i=0;i<700;i++)e.Elite.Tick(.05f);
        Assert.That(e.Elite.MoveMask,Is.EqualTo(7));Assert.That(e.Elite.Attacks,Is.GreaterThanOrEqualTo(3));
    }
    [UnityTest] public IEnumerator PlayerSilhouetteAndLandingMarkerWorkWithoutRevealingEnemies()
    {
        game.terrain.Apply(6);game.player.Teleport(new Vector3(-7,.05f,15));yield return null;
        var readable=game.player.GetComponent<PlayerReadability>();Assert.That(readable.Occluded,Is.True);
        var hidden=Enemy(new Vector3(0,.05f,-18),false);hidden.RefreshVisibility();Assert.That(MapGraphic.CanShow(hidden),Is.False);
        game.player.Teleport(new Vector3(0,5,-2));yield return null;
        Assert.That(readable.LandingVisible,Is.True);Assert.That(readable.LandingPoint.y,Is.LessThan(.15f));
    }
    [Test] public void FoundryKeepsRoofRoomPitAndMazeRoutesConnected()
    {
        game.terrain.Apply(6);Assert.That(Object.FindObjectOfType<FoundryDressing>(),Is.Not.Null);
        foreach(var point in new[]{new Vector3(-7,.05f,15),new Vector3(7,3.3f,15),new Vector3(0,-2.35f,-15),new Vector3(21,.05f,-18)})
            Assert.That(game.terrain.HasRoute(point,new Vector3(0,.05f,-2)),Is.True,"route from "+point);
    }
}
