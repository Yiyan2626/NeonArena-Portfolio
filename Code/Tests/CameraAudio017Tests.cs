using System.Collections;
using NeonArena;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class CameraAudio017Tests
{
    ArenaGame game;
    [UnitySetUp]public IEnumerator Setup()
    {
        Time.timeScale=1;yield return SceneManager.LoadSceneAsync("BorderCity");yield return null;
        game=ArenaGame.Instance;game.StartSession();game.enabled=false;game.Frontier.enabled=false;game.player.enabled=false;
        game.player.Health.DamageBlocked=true;
    }
    [UnityTest]public IEnumerator WalkingSurfacesStayVisibleAtAllFourParkRamps()
    {
        var camera=game.player.viewCamera.GetComponent<ArenaCamera>();camera.enabled=false;
        foreach(string name in new[]{"Park south access","Park north access","Park east access","Park west access"})
        {
            var ramp=GameObject.Find(name);Assert.That(ramp,Is.Not.Null,name);
            var collider=ramp.GetComponent<Collider>();var cut=ramp.GetComponent<CameraOccluder>();
            foreach(float along in new[]{-.25f,0f,.25f})
            {
                Vector3 sample=collider.bounds.center+ramp.transform.forward*(ramp.transform.localScale.z*along);
                Assert.That(collider.Raycast(new Ray(sample+Vector3.up*10,Vector3.down),out var ground,20),Is.True);
                game.player.Teleport(ground.point+Vector3.up*.08f);
                Physics.SyncTransforms();
                foreach(float pitch in new[]{18f,26f})
                {
                    camera.yaw=ramp.transform.eulerAngles.y+180;camera.pitch=pitch;camera.RenderView(true);
                    cut.Reveal();yield return null;camera.RenderView(false);yield return null;
                    Assert.That(cut.SupportingPlayer,Is.True,name);
                    Assert.That(cut.Fade,Is.EqualTo(1).Within(.001f),"Standing ramp was faded: "+name);
                    var deck=GameObject.Find(name+" / engineered deck");Assert.That(deck,Is.Not.Null,name+" refined model missing");
                    int visibleParts=0;
                    foreach(var renderedPart in deck.GetComponentsInChildren<Renderer>())if(renderedPart.enabled)
                    {
                        visibleParts++;Assert.That(renderedPart.forceRenderingOff,Is.False,name+" rendered child vanished");
                        var childCut=renderedPart.GetComponent<CameraOccluder>();
                        if(childCut){Assert.That(childCut.SupportingPlayer,Is.True);Assert.That(childCut.Fade,Is.EqualTo(1).Within(.001f));}
                    }
                    Assert.That(visibleParts,Is.GreaterThan(0),name+" must check actual refined mesh, not only the hidden collider");
                    var feet=game.player.viewCamera.WorldToViewportPoint(ground.point);
                    Assert.That(feet.z,Is.GreaterThan(0));Assert.That(feet.y,Is.InRange(.02f,.95f),name+" feet out of frame");
                    Assert.That(Mathf.Abs(Mathf.DeltaAngle(camera.transform.eulerAngles.x,pitch)),Is.LessThan(.1f));
                }
            }
        }
    }
    [UnityTest]public IEnumerator UnrelatedWallStillCutsAwayAndRestoresAfterRequestExpires()
    {
        var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=new Vector3(200,4,200);
        var cut=wall.AddComponent<CameraOccluder>();cut.Reveal();yield return null;
        Assert.That(cut.Fade,Is.LessThan(1));Assert.That(cut.SupportingPlayer,Is.False);
        yield return new WaitForSecondsRealtime(.5f);Assert.That(cut.Fade,Is.EqualTo(1).Within(.01f));
        Object.Destroy(wall);
    }
    [Test]public void ReferenceShotsHaveImmediateAttackAndSingleShotLengths()
    {
        foreach(var cue in new[]{SoundCue.Pulse,SoundCue.Pierce,SoundCue.RicochetShot,SoundCue.LaserStart})
        {
            var clip=ArenaAudio.Instance.Clip(cue);Assert.That(clip,Is.Not.Null);
            Assert.That(clip.length,Is.InRange(.12f,.6f),"Reference's second shot or silence must not be replayed");
            var data=new float[clip.samples*clip.channels];Assert.That(clip.GetData(data,0),Is.True);
            float energy=0,peak=0;int n=Mathf.Min(data.Length,(int)(clip.frequency*.05f)*clip.channels);
            for(int i=0;i<data.Length;i++){peak=Mathf.Max(peak,Mathf.Abs(data[i]));if(i<n)energy+=data[i]*data[i];}
            Assert.That(Mathf.Sqrt(energy/n),Is.GreaterThan(.18f),cue+" soft attack");Assert.That(peak,Is.LessThan(.8f));
        }
    }
    [Test]public void DenseHitEffectsCannotStealThePlayersGunVoice()
    {
        var audio=ArenaAudio.Instance;audio.ResetSession();audio.PlayShot();
        foreach(SoundCue cue in System.Enum.GetValues(typeof(SoundCue)))
            if(cue!=SoundCue.Pulse&&cue!=SoundCue.Pierce&&cue!=SoundCue.RicochetShot&&cue!=SoundCue.LaserStart)audio.Play(cue,null,.05f);
        bool retained=false;
        foreach(var voice in audio.GetComponents<AudioSource>())if(voice.clip==audio.Clip(SoundCue.Pulse)&&voice.isPlaying)retained=true;
        Assert.That(retained,Is.True,"Weapon's transient was stolen by impacts");
        audio.ResetSession();
    }
}
