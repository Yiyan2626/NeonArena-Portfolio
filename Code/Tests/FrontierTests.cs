using System.Collections;
using System.Linq;
using NeonArena;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
public class FrontierTests
{
    ArenaGame game;FrontierCampaign flow;
    [UnitySetUp]public IEnumerator Setup()
    {
        Time.timeScale=1;yield return SceneManager.LoadSceneAsync("BorderCity");yield return null;game=ArenaGame.Instance;game.StartSession();flow=game.Frontier;
        game.enabled=false;flow.enabled=false;game.player.enabled=false;
    }
    [Test]public void InitialRulesAndMobilityCapsAreStrict()
    {
        var l=game.player.weapon.Loadout;Assert.That(l.FrontierRules,Is.True);Assert.That(l.SelectedAmmo,Is.EqualTo(-1));Assert.That(l.Jump,Is.EqualTo(1));Assert.That(game.player.Dash.MaxCharges,Is.EqualTo(1));
        Assert.That(l.CanUpgrade(AmmoUpgrade.Vitality),Is.False);Assert.That(l.CanUpgrade(AmmoUpgrade.DashRecovery),Is.False);
        game.player.weapon.Upgrade(AmmoUpgrade.Jump);Assert.That(l.Jump,Is.EqualTo(2));Assert.That(l.CanUpgrade(AmmoUpgrade.Jump),Is.False);
        for(int i=0;i<5;i++)game.player.weapon.Upgrade(AmmoUpgrade.DashCapacity);
        Assert.That(game.player.Dash.MaxCharges,Is.EqualTo(3));Assert.That(game.player.Dash.Charges,Is.EqualTo(3));
    }
    [Test]public void AmmoSelectionReplacesInsteadOfStackingAndCannotIncreaseRanks()
    {
        var w=game.player.weapon;
        foreach(var type in new[]{AmmoUpgrade.Piercing,AmmoUpgrade.Ricochet,AmmoUpgrade.Laser,AmmoUpgrade.Piercing,AmmoUpgrade.Piercing})
        {w.Upgrade(type);Assert.That(w.Loadout.TotalRanks,Is.EqualTo(1));Assert.That(w.Loadout.SelectedAmmo,Is.EqualTo((int)type));}
        Assert.That(w.Loadout.Profile.ThroughWalls,Is.True);w.Upgrade(AmmoUpgrade.Ricochet);Assert.That(w.Loadout.Bounces,Is.EqualTo(3));Assert.That(w.Loadout.Profile.ThroughWalls,Is.False);
    }
    EnemyController Spawn(Vector3 p,bool shield=false,bool elite=false)
    {
        var e=game.SpawnAuthored(p,1,false,shield,elite,false);e.enabled=false;e.Motion.enabled=false;e.visual.rotation=Quaternion.Euler(0,180,0);return e;
    }
    [Test]public void PiercePassesSolidWallsAndDamagesMultipleEnemiesOnlyOnce()
    {
        game.player.Teleport(new Vector3(-10,40,0));var e1=Spawn(new Vector3(0,40,4));var e2=Spawn(new Vector3(0,40,8));var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.layer=9;wall.transform.position=new Vector3(0,40.7f,2);wall.transform.localScale=new Vector3(3,3,.6f);
        var p=Object.Instantiate(game.projectiles.prefab);Physics.SyncTransforms();var start=new Vector3(0,40.7f,0);
        p.Launch(start,Vector3.forward,50,13,new ShotProfile(31,0,Color.cyan,1,true));p.Simulate(.25f);
        Assert.That(e1.Health.Current,Is.EqualTo(e1.Health.maxHealth-13));Assert.That(e2.Health.Current,Is.EqualTo(e2.Health.maxHealth-13));Assert.That(p.UniqueHits,Is.EqualTo(2));
        p.Simulate(.15f);Assert.That(p.UniqueHits,Is.EqualTo(2));Object.DestroyImmediate(p.gameObject);Object.DestroyImmediate(wall);
    }
    [Test]public void LaserBreaksShieldInOnePulseWithoutBodyPenetration()
    {
        game.player.Teleport(new Vector3(0,40,0));var e=Spawn(new Vector3(0,40,9),true);Physics.SyncTransforms();var w=game.player.weapon;w.Upgrade(AmmoUpgrade.Laser);var aim=e.Shield.transform.position;
        game.player.AimAt(aim);float hp=e.Health.Current;Assert.That(w.SimulateFire(.7f,true,aim),Is.True);Assert.That(e.Shield.IsIntact,Is.False);Assert.That(e.Health.Current,Is.EqualTo(hp));
    }
    [Test]public void LaserCannotPassWallOrFirstEnemy()
    {
        game.player.Teleport(new Vector3(0,40,0));var e=Spawn(new Vector3(0,40,10));var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.layer=9;wall.transform.position=new Vector3(0,40.8f,5);wall.transform.localScale=new Vector3(5,5,.4f);Physics.SyncTransforms();
        var w=game.player.weapon;w.Upgrade(AmmoUpgrade.Laser);Vector3 aim=e.transform.position+Vector3.up*.8f;game.player.AimAt(aim);float hp=e.Health.Current;w.SimulateFire(.7f,true,aim);Assert.That(e.Health.Current,Is.EqualTo(hp));
        Object.DestroyImmediate(wall);var front=Spawn(new Vector3(0,40,5));Physics.SyncTransforms();w.SimulateFire(.7f,true,aim);Assert.That(front.Health.IsDead,Is.True);Assert.That(e.Health.Current,Is.EqualTo(hp));
    }
    [Test]public void BossDamageCannotSkipPhaseAndEachPhaseHasDifferentAmmoAdvantage()
    {
        var e=Spawn(flow.bossPoint,false,true);var boss=e.gameObject.AddComponent<FrontierBoss>();boss.Configure(flow,game.player,flow.bossCover);
        e.Health.TakeDamage(99999);Assert.That(e.Health.IsDead,Is.False);Assert.That(boss.Stage,Is.EqualTo(1));Assert.That(boss.TransitionPending,Is.True);float hp=e.Health.Current;e.Health.TakeDamage(500);Assert.That(e.Health.Current,Is.EqualTo(hp));
        boss.ResumePhase();Assert.That(boss.Stage,Is.EqualTo(2));Assert.That(boss.AmmoDamage(100,0),Is.GreaterThan(boss.AmmoDamage(100,2)*2));
        e.Health.TakeDamage(99999);Assert.That(e.Health.IsDead,Is.False);boss.ResumePhase();Assert.That(boss.Stage,Is.EqualTo(3));Assert.That(boss.AmmoDamage(100,1),Is.GreaterThan(boss.AmmoDamage(100,2)*2));e.Health.TakeDamage(99999);Assert.That(e.Health.IsDead,Is.True);
    }
    [Test]public void OverheatIntroHasSlowClockAndPauseRestoresIt()
    {
        game.Cinema.OverheatEntry(game.player.transform.position+Vector3.up);Assert.That(game.Cinema.IntroActive,Is.True);Assert.That(Time.timeScale,Is.EqualTo(.16f));
        game.TogglePause();Assert.That(Time.timeScale,Is.Zero);game.Cinema.Tick(1);Assert.That(game.Cinema.IntroActive,Is.True);game.TogglePause();Assert.That(Time.timeScale,Is.EqualTo(.16f));game.Cinema.Tick(.3f);Assert.That(game.Cinema.IntroActive,Is.False);Assert.That(Time.timeScale,Is.EqualTo(1));
    }
    [Test]public void LevelContainsFourExperiencesAndGroundSpawnsHaveRoutes()
    {
        Assert.That(flow.encounters.Select(e=>e.sector).Distinct().Count(),Is.EqualTo(3));Assert.That(flow.terminals.Length,Is.EqualTo(5));Assert.That(flow.supplies.All(s=>s.position.y>6),Is.True);
        foreach(var e in flow.encounters)foreach(var p in e.spawns)
        {
            Assert.That(Physics.CheckCapsule(p+Vector3.up*.55f,p+Vector3.up*1.5f,.48f,1<<9,QueryTriggerInteraction.Ignore),Is.False,e.name+" obstructed "+p);
            if(p.y<2)Assert.That(game.terrain.HasRoute(p,flow.sectorEntrances[e.sector].position),Is.True,e.name+" no route "+p);
        }
    }
    [Test]public void NewPlayerHasRealAvatarAndAuthoredBladeAndTitanSkins()
    {
        var m=game.player.Motion.Model;var a=m.GetComponent<Animator>();Assert.That(m.name,Does.Contain("Ranger"));Assert.That(a.avatar.isHuman,Is.True);Assert.That(a.runtimeAnimatorController,Is.Not.Null);Assert.That(m.skin.sharedMesh.vertexCount,Is.GreaterThan(30000));Assert.That(m.skin.sharedMaterials.Any(x=>x.mainTexture),Is.True);
        Assert.That(Resources.Load<GameObject>("Frontier/Bloodlight"),Is.Not.Null);var e=Spawn(flow.bossPoint,false,true);Assert.That(e.Motion.Model.skin.sharedMesh.vertexCount,Is.GreaterThan(100000));Assert.That(e.Motion.Model.skin.sharedMaterials.Any(x=>x.mainTexture),Is.True);
    }
    [Test]public void JetpackCanPhysicallyRiseFromEntryAndLandOnUpperDeck()
    {
        var p=game.player;p.weapon.Upgrade(AmmoUpgrade.Jump);p.Teleport(flow.JetEntry);p.Flight.Held=true;float highest=p.transform.position.y;
        for(int i=0;i<260;i++)
        {
            if(p.Grounded&&p.transform.position.y<2)p.RequestJump();
            Vector3 direction=p.transform.position.y>7.2f&&p.transform.position.z>-75.1f?Vector3.back:Vector3.zero;
            Vector3 right=Vector3.ProjectOnPlane(p.viewCamera.transform.right,Vector3.up).normalized,forward=Vector3.Cross(right,Vector3.up);
            p.Move(new Vector2(Vector3.Dot(direction,right),Vector3.Dot(direction,forward)),.02f);highest=Mathf.Max(highest,p.transform.position.y);
            if(p.transform.position.z<-74.9f)p.Flight.Held=false;
        }
        Assert.That(highest,Is.GreaterThan(7.2f));Assert.That(p.transform.position.y,Is.GreaterThan(6.6f),"Jet arrival "+p.transform.position+" peak="+highest);Assert.That(p.Grounded,Is.True);Assert.That(p.transform.position.z,Is.LessThan(-74.8f));
    }
    [Test]public void ElevatedSnipersHaveAtLeastOneClearGroundFiringPosition()
    {
        var group=flow.encounters.First(e=>e.name=="护盾狙击阵地");
        foreach(var slot in group.spawns.Where(p=>p.y>5))
        {
            bool clear=false;Vector3 target=slot+Vector3.up*1.45f;
            for(int radius=6;radius<=22&&!clear;radius+=4)for(int a=0;a<12&&!clear;a++)
            {
                Vector3 candidate=new Vector3(slot.x,.5f,slot.z)+Quaternion.Euler(0,a*30,0)*Vector3.forward*radius;
                if(!ArenaTerrain.TryGround(candidate,out var ground,2)||ground.y>2||Physics.CheckCapsule(ground+Vector3.up*.55f,ground+Vector3.up*1.5f,.48f,1<<9))continue;
                clear=!Physics.Linecast(ground+Vector3.up*1.3f,target,1<<9);
            }
            Assert.That(clear,Is.True,"Lower route has no clear shot at elevated sniper "+slot);
        }
    }
    [Test]public void RetiredBossCanReturnAsNormalEnemyAndBeConfiguredAgainSameFrame()
    {
        var e=Spawn(flow.bossPoint,false,true);var boss=e.gameObject.AddComponent<FrontierBoss>();boss.Configure(flow,game.player,flow.bossCover);
        e.Health.TakeDamage(99999);Assert.That(boss.TransitionPending,Is.True);
        e.Spawn(new Vector3(0,40,8),game.player,1);e.enabled=false;
        Assert.That(boss.enabled,Is.False);Assert.That(e.Health.TakeDamage(10),Is.True);Assert.That(e.Health.Current,Is.EqualTo(e.Health.maxHealth-10));
        e.Spawn(flow.bossPoint,game.player,4,false,false,true);e.enabled=false;
        boss.Configure(flow,game.player,flow.bossCover);Assert.That(boss.enabled,Is.True);Assert.That(boss.Stage,Is.EqualTo(1));Assert.That(boss.TransitionPending,Is.False);
        Assert.That(e.Health.Current,Is.EqualTo(3000));e.Health.TakeDamage(99999);Assert.That(e.Health.Current,Is.EqualTo(2000));Assert.That(boss.TransitionPending,Is.True);
    }
    [Test]public void ThreeBouncesAreConsumedThenFourthWallRetiresProjectile()
    {
        var left=GameObject.CreatePrimitive(PrimitiveType.Cube);var right=GameObject.CreatePrimitive(PrimitiveType.Cube);
        left.layer=right.layer=9;left.transform.position=new Vector3(-2,40,0);right.transform.position=new Vector3(2,40,0);left.transform.localScale=right.transform.localScale=new Vector3(.2f,4,8);
        var p=Object.Instantiate(game.projectiles.prefab);Physics.SyncTransforms();p.Launch(new Vector3(0,40,0),Vector3.right,30,10,new ShotProfile(0,3,Color.magenta));p.Simulate(.55f);
        Assert.That(p.BouncesUsed,Is.EqualTo(3));Assert.That(p.InFlight,Is.False);Object.DestroyImmediate(p.gameObject);Object.DestroyImmediate(left);Object.DestroyImmediate(right);
    }
    [Test]public void SafeTerminalRequiresClearAndGrantsOnlyOneMobilityChoice()
    {
        Assert.That(flow.TryOpenNearbyTerminal(),Is.False);
        while(!flow.AwaitingTerminal)
        {
            game.player.Teleport(flow.Destination);flow.Tick(1);
            foreach(var e in game.Enemies)if(e.IsAlive)e.Health.TakeDamage(999);
            game.Cinema.ResetCinema();
        }
        game.player.Teleport(flow.terminals[0].position);Assert.That(flow.TryOpenNearbyTerminal(),Is.True);
        Assert.That(flow.LeaveTerminal(),Is.False);Assert.That(flow.SelectAmmo(AmmoUpgrade.Piercing),Is.True);Assert.That(flow.SelectAmmo(AmmoUpgrade.Laser),Is.True);
        Assert.That(flow.SelectMobility(AmmoUpgrade.DashCapacity),Is.True);Assert.That(flow.SelectMobility(AmmoUpgrade.Jump),Is.False);Assert.That(flow.LeaveTerminal(),Is.True);
        Assert.That(flow.ExitTrialActive,Is.True);Assert.That(game.player.weapon.Loadout.SelectedAmmo,Is.EqualTo(2));
        Assert.That(flow.TryOpenNearbyTerminal(),Is.True);Assert.That(flow.MobilityPending,Is.False);Assert.That(flow.SelectAmmo(AmmoUpgrade.Piercing),Is.True);Assert.That(flow.LeaveTerminal(),Is.True);
        Assert.That(flow.UpgradesClaimed,Is.EqualTo(1));
        var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.layer=9;game.player.Teleport(flow.terminals[0].position+Vector3.right*2.5f);
        wall.transform.position=flow.terminals[0].position+Vector3.right*1.2f+Vector3.up;wall.transform.localScale=new Vector3(.25f,3,3);Physics.SyncTransforms();
        Assert.That(flow.TryOpenNearbyTerminal(),Is.False,"A terminal must not be usable through its adjoining wall");Object.DestroyImmediate(wall);
    }
    [Test]public void CinematicDeathReferenceSurvivesPooledEnemyReuse()
    {
        var e=Spawn(new Vector3(0,1.91f,-14));game.player.Teleport(new Vector3(0,1.91f,-10));game.player.AimAt(e.transform.position+Vector3.up);
        for(int i=0;i<8;i++)game.player.Overheat.RegisterKill();game.player.Overheat.Tick(.01f);Assert.That(game.Cinema.IntroActive,Is.True);game.Cinema.ResetCinema();
        Assert.That(game.player.Overheat.TryExecute(e),Is.True);game.player.Overheat.Tick(.08f);Assert.That(e.Health.IsDead,Is.True);
    }
    [Test]public void ReflectionWallAllowsRealProjectileToHitAroundOccludingCover()
    {
        game.player.Teleport(new Vector3(-10,40,0));var e=Spawn(new Vector3(0,40,6));var bank=GameObject.CreatePrimitive(PrimitiveType.Cube);var cover=GameObject.CreatePrimitive(PrimitiveType.Cube);
        bank.layer=cover.layer=9;bank.transform.position=new Vector3(3,40.7f,4);bank.transform.localScale=new Vector3(.20f,3,12);cover.transform.position=new Vector3(0,40.7f,3);cover.transform.localScale=new Vector3(1.0f,3,.3f);Physics.SyncTransforms();
        Vector3 origin=new Vector3(0,40.7f,0);Assert.That(Physics.Linecast(origin,e.transform.position+Vector3.up*.7f,1<<9),Is.True);
        Assert.That(RicochetSolver.TryBank(origin,e,out var aim),Is.True);
        var p=Object.Instantiate(game.projectiles.prefab);float hp=e.Health.Current;p.Launch(origin,(aim-origin).normalized,48,18,new ShotProfile(0,3,Color.magenta));p.Simulate(.4f);
        Assert.That(e.Health.Current,Is.EqualTo(hp-18));Assert.That(p.BouncesUsed,Is.EqualTo(1));Object.DestroyImmediate(p.gameObject);Object.DestroyImmediate(bank);Object.DestroyImmediate(cover);
    }
}

