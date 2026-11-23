using System.Collections.Generic;
using UnityEngine;

namespace NeonArena
{
    public enum ArenaState { Ready, Playing, Paused, Reward, Intermission, Dead, Victory }

    [DefaultExecutionOrder(-200)]
    public sealed class ArenaGame : MonoBehaviour
    {
        public static ArenaGame Instance { get; private set; }
        public PlayerController player;
        public ProjectilePool projectiles;
        public EnemyController enemyPrefab;
        public Transform enemyRoot;
        public ArenaHUD hud;
        public ArenaTerrain terrain;
        public Vector3[] spawnPoints;
        public bool autoSpawn = true;
        public CityOperation Operation {get;private set;}
        public SpawnTelegraph Arrival {get;private set;}
        public FrontierCampaign Frontier {get;private set;}
        public int CampaignLength=>terrain&&terrain.satelliteCity?4:3;
        public HealthDrops Drops { get; private set; }
        public CombatCinema Cinema { get; private set; }
        public ArenaVision Vision { get; private set; }
        public ArenaState State { get; private set; } = ArenaState.Ready;
        public bool IsPlaying => State == ArenaState.Playing;
        public int Kills { get; private set; }
        public int Hits { get; private set; }
        public float Elapsed { get; private set; }
        public bool Endless { get; private set; }
        public int ChargeAttacks { get; private set; }
        public float LastHitAt { get; private set; } = -10;
        public IReadOnlyList<EnemyController> Enemies => enemies;
        public int ActiveEnemies
        {
            get { int count = 0; foreach (var enemy in enemies) if (enemy.IsAlive) count++; return count; }
        }
        public int Wave { get; private set; } = 1;
        public int ClearedWaves { get; private set; }
        public int PendingSpawns { get; private set; }
        public int WaveTotal { get; private set; }
        public float Countdown { get; private set; }
        public bool RewardInputReady => State == ArenaState.Reward && Time.unscaledTime >= rewardReadyAt;
        float rewardReadyAt;
        readonly List<EnemyController> enemies = new List<EnemyController>(32);
        float nextSpawn;
        int spawnCursor, spawnedThisWave;

        void Awake()
        {
            Instance = this;
            Frontier=GetComponent<FrontierCampaign>();if(Frontier)Frontier.Initialize(this);
            Drops = gameObject.AddComponent<HealthDrops>();
            Cinema = gameObject.AddComponent<CombatCinema>(); Cinema.Initialize();
            Vision = gameObject.AddComponent<ArenaVision>();
            Operation=gameObject.AddComponent<CityOperation>();Arrival=gameObject.AddComponent<SpawnTelegraph>();
            gameObject.AddComponent<CombatFeedback>();
            if(Frontier){gameObject.AddComponent<FrontierCombatReadability>();gameObject.AddComponent<FrontierRunMetrics>();}
            Time.timeScale = 1;
            // Explicit cap also works when the driver overrides windowed VSync.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 120;
            Physics.IgnoreLayerCollision(8, 8, true);
        }

        void Start()
        {
            player.Health.Died += OnPlayerDeath;
            for (int i = 0; i < 32; i++)
            {
                var enemy = Instantiate(enemyPrefab, enemyRoot);
                enemy.gameObject.SetActive(false);
                enemies.Add(enemy);
            }
            hud.Refresh();
        }

        void Update()
        {
            if (State == ArenaState.Ready && !FrontEnd.SettingsOpen && Input.GetKeyDown(KeyCode.Return)) StartSession();
            else if (State == ArenaState.Victory && Input.GetKeyDown(KeyCode.Return)) ContinueEndless();
            else if (!FrontEnd.SettingsOpen && Input.GetKeyDown(KeyCode.R) && State != ArenaState.Ready) StartSession();
            if (!FrontEnd.SettingsOpen && Input.GetKeyDown(KeyCode.Escape) && (State == ArenaState.Playing || State == ArenaState.Paused)) TogglePause();
            if (RewardInputReady)
            {
                if(Frontier)return;
                if (Input.GetKeyDown(KeyCode.Alpha1)) ChooseUpgrade(AmmoUpgrade.Piercing);
                else if (Input.GetKeyDown(KeyCode.Alpha2)) ChooseUpgrade(AmmoUpgrade.Ricochet);
                else if (Input.GetKeyDown(KeyCode.Alpha3)) ChooseUpgrade(AmmoUpgrade.Laser);
                else if (Input.GetKeyDown(KeyCode.Alpha4)) ChooseUpgrade(AmmoUpgrade.Vitality);
                else if (Input.GetKeyDown(KeyCode.Alpha5)) ChooseUpgrade(AmmoUpgrade.DashCapacity);
                else if (Input.GetKeyDown(KeyCode.Alpha6)) ChooseUpgrade(AmmoUpgrade.DashRecovery);
                else if (Input.GetKeyDown(KeyCode.Alpha7)) ChooseUpgrade(AmmoUpgrade.Jump);
            }
            TickWave(Time.deltaTime);
        }

        public void TickWave(float dt)
        {
            Cinema.Tick(dt);
            if (State == ArenaState.Intermission)
            {
                Countdown -= dt;
                if (Countdown <= 0) BeginWave();
                return;
            }
            if (!IsPlaying) return;
            Elapsed += dt;
            if(Frontier){Frontier.Tick(dt);return;}
            if (!autoSpawn) return;
            Operation.Tick(dt);
            if(!Operation.CanSpawn)return;
            if(Arrival.Active)
            {
                if(Arrival.Step(dt))
                {
                    var p=Arrival.Point;Arrival.Clear();
                    if(terrain.TryEncounterPoint(p,player.transform.position,out var valid,out _)&&Vector3.Distance(valid,p)<1&&Vector3.Distance(valid,player.transform.position)>3)
                        foreach(var enemy in enemies)if(!enemy.IsAlive){ActivateEnemy(enemy,valid);PendingSpawns--;break;}
                }
            }
            nextSpawn -= dt;
            if (PendingSpawns > 0 && !Arrival.Active && nextSpawn <= 0 && ActiveEnemies < Mathf.Min(24, 5 + Wave))
            {
                if(terrain.satelliteCity){if(FindCityEncounter(out var p))Arrival.Begin(p);}
                else if (SpawnEnemy()) PendingSpawns--;
                nextSpawn = Mathf.Max(0.25f, 1 - (Wave - 1) * 0.045f);
            }
            if (PendingSpawns == 0 && !Arrival.Active && ActiveEnemies == 0 && !Cinema.FinaleActive && Operation.CanComplete)
            {
                ClearedWaves++;
                State = ArenaState.Reward;
                rewardReadyAt = Time.unscaledTime + 0.35f;
                projectiles.Clear(); player.weapon.StopFiring();
                player.Dash?.Cancel();
                player.Overheat.CancelStrike();
                player.Health.Heal(25);
                ArenaAudio.Instance?.PlayClear();
                hud.Refresh();
            }
        }
        void BeginWave()
        {
            if(Frontier){terrain.Apply(Wave);if(Wave==1)player.Teleport(terrain.entry);State=ArenaState.Playing;PendingSpawns=0;hud.Refresh();return;}
            Drops.Clear();
            if (terrain)
            {
                terrain.Apply(Wave);
                if(!terrain.satelliteCity||Wave==1)player.Teleport(terrain.entry);
                player.viewCamera.GetComponent<ArenaCamera>().RenderView(true);
            }
            Operation.Begin(Wave);Arrival.Clear();
            WaveTotal = Mathf.Min(48, 5 + 2 * (Wave - 1));
            PendingSpawns = autoSpawn ? WaveTotal : 0;
            nextSpawn = 0;
            spawnedThisWave = 0;
            State = ArenaState.Playing;
            hud.Refresh();
        }
        public bool ChooseUpgrade(AmmoUpgrade type)
        {
            if(Frontier)return false;
            if (!RewardInputReady || !player.weapon.Loadout.CanUpgrade(type)) return false;
            State = ClearedWaves == CampaignLength && !Endless ? ArenaState.Victory : ArenaState.Intermission;
            player.weapon.Upgrade(type);
            ArenaAudio.Instance?.Play(SoundCue.Select);
            Wave++;
            Countdown = 1.8f;
            hud.Refresh();
            return true;
        }
        public bool ContinueEndless()
        {
            if(Frontier)return false;
            if (State != ArenaState.Victory) return false;
            Endless = true; State = ArenaState.Intermission; Countdown = 1.8f;
            hud.Refresh(); return true;
        }

        public void StartSession()
        {
            Operation.ResetOperation();Arrival.Clear();Cinema.ResetCinema(true); Drops.Clear(true); SentryBolt.Clear();FrontierMortar.Clear(); CombatFeedback.Instance?.Clear();FrontierCombatReadability.Instance?.Clear();
            Time.timeScale = 1;
            foreach (var enemy in enemies) enemy.gameObject.SetActive(false);
            projectiles.Clear();
            ImpactPool.Instance?.Clear();
            ArenaAudio.Instance?.ResetSession();
            player.ResetPlayer();
            if(Frontier)player.weapon.Loadout.UseFrontierRules();
            Kills = 0; Hits = 0; Elapsed = 0;
            Endless = false; ChargeAttacks = 0; LastHitAt = -10;
            spawnCursor = 0;
            Wave = 1; ClearedWaves = 0; Countdown = 0;
            if(Frontier)Frontier.ResetCampaign();
            foreach(var tactics in FindObjectsOfType<FrontierTactics030>())tactics.ResetTactics();
            BeginWave();
            hud.Refresh();
        }

        public EnemyController SpawnEnemy()
        {
            if (spawnPoints == null || spawnPoints.Length == 0) return null;
            if(terrain.satelliteCity)
            {
                foreach(var enemy in enemies)
                {
                    if(enemy.IsAlive)continue;
                    if(!FindCityEncounter(out var point))return null;
                    ActivateEnemy(enemy,point);return enemy;
                }
                return null;
            }
            foreach (var enemy in enemies)
            {
                if (enemy.IsAlive) continue;
                for (int n = 0; n < spawnPoints.Length; n++)
                {
                    Vector3 point = spawnPoints[spawnCursor++ % spawnPoints.Length];
                    if(!ArenaTerrain.TryGround(point,out point,5))continue;
                    float spawnDistance=Vector3.Distance(point, player.transform.position);
                    if (spawnDistance < 7) continue;
                    if(terrain.satelliteCity && (spawnDistance > (Wave<3?32:46) || !terrain.HasRoute(point,player.transform.position)))continue;
                    if (Physics.CheckSphere(point + Vector3.up * 0.8f, 0.55f, 1 << 9, QueryTriggerInteraction.Ignore)) continue;
                    bool charger = Wave >= 2 && spawnedThisWave % 4 == 1;
                    bool shielded = Wave >= 2 && spawnedThisWave % 4 == 2;
                    bool elite=autoSpawn && Wave%3==0 && spawnedThisWave==WaveTotal-1;
                    enemy.Spawn(point, player, Wave, charger, shielded,elite,Wave>=2 && spawnedThisWave%4==3);
                    spawnedThisWave++;
                    return enemy;
                }
                return null;
            }
            return null;
        }
        public EnemyController SpawnAuthored(Vector3 point,int tier,bool charger,bool shielded,bool elite,bool ranged)
        {
            foreach(var e in enemies)if(!e.IsAlive){e.Spawn(point,player,tier,charger,shielded,elite,ranged);return e;}return null;
        }
        public void MarkFrontierSectorClear(){ClearedWaves++;player.Health.Heal(25);ArenaAudio.Instance?.PlayClear();}
        public void EnterFrontierTerminal(){State=ArenaState.Reward;rewardReadyAt=Time.unscaledTime;projectiles.Clear();SentryBolt.Clear();player.weapon.StopFiring();player.Dash.Cancel();player.Overheat.Cancel();Cinema.ResetCinema();player.Health.DamageBlocked=true;hud.Refresh();}
        public void LeaveFrontierTerminal(){player.Health.DamageBlocked=false;State=ArenaState.Playing;hud.Refresh();}
        public void FrontierAdvance(int wave){Wave=wave;terrain.Apply(Wave);hud.Refresh();}
        public void CompleteFrontier(){ClearedWaves=4;State=ArenaState.Victory;player.weapon.StopFiring();hud.Refresh();}

        bool FindCityEncounter(out Vector3 selected)
        {
            selected=Vector3.zero;float best=float.NegativeInfinity;
            Vector3 origin=player.transform.position;int sequence=spawnCursor++;
            var camera=player.viewCamera;
            // Score a local ring first. A fixed south-to-north grid queued the entire first wave behind the park stairs.
            for(int i=0;i<72;i++)
            {
                float angle=(i%24)*Mathf.PI/12+sequence*.61f;
                float radius=9+(i/24)*4;
                Vector3 candidate=origin+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*radius;
                if(!terrain.TryEncounterPoint(candidate,origin,out var point,out float route))continue;
                float distance=Vector3.Distance(point,origin);if(distance<7||route>27)continue;
                float spacing=20;foreach(var e in enemies)if(e.IsAlive)spacing=Mathf.Min(spacing,Vector3.Distance(e.transform.position,point));
                if(spacing<1.4f)continue;
                bool visible=Vision.CanObserve(point);Vector3 screen=camera.WorldToViewportPoint(point+Vector3.up);
                bool inFrame=screen.z>0&&screen.x>.08f&&screen.x<.92f&&screen.y>.12f&&screen.y<.88f;
                float sectorPenalty=Operation.Enabled?Mathf.Max(0,Vector3.Distance(point,Operation.Destination)-17)*2:0;
                float score=(visible?35:0)+(inFrame?22:0)-Mathf.Abs(distance-11)-route*.8f+Mathf.Min(spacing,4)-sectorPenalty;
                if(score>best){best=score;selected=point;}
            }
            if(best>float.NegativeInfinity)return true;
            // Indoor rooms and elevated players may not have a suitable full ring.
            foreach(var candidate in spawnPoints)
            {
                if(Vector3.Distance(candidate,origin)>32)continue;
                if(!terrain.TryEncounterPoint(candidate,origin,out var point,out float route))continue;
                float distance=Vector3.Distance(point,origin);if(distance<7||route>42)continue;
                bool occupied=false;foreach(var e in enemies)if(e.IsAlive&&Vector3.Distance(e.transform.position,point)<1.4f){occupied=true;break;}
                if(occupied)continue;
                float score=(Vision.CanObserve(point)?25:0)-route;
                if(score>best){best=score;selected=point;}
            }
            return best>float.NegativeInfinity;
        }
        void ActivateEnemy(EnemyController enemy,Vector3 point)
        {
            bool charger=Wave>=2&&spawnedThisWave%4==1,shielded=Wave>=2&&spawnedThisWave%4==2;
            bool elite=autoSpawn&&Wave%(terrain.satelliteCity?4:3)==0&&spawnedThisWave==WaveTotal-1;
            enemy.Spawn(point,player,Wave,charger,shielded,elite,Wave>=2&&spawnedThisWave%4==3);
            spawnedThisWave++;
        }

        public void RegisterKill(Vector3? position = null)
        {
            if (!IsPlaying) return;
            Kills++; player.Overheat.RegisterKill();
            if(Frontier)Frontier.RegisterKill();
            player.viewCamera.GetComponent<ArenaCamera>()?.Kick(.035f);
            if (position.HasValue)
            {
                Drops.TryDrop(position.Value,Random.value);
                if (autoSpawn && PendingSpawns == 0 && !Arrival.Active && ActiveEnemies == 0 && Operation.CanComplete&&(!Frontier||Frontier.CanFinishShot)) Cinema.Finish(position.Value);
            }
        }
        public void RegisterHit() { if (IsPlaying) { Hits++; RegisterDamageFeedback(); } }
        public void RegisterDamageFeedback() { if (IsPlaying) LastHitAt = Time.unscaledTime; }
        public void RegisterCharge() { if (IsPlaying) ChargeAttacks++; }
        void OnPlayerDeath() { Cinema.ResetCinema(); player.Overheat.Cancel(); State = ArenaState.Dead; player.weapon.StopFiring(); player.Dash?.Cancel(); hud.Refresh(); }
        public void TogglePause()
        {
            if (State == ArenaState.Playing) { State = ArenaState.Paused; Time.timeScale = 0; player.weapon.StopFiring(); }
            else if (State == ArenaState.Paused) { State = ArenaState.Playing; }
            Cinema.ApplyClock();
            hud.Refresh();
        }
        void OnApplicationFocus(bool focus)
        {
            if (!focus && !Application.isBatchMode && State == ArenaState.Playing) TogglePause();
        }
        void OnDestroy()
        {
            if (player && player.Health != null) player.Health.Died -= OnPlayerDeath;
            if (Instance == this) { Instance = null; Time.timeScale = 1; }
        }
    }
}
