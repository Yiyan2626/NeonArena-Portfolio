using System.Collections;
using NeonArena;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class CityCombatTests
{
    ArenaGame game;
    [UnitySetUp] public IEnumerator Setup()
    {
        Time.timeScale=1;yield return SceneManager.LoadSceneAsync("SatelliteCity");yield return null;
        game=ArenaGame.Instance;game.autoSpawn=false;game.StartSession();game.player.enabled=false;
    }
    [UnityTearDown] public IEnumerator Cleanup(){Time.timeScale=1;yield return null;}
    [Test] public void SuppliedCityIsThePhysicalBattlefield()
    {
        Assert.That(game.terrain.satelliteCity,Is.True);
        Assert.That(GameObject.Find("Arena Environment"),Is.Null);
        var city=GameObject.Find("Satellite City / PLAYABLE original architecture");Assert.That(city,Is.Not.Null);
        Assert.That(city.GetComponentsInChildren<MeshCollider>().Length,Is.GreaterThan(400));
        Assert.That(ArenaTerrain.WorldHalfWidth*ArenaTerrain.WorldHalfDepth*4,Is.GreaterThan(29000));
        Assert.That(game.terrain.HasRoute(game.terrain.entry,new Vector3(0,.05f,-75)),Is.True);
        Assert.That(game.terrain.HasRoute(game.terrain.entry,new Vector3(0,.05f,40)),Is.True);
    }
    [Test] public void BothOriginalBuildingsConnectGroundFloorToMezzanine()
    {
        foreach(var p in new[]{new Vector3(-24,.3f,-57),new Vector3(22,.3f,-56),new Vector3(-24,5.3f,-49),new Vector3(22,5.3f,-48),new Vector3(-27,5.3f,-22.5f)})
            Assert.That(game.terrain.HasRoute(game.terrain.entry,p),Is.True,"City route: "+p);
    }
    [Test] public void SpawnPointsAlwaysHaveGroundAndCanPursuePlayer()
    {
        for(int i=0;i<20;i++)
        {
            var e=game.SpawnEnemy();Assert.That(e,Is.Not.Null,"Spawn "+i);e.enabled=false;
            Assert.That(game.terrain.HasRoute(e.transform.position,game.player.transform.position),Is.True);
            Assert.That(Physics.Raycast(e.transform.position+Vector3.up*.3f,Vector3.down,1,1<<9),Is.True);
        }
    }
    [Test] public void RampCutawayKeepsManualYawAndUsefulDistance()
    {
        var cam=game.player.viewCamera.GetComponent<ArenaCamera>();cam.pitch=38;cam.distance=13;
        foreach(float yaw in new[]{0f,45f,90f,180f,270f})for(int i=0;i<16;i++)
        {
            game.player.Teleport(new Vector3(-31,.3f,-43+i*.7f));cam.yaw=yaw;cam.RenderView(true);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(cam.transform.eulerAngles.y,yaw)),Is.LessThan(.1f));
            Assert.That(Vector3.Distance(cam.transform.position,game.player.transform.position),Is.GreaterThan(3));
        }
    }
    [UnityTest] public IEnumerator CutawayDoesNotRemovePhysicalWallOrEnemyOcclusion()
    {
        var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.layer=9;g.transform.position=new Vector3(0,3,1);g.transform.localScale=new Vector3(8,4,.3f);
        g.AddComponent<CameraOccluder>();Physics.SyncTransforms();var cut=g.GetComponent<CameraOccluder>();cut.Reveal();yield return null;
        Assert.That(g.GetComponent<Collider>().enabled,Is.True);
        Assert.That(Physics.Linecast(new Vector3(0,3,-2),new Vector3(0,3,4),1<<9),Is.True);
        Object.Destroy(g);
    }
    [UnityTest] public IEnumerator DamageFeedbackAggregatesBeamHitsAndResetsOnRestart()
    {
        var e=game.SpawnEnemy();e.enabled=false;e.Spawn(new Vector3(4,1.85f,-10),game.player);e.RefreshVisibility();yield return null;
        var feedback=CombatFeedback.Instance;
        for(int i=0;i<6;i++)e.Health.TakeDamage(1);
        Assert.That(feedback.ConfirmedHits,Is.EqualTo(6));Assert.That(feedback.VisibleNumbers,Is.EqualTo(1));
        e.Health.TakeDamage(500);Assert.That(feedback.Combo,Is.EqualTo(1));Assert.That(game.Kills,Is.EqualTo(1));
        game.StartSession();Assert.That(feedback.Combo,Is.Zero);Assert.That(feedback.VisibleNumbers,Is.Zero);
    }
    [UnityTest] public IEnumerator GroundEnemyActuallyWalksUpTheIndoorRamp()
    {
        game.player.Teleport(new Vector3(-24,5.3f,-49));game.player.Health.DamageBlocked=true;
        var e=game.SpawnEnemy();Assert.That(e,Is.Not.Null);e.enabled=false;e.Spawn(new Vector3(-24,.3f,-48),game.player);e.moveSpeed=8;
        yield return new WaitForSeconds(.7f);
        for(int i=0;i<900&&Vector3.Distance(e.transform.position,game.player.transform.position)>1.4f;i++){e.Tick(.02f);if(i%4==0)yield return null;}
        Assert.That(e.transform.position.y,Is.GreaterThan(4.8f));Assert.That(Vector3.Distance(e.transform.position,game.player.transform.position),Is.LessThan(2));
    }
    [UnityTest] public IEnumerator ForegroundCameraCutawayPreservesEnemySurfaceAim()
    {
        game.player.Teleport(new Vector3(0,.1f,-33));var e=game.SpawnEnemy();Assert.That(e,Is.Not.Null);
        e.enabled=false;e.Spawn(new Vector3(4,.1f,-33),game.player);e.RefreshVisibility();
        var roof=GameObject.CreatePrimitive(PrimitiveType.Cube);roof.layer=9;roof.transform.position=new Vector3(2,4,-33);roof.transform.localScale=new Vector3(16,.3f,16);
        var cutaway=roof.AddComponent<CameraOccluder>();Physics.SyncTransforms();
        var cam=game.player.viewCamera.GetComponent<ArenaCamera>();cam.yaw=0;cam.pitch=38;cam.distance=13;cam.RenderView(true);
        cutaway.Reveal();yield return new WaitForSeconds(.1f);Assert.That(cutaway.Fade,Is.LessThan(.5f));
        var aim=e.transform.position+Vector3.up*.85f;game.player.AimAndFire(true,game.player.viewCamera.WorldToScreenPoint(aim));
        var ray=game.player.viewCamera.ScreenPointToRay(game.player.viewCamera.WorldToScreenPoint(aim));
        Assert.That(e.GetComponent<CharacterController>().Raycast(ray,out var hit,100),Is.True);
        Assert.That(Vector3.Distance(game.player.AimPoint,hit.point),Is.LessThan(.02f));
        Assert.That(Vector3.Dot(game.player.weapon.LastShotDirection,(hit.point-game.player.weapon.LastShotOrigin).normalized),Is.GreaterThan(.999f));
        Assert.That(roof.GetComponent<Collider>().enabled,Is.True);Object.Destroy(roof);
    }
}
