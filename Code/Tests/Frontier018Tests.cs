using System.Collections;
using System.Linq;
using NeonArena;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
public class Frontier018Tests
{
    ArenaGame game;
    [UnitySetUp]public IEnumerator Setup()
    {Time.timeScale=1;yield return SceneManager.LoadSceneAsync("BorderCity");yield return null;game=ArenaGame.Instance;game.StartSession();game.enabled=false;game.Frontier.enabled=false;game.player.enabled=false;game.player.Health.DamageBlocked=true;yield return null;}
    EnemyController Spawn(Vector3 p,bool shield=false,bool ranged=false)
    {var e=game.SpawnAuthored(p,1,false,shield,false,ranged);e.enabled=false;e.visual.rotation=Quaternion.Euler(0,180,0);Physics.SyncTransforms();e.RefreshVisibility();return e;}
    [Test]public void RuinsAreTexturedOutsidePlaySpaceAndBoundaryStopsTraversal()
    {
        var root=GameObject.Find("Outer quarantine / Ruins018");Assert.That(root,Is.Not.Null);
        var blocks=root.GetComponentsInChildren<LODGroup>();Assert.That(blocks.Length,Is.EqualTo(10));
        foreach(var b in blocks)
        {
            Assert.That(b.GetComponentsInChildren<Collider>().Length,Is.Zero);
            foreach(var r in b.GetComponentsInChildren<Renderer>())
            {var bounds=r.bounds;Assert.That(bounds.max.x<-80||bounds.min.x>80||bounds.max.z<-92||bounds.min.z>92,Is.True,r.name+" intrudes into combat space");Assert.That(r.sharedMaterials.All(m=>m&&m.mainTexture),Is.True);}
        }
        Assert.That(root.GetComponentsInChildren<BoxCollider>().Length,Is.EqualTo(4));
        Assert.That(Physics.Raycast(new Vector3(77,15,0),Vector3.right,out var hit,6,1<<9),Is.True);
        Assert.That(hit.collider.name,Does.StartWith("Sealed city perimeter"));
    }
    [UnityTest]public IEnumerator MeleeHasDodgeableWindupInsteadOfImmediateDamage()
    {
        game.player.Teleport(new Vector3(0,1.93f,-9));game.player.Health.DamageBlocked=false;
        var e=Spawn(new Vector3(0,1.93f,-8));yield return new WaitForSeconds(.9f);
        float hp=game.player.Health.Current;e.Tick(.02f);
        Assert.That(e.MeleePreparing,Is.True);Assert.That(game.player.Health.Current,Is.EqualTo(hp));
        game.player.Teleport(new Vector3(3,1.93f,-9));e.Tick(.5f);
        Assert.That(game.player.Health.Current,Is.EqualTo(hp),"Dodging during windup must avoid the hit");Assert.That(e.MeleePreparing,Is.False);
    }
    [UnityTest]public IEnumerator ShieldHitKeepsLocalImpactAndBreakProducesARecoveryWindow()
    {
        var e=Spawn(new Vector3(0,1.93f,-8),true);yield return null;
        var s=e.Shield;var point=s.transform.position+s.transform.right*.3f;
        Assert.That(e.IsVisibleToPlayer,Is.True);
        s.Hit(10,false,false,point);var props=new MaterialPropertyBlock();s.plate.GetPropertyBlock(props);
        Assert.That(Vector3.Distance(props.GetVector("_HitPos"),point),Is.LessThan(.001f));
        Assert.That(FrontierCombatReadability.Instance.LastImpact,Is.EqualTo("护盾吸收"));
        s.BreakWithLaser(point);Assert.That(s.IsIntact,Is.False);Assert.That(e.Staggered,Is.True);Assert.That(FrontierCombatReadability.Instance.ShieldBreaks,Is.EqualTo(1));
        e.Spawn(new Vector3(0,1.93f,-8),game.player);Assert.That(e.Staggered,Is.False,"Pool reuse retained stagger");
        game.StartSession();Assert.That(FrontierCombatReadability.Instance.ShieldBreaks,Is.Zero);Assert.That(FrontierCombatReadability.Instance.LastImpact,Is.Empty);
    }
    [UnityTest]public IEnumerator RangedWarningsTrackCountdownAndHideWithTheirOwner()
    {
        var e=Spawn(new Vector3(0,1.93f,-8),false,true);e.Ranged.HoldPosition=true;
        e.Arrival.Step(EnemySpawnPresentation.Duration);
        for(int i=0;i<90&&!e.Ranged.Charging;i++)e.Ranged.Tick(.02f);yield return null;
        Assert.That(e.Ranged.Charging,Is.True);Assert.That(FrontierCombatReadability.Instance.VisibleThreats,Is.GreaterThan(0));
        var aim=e.Ranged.LockedAim;game.player.Teleport(new Vector3(2,1.93f,-10));e.Ranged.Tick(.2f);Assert.That(e.Ranged.LockedAim,Is.EqualTo(aim));
        e.gameObject.SetActive(false);yield return null;Assert.That(FrontierCombatReadability.Instance.VisibleThreats,Is.Zero);
    }
    [Test]public void BurstPacingCapsPressureAndLeavesShortRecoveryGaps()
    {
        var f=game.Frontier;bool gap=false,preview=false;
        for(int i=0;i<350;i++)
        {
            f.Tick(.1f);gap|=f.RegroupRemaining>0;preview|=game.Arrival.Active;
            Assert.That(game.ActiveEnemies,Is.LessThanOrEqualTo(5));
            foreach(var e in game.Enemies)e.enabled=false;
        }
        Assert.That(gap,Is.True);Assert.That(preview,Is.True);Assert.That(f.EncounterSpawned,Is.EqualTo(5));
        Assert.That(f.AwaitingTerminal,Is.False,"Unspawned reinforcements must block completion");
        game.StartSession();Assert.That(game.Arrival.Active,Is.False);Assert.That(f.RegroupRemaining,Is.Zero);
    }
    [UnityTest]public IEnumerator BodyAndShieldUseDifferentConfirmationsWithoutFlashingAlbedoWhite()
    {
        var e=Spawn(new Vector3(0,1.93f,-8));yield return null;
        e.Health.TakeDamage(10);Assert.That(FrontierCombatReadability.Instance.BodyConfirmations,Is.EqualTo(1));Assert.That(FrontierCombatReadability.Instance.LastImpact,Is.EqualTo("命中"));
        var props=new MaterialPropertyBlock();e.Motion.Model.skin.GetPropertyBlock(props);Assert.That(props.GetColor("_Color"),Is.Not.EqualTo(Color.white));
    }
}
