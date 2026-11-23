using System.Collections;
using NeonArena;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class Motion027Tests
{
    [UnityTest] public IEnumerator RifleButtStaysAtRightShoulderDuringMovementPitchAndRecoil()
    {
        Time.timeScale=1;
        yield return SceneManager.LoadSceneAsync("BorderCity");
        yield return null;
        var game=ArenaGame.Instance;game.StartSession();game.enabled=false;game.Frontier.enabled=false;
        var p=game.player;p.enabled=false;p.Health.DamageBlocked=true;
        var camera=p.viewCamera.GetComponent<ArenaCamera>();camera.yaw=0;camera.pitch=18;camera.shakeEnabled=false;
        p.Teleport(new Vector3(0,1.91f,-10));camera.RenderView(true);
        for(int frame=0;frame<12;frame++){p.Move(Vector2.zero,Time.deltaTime);yield return null;}
        float worstStock=0,worstGrip=0,worstAim=0,worstPocket=0,worstWristStep=0;
        Quaternion previousLeft=Quaternion.identity,previousRight=Quaternion.identity;
        var directions=new[]{Vector2.up,Vector2.right,Vector2.down,Vector2.left};
        int startShots=p.weapon.ShotsFired;
        for(int frame=0;frame<128;frame++)
        {
            p.Move(directions[frame/32],Time.deltaTime);
            float elevation=Mathf.Sin(frame*Mathf.PI/64)*8;
            p.AimAt(p.transform.position+new Vector3(0,1.4f+elevation,30));
            if(frame%9==0)p.weapon.SimulateFire(.2f,true,p.AimPoint);
            yield return null;
            var v=p.weapon.visuals;var b=p.Motion.Model.joints;
            Quaternion left=Quaternion.Inverse(v.aimPivot.rotation)*b[5].rotation;
            Quaternion right=Quaternion.Inverse(v.aimPivot.rotation)*b[8].rotation;
            if(frame>0)worstWristStep=Mathf.Max(worstWristStep,Quaternion.Angle(previousLeft,left),Quaternion.Angle(previousRight,right));
            previousLeft=left;previousRight=right;
            worstStock=Mathf.Max(worstStock,Vector3.Distance(v.StockWorld,v.ShoulderAnchor));
            worstPocket=Mathf.Max(worstPocket,Vector3.Distance(v.StockWorld,b[6].position));
            worstGrip=Mathf.Max(worstGrip,Vector3.Distance(v.RightGrip,b[8].position),Vector3.Distance(v.LeftGrip,b[5].position));
            worstAim=Mathf.Max(worstAim,Vector3.Angle(p.weapon.muzzle.forward,p.AimPoint-p.weapon.muzzle.position));
        }
        Debug.Log("MOTION027 stock="+worstStock+" shoulderPocket="+worstPocket+" grip="+worstGrip+" aimDegrees="+worstAim);
        Assert.That(p.weapon.ShotsFired,Is.GreaterThan(startShots),"Must exercise real recoil.");
        Debug.Log("MOTION027 wristStep="+worstWristStep);
        Assert.That(worstWristStep,Is.LessThan(20),"Holding a steady rifle must not introduce isolated wrist roll flips.");
        Assert.That(worstStock,Is.LessThan(.015f),"The physical butt back face must remain seated.");
        Assert.That(worstPocket,Is.LessThan(.13f),"Stock anchor must be anatomically near the right shoulder, not a free target.");
        Assert.That(worstGrip,Is.LessThan(.10f),"Shouldering must not break either hand's reach.");
        Assert.That(worstAim,Is.LessThan(1),"Seating the stock must preserve the actual muzzle ray.");
    }
}
