using System.Collections;
using System.Text;
using System.IO;
using NeonArena;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
public class EncounterDiagnostics
{
    [UnityTest] public IEnumerator EnemiesPhysicallyCrossTheParkStairSeam()
    {
        yield return SceneManager.LoadSceneAsync("SatelliteCity");yield return null;
        var game=ArenaGame.Instance;game.autoSpawn=false;game.StartSession();game.player.enabled=false;game.player.Health.DamageBlocked=true;
        foreach(float x in new[]{-2f,0f,2f})
        {
            var enemy=game.SpawnEnemy();Assert.That(enemy,Is.Not.Null);enemy.enabled=false;
            enemy.Spawn(new Vector3(x,.05f,-26),game.player);yield return new WaitForSeconds(.7f);
            for(int i=0;i<650&&Vector3.Distance(enemy.transform.position,game.player.transform.position)>1.5f;i++)
            {enemy.Tick(.02f);if(i%4==0)yield return null;}
            Assert.That(enemy.transform.position.z,Is.GreaterThan(-13),"Stair approach x="+x+" final="+enemy.transform.position);
            enemy.gameObject.SetActive(false);
        }
    }
    [UnityTest] public IEnumerator IndoorAndUpperFloorEncountersRemainSpawnable()
    {
        yield return SceneManager.LoadSceneAsync("SatelliteCity");yield return null;
        var game=ArenaGame.Instance;game.autoSpawn=false;game.StartSession();game.player.enabled=false;
        foreach(var p in new[]{new Vector3(-24,.3f,-57),new Vector3(22,.3f,-56),new Vector3(-24,5.3f,-49),new Vector3(22,5.3f,-48)})
        {
            game.player.Teleport(p);var e=game.SpawnEnemy();Assert.That(e,Is.Not.Null,"No spawn for "+p);
            Assert.That(game.terrain.HasRoute(e.transform.position,p),Is.True);e.gameObject.SetActive(false);
        }
    }
    [UnityTest] public IEnumerator ObserveStationaryOpening()
    {
        yield return SceneManager.LoadSceneAsync("SatelliteCity");yield return null;
        var game=ArenaGame.Instance;game.StartSession();game.player.enabled=false;game.player.Health.DamageBlocked=true;
        var report=new StringBuilder();float start=Time.time;float first=-1;
        for(int i=0;i<25;i++)
        {
            yield return new WaitForSeconds(1);int seen=0;
            foreach(var e in game.Enemies)if(e.IsAlive&&e.IsVisibleToPlayer)seen++;
            if(seen>0&&first<0)first=Time.time-start;
            report.AppendLine("time="+(Time.time-start)+" active="+game.ActiveEnemies+" visible="+seen+" pending="+game.PendingSpawns);
            if(i==7||i==24)foreach(var e in game.Enemies)if(e.IsAlive)
                report.AppendLine(" enemy="+e.transform.position+" route="+game.terrain.HasRoute(e.transform.position,game.player.transform.position)+" visible="+e.IsVisibleToPlayer);
        }
        report.AppendLine("firstVisible="+first);Directory.CreateDirectory("../work");File.WriteAllText("../work/encounter-opening.txt",report.ToString());
        Assert.That(game.ActiveEnemies,Is.GreaterThan(0));
        Assert.That(first,Is.InRange(0,3),"The player should see the opening encounter within three seconds without moving.");
    }
}
