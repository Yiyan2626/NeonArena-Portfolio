using System.Collections;
using NeonArena;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class Motion028Tests
{
    [UnityTest] public IEnumerator RealJumpLandingAndDashKeepRifleSeatedAndRecoverBody()
    {
        Time.timeScale=1;
        yield return SceneManager.LoadSceneAsync("BorderCity");
        yield return null;
        var game=ArenaGame.Instance;game.StartSession();game.enabled=false;game.Frontier.enabled=false;
        var p=game.player;p.enabled=false;p.Health.DamageBlocked=true;
        var camera=p.viewCamera.GetComponent<ArenaCamera>();camera.yaw=0;camera.pitch=18;camera.shakeEnabled=false;
        p.Teleport(new Vector3(0,1.91f,-10));camera.RenderView(true);
        for(float t=0;t<.4f;t+=Time.deltaTime){p.Move(Vector2.zero,Time.deltaTime);yield return null;}
        Assert.IsTrue(p.Grounded,"Test must start from a real floor contact.");
        float startHeight=p.transform.position.y,maxRise=0,maxAirBend=0,maxCompression=0,maxBrace=0;
        float worstStock=0,worstGrip=0,worstAim=0;
        bool sawAir=false,sawLanding=false;
        p.RequestJump();
        for(float t=0;t<1.6f;t+=Time.deltaTime)
        {
            p.Move(Vector2.zero,Time.deltaTime);
            p.AimAt(p.transform.position+new Vector3(0,1.4f,30));
            yield return null;
            var b=p.Motion.Model.joints;var v=p.weapon.visuals;
            maxRise=Mathf.Max(maxRise,p.transform.position.y-startHeight);
            if(!p.Grounded)
            {
                sawAir=true;
                float knee=180-Vector3.Angle(b[9].position-b[10].position,b[11].position-b[10].position);
                maxAirBend=Mathf.Max(maxAirBend,knee);
            }
            if(sawAir&&p.Grounded){sawLanding=true;maxCompression=Mathf.Max(maxCompression,p.Motion.BodyCompression);}
            worstStock=Mathf.Max(worstStock,Vector3.Distance(v.StockWorld,v.ShoulderAnchor));
            worstGrip=Mathf.Max(worstGrip,Vector3.Distance(v.RightGrip,b[8].position),Vector3.Distance(v.LeftGrip,b[5].position));
            worstAim=Mathf.Max(worstAim,Vector3.Angle(p.weapon.muzzle.forward,p.AimPoint-p.weapon.muzzle.position));
        }
        Assert.IsTrue(sawAir&&sawLanding,"Must execute an actual controller jump and floor collision.");
        Assert.That(maxRise,Is.GreaterThan(.7f));
        Assert.That(maxAirBend,Is.GreaterThan(35),"Air silhouette must visibly gather a knee.");
        Assert.That(maxCompression,Is.GreaterThan(.07f),"Impact must visibly compress the pelvis.");
        Assert.That(p.Motion.BodyCompression,Is.LessThan(.015f),"Landing must recover.");
        p.Dash.TryStart(Vector2.right);
        Assert.IsTrue(p.Dash.IsActive,"Must exercise a real directional dash.");
        for(float t=0;t<.9f;t+=Time.deltaTime)
        {
            p.Move(Vector2.zero,Time.deltaTime);p.AimAt(p.transform.position+new Vector3(0,1.4f,30));
            yield return null;
            var b=p.Motion.Model.joints;var v=p.weapon.visuals;
            maxBrace=Mathf.Max(maxBrace,p.Motion.DashBrace);
            worstStock=Mathf.Max(worstStock,Vector3.Distance(v.StockWorld,v.ShoulderAnchor));
            worstGrip=Mathf.Max(worstGrip,Vector3.Distance(v.RightGrip,b[8].position),Vector3.Distance(v.LeftGrip,b[5].position));
            worstAim=Mathf.Max(worstAim,Vector3.Angle(p.weapon.muzzle.forward,p.AimPoint-p.weapon.muzzle.position));
        }
        Debug.Log($"MOTION028 rise={maxRise:F3} knee={maxAirBend:F1} compression={maxCompression:F3} brace={maxBrace:F3} stock={worstStock:F4} grip={worstGrip:F4} aim={worstAim:F3}");
        Assert.That(maxBrace,Is.GreaterThan(.7f));
        Assert.That(p.Motion.DashBrace,Is.LessThan(.02f),"Dash must blend back to locomotion.");
        Assert.That(worstStock,Is.LessThan(.015f));
        Assert.That(worstGrip,Is.LessThan(.10f));
        Assert.That(worstAim,Is.LessThan(1));
    }
}
