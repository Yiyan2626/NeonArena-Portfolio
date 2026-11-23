using System.Collections;
using NeonArena;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class PresentationTests
{
    ArenaGame game;
    [UnitySetUp] public IEnumerator Setup()
    { Time.timeScale=1;yield return SceneManager.LoadSceneAsync("Arena");yield return null;game=ArenaGame.Instance;game.autoSpawn=false;game.StartSession();game.player.enabled=false; }
    [UnityTearDown] public IEnumerator Cleanup(){Time.timeScale=1;yield return null;}
    EnemyController Target(Vector3 p,bool shield=false)
    {var e=game.SpawnEnemy();e.enabled=false;e.Spawn(p,game.player,1,false,shield);e.visual.rotation=Quaternion.LookRotation(Vector3.back);Physics.SyncTransforms();return e;}
    void Lock(EnemyController e)
    {game.player.viewCamera.GetComponent<ArenaCamera>().RenderView(true);game.player.Focus.SetHeld(true,game.player.viewCamera.WorldToScreenPoint(e.transform.position+Vector3.up*.85f),true);}
    [Test] public void RightButtonCannotLockAndMovementFollowsCamera()
    {
        var e=Target(new Vector3(0,.05f,3),true);Lock(e);Assert.That(game.player.Focus.Target,Is.Null);
        var cam=game.player.viewCamera.GetComponent<ArenaCamera>();
        cam.yaw=90;cam.RenderView(true);
        Assert.That(Vector3.Distance(game.player.GetMoveDirection(Vector2.up),Vector3.right),Is.LessThan(.001f));
        Assert.That(Vector3.Distance(game.player.GetMoveDirection(Vector2.right),Vector3.back),Is.LessThan(.001f));
        cam.Orbit(90,0);cam.RenderView(true);
        Assert.That(Vector3.Distance(game.player.GetMoveDirection(Vector2.up),Vector3.back),Is.LessThan(.001f));
        Assert.That(game.player.Focus.Target,Is.Null);
    }
    [Test] public void AcquiringMovingAndReleasingTargetCannotChangeMovementBasis()
    {
        var e=Target(new Vector3(0,.05f,3));var cam=game.player.viewCamera.GetComponent<ArenaCamera>();cam.RenderView(true);
        var before=game.player.GetMoveDirection(new Vector2(1,1));Lock(e);
        Assert.That(Vector3.Distance(before,game.player.GetMoveDirection(new Vector2(1,1))),Is.LessThan(.001f));
        e.transform.position=new Vector3(3,.05f,0);cam.RenderView(true);
        Assert.That(Vector3.Distance(before,game.player.GetMoveDirection(new Vector2(1,1))),Is.LessThan(.001f));
        game.player.Focus.SetHeld(false,Vector2.zero);Assert.That(game.player.Focus.HasTarget,Is.False);
        Assert.That(Vector3.Distance(before,game.player.GetMoveDirection(new Vector2(1,1))),Is.LessThan(.001f));
        Assert.That(before.magnitude,Is.EqualTo(1).Within(.001f));
    }
    [Test] public void LockedDirectionalDashUsesCameraInsteadOfTargetFacing()
    {
        Lock(Target(new Vector3(0,.05f,3)));game.player.AimAndFire(false,Vector2.zero);
        var cam=game.player.viewCamera.GetComponent<ArenaCamera>();cam.yaw=90;cam.RenderView(true);
        Assert.That(game.player.Dash.TryStart(Vector2.up),Is.True);
        Assert.That(Vector3.Distance(game.player.Dash.Direction,Vector3.right),Is.LessThan(.001f));
    }
    [Test] public void OverheadCameraPreservesFullSpeedAndHorizontalDirection()
    {
        game.player.viewCamera.transform.rotation=Quaternion.Euler(90,90,0);
        Assert.That(Vector3.Distance(game.player.GetMoveDirection(Vector2.up),Vector3.right),Is.LessThan(.001f));
        Assert.That(Vector3.Distance(game.player.GetMoveDirection(Vector2.right),Vector3.back),Is.LessThan(.001f));
    }
    [Test] public void FreeAimStillAllowsLeftButtonFire()
    {
        var e=Target(new Vector3(0,.05f,3));Lock(e);game.player.AimAndFire(true,Vector2.zero);
        Assert.That(game.player.weapon.ShotsFired,Is.EqualTo(1));Assert.That(game.player.Focus.HasTarget,Is.False);
    }
    [Test] public void NearbyCoverRaisesCameraInsteadOfPushingItIntoPlayer()
    {
        var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.layer=9;
        wall.transform.position=new Vector3(0,1,-3);wall.transform.localScale=new Vector3(6,2,.4f);
        try
        {
            Physics.SyncTransforms();var camera=game.player.viewCamera.GetComponent<ArenaCamera>();camera.yaw=0;camera.pitch=45;camera.RenderView(true);
            Assert.That(Vector3.Distance(camera.transform.position,game.player.transform.position),Is.GreaterThan(8));
            Assert.That(camera.pitch,Is.EqualTo(45)); // The user's preferred orbit angle is retained.
            Assert.That(camera.transform.position.y,Is.GreaterThan(8));
        }
        finally{Object.DestroyImmediate(wall);}
    }
    [Test] public void FocusFramingRespectsManualCloseZoom()
    {
        var e=Target(new Vector3(0,.05f,3));Lock(e);var camera=game.player.viewCamera.GetComponent<ArenaCamera>();camera.followCharacter=false;camera.Zoom(100);camera.RenderView(true);
        Vector3 middle=game.player.transform.position+Vector3.up*.8f;
        Assert.That(Vector3.Distance(camera.transform.position,middle),Is.EqualTo(7).Within(.02f));
    }
    [Test] public void CameraFocusGrazingCoverCannotCollapseAtAnyYaw()
    {
        var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.layer=9;wall.transform.localScale=new Vector3(6,2,.4f);
        try
        {
            var camera=game.player.viewCamera.GetComponent<ArenaCamera>();camera.pitch=45;
            foreach(int pitch in new[]{20,45,75}) for(int yaw=0;yaw<360;yaw+=30)
            {
                camera.yaw=yaw;camera.pitch=pitch;var rotation=Quaternion.Euler(0,yaw,0);
                wall.transform.SetPositionAndRotation(game.player.transform.position+Vector3.up*.92f+rotation*Vector3.back*.5f,rotation);
                Physics.SyncTransforms();camera.RenderView(true);
                Assert.That(Vector3.Distance(camera.transform.position,game.player.transform.position),Is.GreaterThan(8),"yaw "+yaw);
            }
        }
        finally{Object.DestroyImmediate(wall);}
    }
    [Test] public void FocusCannotSeeThroughWallsOrGrassAndReleasesDeadTargets()
    {
        var e=Target(new Vector3(0,.05f,6));Lock(e);Assert.That(game.player.Focus.HasTarget,Is.False);
        game.terrain.Apply(3);game.player.Teleport(new Vector3(0,.08f,1));game.player.Focus.SetHeld(true,Vector2.zero,true);
        Assert.That(game.player.Focus.Target,Is.Null);Assert.That(game.player.Focus.CanTarget(e),Is.False);
        game.terrain.Apply(2);game.player.Teleport(new Vector3(-8,.08f,2.5f));e.Spawn(new Vector3(0,.05f,2.5f),game.player);
        Assert.That(game.player.Focus.CanTarget(e),Is.False);
        game.terrain.Apply(1);Lock(e);Assert.That(game.player.Focus.HasTarget,Is.False);e.Health.TakeDamage(10000);Assert.That(game.player.Focus.HasTarget,Is.False);
    }
    [Test] public void FocusCannotPersistAcrossPauseOrNewRun()
    {
        Lock(Target(new Vector3(0,.05f,3)));game.TogglePause();Assert.That(game.player.Focus.HasTarget,Is.False);
        game.StartSession();Assert.That(game.player.Focus.Held,Is.False);Assert.That(game.player.Focus.Target,Is.Null);
    }
    [UnityTest] public IEnumerator ShieldTurningLeavesAnActualFlankingWindow()
    {
        var e=Target(new Vector3(0,.05f,2),true);yield return new WaitForSeconds(.7f);
        game.player.Teleport(new Vector3(0,.08f,4));e.Tick(.1f);
        Assert.That(Quaternion.Angle(e.visual.rotation,Quaternion.LookRotation(Vector3.back)),Is.LessThanOrEqualTo(10.51f));
        float hp=e.Health.Current;game.projectiles.TryLaunch(new Vector3(0,.85f,4),Vector3.back,100,25,ShotProfile.Standard);
        game.projectiles.GetComponentInChildren<Projectile>().Simulate(.04f);Assert.That(e.Health.Current,Is.EqualTo(hp-25));Assert.That(e.Shield.Current,Is.EqualTo(90));
    }
    [Test] public void AudioBankHasDistinctNonSilentClipsAndBoundedPeaks()
    {
        var audio=ArenaAudio.Instance;var seen=new System.Collections.Generic.HashSet<AudioClip>();
        for(int i=0;i<System.Enum.GetValues(typeof(SoundCue)).Length;i++)
        {
            var clip=audio.Clip((SoundCue)i);Assert.That(clip,Is.Not.Null,((SoundCue)i).ToString());Assert.That(seen.Add(clip),Is.True);
            var samples=new float[clip.samples];Assert.That(clip.GetData(samples,0),Is.True);float energy=0,peak=0;foreach(float s in samples){energy+=s*s;peak=Mathf.Max(peak,Mathf.Abs(s));}
            Assert.That(energy/samples.Length,Is.GreaterThan(.0001f));Assert.That(peak,Is.LessThan(.8f));
        }
    }
    [Test] public void WeaponProfilesRouteDistinctShotSounds()
    {
        var audio=ArenaAudio.Instance;audio.PlayShot();Assert.That(audio.LastCue,Is.EqualTo(SoundCue.Pulse));
        audio.PlayShot(true);Assert.That(audio.LastCue,Is.EqualTo(SoundCue.Pierce));
        audio.PlayShot(false,true);Assert.That(audio.LastCue,Is.EqualTo(SoundCue.RicochetShot));
        audio.ResetSession();int pierce=audio.Count(SoundCue.Pierce),ricochet=audio.Count(SoundCue.RicochetShot);audio.PlayShot(true,true);
        Assert.That(audio.Count(SoundCue.Pierce),Is.EqualTo(pierce+1));Assert.That(audio.Count(SoundCue.RicochetShot),Is.EqualTo(ricochet+1));
    }
    [Test] public void ShieldHitsRouteBlockPierceReflectBreakAndLaserSounds()
    {
        var e=Target(new Vector3(0,.05f,3),true);var audio=ArenaAudio.Instance;
        e.Shield.Hit(10,false,false);Assert.That(audio.LastCue,Is.EqualTo(SoundCue.ShieldHit));
        e.Shield.Hit(10,true,false);Assert.That(audio.LastCue,Is.EqualTo(SoundCue.ShieldPierce));
        e.Shield.Hit(10,false,true);Assert.That(audio.LastCue,Is.EqualTo(SoundCue.ShieldReflect));
        game.player.weapon.Upgrade(AmmoUpgrade.Laser);game.player.weapon.beam.Evaluate(new Vector3(0,.85f,0),Vector3.forward,.1f,game.player.weapon.Loadout);
        Assert.That(audio.LastCue,Is.EqualTo(SoundCue.LaserBlock));
        e.Shield.Hit(100,true,false);Assert.That(audio.LastCue,Is.EqualTo(SoundCue.ShieldBreak));
    }
    [UnityTest] public IEnumerator LaserLoopStopsOnPauseAndAudioRateLimitPreventsFrameSpam()
    {
        var audio=ArenaAudio.Instance;game.player.weapon.Upgrade(AmmoUpgrade.Laser);
        game.player.weapon.SimulateFire(.1f,true,new Vector3(0,.85f,8));Assert.That(audio.LaserAudible,Is.True);
        game.TogglePause();Assert.That(audio.LaserAudible,Is.False);
        int count=audio.Count(SoundCue.ShieldHit);for(int i=0;i<100;i++)audio.Play(SoundCue.ShieldHit);
        Assert.That(audio.Count(SoundCue.ShieldHit),Is.EqualTo(count+1));
        audio.SetMuted(true);Assert.That(audio.Muted,Is.True);audio.SetMuted(false);yield return null;
    }
    [UnityTest] public IEnumerator ChargeTelegraphRushAndImpactHaveSeparateEvents()
    {
        var e=Target(new Vector3(0,.05f,4));e.Spawn(new Vector3(0,.05f,4),game.player,1,true);yield return new WaitForSeconds(.7f);
        var audio=ArenaAudio.Instance;e.Charge.Tick(.31f);Assert.That(audio.LastCue,Is.EqualTo(SoundCue.ChargeWarn));
        e.Charge.Tick(.9f);Assert.That(audio.LastCue,Is.EqualTo(SoundCue.ChargeRush));
        e.Charge.Tick(.5f);Assert.That(audio.Count(SoundCue.ChargeImpact),Is.GreaterThan(0));
    }
}
