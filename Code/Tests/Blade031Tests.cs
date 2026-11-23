using System.Collections;
using NeonArena;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class Blade031Tests
{
    [UnityTest] public IEnumerator CentralHandleRemainsHeldAcrossRealKillsMovementAndRifleReturn()
    {
        Time.timeScale=1;
        yield return SceneManager.LoadSceneAsync("BorderCity");yield return null;
        var game=ArenaGame.Instance;game.StartSession();game.enabled=false;game.Frontier.enabled=false;game.autoSpawn=false;
        foreach(var enemy in game.Enemies)enemy.gameObject.SetActive(false);
        game.Arrival.Clear();
        var p=game.player;p.enabled=false;p.Overheat.enabled=false;p.Health.DamageBlocked=true;
        var heat=p.Overheat;var camera=p.viewCamera.GetComponent<ArenaCamera>();camera.shakeEnabled=false;camera.yaw=0;
        // This is the same real outdoor lane used by the framebuffer A/B. Ground
        // both ends independently; copying player height into another floor is invalid.
        Assert.IsTrue(ArenaTerrain.TryGround(new Vector3(0,2,-13),out var origin,12));
        Assert.IsTrue(ArenaTerrain.TryGround(new Vector3(0,2,-8),out var targetGround,12));
        p.Teleport(origin);p.visual.rotation=Quaternion.identity;
        for(int i=0;i<12;i++){p.Move(Vector2.zero,Time.deltaTime);yield return null;}
        var target=game.SpawnAuthored(targetGround,1,false,false,false,false);Assert.IsNotNull(target);target.enabled=false;
        target.Arrival.Step(EnemySpawnPresentation.Duration+.01f);Physics.SyncTransforms();
        for(int i=0;i<7;i++)heat.RegisterKill();heat.Tick(.001f);Assert.IsFalse(heat.Active);
        heat.RegisterKill();heat.Tick(.001f);Assert.IsTrue(heat.Active);game.Cinema.ResetCinema();
        yield return null;
        var b=p.Motion.Model.joints;
        Quaternion leftRelation=Quaternion.Inverse(heat.BladeTransform.rotation)*b[5].rotation;
        Quaternion rightRelation=Quaternion.Inverse(heat.BladeTransform.rotation)*b[8].rotation;
        float worstGrip=0,worstRelative=0,closestEdge=100;
        void CheckGrip()
        {
            worstGrip=Mathf.Max(worstGrip,Vector3.Distance(heat.LeftGripWorld,heat.LeftPalmWorld),Vector3.Distance(heat.RightGripWorld,heat.RightPalmWorld));
            worstRelative=Mathf.Max(worstRelative,
                Quaternion.Angle(leftRelation,Quaternion.Inverse(heat.BladeTransform.rotation)*b[5].rotation),
                Quaternion.Angle(rightRelation,Quaternion.Inverse(heat.BladeTransform.rotation)*b[8].rotation));
            for(int arm=3;arm<=6;arm+=3)
            {
                float angle=Vector3.Angle(b[arm].position-b[arm+1].position,b[arm+2].position-b[arm+1].position);
                Assert.That(angle,Is.InRange(12f,169f),"Arm must keep a bent, finite elbow rather than lock straight or fold shut.");
            }
        }
        // These frames exercise the real Animator and moving pelvis, not a static IK call.
        for(int frame=0;frame<32;frame++)
        {
            p.Move(frame<16?Vector2.right:Vector2.left,Time.deltaTime);heat.Tick(Time.deltaTime);
            yield return null;CheckGrip();
        }
        for(int style=0;style<3;style++)
        {
            if(style>0){target.Spawn(targetGround,p,1,false,false);target.Arrival.Step(EnemySpawnPresentation.Duration+.01f);}
            Physics.SyncTransforms();
            Assert.IsTrue(heat.CanExecute(target),$"Real lane target must be visible and in range. player={p.transform.position} target={target.transform.position} alive={target.IsAlive} sight={p.Focus.CanTarget(target)}");
            Assert.IsTrue(heat.TryLanding(target,out _),"Real lane must have a grounded, unoccupied blink landing.");
            Assert.IsTrue(heat.TryExecute(target),$"Attack gates: active={heat.Active} striking={heat.Striking} cooldown={heat.Cooldown} playing={game.IsPlaying} intro={game.Cinema.IntroActive} finale={game.Cinema.FinaleActive}");Assert.AreEqual(style,heat.Style);
            Assert.IsFalse(heat.TryExecute(target),"A second input cannot bypass the active strike.");game.Cinema.ResetCinema();
            closestEdge=100;
            for(int frame=0;frame<40;frame++)
            {
                heat.Tick(1f/120);yield return null;CheckGrip();
                if(frame==5)Assert.IsTrue(target.IsAlive,"Gameplay damage must not happen before .06 s.");
                if(frame==7)
                {
                    Assert.IsFalse(target.IsAlive,"The real hit must kill a normal enemy at .06 s.");
                    Vector3 tip=heat.BladeTransform.TransformPoint(new Vector3(0,0,2.25f));
                    closestEdge=Vector3.Distance(tip,target.transform.position+Vector3.up*1.15f);
                    Assert.That(closestEdge,Is.LessThan(.72f),"Visible cutting head must pass the victim at the damage frame.");
                }
            }
            Assert.AreEqual(style+1,heat.Executions,"One real health death per strike.");
        }
        heat.Cancel();game.Cinema.ResetCinema();
        for(int frame=0;frame<30;frame++)
        {
            p.Move(Vector2.zero,Time.deltaTime);p.AimAt(p.transform.position+new Vector3(0,1.4f,30));yield return null;
        }
        Assert.IsFalse(heat.BladeTransform.gameObject.activeSelf);
        Assert.That(Vector3.Distance(p.weapon.visuals.RightGrip,b[8].position),Is.LessThan(.10f));
        Assert.That(Vector3.Distance(p.weapon.visuals.LeftGrip,b[5].position),Is.LessThan(.10f));
        Assert.That(worstGrip,Is.LessThan(.05f),"Both palm contacts must stay within 5 cm of the actual central handle.");
        Assert.That(worstRelative,Is.LessThan(.1f),"Neither wrist may roll independently of its rigid handle.");
        Assert.That(PlayerOverheat.Duration,Is.EqualTo(10));Assert.That(PlayerOverheat.StrikeDuration,Is.EqualTo(.24f));Assert.That(PlayerOverheat.StrikeInterval,Is.EqualTo(.30f));
        Debug.Log($"BLADE031 grip={worstGrip:F5} relativeWrist={worstRelative:F3} lastImpactDistance={closestEdge:F3} executions={heat.Executions}");
        // Expiry must exercise the same release path without an explicit Cancel.
        target.Spawn(targetGround,p,1,false,false);target.Arrival.Step(EnemySpawnPresentation.Duration+.01f);Physics.SyncTransforms();
        for(int i=0;i<8;i++)heat.RegisterKill();heat.Tick(.001f);game.Cinema.ResetCinema();
        Assert.IsTrue(heat.Active);yield return null;CheckGrip();
        heat.Tick(PlayerOverheat.Duration);
        for(int i=0;i<30;i++){p.AimAt(p.transform.position+new Vector3(0,1.4f,30));yield return null;}
        Assert.IsFalse(heat.Active);Assert.IsFalse(heat.Striking);Assert.IsFalse(heat.BladeTransform.gameObject.activeSelf);
        Assert.That(Vector3.Distance(p.weapon.visuals.RightGrip,b[8].position),Is.LessThan(.10f));
        Assert.That(Vector3.Distance(p.weapon.visuals.LeftGrip,b[5].position),Is.LessThan(.10f));
        for(int i=0;i<8;i++)heat.RegisterKill();heat.Tick(.001f);game.Cinema.ResetCinema();
        Assert.IsTrue(heat.Active);yield return null;
        game.StartSession();yield return null;
        Assert.IsFalse(heat.Active);Assert.IsFalse(heat.Striking);Assert.IsFalse(heat.BladeTransform.gameObject.activeSelf);
        Assert.AreEqual(0,heat.Charge);Assert.AreEqual(0,heat.Executions);
    }
}

