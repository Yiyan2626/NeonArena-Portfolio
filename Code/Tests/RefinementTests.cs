using System.Collections;
using System.Linq;
using NeonArena;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class RefinementTests
{
    ArenaGame game;
    [UnitySetUp] public IEnumerator Setup()
    {
        Time.timeScale=1;yield return SceneManager.LoadSceneAsync("SatelliteCity");yield return null;
        game=ArenaGame.Instance;game.StartSession();game.autoSpawn=false;game.enabled=false;game.player.enabled=false;
        foreach(var relay in Object.FindObjectsOfType<CityRelay>())relay.enabled=false;
    }
    [UnityTearDown] public IEnumerator Cleanup(){Time.timeScale=1;yield return null;}
    [Test] public void ImportedDecksRetainMetreScaleAndCoordinateConversion()
    {
        var root=GameObject.Find("Satellite service architecture 0.13");Assert.That(root,Is.Not.Null);
        var renderers=root.GetComponentsInChildren<Renderer>();
        var skywalk=renderers.First(r=>r.name=="Skywalk");
        Assert.That(skywalk.bounds.size.z,Is.InRange(26,27));Assert.That(skywalk.bounds.size.y,Is.InRange(1,2));
        var cabinet=Object.FindObjectsOfType<CityRelay>().First(r=>r.index==1).GetComponentInChildren<Renderer>();
        Assert.That(cabinet.bounds.size.y,Is.InRange(1,1.3f));
    }
    [UnityTest] public IEnumerator EveryRampCanBeWalkedInBothDirections()
    {
        var ramps=Object.FindObjectsOfType<BoxCollider>().Where(c=>c.name.Contains("access")||c.name.Contains("indoor ascent")||c.name=="West arcade approach"||c.name=="West mezzanine return").ToArray();
        Assert.That(ramps.Length,Is.EqualTo(8));
        foreach(var ramp in ramps)foreach(int sign in new[]{-1,1})
        {
            var t=ramp.transform;Vector3 start=t.TransformPoint(new Vector3(0,.5f,-.46f*sign))+Vector3.up*.04f;
            Vector3 end=t.TransformPoint(new Vector3(0,.5f,.46f*sign));
            game.player.Teleport(start);
            for(int step=0;step<320;step++)
            {
                Vector3 delta=Vector3.ProjectOnPlane(end-game.player.transform.position,Vector3.up);
                if(delta.magnitude<.5f)break;
                var direction=delta.normalized;
                game.player.Move(new Vector2(Vector3.Dot(direction,game.player.GetMoveDirection(Vector2.right)),Vector3.Dot(direction,game.player.GetMoveDirection(Vector2.up))),.02f);
                if(step%30==0)yield return null;
            }
            Assert.That(Vector3.Distance(end,game.player.transform.position),Is.LessThan(.85f),ramp.name+" direction="+sign+" actual="+game.player.transform.position);
        }
    }
    [Test] public void RelayRequiresHoldFreezesInPauseAndRespectsWallsAndFloors()
    {
        var relay=Object.FindObjectsOfType<CityRelay>().First(r=>r.index==1);
        game.player.Teleport(relay.transform.position+Vector3.forward*2);
        relay.Step(.6f,true);Assert.That(relay.Charge,Is.GreaterThan(.5f));Assert.That(relay.Armed,Is.False);
        relay.Step(.3f,false);Assert.That(relay.Charge,Is.Zero);
        relay.Step(1.2f,true);Assert.That(relay.Armed,Is.True);
        game.TogglePause();float fuse=relay.Fuse;relay.Step(10,true);Assert.That(relay.Fuse,Is.EqualTo(fuse));game.TogglePause();
        Vector3 target=relay.transform.position+Vector3.forward*3;
        Assert.That(relay.CanAffect(target,11),Is.True);Assert.That(relay.CanAffect(target+Vector3.up*5,11),Is.False);
        var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.layer=9;wall.transform.position=relay.transform.position+Vector3.forward*1.5f+Vector3.up;wall.transform.localScale=new Vector3(3,3,.3f);Physics.SyncTransforms();
        try{Assert.That(relay.CanAffect(target,11),Is.False);}finally{Object.DestroyImmediate(wall);}
        game.player.Teleport(relay.transform.position+Vector3.forward*7);relay.Step(2.1f,false);Assert.That(relay.Spent,Is.True);Assert.That(relay.Armed,Is.False);
        relay.Step(5,true);Assert.That(relay.Spent,Is.True);
    }
    [UnityTest] public IEnumerator RobotFootworkChangesAndGunHandRemainsAttached()
    {
        var motion=game.player.Motion;motion.enabled=false;var model=motion.Model;
        Vector3 initial=model.joints[11].position-game.player.transform.position;
        for(int i=0;i<12;i++){game.player.Move(Vector2.right,.016f);motion.Tick(.016f);yield return null;}
        Vector3 after=model.joints[11].position-game.player.transform.position;
        Assert.That(Vector3.Distance(initial,after),Is.GreaterThan(.04f));
        Vector3 grip=game.player.weapon.muzzle.position-game.player.visual.forward*.62f-game.player.visual.right*.06f+Vector3.up*.03f;
        Assert.That(Vector3.Distance(grip,model.joints[8].position),Is.LessThan(.18f));
        game.player.RequestJump();game.player.Move(Vector2.zero,.02f);motion.Tick(.02f);
        Assert.That(motion.State,Is.EqualTo(MotionState.Jump));
        Assert.That(float.IsNaN(model.joints[10].rotation.x),Is.False);
    }
}
