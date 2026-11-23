using System.Collections;
using NeonArena;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class Motion026Tests
{
    ArenaGame game;
    [UnitySetUp] public IEnumerator Setup()
    {
        Time.timeScale=1;
        yield return SceneManager.LoadSceneAsync("BorderCity");
        yield return null;
        game=ArenaGame.Instance;game.StartSession();game.enabled=false;game.Frontier.enabled=false;
        game.player.enabled=false;game.player.Health.DamageBlocked=true;
        var camera=game.player.viewCamera.GetComponent<ArenaCamera>();camera.yaw=0;camera.pitch=18;camera.shakeEnabled=false;
        game.player.Teleport(new Vector3(0,1.91f,-10));
        game.player.AimAt(game.player.transform.position+Vector3.forward*30+Vector3.up*1.4f);
        camera.RenderView(true);
        for(int frame=0;frame<12;frame++){game.player.Move(Vector2.zero,Time.deltaTime);yield return null;}
    }

    [UnityTest] public IEnumerator ContinuousDirectionsJumpDashAndFireKeepBothHandsOnTheRifle()
    {
        var p=game.player;var bones=p.Motion.Model.joints;
        float worstGrip=0,worstAim=0;int samples=0,airborne=0;float distance=p.DistanceTravelled;
        foreach(var input in new[]{Vector2.up,Vector2.right,Vector2.down,Vector2.left,Vector2.zero})
        {
            if(input==Vector2.zero)p.RequestJump();
            for(int frame=0;frame<32;frame++)
            {
                p.Move(input,Time.deltaTime);
                p.AimAt(p.transform.position+Vector3.forward*30+Vector3.up*1.4f);
                if(frame%8==0)p.weapon.SimulateFire(.2f,true,p.AimPoint);
                // Resume on the next frame after the previous frame's animation and
                // LateUpdate. WaitForEndOfFrame never resumes in Unity batchmode.
                yield return null;
                Sample(p,ref worstGrip,ref worstAim);samples++;
                if(!p.Grounded)airborne++;
            }
        }
        Assert.That(p.Dash.TryStart(Vector2.right),Is.True);
        for(int frame=0;frame<24;frame++)
        {
            p.Move(Vector2.zero,Time.deltaTime);
            p.AimAt(p.transform.position+Vector3.forward*30+Vector3.up*1.4f);
            yield return null;
            Sample(p,ref worstGrip,ref worstAim);samples++;
        }
        Assert.That(p.DistanceTravelled-distance,Is.GreaterThan(1),"The test must actually move the collision controller.");
        Assert.That(airborne,Is.GreaterThan(0),"The sequence must enter an actual jump.");
        Assert.That(p.weapon.ShotsFired,Is.GreaterThan(0),"The sequence must fire real weapon logic.");
        Assert.That(worstGrip,Is.LessThan(.12f),"Rifle grip error during continuous movement, jump, dash or recoil.");
        Assert.That(worstAim,Is.LessThan(1),"The visible muzzle must point at the actual aim target after pose correction.");
        Debug.Log("MOTION026 samples="+samples+" maxGrip="+worstGrip+" maxAimDegrees="+worstAim+" airborne="+airborne);
    }
    static void Sample(PlayerController p,ref float grip,ref float aim)
    {
        var b=p.Motion.Model.joints;var v=p.weapon.visuals;
        grip=Mathf.Max(grip,Vector3.Distance(b[5].position,v.LeftGrip),Vector3.Distance(b[8].position,v.RightGrip));
        aim=Mathf.Max(aim,Vector3.Angle(p.weapon.muzzle.forward,p.AimPoint-p.weapon.muzzle.position));
        Assert.That(float.IsNaN(b[10].rotation.x)||float.IsNaN(b[13].rotation.x),Is.False,"The knee solver produced a non-finite pose.");
        Assert.That(p.Motion.Model.transform.InverseTransformPoint(b[11].position).x,Is.LessThan(.06f),"Left foot crossed the body centre.");
        Assert.That(p.Motion.Model.transform.InverseTransformPoint(b[14].position).x,Is.GreaterThan(-.06f),"Right foot crossed the body centre.");
    }

    [Test] public void ReachSolverStaysFiniteWhenPoleAndArmAreParallel()
    {
        var root=new GameObject("Reach pole singularity");
        try
        {
            var elbow=new GameObject("elbow").transform;elbow.SetParent(root.transform);elbow.localPosition=Vector3.right*.3f;
            var hand=new GameObject("hand").transform;hand.SetParent(elbow);hand.localPosition=Vector3.right*.3f;
            var target=new Vector3(.45f,0,0);
            RobotLocomotion.Solve(root.transform,elbow,hand,target,Vector3.right);
            Assert.That(Vector3.Distance(hand.position,target),Is.LessThan(.001f));
            Assert.That(Vector3.Distance(root.transform.position,elbow.position),Is.EqualTo(.3f).Within(.001f));
            Assert.That(Vector3.Distance(elbow.position,hand.position),Is.EqualTo(.3f).Within(.001f));
        }
        finally{Object.DestroyImmediate(root);}
    }
}
