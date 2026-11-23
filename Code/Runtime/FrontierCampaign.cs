using System;
using UnityEngine;
namespace NeonArena
{
    [Serializable] public class FrontierEncounter
    {
        public string name;public int sector;public Vector3 center;public Vector3[] spawns;
        public int count=6;public float radius=18;public int shieldEvery,rangeEvery,chargeEvery;
        [Tooltip("Zero uses the sector's pacing profile; existing authored encounters remain compatible.")]
        public int batchSize;
        public float spawnInterval,regroupSeconds;
    }
    public sealed class FrontierCampaign:MonoBehaviour
    {
        public FrontierEncounter[] encounters;
        public Transform[] terminals;
        public Transform[] sectorEntrances;
        public Transform[] routeGates;
        public Transform[] supplies;
        public Vector3 bossPoint=new Vector3(22,.30f,-55);
        public Vector3[] bossCover;
        public int Sector{get;private set;}
        public int EncounterIndex{get;private set;}
        public bool Completed{get;private set;}
        public bool AwaitingTerminal{get;private set;}
        public bool TerminalOpen{get;private set;}
        public bool MobilityPending{get;private set;}
        public bool BossTerminal{get;private set;}
        public bool Revisiting{get;private set;}
        public bool AmmoChosen{get;private set;}
        public bool ExitTrialActive{get;private set;}
        public bool UpperRouteChosen{get;private set;}
        public int UpgradesClaimed{get;private set;}
        public FrontierBoss Boss{get;private set;}
        public readonly float[] SectorSeconds=new float[4];
        public readonly int[] SectorKills=new int[4];
        public static readonly string[] Names={"01 城郊外围广场","02 市政厅办公楼","03 高架管线区","04 中央控制室"};
        int spawned;float spawnWait,sectorStarted;bool activated,bossSpawned,routeResolved;
        float regroup;bool arrivalPending;
        public bool EncounterActive=>activated||ExitTrialActive||Sector==3&&Boss&&!Boss.TransitionPending;
        public float RegroupRemaining=>Mathf.Max(0,regroup);
        public int EncounterSpawned=>spawned;
        public int EnemyLimit=>Sector==0?5:Sector==1?6:8;
        public bool RouteResolved=>routeResolved;
        public Vector3 JetEntry=>new Vector3(-12,.3f,-71.5f);
        public Vector3 LowerEntry=>new Vector3(-8,.3f,-75);
        public bool ArrivalPending=>arrivalPending;
        public bool BossResupply=>Sector==3&&Boss&&Boss.TransitionPending;
        public FrontierEncounter CurrentEncounter=>encounters!=null&&EncounterIndex>=0&&EncounterIndex<encounters.Length?encounters[EncounterIndex]:null;
        public int PendingEnemies=>AwaitingTerminal||Completed?0:ExitTrialActive?Mathf.Max(0,6-spawned):Sector<3&&CurrentEncounter!=null?Mathf.Max(0,CurrentEncounter.count-(activated?spawned:0)):0;
        public int BatchSize=>CurrentEncounter!=null&&CurrentEncounter.batchSize>0?CurrentEncounter.batchSize:Sector==0?3:4;
        public float SpawnInterval=>CurrentEncounter!=null&&CurrentEncounter.spawnInterval>0?CurrentEncounter.spawnInterval:Sector==0?.72f:Sector==1?.62f:.52f;
        public float BatchRecovery=>CurrentEncounter!=null&&CurrentEncounter.regroupSeconds>0?CurrentEncounter.regroupSeconds:Sector==0?3.2f:Sector==1?2.8f:2.3f;
        int openTerminalIndex=-1;
        ArenaGame game;
        public string Objective {
            get {
                if(Completed)return "边境城市已夺回";
                if(AwaitingTerminal)return "区段肃清 · 安全终端已解锁";
                if(BossResupply)return "阶段突破 · 前往两侧终端重新装配";
                if(ExitTrialActive)return "出口追击 · 在场 "+game.ActiveEnemies+(regroup>0?" / 增援 "+regroup.ToString("0.0")+"s":" / 待援 "+PendingEnemies);
                if(Sector==2&&!routeResolved)return "管线分岔 · 下层主路 / 上层喷气捷径";
                if(Sector==3)return Boss?"守卫机甲 · "+Boss.Instruction:"进入中央控制室 · 重型守卫警戒";
                var encounter=CurrentEncounter;if(encounter==null)return "前往安全终端";
                if(!activated)return (regroup>0?"交战点肃清 · 前往 ":"推进 · ")+encounter.name;
                if(regroup>0&&PendingEnemies>0)return encounter.name+" · 在场 "+game.ActiveEnemies+" / 增援 "+regroup.ToString("0.0")+"s";
                return encounter.name+" · 在场 "+game.ActiveEnemies+(PendingEnemies>0?" / 待援 "+PendingEnemies:" / 最后一批");
            }
        }
        public Vector3 Destination=>ExitTrialActive?new Vector3(-16,.3f,-44):AwaitingTerminal?terminals[Mathf.Min(Sector,2)].position:Sector==3?bossPoint:encounters[EncounterIndex].center;
        public bool CanFinishShot=>ExitTrialActive?spawned>=6:Sector==3?Boss&&Boss.Defeated:CurrentEncounter!=null&&spawned>=CurrentEncounter.count&&(UpperRouteChosen||EncounterIndex+1>=encounters.Length||encounters[EncounterIndex+1].sector!=Sector);
        public void Initialize(ArenaGame owner){game=owner;}
        public void ResetCampaign()
        {
            Sector=EncounterIndex=spawned=UpgradesClaimed=0;spawnWait=0;sectorStarted=game.Elapsed;
            activated=bossSpawned=Completed=AwaitingTerminal=TerminalOpen=MobilityPending=BossTerminal=Revisiting=AmmoChosen=false;
            ExitTrialActive=false;
            UpperRouteChosen=false;
            routeResolved=false;
            regroup=0;arrivalPending=false;game.Arrival.Clear();
            if(Boss)Boss.Retire();Boss=null;openTerminalIndex=-1;
            Array.Clear(SectorSeconds,0,4);Array.Clear(SectorKills,0,4);
            foreach(var gate in routeGates)if(gate)gate.gameObject.SetActive(true);
            foreach(var supply in supplies)if(supply)supply.gameObject.SetActive(true);
        }
        void Update()
        {
            if(!game||!game.IsPlaying)return;
            if(Input.GetKeyDown(KeyCode.F))TryOpenNearbyTerminal();
            if(game.player.weapon.Loadout.Jump>=2)foreach(var gate in routeGates)if(gate&&gate.gameObject.activeSelf)gate.gameObject.SetActive(false);
            foreach(var supply in supplies)if(supply&&supply.gameObject.activeSelf&&(game.player.transform.position-supply.position).sqrMagnitude<2.2f*2.2f){game.player.Health.Heal(25);supply.gameObject.SetActive(false);ArenaAudio.Instance?.Play(SoundCue.Heal);}
        }
        public void Tick(float dt)
        {
            if(dt<=0||Completed||AwaitingTerminal||TerminalOpen)return;
            if(regroup>0){regroup=Mathf.Max(0,regroup-dt);return;}
            if(ExitTrialActive)
            {
                spawnWait-=dt;
                if(arrivalPending)
                {
                    if(!game.Arrival.Step(dt))return;
                    game.Arrival.Clear();arrivalPending=false;spawnWait=0;
                }
                else if(spawned<6&&spawnWait<=0&&game.ActiveEnemies<EnemyLimit&&spawned%3==0)
                {game.Arrival.Begin(new Vector3(-19,.35f,-42));arrivalPending=true;return;}
                if(spawned<6&&spawnWait<=0&&game.ActiveEnemies<EnemyLimit)
                {
                    Vector3 p=new Vector3(-19,.35f,-42-(spawned%3)*1.5f);
                    if(ArenaTerrain.TryGround(p,out var floor,2))p=floor;
                    if(game.SpawnAuthored(p,1,false,false,false,false)){spawned++;if(spawned==3)regroup=2.4f;}
                    spawnWait=.8f;
                }
                if(spawned==6&&game.ActiveEnemies==0&&!game.Cinema.FinaleActive){ExitTrialActive=false;SectorSeconds[0]=game.Elapsed-sectorStarted;Sector=1;sectorStarted=game.Elapsed;game.FrontierAdvance(2);}
                return;
            }
            if(Sector==3)
            {
                if(!bossSpawned&&Vector3.Distance(game.player.transform.position,bossPoint)<18)
                {
                    if(!arrivalPending){game.Arrival.Begin(bossPoint);arrivalPending=true;return;}
                    if(!game.Arrival.Step(dt))return;
                    game.Arrival.Clear();arrivalPending=false;
                    var enemy=game.SpawnAuthored(bossPoint,4,false,false,true,false);
                    if(enemy){Boss=enemy.GetComponent<FrontierBoss>();if(!Boss)Boss=enemy.gameObject.AddComponent<FrontierBoss>();Boss.Configure(this,game.player,bossCover);bossSpawned=true;}
                }
                if(bossSpawned&&Boss&&Boss.Defeated&&!game.Cinema.FinaleActive){Completed=true;SectorSeconds[3]=game.Elapsed-sectorStarted;game.CompleteFrontier();}
                return;
            }
            var group=encounters[EncounterIndex];
            if(Sector==2&&!routeResolved)
            {
                var p=game.player.transform.position;
                if(game.player.weapon.Loadout.Jump<2)routeResolved=true;
                else if(p.y>=6.6f&&p.z>=-77.6f&&p.z<=-74.4f&&Mathf.Abs(p.x)<=15){UpperRouteChosen=true;routeResolved=true;}
                else if(p.y<2&&p.z<-74&&p.x>-11)routeResolved=true;
                else return;
            }
            if(Sector==2&&!activated&&UpperRouteChosen)
            {
                // The elevated shortcut genuinely bypasses the dense lower-road encounters.
                while(EncounterIndex<encounters.Length&&encounters[EncounterIndex].sector==2&&!HasElevatedSpawns(encounters[EncounterIndex]))EncounterIndex++;
                if(EncounterIndex>=encounters.Length||encounters[EncounterIndex].sector!=2){AwaitingTerminal=true;game.MarkFrontierSectorClear();return;}
                group=encounters[EncounterIndex];
            }
            if(!activated)
            {
                if(Vector3.Distance(game.player.transform.position,group.center)>group.radius)return;
                activated=true;spawned=0;spawnWait=.65f;arrivalPending=false;
                ArenaAudio.Instance?.Play(SoundCue.ChargeWarn,group.center,.6f);
            }
            spawnWait-=dt;
            if(arrivalPending)
            {
                if(!game.Arrival.Step(dt))return;
                game.Arrival.Clear();arrivalPending=false;spawnWait=0;
            }
            else if(spawned<group.count&&spawnWait<=0&&game.ActiveEnemies<EnemyLimit&&spawned%BatchSize==0)
            {
                game.Arrival.Begin(group.spawns[spawned%group.spawns.Length]);arrivalPending=true;return;
            }
            if(spawned<group.count&&spawnWait<=0&&game.ActiveEnemies<EnemyLimit)
            {
                Vector3 point=group.spawns[spawned%group.spawns.Length];
                // Resolve authored slots against physical floors, never move a sniper to the lower floor.
                if(ArenaTerrain.TryGround(point,out var floor,2))point=floor;
                int n=spawned+1;
                var e=game.SpawnAuthored(point,Sector+1,group.chargeEvery>0&&n%group.chargeEvery==0,group.shieldEvery>0&&n%group.shieldEvery==0,false,point.y>5||group.rangeEvery>0&&n%group.rangeEvery==0);
                if(e){spawned++;if(e.Ranged.IsRanged)e.Ranged.HoldPosition=true;}
                spawnWait=SpawnInterval;
                if(e&&spawned%BatchSize==0&&spawned<group.count)regroup=BatchRecovery;
            }
            if(spawned<group.count||game.ActiveEnemies>0||game.Cinema.FinaleActive)return;
            EncounterIndex++;activated=false;arrivalPending=false;game.Arrival.Clear();
            regroup=1.5f;
            if(EncounterIndex>=encounters.Length||encounters[EncounterIndex].sector!=Sector){AwaitingTerminal=true;game.MarkFrontierSectorClear();}
        }
        static bool HasElevatedSpawns(FrontierEncounter encounter)
        {
            if(encounter.spawns==null)return false;
            foreach(var point in encounter.spawns)if(point.y>5)return true;
            return false;
        }
        public bool TerminalAvailable(int index)
        {
            if(terminals==null||index<0||index>=terminals.Length||!terminals[index]||Completed)return false;
            return index<3?index<UpgradesClaimed||AwaitingTerminal&&index==Sector:BossResupply;
        }
        // Shared with guidance: an unlocked terminal is not necessarily safe or visible from here.
        public string TerminalBlockReason(int index)
        {
            if(!TerminalAvailable(index))return "终端未激活 · 清除本区段敌人";
            foreach(var enemy in game.Enemies)
                if(enemy.IsAlive&&!(index>=3&&BossResupply&&enemy.GetComponent<FrontierBoss>()==Boss)&&Vector3.Distance(enemy.transform.position,terminals[index].position)<8)
                    return "终端附近有敌人 · 先解除威胁";
            if(!TerminalVisible(index))return "终端被遮挡 · 绕至正面";
            if(Vector3.Distance(game.player.transform.position,terminals[index].position)>3.5f)return "前往终端 · 靠近后按 F";
            return "";
        }
        public bool TryOpenNearbyTerminal()
        {
            if(!game.IsPlaying||Completed||TerminalOpen)return false;
            int count=Mathf.Min(3,UpgradesClaimed+(AwaitingTerminal&&UpgradesClaimed<=Sector?1:0));
            for(int i=0;i<count;i++)
            {
                if(Vector3.Distance(game.player.transform.position,terminals[i].position)>3.5f)continue;
                if(TerminalBlockReason(i).Length>0)continue;
                OpenTerminal(i,false,!AwaitingTerminal||i!=Sector);return true;
            }
            if(Sector==3&&Boss&&Boss.TransitionPending)
            {
                for(int i=3;i<terminals.Length;i++)if(TerminalBlockReason(i).Length==0){OpenTerminal(i,true,false);return true;}
            }
            return false;
        }
        bool TerminalVisible(int index)
        {
            Vector3 from=game.player.transform.position+Vector3.up*.85f,delta=terminals[index].position+Vector3.up*1.3f-from;
            foreach(var hit in Physics.RaycastAll(from,delta.normalized,delta.magnitude,1<<9,QueryTriggerInteraction.Ignore))if(!hit.transform.IsChildOf(terminals[index]))return false;
            return true;
        }
        void OpenTerminal(int index,bool boss,bool revisit)
        {
            openTerminalIndex=index;TerminalOpen=true;BossTerminal=boss;Revisiting=revisit;
            MobilityPending=!boss&&!revisit;AmmoChosen=false;
            game.EnterFrontierTerminal();
        }
        public bool SelectAmmo(AmmoUpgrade type)
        {
            if(!TerminalOpen||(int)type<0||(int)type>2)return false;
            game.player.weapon.Upgrade(type);AmmoChosen=true;ArenaAudio.Instance?.Play(SoundCue.Select);return true;
        }
        public bool SelectMobility(AmmoUpgrade type)
        {
            if(!TerminalOpen||!MobilityPending||(type!=AmmoUpgrade.Jump&&type!=AmmoUpgrade.DashCapacity)||!game.player.weapon.Loadout.CanUpgrade(type))return false;
            game.player.weapon.Upgrade(type);MobilityPending=false;UpgradesClaimed++;return true;
        }
        public bool LeaveTerminal()
        {
            if(!TerminalOpen||MobilityPending||(!AmmoChosen&&game.player.weapon.Loadout.SelectedAmmo<0))return false;
            TerminalOpen=false;
            if(BossTerminal){Boss.ResumePhase();BossTerminal=false;}
            else if(!Revisiting)
            {
                AwaitingTerminal=false;
                if(Sector==0){ExitTrialActive=true;spawned=0;spawnWait=.8f;}
                else {SectorSeconds[Sector]=game.Elapsed-sectorStarted;Sector++;sectorStarted=game.Elapsed;game.FrontierAdvance(Sector+1);}
            }
            Revisiting=false;game.LeaveFrontierTerminal();return true;
        }
        public void RegisterKill(){SectorKills[Sector]++;}
        public void BossPhaseBreak()
        {
            game.player.Overheat.Cancel();game.projectiles.Clear();SentryBolt.Clear();
            FrontierMortar.Clear();
            foreach(var e in game.Enemies)if(e.IsAlive&&(!Boss||e!=Boss.GetComponent<EnemyController>()))e.gameObject.SetActive(false);
        }
    }
}
