using UnityEngine;
using UnityEngine.UI;
using static NeonArena.ArenaTheme;

namespace NeonArena
{
    [DefaultExecutionOrder(200)]
    public sealed class ArenaHUD : MonoBehaviour
    {
        public Text healthLabel,killsLabel,timerLabel,enemiesLabel,accuracyLabel;
        public Image healthFill;
        public GameObject overlay;
        public Text overlayEyebrow,overlayTitle,overlayBody,buttonLabel,footerHint;
        public Button primaryButton;
        public RectTransform crosshair;
        public Image damageVeil;
        Text wave, focusLabel, heatLabel, cinematicLabel, healthWarning, dashLabel;
        Image dashFill,helpBacking;
        CriticalEdgeGraphic criticalEdge;
        Image heatFill, cinemaTop, cinemaBottom;
        GameObject heatPanel;
        Transform headerPanel, vitalsPanel;
        Text controls,flightLabel,visionLabel,comboLabel;
        Image fuelFill,bladeFlash;
        GameObject flightPanel;
        RectTransform targetReticle;
        float nextRefresh,lastHealth=100,veilAlpha;
        ArenaGlyph aimGlyph;
        void Awake()
        {
            gameObject.AddComponent<CombatPresentation>();
            if(!GetComponent<FrontierScenePolish028>())gameObject.AddComponent<FrontierScenePolish028>();
            var top=Box("Header",transform,new Vector2(0,-44),new Vector2(1600,88),new Color(.025f,.035f,.05f,.92f),new Vector2(.5f,1));
            headerPanel=top.transform;
            Box("Header Rule",top.transform,new Vector2(0,-43),new Vector2(1536,1),new Color(.65f,.58f,.4f,.5f));
            Text("Brand",top.transform,new Vector2(-590,5),new Vector2(360,38),"NEON  /  ARENA",24,Paper,TextAnchor.MiddleLeft);
            Text("Build",top.transform,new Vector2(-590,-23),new Vector2(360,20),"FIELD EXERCISE     /     0.13",11,Muted,TextAnchor.MiddleLeft);
            wave=Text("Wave",top.transform,new Vector2(0,8),new Vector2(580,28),"边境试炼",22,Gold);
            timerLabel=Text("Timer",top.transform,new Vector2(0,-22),new Vector2(160,22),"00:00",15,Muted);
            killsLabel=Text("Kills",top.transform,new Vector2(600,9),new Vector2(300,32),"",22,Paper,TextAnchor.MiddleRight);
            accuracyLabel=Text("Accuracy",top.transform,new Vector2(600,-22),new Vector2(300,22),"",12,Muted,TextAnchor.MiddleRight);
            var vitals=Box("Vitals",transform,new Vector2(193,81),new Vector2(330,106),new Color(.035f,.045f,.065f,.92f),Vector2.zero);
            vitalsPanel=vitals.transform;
            Icon("Vitality Icon",vitals.transform,new Vector2(-126,10),40,3,Gold);
            Text("HP Caption",vitals.transform,new Vector2(35,32),new Vector2(210,22),"生命 / INTEGRITY",12,Muted,TextAnchor.MiddleLeft);
            healthLabel=Text("Health",vitals.transform,new Vector2(35,5),new Vector2(210,36),"",25,Paper,TextAnchor.MiddleLeft);
            Box("Health Track",vitals.transform,new Vector2(0,-32),new Vector2(290,6),new Color(.14f,.18f,.21f));
            healthFill=Box("Health Fill",vitals.transform,new Vector2(-145,-32),new Vector2(290,6),Gold); healthFill.rectTransform.pivot=new Vector2(0,.5f);
            enemiesLabel=Text("Hostiles",transform,new Vector2(-172,165),new Vector2(285,26),"",15,Paper,TextAnchor.MiddleRight,new Vector2(1,0));
            controls=Text("Controls",transform,new Vector2(0,17),new Vector2(1150,24),"WASD 移动   ·   左键开火   ·   右键自由环绕   ·   空格跳跃 / Shift 冲刺   ·   Q E 转视角   ·   Tab 俯视   ·   Esc 暂停   ·   M 地图层   ·   F3 静音",12,Muted,anchor:new Vector2(.5f,0));
            damageVeil=Box("Damage Veil",transform,Vector2.zero,Vector2.zero,Color.clear); damageVeil.rectTransform.anchorMin=Vector2.zero;damageVeil.rectTransform.anchorMax=Vector2.one;
            aimGlyph=Icon("Aim",transform,Vector2.zero,27,6,Gold);crosshair=aimGlyph.rectTransform;
            targetReticle=Rect("Focus Reticle",transform,Vector2.zero,new Vector2(100,100));
            Icon("Target Ring",targetReticle,Vector2.zero,86,6,Gold);
            focusLabel=Text("Target Info",targetReticle,new Vector2(0,66),new Vector2(320,50),"",15,Gold);
            heatPanel=Box("Heat Panel",transform,new Vector2(0,-170),new Vector2(720,56),new Color(.025f,.07f,.09f,.85f),new Vector2(.5f,1)).gameObject;
            heatLabel=Text("Heat",heatPanel.transform,new Vector2(0,7),new Vector2(700,32),"",16,Color.cyan);
            Box("Heat Track",heatPanel.transform,new Vector2(0,-19),new Vector2(670,4),new Color(.1f,.2f,.23f));
            heatFill=Box("Heat Fill",heatPanel.transform,new Vector2(-335,-19),new Vector2(670,4),Color.cyan);heatFill.rectTransform.pivot=new Vector2(0,.5f);
            cinemaTop=Box("Cinema Top",transform,new Vector2(0,-23),new Vector2(1600,46),Color.black,new Vector2(.5f,1));
            cinemaBottom=Box("Cinema Bottom",transform,new Vector2(0,23),new Vector2(1600,46),Color.black,new Vector2(.5f,0));
            cinematicLabel=Text("Final Strike",transform,new Vector2(0,-240),new Vector2(900,55),"",32,Gold,anchor:new Vector2(.5f,1));
            flightPanel=Box("Mobility",transform,new Vector2(190,-162),new Vector2(330,80),new Color(.025f,.06f,.09f,.9f),new Vector2(0,1)).gameObject;
            flightLabel=Text("Flight",flightPanel.transform,new Vector2(0,9),new Vector2(314,48),"",14,Color.cyan);
            Box("Fuel Track",flightPanel.transform,new Vector2(0,-29),new Vector2(296,4),Muted);
            fuelFill=Box("Fuel",flightPanel.transform,new Vector2(-148,-29),new Vector2(296,4),Color.cyan);fuelFill.rectTransform.pivot=new Vector2(0,.5f);
            visionLabel=Text("Vision State",transform,new Vector2(190,-225),new Vector2(336,40),"",14,Gold,anchor:new Vector2(0,1));
            comboLabel=Text("Blade Combo",transform,new Vector2(0,-226),new Vector2(700,34),"",24,Color.cyan,anchor:new Vector2(.5f,1));
            bladeFlash=Box("Blade Impact Flash",transform,Vector2.zero,Vector2.zero,Color.clear);bladeFlash.rectTransform.anchorMin=Vector2.zero;bladeFlash.rectTransform.anchorMax=Vector2.one;
            overlay=Box("Session Overlay",transform,Vector2.zero,Vector2.zero,new Color(.018f,.025f,.04f,.88f)).gameObject;
            var veil=overlay.GetComponent<RectTransform>();veil.anchorMin=Vector2.zero;veil.anchorMax=Vector2.one; overlay.GetComponent<Image>().raycastTarget=true;
            var card=Box("Session Card",overlay.transform,Vector2.zero,new Vector2(730,418),new Color(.08f,.10f,.13f));
            Box("Gold Edge",card.transform,new Vector2(-363,0),new Vector2(3,418),Gold);
            Icon("Session Seal",card.transform,new Vector2(283,119),85,6,new Color(.78f,.68f,.43f,.45f));
            overlayEyebrow=Text("Eyebrow",card.transform,new Vector2(-25,155),new Vector2(600,26),"",13,Gold,TextAnchor.MiddleLeft);
            overlayTitle=Text("Title",card.transform,new Vector2(-25,96),new Vector2(600,62),"",40,Paper,TextAnchor.MiddleLeft);
            overlayBody=Text("Body",card.transform,new Vector2(-25,9),new Vector2(600,106),"",18,Muted,TextAnchor.MiddleLeft);
            var action=Box("Primary Action",card.transform,new Vector2(-166,-119),new Vector2(270,50),Gold);action.raycastTarget=true;
            primaryButton=action.gameObject.AddComponent<Button>();primaryButton.targetGraphic=action;
            buttonLabel=Text("Action",action.transform,Vector2.zero,new Vector2(260,42),"",19,Ink);
            footerHint=Text("Hint",card.transform,new Vector2(-25,-174),new Vector2(600,28),"",13,Muted,TextAnchor.MiddleLeft);
        }
        bool showHelp;
        public bool HelpOpen => showHelp;
        void Start()
        {
            primaryButton.onClick.AddListener(OnPrimary);
            LayoutFieldHUD();
            Refresh();
        }
        static void Place(Transform t,Vector2 position,Vector2 size,Vector2? anchor=null)
        {
            var r=t.GetComponent<RectTransform>();r.localScale=Vector3.one;r.anchoredPosition=position;r.sizeDelta=size;
            if(anchor.HasValue)r.anchorMin=r.anchorMax=anchor.Value;
        }
        void LayoutFieldHUD()
        {
            Place(headerPanel,new Vector2(0,-34),new Vector2(1600,68));
            headerPanel.GetComponent<Image>().color=Color.clear;
            var telemetry=Box("Combat telemetry",headerPanel,new Vector2(620,0),new Vector2(310,42),new Color(.01f,.025f,.04f,.82f));
            telemetry.transform.SetAsFirstSibling();
            headerPanel.Find("Build").gameObject.SetActive(false);headerPanel.Find("Header Rule").gameObject.SetActive(false);
            Place(headerPanel.Find("Brand"),new Vector2(-445,1),new Vector2(650,26));
            headerPanel.Find("Brand").GetComponent<Text>().text="FRONTIER / 城市行动";
            headerPanel.Find("Brand").GetComponent<Text>().fontSize=18;
            headerPanel.Find("Brand").GetComponent<Text>().color=FieldUI.Cyan;
            Place(wave.transform,new Vector2(-445,-33),new Vector2(650,32));wave.fontSize=26;wave.alignment=TextAnchor.MiddleLeft;wave.color=FieldUI.Paper;
            Place(timerLabel.transform,new Vector2(520,0),new Vector2(100,28));timerLabel.fontSize=20;
            Place(killsLabel.transform,new Vector2(675,0),new Vector2(190,30));killsLabel.fontSize=23;
            Place(accuracyLabel.transform,new Vector2(646,-29),new Vector2(250,22));accuracyLabel.fontSize=20;
            // One compact instrument cluster, 68% of the previous left-hand footprint.
            Place(vitalsPanel,new Vector2(208,172),new Vector2(360,216));FieldUI.Decorate(vitalsPanel.GetComponent<Image>(),FieldUI.Cyan);
            Place(vitalsPanel.Find("Vitality Icon"),new Vector2(-145,76),new Vector2(26,26));
            Place(vitalsPanel.Find("HP Caption"),new Vector2(103,76),new Vector2(110,23));
            vitalsPanel.Find("HP Caption").GetComponent<Text>().text="生命 / HP";
            vitalsPanel.Find("HP Caption").GetComponent<Text>().fontSize=18;
            Place(healthLabel.transform,new Vector2(-38,75),new Vector2(164,38));healthLabel.fontSize=30;
            Place(vitalsPanel.Find("Health Track"),new Vector2(0,48),new Vector2(320,7));
            Place(healthFill.transform,new Vector2(-160,48),new Vector2(320,7));
            healthWarning=Text("Critical integrity",vitalsPanel,new Vector2(103,76),new Vector2(110,26),"",20,FieldUI.Danger,TextAnchor.MiddleRight);
            heatPanel.transform.SetParent(vitalsPanel,false);
            Place(heatPanel.transform,new Vector2(0,15),new Vector2(320,45),new Vector2(.5f,.5f));heatPanel.GetComponent<Image>().color=Color.clear;
            Place(heatLabel.transform,new Vector2(0,5),new Vector2(320,30));heatLabel.fontSize=23;heatLabel.alignment=TextAnchor.MiddleLeft;heatLabel.color=FieldUI.Amber;
            Place(heatPanel.transform.Find("Heat Track"),new Vector2(0,-16),new Vector2(320,4));
            Place(heatFill.transform,new Vector2(-160,-16),new Vector2(320,4));heatFill.color=FieldUI.Amber;
            flightPanel.transform.SetParent(vitalsPanel,false);
            Place(flightPanel.transform,new Vector2(0,-59),new Vector2(320,90),new Vector2(.5f,.5f));flightPanel.GetComponent<Image>().color=Color.clear;
            Place(flightLabel.transform,new Vector2(0,-21),new Vector2(320,28));flightLabel.fontSize=20;flightLabel.alignment=TextAnchor.MiddleLeft;flightLabel.color=FieldUI.Muted;
            Place(flightPanel.transform.Find("Fuel Track"),new Vector2(0,-40),new Vector2(320,3));
            Place(fuelFill.transform,new Vector2(-160,-40),new Vector2(320,3));fuelFill.color=FieldUI.Cyan;
            dashLabel=Text("Dash readiness",flightPanel.transform,new Vector2(0,20),new Vector2(320,30),"",23,FieldUI.Paper,TextAnchor.MiddleLeft);
            Box("Dash recharge track",flightPanel.transform,new Vector2(0,0),new Vector2(320,3),new Color(.1f,.22f,.25f));
            dashFill=Box("Dash recharge",flightPanel.transform,new Vector2(-160,0),new Vector2(320,3),FieldUI.Cyan);dashFill.rectTransform.pivot=new Vector2(0,.5f);
            Place(visionLabel.transform,new Vector2(216,-184),new Vector2(376,45));visionLabel.fontSize=20;
            Place(enemiesLabel.transform,new Vector2(-213,91),new Vector2(370,52));enemiesLabel.fontSize=23;
            Place(controls.transform,new Vector2(0,28),new Vector2(1480,44));controls.fontSize=20;
            focusLabel.fontSize=23;focusLabel.rectTransform.sizeDelta=new Vector2(390,62);
            Place(comboLabel.transform,new Vector2(0,176),new Vector2(700,34),new Vector2(.5f,0));
            // Keep the backing and text in one layout group, with text rendered last.
            var helpRoot=Rect("Controls surround",transform,Vector2.zero,Vector2.zero);
            helpRoot.anchorMin=Vector2.zero;helpRoot.anchorMax=Vector2.one;helpRoot.offsetMin=helpRoot.offsetMax=Vector2.zero;
            helpBacking=Box("Help contrast",helpRoot,new Vector2(0,28),new Vector2(1500,48),new Color(.01f,.025f,.04f,.88f),new Vector2(.5f,0));
            controls.transform.SetParent(helpRoot,false);controls.transform.SetAsLastSibling();
            helpBacking.gameObject.AddComponent<HUDVisibilityMirror>().source=controls.gameObject;
            criticalEdge=Rect("Critical peripheral warning",transform,Vector2.zero,Vector2.zero).gameObject.AddComponent<CriticalEdgeGraphic>();
            criticalEdge.rectTransform.anchorMin=Vector2.zero;criticalEdge.rectTransform.anchorMax=Vector2.one;criticalEdge.rectTransform.offsetMin=criticalEdge.rectTransform.offsetMax=Vector2.zero;criticalEdge.raycastTarget=false;
            criticalEdge.transform.SetAsFirstSibling();

            var card=overlay.transform.Find("Session Card");FieldUI.Decorate(card.GetComponent<Image>(),FieldUI.Cyan);
            Place(card,Vector2.zero,new Vector2(860,448));
            Place(overlayBody.transform,new Vector2(0,2),new Vector2(760,128));overlayBody.fontSize=23;
            Place(overlayTitle.transform,new Vector2(0,101),new Vector2(760,62));
            Place(overlayEyebrow.transform,new Vector2(0,173),new Vector2(760,28));
            Place(footerHint.transform,new Vector2(0,-190),new Vector2(760,30));
            card.Find("Gold Edge").gameObject.SetActive(false);overlayEyebrow.fontSize=20;overlayEyebrow.color=FieldUI.Cyan;
            footerHint.fontSize=20;overlayBody.color=FieldUI.Paper;
            primaryButton.GetComponent<Image>().color=FieldUI.Cyan;
        }
        void LateUpdate()
        {
            var game=ArenaGame.Instance;if(!game)return;
            if(Input.GetKeyDown(KeyCode.H))showHelp=!showHelp;
            Place(controls.transform,new Vector2(0,showHelp?59:28),new Vector2(showHelp?1480:720,showHelp?106:36));
            Place(helpBacking.transform,controls.rectTransform.anchoredPosition,controls.rectTransform.sizeDelta+new Vector2(20,8));
            Place(vitalsPanel,new Vector2(208,showHelp?236:158),new Vector2(360,216));
            Place(enemiesLabel.transform,new Vector2(-213,showHelp?176:91),new Vector2(370,52));
            var focus=game.player.Focus;
            bool finale=game.Cinema.FinaleActive;
            flightPanel.SetActive(game.IsPlaying && !finale);visionLabel.gameObject.SetActive(game.IsPlaying && !finale && game.Vision.PlayerInBrush);comboLabel.gameObject.SetActive(game.IsPlaying && !finale);
            var flight=game.player.Flight;
            var dash=game.player.Dash;
            dashLabel.text="冲刺 "+dash.Charges+" / "+dash.MaxCharges+(dash.CooldownLeft>0?"   "+dash.CooldownLeft.ToString("0.0")+"s":"   SHIFT 就绪");
            dashLabel.color=dash.Charges>0?FieldUI.Paper:FieldUI.Amber;
            Fill(dashFill,dash.Charges==dash.MaxCharges?1:1-dash.CooldownLeft/dash.cooldown);
            flightLabel.text=flight.Rank<2 ? "空格跳跃 Lv."+flight.Rank+" · Lv.2 喷气" : "喷气 "+flight.Fuel.ToString("0.0")+"s · 长按空格";
            Fill(fuelFill,flight.Rank<2?0:flight.Fuel/PlayerFlight.Capacity);
            visionLabel.text=game.Vision.PlayerInBrush?"草丛视野受限  ·  仅照亮附近 7 米":"视野照明  ·  暗区敌人不可见 / 地图不显示敌人";
            int combo=game.player.Overheat.Combo;
            comboLabel.text=game.player.Overheat.Active && combo>0?(combo>=8?"超 越":combo>=4?"连 锁":"压 制")+"  /  连斩 × "+combo:"";
            float flash=Mathf.Clamp01(1-(Time.unscaledTime-game.player.Overheat.LastHitAt)/.1f);
            bladeFlash.color=new Color(.4f,.9f,1,game.IsPlaying && !finale?flash*.07f*CombatPresentation.FlashGain:0);
            headerPanel.gameObject.SetActive(!finale);vitalsPanel.gameObject.SetActive(!finale);controls.gameObject.SetActive(!finale && (showHelp||game.Elapsed<12));enemiesLabel.gameObject.SetActive(!finale);
            float bars=game.Cinema.Letterbox;
            cinemaTop.gameObject.SetActive(bars>0);cinemaBottom.gameObject.SetActive(bars>0);
            cinemaTop.rectTransform.localScale=new Vector3(1,bars,1);cinemaBottom.rectTransform.localScale=new Vector3(1,bars,1);
            cinematicLabel.text=game.Cinema.IntroActive?"过 热 启 动   /   BLOODLIGHT":game.Cinema.FinaleActive ? "终 结    /    FINAL STRIKE" : "";
            bool locked=focus && focus.HasTarget;
            crosshair.gameObject.SetActive(game.IsPlaying && !locked);
            crosshair.position=game.player.viewCamera.GetComponent<ArenaCamera>().UsesFollowControls || game.player.viewCamera.GetComponent<ArenaCamera>().IsOrbiting ? new Vector3(Screen.width*.5f,Screen.height*.5f) : Input.mousePosition;
            var orbit=game.player.viewCamera.GetComponent<ArenaCamera>();
            if(orbit.UsesFollowControls&&orbit.IsOrbiting)
            {
                var aimScreen=game.player.viewCamera.WorldToScreenPoint(game.player.AimPoint);
                crosshair.position=aimScreen;
                crosshair.gameObject.SetActive(game.IsPlaying&&aimScreen.z>0&&aimScreen.x>0&&aimScreen.x<Screen.width&&aimScreen.y>0&&aimScreen.y<Screen.height);
            }
            bool hit=Time.unscaledTime-game.LastHitAt<.09f; crosshair.localScale=Vector3.one*(hit?1.3f:1);aimGlyph.color=game.player.weapon.AimBlocked?new Color(1,.28f,.18f):hit?Color.white:Gold;
            targetReticle.gameObject.SetActive(game.IsPlaying && locked);
            if(locked)
            {
                targetReticle.position=game.player.viewCamera.WorldToScreenPoint(focus.AimPoint);
                float uiScale=Screen.width/1600f;
                Vector3 caption=targetReticle.position;
                caption.y=Mathf.Clamp(caption.y+(caption.y>Screen.height-290*uiScale?-66:66)*uiScale,170*uiScale,Screen.height-260*uiScale);
                caption.x=Mathf.Clamp(caption.x,170*uiScale,Screen.width-170*uiScale);
                focusLabel.rectTransform.position=caption;
                var enemy=focus.Target;bool shield=enemy.Shield && enemy.Shield.IsIntact;
                bool behind=Vector3.Dot(enemy.visual.forward,(game.player.transform.position-enemy.transform.position).normalized)<-.3f;
                focusLabel.text=shield ? (behind?"背部暴露 · 可攻击本体":"正面护盾 · 移动与冲刺绕后") : "锁定目标 · 左键射击";
            }
            float health=game.player.Health.Current;if(health<lastHealth)veilAlpha=.16f;lastHealth=health;
            veilAlpha=Mathf.MoveTowards(veilAlpha,0,Time.unscaledDeltaTime*.7f);damageVeil.color=new Color(1,.13f,.08f,veilAlpha);
            if(Time.unscaledTime>=nextRefresh){nextRefresh=Time.unscaledTime+.1f;Refresh();}
        }
        public void Refresh()
        {
            var game=ArenaGame.Instance;if(!game || game.player.Health==null)return;
            var heat=game.player.Overheat;
            heatPanel.SetActive(game.IsPlaying && !game.Cinema.FinaleActive);
            heatLabel.text=heat.Active?"过热斩击  "+heat.Remaining.ToString("0.0")+"s":"过热  "+heat.Charge+" / "+PlayerOverheat.RequiredKills;
            Fill(heatFill,heat.Active ? heat.Remaining/PlayerOverheat.Duration : heat.Charge/(float)PlayerOverheat.RequiredKills);
            float ratio=game.player.Health.Current/game.player.Health.maxHealth;Fill(healthFill,ratio);healthFill.color=ratio>.3f?FieldUI.Cyan:FieldUI.Danger;
            if(criticalEdge)criticalEdge.gameObject.SetActive(game.IsPlaying && ratio<=.3f && !game.Cinema.FinaleActive);
            if(healthWarning)healthWarning.text=ratio<=.3f?"低生命":"";
            vitalsPanel.Find("HP Caption").gameObject.SetActive(ratio>.3f);
            healthLabel.color=ratio<=.3f?FieldUI.Danger:FieldUI.Paper;
            vitalsPanel.GetComponent<Image>().color=ratio<=.3f?new Color(.19f,.045f,.04f,.96f):new Color(.035f,.065f,.085f,.94f);
            healthLabel.text=Mathf.CeilToInt(game.player.Health.Current)+" / "+Mathf.CeilToInt(game.player.Health.maxHealth);
            killsLabel.text=game.Kills.ToString("00")+"  /  击破";
            int visibleEnemies=0;foreach(var e in game.Enemies)if(e.IsAlive&&e.IsVisibleToPlayer)visibleEnemies++;
            enemiesLabel.text="敌人 "+game.ActiveEnemies.ToString("00")+" · 可见 "+visibleEnemies.ToString("00")+(game.PendingSpawns>0?" · 待入场 "+game.PendingSpawns:visibleEnemies==0&&game.ActiveEnemies>0?" · 接近中":"");
            timerLabel.text=((int)game.Elapsed/60).ToString("00")+":"+((int)game.Elapsed%60).ToString("00");
            accuracyLabel.text=(game.player.weapon.ShotsFired==0?"—":Mathf.RoundToInt(100f*game.Hits/game.player.weapon.ShotsFired)+"%")+"  命中率";
            wave.text=game.Frontier?FrontierCampaign.Names[game.Frontier.Sector]:game.State==ArenaState.Ready?"卫 星 城 行 动":game.Operation.Enabled?game.Operation.SectorName:"第 "+game.Wave.ToString("00")+" 关 / "+game.terrain.LayoutName;
            accuracyLabel.gameObject.SetActive(game.State==ArenaState.Paused||showHelp);
            controls.text=showHelp?"WASD 移动 · 左键射击 · 空格跳跃 · Shift 冲刺 · 右键自由观察 · Q/E 转身\nTab 俯视 · M 地图层 · Esc 暂停 · F2 震动 · F3 音效 · F4 闪光\n地图：蓝● 玩家 · 红● 敌人 · 青■ 目标 · 紫● 迅捷 · 橙● 射手 · 红■ 盾怪 · 金■ 精英 · ↑↓ 分层":"鼠标转向   ·   右键观察   ·   H 展开操作帮助";
            bool show=game.State==ArenaState.Ready||game.State==ArenaState.Paused||game.State==ArenaState.Dead||game.State==ArenaState.Victory;overlay.SetActive(show);if(!show)return;
            overlay.transform.SetAsLastSibling();
            if(game.State==ArenaState.Ready)
            {overlayEyebrow.text="TACTICAL TRIAL  /  MODULAR ARSENAL";overlayTitle.text="进入边境试炼";overlayBody.text="关卡逐步加入高台、坑地、楼房与迷宫。\n击杀 8 名敌人进入 10 秒过热：无敌、左键瞬移斩击。\n血包靠近拾取；右键自由转镜头，WASD 始终随镜头移动。";buttonLabel.text="开始挑战  →";footerHint.text="Enter 开始 · 空格跳跃 / Shift 冲刺 · 奖励 1—7 · F3 静音";}
            else if(game.State==ArenaState.Paused)
            {overlayEyebrow.text="SIMULATION ON HOLD";overlayTitle.text="战斗暂停";overlayBody.text="鼠标转动角色，镜头自动跟随；右键自由观察。\nWASD 移动 · 左键射击 · 空格跳跃 · Shift 冲刺\n滚轮缩放 · Tab 俯视 · M 地图层 · H 操作帮助";buttonLabel.text="继续战斗  →";footerHint.text="Esc 继续 · F1 系统设置 · R 重新开始（清空本局成长）";}
            else if(game.State==ArenaState.Victory)
            {overlayEyebrow.text="OPERATION COMPLETE";overlayTitle.text="试炼完成";overlayBody.text=game.Kills+" 次击破   /   "+timerLabel.text+"   /   "+game.player.Dash.Uses+" 次冲刺\n保留当前构筑，进入后续无尽挑战。";buttonLabel.text="继续深入  →";footerHint.text="Enter 保留成长继续 · R 从第一关重新挑战";}
            else
            {overlayEyebrow.text="SESSION COMPLETE";overlayTitle.text="信号中断";overlayBody.text="完成 "+game.ClearedWaves+" 关   /   "+game.Kills+" 次击破\n下一次尝试生命与冲刺强化，或绕到盾兵背后。";buttonLabel.text="再次挑战  →";footerHint.text="R 重新开始 · 新局清空升级";}
            if(game.Frontier && game.State==ArenaState.Victory){overlayTitle.text="边境城市已夺回";overlayBody.text=game.Kills+" 次击破 / "+timerLabel.text+"\n四区段行动完成。";buttonLabel.text="重新挑战 →";footerHint.text="R 重新开始 / 更换弹种与机动路线尝试";}
        }
        void OnPrimary(){if(ArenaGame.Instance.State==ArenaState.Paused)ArenaGame.Instance.TogglePause();else if(ArenaGame.Instance.State==ArenaState.Victory&&!ArenaGame.Instance.Frontier)ArenaGame.Instance.ContinueEndless();else ArenaGame.Instance.StartSession();}
        void OnDestroy(){if(primaryButton)primaryButton.onClick.RemoveListener(OnPrimary);}
    }
    public sealed class HUDVisibilityMirror:MonoBehaviour
    {
        public GameObject source;
        Image backing;
        void Awake(){backing=GetComponent<Image>();}
        void LateUpdate(){if(backing&&source)backing.enabled=source.activeSelf;}
    }
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CriticalEdgeGraphic:MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;float width=Mathf.Min(80,r.width*.055f);
            Color hot=new Color(.95f,.15f,.075f,.38f),clear=new Color(.95f,.15f,.075f,0);
            void Strip(Vector2 a,Vector2 b,Vector2 c,Vector2 d)
            {
                int n=vh.currentVertCount;vh.AddVert(a,hot,Vector2.zero);vh.AddVert(b,hot,Vector2.zero);vh.AddVert(c,clear,Vector2.zero);vh.AddVert(d,clear,Vector2.zero);vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
            }
            Strip(new Vector2(r.xMin,r.yMin),new Vector2(r.xMin,r.yMax),new Vector2(r.xMin+width,r.yMax),new Vector2(r.xMin+width,r.yMin));
            Strip(new Vector2(r.xMax,r.yMax),new Vector2(r.xMax,r.yMin),new Vector2(r.xMax-width,r.yMin),new Vector2(r.xMax-width,r.yMax));
            Strip(new Vector2(r.xMax,r.yMin),new Vector2(r.xMin,r.yMin),new Vector2(r.xMin,r.yMin+width*.65f),new Vector2(r.xMax,r.yMin+width*.65f));
        }
    }

}
