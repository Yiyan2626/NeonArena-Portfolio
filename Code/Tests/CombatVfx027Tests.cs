using System.Collections;
using NeonArena;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class CombatVfx027Tests
{
    ArenaGame game;CombatVfx027 fx;
    [UnitySetUp] public IEnumerator Setup()
    {
        Time.timeScale=1;yield return SceneManager.LoadSceneAsync("BorderCity");yield return null;
        game=ArenaGame.Instance;game.StartSession();game.enabled=false;game.Frontier.enabled=false;game.player.enabled=false;
        foreach(var enemy in Object.FindObjectsOfType<EnemyController>())enemy.gameObject.SetActive(false);
        fx=CombatVfx027.Instance;Assert.That(fx,Is.Not.Null);fx.Clear();
    }
    [UnityTearDown] public IEnumerator Cleanup(){Time.timeScale=1;if(fx)fx.Clear();yield return null;}
    [Test] public void RepeatedCombatUsesBoundedGeometryAndSessionClearRemovesEveryLayer()
    {
        int children=fx.transform.childCount;Vector3 at=new Vector3(0,40,0);
        for(int i=0;i<200;i++)
        {
            fx.Travel(at,at+Vector3.forward,Vector3.forward,Color.cyan,0);
            fx.Impact(at,Vector3.back,Vector3.forward,Color.cyan,1,true);
            fx.Laser(at,at+Vector3.forward*20);
            fx.Death(at,Vector3.forward,false);
        }
        Assert.That(fx.transform.childCount,Is.EqualTo(children),"No per-shot object creation");
        Assert.That(fx.ActiveRibbons,Is.LessThanOrEqualTo(CombatVfx027.RibbonCapacity));
        Assert.That(fx.ActiveParticles,Is.LessThanOrEqualTo(1252));
        Assert.That(fx.BouncesPresented,Is.EqualTo(200));Assert.That(fx.DeathsPresented,Is.EqualTo(200));
        ImpactPool.Instance.Clear();
        Assert.That(fx.ActiveRibbons,Is.Zero);Assert.That(fx.ActiveParticles,Is.Zero);
        Assert.That(fx.BouncesPresented,Is.Zero);Assert.That(fx.DeathsPresented,Is.Zero);
    }
    [UnityTest] public IEnumerator PausePreventsEmissionAndResumesExistingEffects()
    {
        Vector3 at=new Vector3(0,40,0);fx.Laser(at,at+Vector3.forward*15);
        game.TogglePause();yield return null;
        int ribbons=fx.ActiveRibbons,particles=fx.ActiveParticles,pulses=fx.LaserPulses;
        fx.Laser(at,at+Vector3.forward*15);fx.Impact(at,Vector3.up,Vector3.forward,Color.white,2,false);
        yield return new WaitForSecondsRealtime(.18f);
        Assert.That(fx.ActiveRibbons,Is.EqualTo(ribbons));Assert.That(fx.ActiveParticles,Is.EqualTo(particles));Assert.That(fx.LaserPulses,Is.EqualTo(pulses));
        game.TogglePause();yield return new WaitForSeconds(.3f);
        Assert.That(fx.ActiveRibbons,Is.Zero);
    }
}
