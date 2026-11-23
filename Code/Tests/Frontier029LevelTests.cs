using System.Collections;
using System.Linq;
using NeonArena;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class Frontier029LevelTests
{
    ArenaGame game;
    [UnitySetUp] public IEnumerator Setup()
    {
        Time.timeScale=1;yield return SceneManager.LoadSceneAsync("BorderCity");yield return null;
        game=ArenaGame.Instance;game.StartSession();
        game.enabled=game.Frontier.enabled=game.player.enabled=false;
        game.player.Health.DamageBlocked=true;
        game.player.viewCamera.GetComponent<ArenaCamera>().enabled=false;
        game.player.viewCamera.transform.rotation=Quaternion.identity;
    }
    void Walk(Vector3 target)
    {
        int step=0;
        while(step++<750)
        {
            var delta=target-game.player.transform.position;delta.y=0;
            if(delta.magnitude<.22f)break;
            game.player.Move(new Vector2(delta.x,delta.z).normalized,.02f);
        }
        Assert.That(Vector3.Distance(Vector3.ProjectOnPlane(game.player.transform.position-target,Vector3.up),Vector3.zero),Is.LessThan(.3f),"Physical walk stuck approaching "+target+" at "+game.player.transform.position);
        Assert.That(game.player.transform.position.y,Is.InRange(-.1f,.4f),"The maintenance route must remain ground traversable");
    }
    void WalkRoute(Vector3[] route)
    {
        game.player.Teleport(route[0]);
        for(int i=1;i<route.Length;i++)Walk(route[i]);
    }
    [Test] public void BothAnnexLanesArePhysicallyWalkableWithInitialMobilityInBothDirections()
    {
        Assert.That(game.player.weapon.Loadout.Jump,Is.EqualTo(1));
        WalkRoute(FrontierAnnex029.DirectRoute);WalkRoute(FrontierAnnex029.DirectRoute.Reverse().ToArray());
        WalkRoute(FrontierAnnex029.CoveredRoute);WalkRoute(FrontierAnnex029.CoveredRoute.Reverse().ToArray());
        Assert.That(game.player.weapon.Loadout.Jump,Is.EqualTo(1));
    }
    [Test] public void AnnexAddsConnectedGroundAndRealSightlineChoiceWithoutAddingEnemyBudget()
    {
        var annex=Object.FindObjectOfType<FrontierAnnex029>();Assert.That(annex,Is.Not.Null);
        Assert.That(game.Frontier.encounters.Length,Is.EqualTo(9));
        Assert.That(game.Frontier.encounters.Sum(e=>e.count),Is.EqualTo(90));
        Assert.That(game.Frontier.encounters[annex.encounterIndex].count,Is.EqualTo(10));
        foreach(var point in FrontierAnnex029.CoveredRoute)
        {
            Assert.That(ArenaTerrain.TryGround(point,out var floor,1),Is.True,"Missing new floor "+point);
            Assert.That(game.terrain.HasRoute(FrontierAnnex029.Entrance,point),Is.True,"Disconnected route "+point);
        }
        var directStart=new Vector3(-10,1.4f,-95);var directEnd=new Vector3(15,1.4f,-95);
        Assert.That(Physics.Linecast(directStart,directEnd,1<<9,QueryTriggerInteraction.Ignore),Is.False,"Short lane should expose the ranged sentry");
        Assert.That(Physics.Linecast(new Vector3(0,1.4f,-102),new Vector3(0,1.4f,-95),1<<9,QueryTriggerInteraction.Ignore),Is.True,"Machinery must interrupt the flank sightline");
        foreach(var point in game.Frontier.encounters[annex.encounterIndex].spawns)
            Assert.That(game.terrain.TryEncounterPoint(point,FrontierAnnex029.Decision,out var spawn,out var distance),Is.True,"Unusable annex spawn "+point);
    }
    [Test] public void LastPipelineEncounterCannotTriggerFromOldStreetAndResetsCleanly()
    {
        var encounter=game.Frontier.encounters.Last(e=>e.sector==2);
        Assert.That(encounter.name,Is.EqualTo(FrontierAnnex029.EncounterName));
        Assert.That(Vector3.Distance(new Vector3(-10,.12f,-88),encounter.center),Is.GreaterThan(encounter.radius));
        Assert.That(Vector3.Distance(FrontierAnnex029.Decision,encounter.center),Is.LessThan(encounter.radius));
        game.player.Teleport(FrontierAnnex029.Flank);game.StartSession();
        Assert.That(game.Frontier.Sector,Is.Zero);Assert.That(game.Frontier.EncounterIndex,Is.Zero);
        Assert.That(game.Frontier.UpperRouteChosen||game.Frontier.TerminalOpen,Is.False);
        Assert.That(Vector3.Distance(game.player.transform.position,game.terrain.entry),Is.LessThan(.1f));
        Assert.That(game.terrain.HasRoute(FrontierAnnex029.Entrance,FrontierAnnex029.Exit),Is.True);
    }
}
