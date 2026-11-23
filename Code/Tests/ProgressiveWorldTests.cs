using System.Collections;
using NeonArena;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class ProgressiveWorldTests
{
    ArenaGame game;
    [UnitySetUp] public IEnumerator Setup()
    {Time.timeScale=1;yield return SceneManager.LoadSceneAsync("Arena");yield return null;game=ArenaGame.Instance;game.autoSpawn=false;game.StartSession();game.player.enabled=false;game.player.Overheat.enabled=false;game.player.Teleport(new Vector3(0,.05f,0));}
    [UnityTearDown] public IEnumerator Cleanup(){Time.timeScale=1;yield return null;}
    EnemyController Enemy(Vector3 p,bool fast=false,bool shield=false)
    {var e=game.SpawnEnemy();Assert.That(e,Is.Not.Null);e.enabled=false;e.Spawn(p,game.player,1,fast,shield);Physics.SyncTransforms();return e;}
    [Test] public void ComplexityStartsSimpleAndAddsRealPitRoomsAndMaze()
    {
        Assert.That(game.terrain.Complexity,Is.EqualTo(1));
        Assert.That(ArenaTerrain.TryGround(new Vector3(-17,3.1f,5),out var ground,5),Is.True);Assert.That(ground.y,Is.LessThan(.1f));
        game.terrain.Apply(3);Assert.That(ArenaTerrain.TryGround(new Vector3(-17,3.1f,5),out ground,5),Is.True);Assert.That(ground.y,Is.GreaterThan(2.9f));
        game.terrain.Apply(4);Assert.That(ArenaTerrain.TryGround(new Vector3(0,.1f,-15),out ground,5),Is.True);Assert.That(ground.y,Is.LessThan(-2));
        Assert.That(game.terrain.HasRoute(ground,new Vector3(0,.05f,-2)),Is.True,"pit exit");
        game.terrain.Apply(5);
        Assert.That(game.terrain.HasRoute(new Vector3(-7,.05f,15),new Vector3(0,.05f,-2)),Is.True,"room exit");
        Assert.That(game.terrain.HasRoute(new Vector3(-7,3.3f,15),new Vector3(0,.05f,-2)),Is.True,"roof route");
        game.terrain.Apply(8);Assert.That(game.terrain.HasRoute(new Vector3(21,.05f,-18),new Vector3(0,.05f,-2)),Is.True,"maze route");
        game.terrain.Apply(12);Assert.That(game.terrain.Complexity,Is.EqualTo(8));
        game.StartSession();Assert.That(game.terrain.Complexity,Is.EqualTo(1));
    }
    [Test] public void JumpRewardHasTwoRanksAndDoesNotIncreaseWeaponDamage()
    {
        var w=game.player.weapon;float damage=w.Loadout.DamageMultiplier;
        w.Upgrade(AmmoUpgrade.Jump);Assert.That(w.Loadout.Jump,Is.EqualTo(1));
        game.player.Move(Vector2.zero,.03f);game.player.RequestJump();float peak=0;
        for(int i=0;i<70;i++){game.player.Move(Vector2.zero,.02f);peak=Mathf.Max(peak,game.player.transform.position.y);}
        Assert.That(peak,Is.GreaterThan(2.35f));Assert.That(peak,Is.LessThan(2.85f));
        w.Upgrade(AmmoUpgrade.Jump);w.Upgrade(AmmoUpgrade.Jump);Assert.That(w.Loadout.Jump,Is.EqualTo(2));Assert.That(w.Loadout.CanUpgrade(AmmoUpgrade.Jump),Is.False);
        Assert.That(w.Loadout.DamageMultiplier,Is.EqualTo(damage));game.StartSession();Assert.That(w.Loadout.Jump,Is.Zero);
    }
    [Test] public void JetpackConsumesFiniteFuelFallsAndOnlyRechargesOnGround()
    {
        var p=game.player;p.weapon.Upgrade(AmmoUpgrade.Jump);p.weapon.Upgrade(AmmoUpgrade.Jump);p.Flight.Held=true;p.RequestJump();
        float peak=0;for(int i=0;i<155;i++){p.Move(Vector2.zero,.02f);peak=Mathf.Max(peak,p.transform.position.y);}
        Assert.That(peak,Is.GreaterThan(6));Assert.That(peak,Is.LessThan(9.8f));Assert.That(p.Flight.Fuel,Is.Zero);Assert.That(p.VerticalSpeed,Is.LessThan(0));
        p.Flight.Held=false;float fuel=p.Flight.Fuel;p.Move(Vector2.zero,.03f);Assert.That(p.Flight.Fuel,Is.EqualTo(fuel));
        for(int i=0;i<300;i++)p.Move(Vector2.zero,.02f);
        Assert.That(p.transform.position.y,Is.LessThan(.15f));Assert.That(p.Flight.Fuel,Is.EqualTo(PlayerFlight.Capacity));
    }
    [Test] public void JetpackPauseFreezesFuelAndRoofBlocksAscent()
    {
        game.terrain.Apply(5);var p=game.player;p.weapon.Upgrade(AmmoUpgrade.Jump);p.weapon.Upgrade(AmmoUpgrade.Jump);
        p.Teleport(new Vector3(-7,.05f,15));p.Flight.Held=true;p.RequestJump();
        float peak=0;for(int i=0;i<90;i++){p.Move(Vector2.zero,.02f);peak=Mathf.Max(peak,p.transform.position.y);}
        Assert.That(peak,Is.LessThan(1.4f));game.TogglePause();float fuel=p.Flight.Fuel;p.Move(Vector2.zero,5);Assert.That(p.Flight.Fuel,Is.EqualTo(fuel));
        game.StartSession();Assert.That(p.Flight.Fuel,Is.EqualTo(PlayerFlight.Capacity));Assert.That(p.Flight.Held,Is.False);
    }
    [Test] public void BrushDarknessVisibilityAndMapDoNotRevealHiddenEnemies()
    {
        game.terrain.Apply(2);game.player.Teleport(new Vector3(-8,.05f,6));
        Assert.That(game.Vision.PlayerInBrush,Is.True);Assert.That(game.Vision.CurrentRadius,Is.EqualTo(7));
        var near=Enemy(new Vector3(-7,.05f,6));var far=Enemy(new Vector3(-8,.05f,15));
        near.RefreshVisibility();far.RefreshVisibility();Assert.That(MapGraphic.CanShow(near),Is.True);Assert.That(MapGraphic.CanShow(far),Is.False);
        Assert.That(game.player.Focus.CanTarget(far),Is.False);
        game.player.Teleport(new Vector3(-8,.05f,11));far.RefreshVisibility();Assert.That(MapGraphic.CanShow(far),Is.True);
        game.player.viewCamera.GetComponent<ArenaCamera>().Orbit(180,0);Assert.That(MapGraphic.CanShow(near),Is.False,"rotating camera cannot reveal brush");
    }
    [Test] public void MinimapColorsFloorsAndShieldIdentitySurviveShieldBreak()
    {
        var basic=Enemy(new Vector3(0,.05f,3));var fast=Enemy(new Vector3(3,.05f,3),true);var shield=Enemy(new Vector3(-3,.05f,3),false,true);
        Assert.That(MapGraphic.EnemyColor(basic),Is.Not.EqualTo(MapGraphic.EnemyColor(fast)));
        shield.Shield.Hit(10000,true,false);Assert.That(shield.Shield.IsShieldType,Is.True);
        Assert.That(MapGraphic.Floor(new Vector3(0,3.2f,0)),Is.EqualTo(1));Assert.That(MapGraphic.Floor(new Vector3(0,-2.4f,0)),Is.EqualTo(-1));
        basic.Health.TakeDamage(10000);Assert.That(MapGraphic.CanShow(basic),Is.False);
    }
    [Test] public void BlinkResetsCameraAnchorAcrossBuildingAndTerrace()
    {
        game.terrain.Apply(8);
        var camera=game.player.viewCamera.GetComponent<ArenaCamera>();camera.followCharacter=false;camera.yaw=30;camera.pitch=45;
        game.player.Teleport(new Vector3(7,3.3f,15));
        game.player.Teleport(new Vector3(-14,.05f,0));
        var screen=game.player.viewCamera.WorldToViewportPoint(game.player.transform.position+Vector3.up*.8f);
        Assert.That(screen.x,Is.EqualTo(.5f).Within(.02f));
        Assert.That(screen.y,Is.EqualTo(.5f).Within(.02f));
        Assert.That(Vector3.Distance(camera.transform.position,game.player.transform.position),Is.GreaterThan(3));
    }
    [Test] public void FasterBladeHitsEarlyChainsAndRespectsCooldown()
    {
        var a=Enemy(new Vector3(0,.05f,4));var b=Enemy(new Vector3(3,.05f,4));var h=game.player.Overheat;
        for(int i=0;i<8;i++)game.RegisterKill();h.Tick(.01f);
        Assert.That(h.TryExecute(a),Is.True);h.Tick(.061f);Assert.That(a.IsAlive,Is.False);
        Assert.That(h.TryExecute(b),Is.False);h.Tick(.241f);Assert.That(h.TryExecute(b),Is.True);h.Tick(.061f);
        Assert.That(h.Combo,Is.EqualTo(2));Assert.That(b.IsAlive,Is.False);Assert.That(h.Remaining,Is.LessThan(10));
    }
}
