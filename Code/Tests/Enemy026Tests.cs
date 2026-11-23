using System.Collections;
using NeonArena;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class Enemy026Tests
{
    ArenaGame game;
    GameObject floor,wall;
    [UnitySetUp] public IEnumerator Setup()
    {
        Time.timeScale=1;
        yield return SceneManager.LoadSceneAsync("BorderCity");yield return null;
        game=ArenaGame.Instance;game.StartSession();game.enabled=false;game.Frontier.enabled=false;
        game.player.enabled=false;game.player.Health.DamageBlocked=false;
        foreach(var enemy in Object.FindObjectsOfType<EnemyController>())enemy.gameObject.SetActive(false);
        floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.layer=9;
        floor.transform.position=new Vector3(0,39.5f,0);floor.transform.localScale=new Vector3(60,1,60);
        game.player.Teleport(new Vector3(0,40.04f,5));Physics.SyncTransforms();
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        Time.timeScale=1;SentryBolt.Clear();
        if(wall)Object.Destroy(wall);if(floor)Object.Destroy(floor);yield return null;
    }
    EnemyController Spawn(bool ranged=false,bool charger=false)
    {
        var e=game.SpawnAuthored(new Vector3(0,40.04f,0),1,charger,false,false,ranged);
        // These synchronous tests do not yield while stepping arrival; leave its lifecycle enabled.
        Assert.That(e,Is.Not.Null);e.enabled=false;e.Motion.enabled=false;
        Physics.SyncTransforms();return e;
    }
    void Wall(float z)
    {
        wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.layer=9;
        wall.transform.position=new Vector3(0,41.5f,z);wall.transform.localScale=new Vector3(20,3,.3f);Physics.SyncTransforms();
    }
    [Test] public void SpawnKeepsScaleFreezesAttacksAndPoolRestoresVisualRoot()
    {
        var e=Spawn(true);Vector3 scale=e.visual.localScale;float hp=game.player.Health.Current;
        Assert.That(e.Spawning,Is.True);Assert.That(e.Arrival.HideBody,Is.True);
        e.Tick(3);e.Ranged.Tick(3);
        Assert.That(e.Ranged.Shots,Is.Zero);Assert.That(game.player.Health.Current,Is.EqualTo(hp));
        e.Arrival.Step(.3f);float airborne=e.visual.localPosition.y;
        Assert.That(e.visual.localScale,Is.EqualTo(scale));
        game.TogglePause();float elapsed=e.Arrival.Elapsed;e.Arrival.Step(2);e.Ranged.Tick(2);
        Assert.That(e.Arrival.Elapsed,Is.EqualTo(elapsed));Assert.That(e.Ranged.Shots,Is.Zero);
        game.TogglePause();e.Arrival.Step(.4f);
        Assert.That(e.Spawning,Is.False);Assert.That(e.visual.localPosition.y,Is.LessThan(airborne));
        Vector3 rest=e.visual.localPosition;
        e.Spawn(new Vector3(0,40.04f,0),game.player,1,false,false,false,true);e.Arrival.Step(.3f);
        e.Health.TakeDamage(9999);Assert.That(e.gameObject.activeSelf,Is.False);
        Assert.That(e.visual.localPosition,Is.EqualTo(rest));
        e.Spawn(new Vector3(0,40.04f,0),game.player,1,false,false,false,true);
        Assert.That(e.Arrival.Elapsed,Is.Zero);Assert.That(e.Ranged.Charging,Is.False);Assert.That(e.Ranged.Shots,Is.Zero);
        game.StartSession();Assert.That(e.gameObject.activeSelf,Is.False);Assert.That(e.Arrival.Active,Is.False);
    }
    [Test] public void ArrivalScanRetainsTextureAndRestoresTheExactOriginalMaterial()
    {
        var e=Spawn(true);var skin=e.Motion.Model.skin;
        var arrival=skin.sharedMaterial;var texture=arrival.mainTexture;
        Assert.That(arrival.shader.name,Is.EqualTo("NeonArena/EnemyArrival026"));
        Assert.That(texture,Is.Not.Null);
        e.Arrival.Step(.3f);Assert.That(e.Arrival.RevealProgress,Is.InRange(.4f,.6f));
        var block=new MaterialPropertyBlock();skin.GetPropertyBlock(block);
        Assert.That(block.GetFloat("_ArrivalActive"),Is.EqualTo(1));
        e.Arrival.Step(1);var original=skin.sharedMaterial;
        Assert.That(original,Is.Not.SameAs(arrival));Assert.That(original.mainTexture,Is.SameAs(texture));
        e.Spawn(new Vector3(0,40.04f,0),game.player,1,false,false,false,true);
        Assert.That(skin.sharedMaterial,Is.SameAs(arrival),"Pool must reuse the cached scan material");
        e.gameObject.SetActive(false);Assert.That(skin.sharedMaterial,Is.SameAs(original));
        skin.GetPropertyBlock(block);Assert.That(block.GetFloat("_ArrivalActive"),Is.Zero);
    }
    [Test] public void RangedGroundGuardRetreatsAndKeepsLockedAimButNeverFiresThroughWall()
    {
        var e=Spawn(true);e.Arrival.Step(1);e.Ranged.HoldPosition=true;
        float distance=Vector3.Distance(e.transform.position,game.player.transform.position);
        for(int i=0;i<30;i++)e.Ranged.Tick(.02f);
        Assert.That(Vector3.Distance(e.transform.position,game.player.transform.position),Is.GreaterThan(distance+.5f));
        for(int i=0;i<60&&!e.Ranged.Charging;i++)e.Ranged.Tick(.02f);
        Assert.That(e.Ranged.Charging,Is.True);Vector3 aim=e.Ranged.LockedAim;
        game.player.Teleport(new Vector3(3,40.04f,5));e.Ranged.Tick(.1f);
        Assert.That(e.Ranged.LockedAim,Is.EqualTo(aim));Assert.That(e.Ranged.Shots,Is.Zero);
        Wall(2);e.Ranged.Tick(.8f);Assert.That(e.Ranged.Shots,Is.Zero);
        for(int i=0;i<180;i++)e.Ranged.Tick(.02f);
        Assert.That(e.Ranged.Charging,Is.False);Assert.That(e.Ranged.Shots,Is.Zero);
    }
    [Test] public void ElevatedGuardHoldsPerchAndFiresVerticallyThenRetreatsOnSameLevel()
    {
        var e=Spawn(true);e.Arrival.Step(1);e.Ranged.HoldPosition=true;
        game.player.Teleport(new Vector3(0,43.5f,5));Physics.SyncTransforms();
        Vector3 start=e.transform.position;
        for(int i=0;i<90&&!e.Ranged.Charging;i++)e.Ranged.Tick(.02f);
        Assert.That(e.Ranged.Charging,Is.True,"Height difference must not prevent a clear shot");
        Vector3 shotDirection=(e.Ranged.LockedAim-(e.transform.position+Vector3.up*1.25f)).normalized;
        Assert.That(shotDirection.y,Is.GreaterThan(.4f),"Aim must retain its vertical component");
        for(int i=0;i<50;i++)e.Ranged.Tick(.02f);
        Assert.That(e.Ranged.Shots,Is.GreaterThan(0));
        Assert.That(Vector2.Distance(new Vector2(start.x,start.z),new Vector2(e.transform.position.x,e.transform.position.z)),Is.LessThan(.01f),"Guard drifted off its post");
        game.player.Teleport(new Vector3(0,40.04f,5));Physics.SyncTransforms();
        float distance=Vector3.ProjectOnPlane(game.player.transform.position-e.transform.position,Vector3.up).magnitude;
        for(int i=0;i<30;i++)e.Ranged.Tick(.02f);
        float after=Vector3.ProjectOnPlane(game.player.transform.position-e.transform.position,Vector3.up).magnitude;
        Assert.That(after,Is.GreaterThan(distance+.5f),"Same-level pressure should permit retreat despite HoldPosition");
    }
    [Test] public void UnobstructedShotHasFullDodgeWindowAndPausedWindupDoesNotAdvance()
    {
        var e=Spawn(true);e.Arrival.Step(1);game.player.Teleport(new Vector3(0,40.04f,13));
        e.Ranged.Tick(1.5f);Assert.That(e.Ranged.Charging,Is.True);
        float remaining=e.Ranged.WarningRemaining;
        game.TogglePause();e.Ranged.Tick(1);Assert.That(e.Ranged.WarningRemaining,Is.EqualTo(remaining));game.TogglePause();
        e.Ranged.Tick(.6f);Assert.That(e.Ranged.Shots,Is.Zero);
        e.Ranged.Tick(.26f);Assert.That(e.Ranged.Shots,Is.EqualTo(1));
    }
    [Test] public void CrowdSeparationIsSymmetricAndRetreatProbeRejectsWallsAndLedges()
    {
        var a=Spawn();var b=Spawn();a.Arrival.Step(1);b.Arrival.Step(1);Physics.SyncTransforms();
        Vector3 sa=a.Separate(Vector3.zero),sb=b.Separate(Vector3.zero);
        Assert.That(sa.sqrMagnitude,Is.GreaterThan(.1f));Assert.That(Vector3.Dot(sa,sb),Is.LessThan(0));
        Wall(.7f);Assert.That(a.CanStep(Vector3.forward),Is.False);
        var motor=a.GetComponent<CharacterController>();motor.enabled=false;a.transform.position=new Vector3(29.8f,40.04f,0);motor.enabled=true;
        Assert.That(a.CanStep(Vector3.right),Is.False);
    }
    [Test] public void ChargeDirectionLocksAndCannotHitAcrossANewWall()
    {
        var e=Spawn(false,true);e.Arrival.Step(1);e.Charge.Tick(.31f);
        Assert.That(e.Charge.Phase,Is.EqualTo(ChargePhase.Telegraph));Vector3 direction=e.Charge.LockedDirection;
        game.player.Teleport(new Vector3(.5f,40.04f,5));e.Charge.Tick(.5f);
        Assert.That(e.Charge.LockedDirection,Is.EqualTo(direction));float hp=game.player.Health.Current;
        Assert.That(game.player.Health.Current,Is.EqualTo(hp));
        // A wall appears after the attack was committed, with the player just across it.
        game.player.Teleport(new Vector3(0,40.04f,1.05f));Wall(.55f);e.Charge.Tick(.4f);e.Charge.Tick(.2f);
        Assert.That(game.player.Health.Current,Is.EqualTo(hp));
    }
}
