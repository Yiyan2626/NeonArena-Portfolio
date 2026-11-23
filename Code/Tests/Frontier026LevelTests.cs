using System.Collections;
using NeonArena;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class Frontier026LevelTests
{
    ArenaGame game;
    FrontierCampaign flow;

    [UnitySetUp] public IEnumerator Setup()
    {
        Time.timeScale=1;
        yield return SceneManager.LoadSceneAsync("BorderCity");
        yield return null;
        game=ArenaGame.Instance;game.StartSession();flow=game.Frontier;
        game.enabled=flow.enabled=game.player.enabled=false;
        game.player.Health.DamageBlocked=true;
    }

    void ClearPresentEnemies()
    {
        foreach(var enemy in game.Enemies)if(enemy.IsAlive)enemy.Health.TakeDamage(99999);
        game.Cinema.ResetCinema();
    }

    void ReachTerminal()
    {
        for(int i=0;i<1500&&!flow.AwaitingTerminal;i++)
        {
            game.player.Teleport(flow.Destination);flow.Tick(.25f);ClearPresentEnemies();
        }
        Assert.That(flow.AwaitingTerminal,Is.True,"Authored encounters must reach their terminal within a bounded simulation");
    }

    void ClaimTerminal(AmmoUpgrade mobility)
    {
        game.player.Teleport(flow.terminals[flow.Sector].position);
        Assert.That(flow.TryOpenNearbyTerminal(),Is.True);
        Assert.That(flow.SelectAmmo(AmmoUpgrade.Laser),Is.True);
        Assert.That(flow.SelectMobility(mobility),Is.True);
        Assert.That(flow.LeaveTerminal(),Is.True);
    }

    void ReachPipeline(bool jet)
    {
        ReachTerminal();ClaimTerminal(jet?AmmoUpgrade.Jump:AmmoUpgrade.DashCapacity);
        for(int i=0;i<1500&&flow.Sector==0;i++)
        {game.player.Teleport(flow.Destination);flow.Tick(.25f);ClearPresentEnemies();}
        Assert.That(flow.Sector,Is.EqualTo(1));
        ReachTerminal();ClaimTerminal(AmmoUpgrade.DashCapacity);
        Assert.That(flow.Sector,Is.EqualTo(2));
    }

    [Test] public void EmptyBattlefieldDuringRecoveryDoesNotUnlockTerminalWhileReinforcementsRemain()
    {
        bool recovery=false;
        for(int i=0;i<200&&!recovery;i++)
        {
            flow.Tick(.1f);ClearPresentEnemies();
            recovery=flow.RegroupRemaining>0;
        }
        Assert.That(recovery,Is.True);
        Assert.That(game.ActiveEnemies,Is.Zero);
        Assert.That(flow.PendingEnemies,Is.GreaterThan(0));
        Assert.That(flow.Objective,Does.Contain("增援"));
        Assert.That(flow.AwaitingTerminal,Is.False);
        Assert.That(flow.TerminalAvailable(0),Is.False);
        ReachTerminal();
        Assert.That(flow.PendingEnemies,Is.Zero);
        Assert.That(flow.Objective,Does.Contain("肃清"));
    }

    [Test] public void TerminalGuidanceAndInteractionAgreeAboutOcclusionAndNearbyThreats()
    {
        ReachTerminal();var terminal=flow.terminals[0];game.player.Teleport(terminal.position);
        Assert.That(flow.TerminalBlockReason(0),Is.Empty);
        game.player.Teleport(terminal.position+Vector3.right*2.5f);
        var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            wall.layer=9;wall.transform.position=terminal.position+Vector3.right*1.2f+Vector3.up;
            wall.transform.localScale=new Vector3(.25f,3,3);Physics.SyncTransforms();
            Assert.That(flow.TerminalBlockReason(0),Does.Contain("遮挡"));
            Assert.That(flow.TryOpenNearbyTerminal(),Is.False);
        }
        finally { Object.DestroyImmediate(wall); }
        game.player.Teleport(terminal.position);
        var enemy=game.SpawnAuthored(terminal.position+Vector3.right*5,1,false,false,false,false);
        Assert.That(enemy,Is.Not.Null);enemy.enabled=false;
        Assert.That(flow.TerminalBlockReason(0),Does.Contain("敌人"));
        Assert.That(flow.TryOpenNearbyTerminal(),Is.False);
        ClearPresentEnemies();Assert.That(flow.TerminalBlockReason(0),Is.Empty);
        Assert.That(flow.TryOpenNearbyTerminal(),Is.True);
    }

    [Test] public void JetUpgradeDoesNotRedirectGroundNavigationUntilPlayerActuallyAscends()
    {
        ReachPipeline(true);
        Assert.That(FrontierGuidance.Goal(game),Is.EqualTo(flow.LowerEntry));
        game.player.Teleport(flow.JetEntry);
        Assert.That(FrontierGuidance.Goal(game),Is.EqualTo(flow.LowerEntry));
        game.player.Teleport(flow.JetEntry+Vector3.up*3);
        Assert.That(FrontierGuidance.Goal(game).y,Is.GreaterThan(6.6f));
        // This is a route-state test; FrontierTests separately simulates physical jet ascent.
        game.player.Teleport(new Vector3(-12,7,-75.1f));flow.Tick(2);flow.Tick(.1f);
        Assert.That(flow.UpperRouteChosen,Is.True);
        Assert.That(flow.CurrentEncounter.name,Is.EqualTo("护盾狙击阵地"));
        ReachTerminal();
        Assert.That(flow.TerminalAvailable(2),Is.True);
        game.player.Teleport(new Vector3(-12,7,-75.1f));
        Assert.That(FrontierGuidance.Goal(game).x,Is.EqualTo(10),"Upper clear first guides the player along the real deck toward its east descent");
    }

    [Test] public void LowerRouteRemainsSelectableWithoutJetUpgrade()
    {
        ReachPipeline(false);
        Assert.That(game.player.weapon.Loadout.Jump,Is.EqualTo(1));
        Assert.That(game.player.Dash.MaxCharges,Is.EqualTo(3));
        game.player.Teleport(flow.LowerEntry);flow.Tick(2);flow.Tick(.1f);
        Assert.That(flow.RouteResolved,Is.True);
        Assert.That(flow.UpperRouteChosen,Is.False);
        Assert.That(flow.CurrentEncounter.name,Is.EqualTo("管线区下层围堵"));
        ReachTerminal();Assert.That(flow.TerminalAvailable(2),Is.True);
        Assert.That(flow.UpperRouteChosen,Is.False);
    }

    [Test] public void BossArrivalWarnsBeforeSpawningAndInactiveBossCannotBlockPhaseTerminal()
    {
        ReachPipeline(false);ReachTerminal();ClaimTerminal(AmmoUpgrade.Jump);
        game.player.Teleport(flow.bossPoint);flow.Tick(2);flow.Tick(.1f);
        Assert.That(flow.ArrivalPending,Is.True);
        Assert.That(flow.Boss,Is.Null);
        flow.Tick(SpawnTelegraph.Duration+.1f);
        Assert.That(flow.Boss,Is.Not.Null);
        var boss=flow.Boss.GetComponent<EnemyController>();boss.enabled=false;
        boss.Health.TakeDamage(99999);
        Assert.That(flow.BossResupply,Is.True);
        var terminal=flow.terminals[3];
        var controller=boss.GetComponent<CharacterController>();controller.enabled=false;
        boss.transform.position=terminal.position+Vector3.right*4;controller.enabled=true;
        game.player.Teleport(terminal.position);
        Assert.That(flow.TerminalBlockReason(3),Is.Empty,"The disabled phase boss is not an active threat");
        Assert.That(flow.TryOpenNearbyTerminal(),Is.True);
        Assert.That(flow.BossTerminal,Is.True);
    }

    [UnityTest] public IEnumerator ClosedTerminalCannotBeReactivatedByFrontEndCanvasGate()
    {
        var panel=game.hud.transform.Find("Safe terminal");
        Assert.That(panel,Is.Not.Null);
        var fade=panel.Find("Terminal transition").GetComponent<CanvasGroup>();
        for(int frame=0;frame<8;frame++)yield return null;
        Assert.That(flow.TerminalOpen,Is.False);
        Assert.That(panel.gameObject.activeSelf,Is.False,"FrontEnd must not reopen an uninitialized terminal via alpha");
        ReachTerminal();game.player.Teleport(flow.terminals[0].position);
        Assert.That(flow.TryOpenNearbyTerminal(),Is.True);
        yield return new WaitForSecondsRealtime(.4f);
        Assert.That(panel.gameObject.activeSelf,Is.True);
        Assert.That(fade.alpha,Is.GreaterThan(.9f));
        flow.SelectAmmo(AmmoUpgrade.Piercing);flow.SelectMobility(AmmoUpgrade.DashCapacity);flow.LeaveTerminal();
        yield return new WaitForSecondsRealtime(.7f);
        Assert.That(panel.gameObject.activeSelf,Is.False);
        Assert.That(fade.alpha,Is.LessThan(.01f));
        game.StartSession();
        for(int frame=0;frame<8;frame++)yield return null;
        Assert.That(panel.gameObject.activeSelf,Is.False,"Session restart must not resurrect a closed terminal");
    }

    [UnityTest] public IEnumerator RouteHintReceivesReadableTypographyAndExactlyOneBackingAfterInitialization()
    {
        for(int frame=0;frame<5;frame++)yield return null;
        UnityEngine.UI.Text hint=null,objective=null;
        int backingCount=0,missionCount=0;UnityEngine.UI.Image backing=null,mission=null;
        foreach(Transform child in game.hud.transform)
        {
            if(child.name=="Route / interaction")hint=child.GetComponent<UnityEngine.UI.Text>();
            if(child.name=="Frontier objective")objective=child.GetComponent<UnityEngine.UI.Text>();
            if(child.name=="Navigation contrast"){backingCount++;backing=child.GetComponent<UnityEngine.UI.Image>();}
            if(child.name=="Mission contrast"){missionCount++;mission=child.GetComponent<UnityEngine.UI.Image>();}
        }
        Assert.That(hint,Is.Not.Null);
        Assert.That(hint.fontSize,Is.EqualTo(23),"Critical route hints retain the readable 23px reference size");
        Assert.That(backingCount,Is.EqualTo(1),"Repeated lazy initialization must not create duplicate backings");
        Assert.That(missionCount,Is.EqualTo(1));
        Assert.That(backing.raycastTarget||mission.raycastTarget,Is.False);
        Assert.That(mission.transform.GetSiblingIndex(),Is.LessThan(hint.transform.GetSiblingIndex()));
        var routeCorners=new Vector3[4];var objectiveCorners=new Vector3[4];
        hint.rectTransform.GetWorldCorners(routeCorners);objective.rectTransform.GetWorldCorners(objectiveCorners);
        foreach(var corner in routeCorners)
            Assert.That(mission.rectTransform.rect.Contains(mission.rectTransform.InverseTransformPoint(corner)),Is.True,"All route text must be backed by the unified mission plate");
        Assert.That(routeCorners[1].y,Is.LessThan(objectiveCorners[0].y),"Route and objective lines must not overlap");
        var missionCorners=new Vector3[4];var encounterCorners=new Vector3[4];var mapCorners=new Vector3[4];
        mission.rectTransform.GetWorldCorners(missionCorners);
        game.hud.transform.Find("Elite Encounter").GetComponent<RectTransform>().GetWorldCorners(encounterCorners);
        game.hud.transform.Find("Tactical Minimap").GetComponent<RectTransform>().GetWorldCorners(mapCorners);
        Assert.That(encounterCorners[0].x,Is.GreaterThan(missionCorners[2].x),"Boss panel must have a separate horizontal lane from mission text");
        Assert.That(encounterCorners[2].x,Is.LessThan(mapCorners[0].x),"Boss panel must not intrude into the minimap");
        string longest="已到喷气入口 · 长按空格走上层 / 继续沿箭头走下层";
        float textHeight=hint.cachedTextGeneratorForLayout.GetPreferredHeight(longest,hint.GetGenerationSettings(hint.rectTransform.rect.size))/hint.pixelsPerUnit;
        Assert.That(textHeight,Is.LessThanOrEqualTo(hint.rectTransform.rect.height),"Longest route instruction must fit at full font size");
        yield return new WaitForSecondsRealtime(.3f);
        Assert.That(mission.color.a,Is.GreaterThan(.7f),"Combat text must have a dark readable shared backing");
        Assert.That(backing.color.a,Is.EqualTo(0),"Navigation must not add a second opaque stripe over the shared mission plate");
    }

    [UnityTest] public IEnumerator ObjectiveMarkerVisibilitySurvivesFrontEndGateWhenAimCentered()
    {
        var camera=game.player.viewCamera;camera.GetComponent<ArenaCamera>().enabled=false;
        camera.transform.position=game.player.transform.position+new Vector3(0,3,5);
        camera.transform.LookAt(FrontierGuidance.Goal(game)+Vector3.up*1.4f);
        yield return new WaitForSecondsRealtime(.6f);
        var marker=game.hud.transform.Find("Objective direction");
        var visibility=marker.Find("Objective visibility").GetComponent<CanvasGroup>();
        Assert.That(marker.GetComponent<CanvasGroup>().alpha,Is.EqualTo(1),"FrontEnd keeps the combat HUD available");
        Assert.That(visibility.alpha,Is.LessThan(.02f),"Local marker suppression must survive the outer HUD visibility gate");
        UnityEngine.UI.Text route=null;
        foreach(Transform child in game.hud.transform)
            if(child.name=="Route / interaction")route=child.GetComponent<UnityEngine.UI.Text>();
        Assert.That(route,Is.Not.Null);
        Assert.That(route.text,Is.Not.Empty,"Persistent route instruction remains available");
    }
}
