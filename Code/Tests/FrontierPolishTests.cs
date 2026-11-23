using System.Collections;
using NeonArena;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
public class FrontierPolishTests
{
    ArenaGame game;
    [UnitySetUp]public IEnumerator Setup()
    {Time.timeScale=1;yield return SceneManager.LoadSceneAsync("BorderCity");yield return null;game=ArenaGame.Instance;game.StartSession();game.enabled=false;game.Frontier.enabled=false;game.player.enabled=false;game.player.Health.DamageBlocked=true;}
    [UnityTest]public IEnumerator AnimatedBodyCannotRunAwayFromControllerOrFloatDuringLocomotion()
    {
        var p=game.player;
        // A locomotion sample needs a grounded controller. A disabled, freshly
        // spawned controller otherwise presents as a stationary airborne apex.
        p.Teleport(new Vector3(0,1.91f,-10));
        // Also allow the real impact pose to finish before choosing a rest pose.
        for(float t=0;t<1f;t+=Time.deltaTime){p.Move(Vector2.zero,Time.deltaTime);yield return null;}
        Assert.IsTrue(p.Grounded,"Locomotion fixture must settle on the actual floor.");
        var m=p.Motion.Model;var a=m.GetComponent<Animator>();p.Motion.enabled=false;
        var retarget=new RetargetedMotion(m,p);Vector3 rest=m.joints[0].localPosition;float maxFootTravel=0;
        foreach(var clip in new[]{"Walk","Run","Sprint","Jump","Fall","Idle"})
        {
            a.Play(clip,0,0);a.Update(0);
            for(int i=0;i<180;i++)
            {
                a.Update(1f/60);retarget.Evaluate(1f/60,clip=="Idle"?Vector3.zero:p.visual.forward*7,clip=="Sprint"?MotionState.Dash:MotionState.Run);
                if(clip=="Run")maxFootTravel=Mathf.Max(maxFootTravel,Mathf.Abs(m.transform.InverseTransformPoint(m.joints[11].position).z));
                Vector3 pose=m.joints[0].localPosition;
                Assert.That(Vector2.Distance(new Vector2(pose.x,pose.z),new Vector2(rest.x,rest.z)),Is.LessThan(.04f),clip+" hips escaped root");
                Assert.That(Mathf.Abs(pose.y-rest.y),Is.LessThan(.14f),clip+" motion overrides physical jump height");
            }
        }
        Assert.That(maxFootTravel,Is.GreaterThan(.20f),"Locomotion must actually animate the legs, not freeze in bind pose");
        yield return null;
    }
    [Test]public void FrontierCameraNeverChangesPitchToAvoidParkRamp()
    {
        var c=game.player.viewCamera.GetComponent<ArenaCamera>();c.pitch=26;c.yaw=180;
        foreach(float z in new[]{-27f,-25f,-22f,-19f,-16f,-13f,-11f})
        {
            float y=Mathf.Lerp(.12f,1.91f,Mathf.InverseLerp(-27,-11,z));game.player.Teleport(new Vector3(0,y,z));c.RenderView(true);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(c.transform.eulerAngles.x,26)),Is.LessThan(.1f),"Ramp changed view to overhead");
            Assert.That(Vector3.Distance(c.transform.position,game.player.transform.position),Is.LessThan(12));
        }
        c.pitch=-50;c.RenderView(true);Assert.That(c.transform.forward.y,Is.GreaterThan(.7f),"Cannot aim above horizon");
    }
    [Test]public void RifleGripsRemainReachableWhenAimingAcrossTheHorizonAndUpwards()
    {
        var p=game.player;var m=p.Motion.Model;var a=m.GetComponent<Animator>();p.Motion.enabled=false;var retarget=new RetargetedMotion(m,p);
        foreach(float degrees in new[]{-20f,0f,35f,65f})
        foreach(string clip in new[]{"Idle","Run"})for(int sample=0;sample<12;sample++)
        {
            var direction=Quaternion.AngleAxis(-degrees,p.visual.right)*p.visual.forward;
            p.AimAt(p.transform.position+Vector3.up*1.28f+direction*30);a.Play(clip,0,sample/12f);a.Update(0);retarget.Evaluate(.016f,clip=="Run"?p.visual.forward*7:Vector3.zero,MotionState.Run);
            Assert.That(Vector3.Distance(m.joints[8].position,p.weapon.visuals.RightGrip),Is.LessThan(.06f),"Right hand lost rifle at "+degrees);
            Assert.That(Vector3.Distance(m.joints[5].position,p.weapon.visuals.LeftGrip),Is.LessThan(.08f),"Left hand cannot reach foregrip at "+degrees);
        }
    }
    [Test]public void RampVehicleIsCompleteAndOutsideWalkingLaneAndFountainCardsAreInactive()
    {
        var city=GameObject.Find("Satellite City / PLAYABLE original architecture");MeshRenderer car=null;
        foreach(var r in city.GetComponentsInChildren<MeshRenderer>(true))
        {
            if(r.name=="mesh_361.001")car=r;
            if(r.sharedMaterial.name=="CityMat_145"&&Mathf.Abs(r.bounds.center.x)<5&&Mathf.Abs(r.bounds.center.z)<3)Assert.That(r.gameObject.activeSelf,Is.False);
        }
        Assert.That(car,Is.Not.Null);Assert.That(car.bounds.size.x,Is.GreaterThan(2.5f));Assert.That(car.bounds.min.x,Is.GreaterThan(5));Assert.That(car.bounds.min.y,Is.GreaterThanOrEqualTo(.035f));
        Assert.That(Object.FindObjectOfType<CityFountain>(),Is.Not.Null);
    }
    [UnityTest]public IEnumerator GuidanceAndTexturedPoolsExistAndProduceEffects()
    {
        yield return null;var guide=game.hud.GetComponent<FrontierGuidance>();Assert.That(guide,Is.Not.Null);
        Assert.That(guide.Label,Is.EqualTo(game.Frontier.encounters[0].name));Assert.That(guide.TerminalAvailable(0),Is.False);
        var fx=FrontierCombatFx.Instance;Assert.That(fx,Is.Not.Null);fx.Shot(game.player.weapon.muzzle,-1);fx.DashStart(game.player.transform.position,Vector3.forward);
        yield return null;
        Assert.That(fx.ShotsPresented,Is.GreaterThan(0));Assert.That(fx.DashBursts,Is.GreaterThan(0));
        int particles=0;foreach(var ps in fx.GetComponentsInChildren<ParticleSystem>()){Assert.That(ps.GetComponent<ParticleSystemRenderer>().sharedMaterial.mainTexture,Is.Not.Null);particles+=ps.particleCount;}
        Assert.That(particles,Is.GreaterThan(10));
    }
}
