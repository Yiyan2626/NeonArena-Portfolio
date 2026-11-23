using System.Collections.Generic;
using UnityEngine;

namespace NeonArena
{
    [DefaultExecutionOrder(130)]
    public sealed class PlayerOverheat : MonoBehaviour
    {
        public const int RequiredKills = 8;
        public const float Duration = 10, BlinkRange = 18, StrikeDuration=.24f, StrikeInterval=.30f;
        public int Combo { get; private set; }
        public float LastHitAt { get; private set; }=-10;
        public float ActivatedAt { get; private set; }=-10;
        public int Charge { get; private set; }
        public float Remaining { get; private set; }
        public bool Active => Remaining > 0;
        public bool Striking => strikeLeft > 0;
        public int Activations { get; private set; }
        public int Executions { get; private set; }
        public int Style { get; private set; }
        public float Cooldown => cooldown;
        PlayerController player;
        Transform blade;
        // Bloodlight is a 2.25 m double-ended blade. Its prefab datum is one tip,
        // while the narrow central handle is at z=1.125 m.
        public Transform BladeTransform => blade;
        public float StrikeAge => Striking ? StrikeDuration-strikeLeft : 0;
        public Vector3 LeftGripWorld => blade.TransformPoint(new Vector3(0,0,1.235f));
        public Vector3 RightGripWorld => blade.TransformPoint(new Vector3(0,0,1.015f));
        public Vector3 LeftPalmWorld { get; private set; }
        public Vector3 RightPalmWorld { get; private set; }
        bool riggedBlade;
        public void SetRiggedBlade(Vector3 centre,Quaternion rotation,Vector3 leftPalm,Vector3 rightPalm)
        {
            blade.SetPositionAndRotation(centre-rotation*Vector3.forward*1.125f,rotation);
            LeftPalmWorld=leftPalm;RightPalmWorld=rightPalm;riggedBlade=true;
        }
        public Vector3 BladeFocus=>blade?blade.position+blade.forward*1.05f:transform.position+Vector3.up;
        LineRenderer arc, blink, halo, core;
        readonly LineRenderer[] echoes=new LineRenderer[4];
        readonly float[] echoLife=new float[4];
        int echoIndex;
        Material glow;
        float buffered;
        Material cyan, white, grip;
        readonly List<Renderer> guns = new List<Renderer>();
        EnemyController victim;
        float cooldown, strikeLeft;
        bool struck;
        Vector3 strikePoint, heading;
        Quaternion facing;
        public void Initialize(PlayerController owner)
        {
            player = owner;
            cyan = RuntimeArt.Material(ArenaGame.Instance&&ArenaGame.Instance.Frontier?new Color(1,.025f,.06f):new Color(.04f,.88f,1),2.5f);
            white = RuntimeArt.Material(new Color(.8f,1,1),2);
            grip = RuntimeArt.Material(new Color(.06f,.09f,.15f));
            blade = new GameObject("Overheat Laser Katana").transform; blade.SetParent(player.visual,false);
            var source=Resources.Load<GameObject>("Frontier/Bloodlight");
            if(ArenaGame.Instance&&ArenaGame.Instance.Frontier&&source)Instantiate(source,blade,false);
            else {
            RuntimeArt.Box(blade,"Grip",Vector3.zero,new Vector3(.12f,.12f,.45f),grip);
            RuntimeArt.Box(blade,"Guard",new Vector3(0,0,.2f),new Vector3(.44f,.09f,.12f),cyan);
            RuntimeArt.Box(blade,"Plasma Edge",new Vector3(0,0,1.25f),new Vector3(.12f,.045f,2.05f),cyan);
            RuntimeArt.Box(blade,"White Core",new Vector3(0,.025f,1.22f),new Vector3(.045f,.02f,1.96f),white);
            }
            if(ArenaGame.Instance&&ArenaGame.Instance.Frontier)
            {
                var light=blade.gameObject.AddComponent<Light>();light.type=LightType.Point;light.color=new Color(1,.025f,.06f);light.range=3;light.intensity=1.8f;light.shadows=LightShadows.None;
            }
            arc = RuntimeArt.Line(transform,"Blade Crescent",cyan,.12f); arc.positionCount = 28;
            glow=new Material(Shader.Find("NeonArena/BladeGlow"));glow.color=new Color(.08f,.75f,1,.55f);
            halo=RuntimeArt.Line(transform,"Broad Plasma Sweep",glow,.55f);halo.positionCount=28;
            core=RuntimeArt.Line(transform,"Sweep White Core",white,.065f);core.positionCount=28;
            for(int i=0;i<echoes.Length;i++){echoes[i]=RuntimeArt.Line(transform,"Cut Afterimage",glow,.12f);echoes[i].positionCount=28;}
            blink = RuntimeArt.Line(transform,"Blink Trace",white,.07f); blink.positionCount = 2;
            foreach (string name in new[] { "Pulse Rifle", "Rifle Rail" })
            { var item = player.visual.Find(name); if (item) guns.AddRange(item.GetComponentsInChildren<Renderer>(true)); }
            if(player.weapon.visuals.receiver)guns.AddRange(player.weapon.visuals.receiver.GetComponentsInChildren<Renderer>(true));
            foreach (var item in new[] { player.weapon.visuals.piercingModule, player.weapon.visuals.ricochetModule, player.weapon.visuals.laserModule })
                if (item) guns.AddRange(item.GetComponentsInChildren<Renderer>(true));
            Show(false);
        }
        void Show(bool active)
        { if (blade) blade.gameObject.SetActive(active); foreach (var r in guns) if (r) r.enabled = !active; }
        public void RegisterKill()
        { if (!Active) Charge = Mathf.Min(RequiredKills,Charge + 1); }
        void Update() => Tick(Time.deltaTime);
        public void Tick(float dt)
        {
            var game = ArenaGame.Instance;
            if (!game || !game.IsPlaying || game.Cinema.FinaleActive || dt <= 0) return;
            if (!Active && Charge == RequiredKills && game.ActiveEnemies > 0)
            {
                Charge = 0; Remaining = Duration; Activations++; cooldown = 0; Combo=0;ActivatedAt=Time.unscaledTime;
                player.weapon.StopFiring(); game.projectiles.Clear(); Show(true);
                ArenaAudio.Instance?.Play(SoundCue.Overheat);
                ImpactPool.Instance?.Emit(transform.position + Vector3.up,Color.cyan,40);
                if(game.Frontier){blade.localPosition=new Vector3(.45f,.85f,.2f);blade.localRotation=Quaternion.Euler(-35,10,-15);game.Cinema.OverheatEntry(BladeFocus);}
            }
            if (!Active) return;
            if(game.Cinema.IntroActive)return;
            buffered=Mathf.Max(0,buffered-dt);
            for(int i=0;i<echoes.Length;i++){echoLife[i]=Mathf.Max(0,echoLife[i]-dt);echoes[i].enabled=echoLife[i]>0;echoes[i].widthMultiplier=.20f*echoLife[i]/.32f;}
            Remaining = Mathf.Max(0,Remaining - dt); cooldown = Mathf.Max(0,cooldown - dt);
            if (Striking)
            {
                strikeLeft = Mathf.Max(0,strikeLeft - dt);
                if (!struck && strikeLeft <= StrikeDuration-.06f)
                {
                    struck = true;
                    if (victim && victim.IsAlive)
                    {
                        bool elite=victim.Elite && victim.Elite.IsElite;
                        Combo++; LastHitAt=Time.unscaledTime;
                        var struckEnemy=victim;
                        struckEnemy.Health.TakeDamage(elite?struckEnemy.Health.maxHealth*.30f:struckEnemy.Health.Current);
                        if(struckEnemy.Health.IsDead)Executions++;
                        else if(elite)struckEnemy.Elite.BladeStagger();
                        player.viewCamera.GetComponent<ArenaCamera>().Kick(.035f);
                        int index=echoIndex++%echoes.Length;echoLife[index]=.32f;
                        for(int i=0;i<28;i++)echoes[index].SetPosition(i,arc.GetPosition(i));
                        ArenaAudio.Instance?.Play(SoundCue.BladeHit);
                        ImpactPool.Instance?.Emit(strikePoint+Vector3.up*.85f,Color.cyan,40);
                    }
                }
                if (!Striking) { player.visual.localScale = Vector3.one; player.visual.rotation = facing; arc.enabled = blink.enabled = halo.enabled = core.enabled = false; }
            }
            if (!Active) End();
            else if(buffered>0 && !Striking && cooldown<=0)TryExecute();
        }
        public bool CanExecute(EnemyController enemy)
        {
            return enemy && enemy.IsAlive && player.Focus.CanTarget(enemy) &&
                (enemy.transform.position-transform.position).sqrMagnitude <= BlinkRange * BlinkRange;
        }
        public EnemyController FindTarget()
        {
            if (player.Focus.HasTarget && CanExecute(player.Focus.Target) && TryLanding(player.Focus.Target,out _)) return player.Focus.Target;
            EnemyController best = null; float score = float.PositiveInfinity;
            foreach (var e in ArenaGame.Instance.Enemies)
            {
                if (!CanExecute(e) || !TryLanding(e,out _)) continue;
                Vector3 screen = player.viewCamera.WorldToViewportPoint(e.transform.position+Vector3.up);
                if (screen.z <= 0 || screen.x < 0 || screen.x > 1 || screen.y < 0 || screen.y > 1) continue;
                float d = (e.transform.position-transform.position).sqrMagnitude;
                if (d < score) { score = d; best = e; }
            }
            return best;
        }
        public void RequestAttack() { buffered=.18f;TryExecute(); }
        public bool TryExecute(EnemyController enemy = null)
        {
            var game = ArenaGame.Instance;
            if (!Active || Striking || cooldown > 0 || !game.IsPlaying || game.Cinema.FinaleActive||game.Cinema.IntroActive) return false;
            if (!enemy) enemy = FindTarget();
            if (!CanExecute(enemy) || !TryLanding(enemy,out var landing)) return false;
            Vector3 start = transform.position;
            victim = enemy; strikePoint = enemy.transform.position;
            heading = Vector3.ProjectOnPlane(strikePoint-landing,Vector3.up).normalized;
            player.Dash.Cancel(); player.Teleport(landing);
            facing = Quaternion.LookRotation(heading); player.visual.rotation = facing;
            Style = Executions % 3; strikeLeft = StrikeDuration; cooldown = StrikeInterval; struck = false;buffered=0;
            blink.SetPosition(0,start+Vector3.up); blink.SetPosition(1,landing+Vector3.up); blink.enabled = true;
            game.Cinema.Strike(strikePoint,Style);
            ArenaAudio.Instance?.Play(SoundCue.Blink); ArenaAudio.Instance?.Play((SoundCue)((int)SoundCue.SlashA+Style));
            return true;
        }
        public bool TryLanding(EnemyController enemy, out Vector3 landing)
        {
            Vector3 away = Vector3.ProjectOnPlane(transform.position-enemy.transform.position,Vector3.up).normalized;
            if (away.sqrMagnitude < .1f) away = -enemy.visual.forward;
            for (int i = 0; i < 8; i++)
            {
                Vector3 candidate = enemy.transform.position + Quaternion.Euler(0,i*45,0)*away*1.35f;
                if (!ArenaTerrain.TryGround(candidate,out landing,1.2f)) continue;
                if (Mathf.Abs(landing.y-enemy.transform.position.y) > .8f) continue;
                if (Physics.CheckCapsule(landing+Vector3.up*.42f,landing+Vector3.up*1.3f,.4f,(1<<9)|(1<<8),QueryTriggerInteraction.Ignore)) continue;
                if (Physics.Linecast(landing+Vector3.up*.85f,enemy.transform.position+Vector3.up*.85f,1<<9,QueryTriggerInteraction.Ignore)) continue;
                return true;
            }
            landing = transform.position; return false;
        }
        void LateUpdate()
        {
            if (!blade || !Active) return;
            if(riggedBlade)
            {
                // The post-Animator arm pass owns the rigid blade/hand relationship.
                // Effects follow the physical cutting edge instead of a separate 2.8 m arc.
                bool cutting=Striking&&StrikeAge>=.025f&&StrikeAge<=.115f;
                arc.enabled=halo.enabled=core.enabled=cutting;
                if(cutting)
                {
                    Vector3 a=blade.TransformPoint(new Vector3(0,0,1.60f));
                    Vector3 b=blade.TransformPoint(new Vector3(0,0,2.25f));
                    for(int i=0;i<28;i++){Vector3 p=Vector3.Lerp(a,b,i/27f);arc.SetPosition(i,p);halo.SetPosition(i,p);core.SetPosition(i,p);}
                    arc.widthMultiplier=.055f;halo.widthMultiplier=.11f;core.widthMultiplier=.025f;
                }
                return;
            }
            if (!Striking) { blade.localPosition = new Vector3(.45f,.8f,.2f); if(player.Motion)blade.position=player.Motion.Model.joints[8].position; blade.localRotation = Quaternion.Euler(-28,15,-20); return; }
            float t = Mathf.Clamp01(1-strikeLeft/StrikeDuration), sweep = Mathf.SmoothStep(-105,105,t);
            blade.localPosition = new Vector3(.3f,1,.15f);
            if(player.Motion)blade.position=player.Motion.Model.joints[8].position;
            blade.localRotation = Style == 0 ? Quaternion.Euler(0,sweep,0) : Style == 1 ? Quaternion.Euler(-40,sweep,45) : Quaternion.Euler(sweep-90,0,0);
            player.visual.rotation = facing * Quaternion.Euler(0,Mathf.Sin(t*Mathf.PI)* (Style==1?-22:22),Style==2?0:Mathf.Sin(t*Mathf.PI)*-8);
            arc.enabled = halo.enabled = core.enabled = true;
            Color chain=Combo>=8?new Color(1,.65f,.08f):Combo>=4?new Color(.5f,.3f,1):Color.cyan;
            halo.startColor=halo.endColor=chain;
            Quaternion plane = facing * (Style==1?Quaternion.Euler(0,0,45):Style==2?Quaternion.Euler(0,0,90):Quaternion.identity);
            for (int i = 0; i < 28; i++)
            {
                float a = Mathf.Lerp(sweep-55,sweep,i/27f)*Mathf.Deg2Rad;
                Vector3 p=transform.position+Vector3.up+plane*new Vector3(Mathf.Sin(a)*2.8f,0,Mathf.Cos(a)*2.8f);
                arc.SetPosition(i,p);halo.SetPosition(i,p);core.SetPosition(i,p);
            }
            arc.widthMultiplier = .23f*Mathf.Sin(t*Mathf.PI);
            halo.widthMultiplier=.65f*Mathf.Sin(t*Mathf.PI);core.widthMultiplier=.075f*Mathf.Sin(t*Mathf.PI);
        }
        void End()
        {
            Remaining = 0; strikeLeft = buffered = 0; victim = null; riggedBlade=false; Show(false);
            for(int i=0;i<echoes.Length;i++){echoLife[i]=0;if(echoes[i])echoes[i].enabled=false;}
            if (arc) arc.enabled = blink.enabled = halo.enabled = core.enabled = false;
            if (player) { player.visual.localScale = Vector3.one; player.visual.rotation = Quaternion.Euler(0,player.visual.eulerAngles.y,0); }
        }
        public void CancelStrike()
        {
            strikeLeft = buffered = 0; victim = null;
            if (arc) arc.enabled = blink.enabled = halo.enabled = core.enabled = false;
            if (player) player.visual.rotation = Quaternion.Euler(0,player.visual.eulerAngles.y,0);
        }
        public void Cancel() => End();
        public void ResetOverheat()
        { End(); Charge = Activations = Executions = Combo = 0; cooldown = 0; LastHitAt=ActivatedAt=-10; }
        void OnDestroy() { if (cyan) Destroy(cyan); if (white) Destroy(white); if (grip) Destroy(grip); if(glow)Destroy(glow); }
    }
}
