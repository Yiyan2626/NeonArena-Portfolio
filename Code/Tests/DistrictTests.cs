using System.Collections;
using NeonArena;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class DistrictTests
{
    ArenaGame game;
    [UnitySetUp] public IEnumerator Setup(){Time.timeScale=1;yield return SceneManager.LoadSceneAsync("Arena");yield return null;game=ArenaGame.Instance;game.autoSpawn=false;game.StartSession();game.player.enabled=false;}
    [UnityTearDown] public IEnumerator Cleanup(){Time.timeScale=1;SentryBolt.Clear();yield return null;}
    EnemyController Sentry(Vector3 p){var e=game.SpawnEnemy();e.enabled=false;e.Spawn(p,game.player,2,false,false,false,true);e.Arrival.Step(EnemySpawnPresentation.Duration+.01f);return e;}
    [Test] public void DistrictIsLargerAndHasConnectedIndoorAndRoofRoutes()
    {
        game.terrain.Apply(6);Assert.That(ArenaTerrain.HalfWidth*2*ArenaTerrain.HalfDepth*2,Is.GreaterThan(9000));
        Assert.That(game.terrain.HasRoute(new Vector3(-38,0,-4),new Vector3(-32,4,0)),Is.True,"Hall-to-roof ramp");
        Assert.That(game.terrain.HasRoute(new Vector3(0,0,22),new Vector3(0,6,35)),Is.True,"Satellite station ramp");
        Assert.That(ArenaTerrain.TryGround(new Vector3(38,4.1f,6),out var roof),Is.True);Assert.That(roof.y,Is.EqualTo(4.04f).Within(.08f));
    }
    [Test] public void LaterRoutesAreAddedWithoutClosingTheInitialHall()
    {
        var d=game.terrain.GetComponent<SatelliteDistrict>();game.terrain.Apply(1);Assert.That(d.phases[0].activeSelf,Is.True);Assert.That(d.phases[2].activeSelf,Is.False);
        game.terrain.Apply(6);foreach(var phase in d.phases)Assert.That(phase.activeSelf,Is.True);
    }
    [Test] public void SuppliedRobotAndGuardianHaveDifferentSkinnedMeshes()
    {
        var e=Sentry(new Vector3(0,.05f,5));Assert.That(e.Motion.Model.imported,Is.True);Assert.That(game.player.Motion.Model.imported,Is.True);
        Assert.That(e.Motion.Model.skin.sharedMesh,Is.Not.SameAs(game.player.Motion.Model.skin.sharedMesh));
        Assert.That(e.Motion.Model.skin.sharedMaterials[0].mainTexture,Is.Not.Null);Assert.That(game.player.Motion.Model.sourceBones.Length,Is.GreaterThan(15));
    }
    [Test] public void ImportedHandsFollowElbowsInsteadOfDisconnectedIKTargets()
    {
        var m=game.player.Motion.Model;Assert.That(m.joints[8].IsChildOf(m.joints[7]),Is.True);Assert.That(m.joints[5].IsChildOf(m.joints[4]),Is.True);
        Vector3 before=m.joints[8].position;m.joints[7].localRotation=Quaternion.Euler(60,0,0);Assert.That(Vector3.Distance(before,m.joints[8].position),Is.GreaterThan(.15f));
    }
    [Test] public void SentryAimsUpAndLocksWarningBeforeShot()
    {
        var e=Sentry(new Vector3(0,.05f,4));game.player.Teleport(new Vector3(0,4,-2));e.Ranged.Tick(2);
        Assert.That(e.Ranged.Charging,Is.True);Vector3 aim=e.Ranged.LockedAim;Assert.That(aim.y,Is.GreaterThan(4));
        game.player.Teleport(new Vector3(4,4,-2));e.Ranged.Tick(.9f);Assert.That(e.Ranged.Shots,Is.EqualTo(1));Assert.That(e.Ranged.LockedAim,Is.EqualTo(aim));
        e.Ranged.Tick(.1f);Assert.That(e.Ranged.Charging,Is.False);
    }
    [UnityTest] public IEnumerator WallInterceptsPlasmaBeforeItHitsPlayer()
    {
        game.player.Teleport(new Vector3(0,.05f,-2));float hp=game.player.Health.Current;
        var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.layer=9;wall.transform.position=new Vector3(0,1,0);wall.transform.localScale=new Vector3(4,2,.3f);Physics.SyncTransforms();
        SentryBolt.Fire(new Vector3(0,.9f,3),Vector3.back,20,game.player);yield return new WaitForSeconds(.5f);
        Assert.That(game.player.Health.Current,Is.EqualTo(hp));Assert.That(Object.FindObjectsOfType<SentryBolt>().Length,Is.Zero);Object.Destroy(wall);
    }
    [UnityTest] public IEnumerator PlasmaDamagesPlayerAndFreezesDuringPause()
    {
        game.player.Teleport(new Vector3(0,.05f,-2));float hp=game.player.Health.Current;
        SentryBolt.Fire(new Vector3(0,.9f,3),Vector3.back,20,game.player);var bolt=Object.FindObjectOfType<SentryBolt>();Vector3 before=bolt.transform.position;
        game.TogglePause();yield return new WaitForSecondsRealtime(.15f);Assert.That(bolt.transform.position,Is.EqualTo(before));game.TogglePause();yield return new WaitForSeconds(.5f);
        Assert.That(game.player.Health.Current,Is.EqualTo(hp-20));
    }
    [Test] public void ReusingSentryClearsItsWarningAndNewRunClearsProjectiles()
    {
        var e=Sentry(new Vector3(0,.05f,5));e.Ranged.Tick(2);Assert.That(e.Ranged.Charging,Is.True);e.Spawn(new Vector3(0,.05f,5),game.player);
        Assert.That(e.Ranged.IsRanged,Is.False);Assert.That(e.Ranged.Charging,Is.False);Assert.That(e.Motion.Model.kind,Is.EqualTo(1));
    }
    [UnityTest] public IEnumerator JetpackCanLiftOffFromTheSixMetreDeck()
    {
        game.terrain.Apply(6);game.player.weapon.Upgrade(AmmoUpgrade.Jump);game.player.weapon.Upgrade(AmmoUpgrade.Jump);game.player.Teleport(new Vector3(0,6.05f,34));
        for(int i=0;i<12;i++){game.player.Move(Vector2.zero,.016f);yield return null;}
        game.player.RequestJump();for(int i=0;i<140;i++){game.player.Flight.Held=true;game.player.Move(Vector2.zero,.016f);yield return null;}
        Assert.That(game.player.transform.position.y,Is.GreaterThan(10));Assert.That(game.player.transform.position.y,Is.LessThanOrEqualTo(15.1f));
    }
}
