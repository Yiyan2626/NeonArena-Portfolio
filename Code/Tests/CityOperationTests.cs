using System.Collections;
using NeonArena;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class CityOperationTests
{
    ArenaGame game;
    [UnitySetUp] public IEnumerator Setup()
    {
        Time.timeScale=1;yield return SceneManager.LoadSceneAsync("SatelliteCity");yield return null;
        game=ArenaGame.Instance;game.StartSession();game.enabled=false;game.player.enabled=false;game.player.Health.DamageBlocked=true;
    }
    [UnityTearDown] public IEnumerator Cleanup(){Time.timeScale=1;yield return null;}
    [Test] public void ArrivalWarnsBeforeSpawningAndFreezesWhenPaused()
    {
        game.TickWave(.01f);Assert.That(game.Arrival.Active,Is.True);Assert.That(game.ActiveEnemies,Is.Zero);
        game.TickWave(.5f);Assert.That(game.ActiveEnemies,Is.Zero);float remaining=game.Arrival.Remaining;
        game.TogglePause();game.TickWave(2);Assert.That(game.Arrival.Remaining,Is.EqualTo(remaining));
        game.TogglePause();game.TickWave(.6f);Assert.That(game.ActiveEnemies,Is.EqualTo(1));
        game.StartSession();Assert.That(game.Arrival.Active,Is.False);Assert.That(game.ActiveEnemies,Is.Zero);
    }
    [Test] public void TransitHasNoSpawnOrPrematureReward()
    {
        game.Operation.Begin(2);Assert.That(game.Operation.Travelling,Is.True);
        game.TickWave(20);Assert.That(game.Arrival.Active,Is.False);Assert.That(game.State,Is.EqualTo(ArenaState.Playing));
        game.player.Teleport(game.Operation.Destination);game.TickWave(.02f);
        Assert.That(game.Operation.Travelling,Is.False);Assert.That(game.Arrival.Active,Is.True);
    }
    [Test] public void DefendRequiresCorrectFloorAndPausesOutsideZone()
    {
        var op=game.Operation;op.Begin(3);game.player.Teleport(op.Destination+Vector3.up*5);op.Tick(20);
        Assert.That(op.Travelling,Is.True);Assert.That(op.HoldProgress,Is.Zero);
        game.player.Teleport(op.Destination);op.Tick(6);Assert.That(op.HoldProgress,Is.EqualTo(6));
        game.player.Teleport(op.Destination+Vector3.right*9);op.Tick(20);Assert.That(op.HoldProgress,Is.EqualTo(6));
        game.player.Teleport(op.Destination);game.TogglePause();op.Tick(20);Assert.That(op.HoldProgress,Is.EqualTo(6));
        game.TogglePause();op.Tick(12);Assert.That(op.CanComplete,Is.True);
    }
    [UnityTest] public IEnumerator AllSectorsReachableByWalkingWithoutTeleportOrJetpack()
    {
        for(int wave=2;wave<=4;wave++)
        {
            game.Operation.Begin(wave);var dest=game.Operation.Destination;
            Assert.That(game.terrain.HasRoute(game.player.transform.position,dest),Is.True,"Sector "+wave);
            for(int i=0;i<3000&&!game.Operation.PlayerInZone;i++)
            {
                var step=game.terrain.Steer(game.player.transform.position,dest);
                var input=new Vector2(Vector3.Dot(step,game.player.GetMoveDirection(Vector2.right)),Vector3.Dot(step,game.player.GetMoveDirection(Vector2.up)));
                game.player.Move(input,.02f);if(i%5==0)yield return null;
            }
            Assert.That(game.Operation.PlayerInZone,Is.True,"Sector "+wave+" stuck at "+game.player.transform.position);
            game.Operation.Tick(.02f);Assert.That(game.Operation.CanSpawn,Is.True);
        }
    }
    [UnityTest] public IEnumerator CampaignKeepsPositionAndEndsAfterFourSectorsWithElite()
    {
        bool sawElite=false;
        for(int wave=1;wave<=4;wave++)
        {
            game.player.Teleport(game.Operation.Destination);game.Operation.Tick(20);
            for(int i=0;i<100&&game.State==ArenaState.Playing;i++)
            {
                game.TickWave(1.1f);
                foreach(var e in game.Enemies)if(e.IsAlive){sawElite|=e.Elite.IsElite;e.Health.TakeDamage(10000);}
                game.Cinema.ResetCinema(true);
            }
            Assert.That(game.State,Is.EqualTo(ArenaState.Reward),"wave "+wave);
            var position=game.player.transform.position;yield return new WaitForSecondsRealtime(.4f);
            Assert.That(game.ChooseUpgrade(AmmoUpgrade.Vitality),Is.True);
            if(wave<4){game.TickWave(2);Assert.That(Vector3.Distance(position,game.player.transform.position),Is.LessThan(.01f));}
        }
        Assert.That(sawElite,Is.True);Assert.That(game.State,Is.EqualTo(ArenaState.Victory));
    }
    [Test] public void MouseTurnsCharacterAndCameraConvergesWithoutOscillation()
    {
        var camera=game.player.viewCamera.GetComponent<ArenaCamera>();camera.yaw=350;game.player.visual.rotation=Quaternion.Euler(0,350,0);
        game.player.ApplyLookInput(new Vector2(20,0),false,.02f);
        float target=game.player.visual.eulerAngles.y,previous=Mathf.Abs(Mathf.DeltaAngle(camera.yaw,target));
        Assert.That(previous,Is.GreaterThan(20));
        for(int i=0;i<90;i++){camera.FollowHeading(target,1f/60);float error=Mathf.Abs(Mathf.DeltaAngle(camera.yaw,target));Assert.That(error,Is.LessThanOrEqualTo(previous+.001f));previous=error;}
        Assert.That(previous,Is.LessThan(.1f));
    }
    [Test] public void FreeLookPreservesHeadingAndReleaseReturnsBehindCharacter()
    {
        var cam=game.player.viewCamera.GetComponent<ArenaCamera>();var body=game.player.visual.eulerAngles.y;
        game.player.ApplyLookInput(new Vector2(30,10),true,.02f);cam.Orbit(90,5);cam.RenderView(true);
        Assert.That(Mathf.Abs(Mathf.DeltaAngle(body,game.player.visual.eulerAngles.y)),Is.LessThan(.01f));
        Assert.That(Vector3.Dot(game.player.GetMoveDirection(Vector2.up),Quaternion.Euler(0,cam.yaw,0)*Vector3.forward),Is.GreaterThan(.99f));
        for(int i=0;i<90;i++)cam.FollowHeading(body,1f/60);
        Assert.That(Mathf.Abs(Mathf.DeltaAngle(cam.yaw,body)),Is.LessThan(.1f));
    }
    [Test] public void CentredAimPointsAheadOfPlayerAndPauseIgnoresLook()
    {
        var cam=game.player.viewCamera.GetComponent<ArenaCamera>();cam.RenderView(true);
        game.player.AimAndFire(false,new Vector2(Screen.width*.5f,Screen.height*.5f));
        var delta=Vector3.ProjectOnPlane(game.player.AimPoint-game.player.transform.position,Vector3.up);
        Assert.That(delta.magnitude,Is.GreaterThan(3));Assert.That(Vector3.Dot(delta.normalized,game.player.visual.forward),Is.GreaterThan(.9f));
        float yaw=game.player.visual.eulerAngles.y;game.TogglePause();game.player.ApplyLookInput(new Vector2(30,0),false,.02f);
        Assert.That(game.player.visual.eulerAngles.y,Is.EqualTo(yaw));
    }
    [Test] public void FollowCameraKeepsPlayerVisibleAtCloseZoomAndAfterTeleport()
    {
        var cam=game.player.viewCamera.GetComponent<ArenaCamera>();cam.Zoom(100);
        foreach(var p in new[]{game.terrain.entry,new Vector3(-24,.3f,-54),new Vector3(-28,5.3f,-49)})
        foreach(float yaw in new[]{0f,90f,180f,270f})
        {
            cam.yaw=yaw;game.player.Teleport(p);cam.RenderView(true);
            var screen=game.player.viewCamera.WorldToViewportPoint(p+Vector3.up*.85f);
            Assert.That(screen.z,Is.GreaterThan(0));Assert.That(screen.x,Is.InRange(.08f,.92f));Assert.That(screen.y,Is.InRange(.08f,.92f),p+" yaw="+yaw);
        }
    }
    [Test] public void FollowCameraRetractsBeforeAnActualViewObstruction()
    {
        var cam=game.player.viewCamera.GetComponent<ArenaCamera>();cam.yaw=0;cam.pitch=38;cam.distance=13;cam.RenderView(true);
        var prior=cam.transform.position;var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.layer=9;
        wall.transform.SetPositionAndRotation(prior+cam.transform.forward*4,cam.transform.rotation);wall.transform.localScale=new Vector3(4,4,.3f);
        try
        {
            Physics.SyncTransforms();cam.RenderView(true);
            Assert.That(Vector3.Dot(cam.transform.position-prior,cam.transform.forward),Is.GreaterThan(4));
            Assert.That(Physics.CheckSphere(cam.transform.position,.2f,1<<9),Is.False);
        }
        finally{Object.DestroyImmediate(wall);}
    }
    [UnityTest] public IEnumerator UpperFloorFacadeFadesAlongCameraToCharacterRay()
    {
        var cam=game.player.viewCamera.GetComponent<ArenaCamera>();cam.yaw=180;cam.pitch=38;cam.distance=13;
        game.player.Teleport(new Vector3(-28,5.25f,-49));cam.RenderView(true);yield return new WaitForSeconds(.25f);
        var delta=game.player.transform.position+Vector3.up*.85f-cam.transform.position;int walls=0;
        bool backfaces=Physics.queriesHitBackfaces;Physics.queriesHitBackfaces=true;
        var hits=Physics.RaycastAll(cam.transform.position,delta.normalized,delta.magnitude,(1<<9)|(1<<11),QueryTriggerInteraction.Collide);Physics.queriesHitBackfaces=backfaces;
        foreach(var hit in hits)
        {
            var cut=hit.collider.GetComponent<CameraOccluder>();if(!cut)continue;walls++;
            Assert.That(cut.Fade,Is.LessThan(.15f),hit.collider.name);
            Assert.That(hit.collider.enabled,Is.True);
        }
        foreach(var r in Object.FindObjectsOfType<MeshRenderer>())if(r.bounds.IntersectRay(new Ray(cam.transform.position,delta.normalized),out float d)&&d<delta.magnitude)
        {
            var cut=r.GetComponent<CameraOccluder>();if(!cut||!r.GetComponent<MeshCollider>())continue;walls++;
            Assert.That(cut.Fade,Is.LessThan(.15f),"Imported surface "+r.name);
            Assert.That(r.GetComponent<MeshCollider>().enabled,Is.True);
        }
        Assert.That(walls,Is.GreaterThan(0),"The regression must exercise the actual facade.");
    }
}
