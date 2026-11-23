using System;
using System.IO;
using UnityEngine;
namespace NeonArena
{
    public sealed class FrontierRunMetrics:MonoBehaviour
    {
        [Serializable]public class Sector {public float combat,travel,terminal;public int hitsTaken;public float damageTaken;}
        [Serializable]public class Report {public string version,utc,result;public bool upperRoute;public int kills,shots;public float gameSeconds;public Sector[] sectors;}
        public readonly Sector[] sectors={new Sector(),new Sector(),new Sector(),new Sector()};
        ArenaGame game;bool recorded,started;float previousElapsed;
        public string LastReportPath{get;private set;}
        void Start(){game=ArenaGame.Instance;game.player.Health.Damaged+=Damaged;}
        void Damaged(float damage){if(game&&game.Frontier){var s=sectors[game.Frontier.Sector];s.hitsTaken++;s.damageTaken+=damage;}}
        void Update()
        {
            if(!game||!game.Frontier)return;
            if(game.Elapsed<previousElapsed||(!started&&game.IsPlaying))
            {for(int i=0;i<4;i++)sectors[i]=new Sector();recorded=false;started=true;}
            previousElapsed=game.Elapsed;var f=game.Frontier;var sector=sectors[Mathf.Clamp(f.Sector,0,3)];
            if(f.TerminalOpen)sector.terminal+=Time.unscaledDeltaTime;
            else if(game.IsPlaying&&!game.Cinema.FinaleActive){if(f.EncounterActive)sector.combat+=Time.deltaTime;else sector.travel+=Time.deltaTime;}
            if(started&&!recorded&&(game.State==ArenaState.Victory||game.State==ArenaState.Dead)){recorded=true;Save(game.State.ToString());}
        }
        public void Save(string result)
        {
            var report=new Report{version=Application.version,utc=DateTime.UtcNow.ToString("o"),result=result,upperRoute=game.Frontier.UpperRouteChosen,kills=game.Kills,shots=game.player.weapon.ShotsFired,gameSeconds=game.Elapsed,sectors=sectors};
            string dir=Path.Combine(Application.persistentDataPath,"Playtests");Directory.CreateDirectory(dir);
            LastReportPath=Path.Combine(dir,"run-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+".json");File.WriteAllText(LastReportPath,JsonUtility.ToJson(report,true));
            Debug.Log("LOCAL_RUN_METRICS "+LastReportPath);
        }
        void OnDestroy(){if(game&&game.player)game.player.Health.Damaged-=Damaged;}
    }
}
