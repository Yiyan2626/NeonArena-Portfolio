using UnityEngine;
using UnityEngine.UI;
using static NeonArena.ArenaTheme;
namespace NeonArena
{
    public sealed class FrontierHUD:MonoBehaviour
    {
        GameObject panel;Text objective,caption,status,heading,upgradeHeading,nextHint,terminalFooter;Button next;
        readonly Button[] ammo=new Button[3],mobility=new Button[2];
        readonly Text[] ammoLabel=new Text[3],moveLabel=new Text[2],ammoAction=new Text[3],moveAction=new Text[2],ammoTag=new Text[3];
        readonly Image[] ammoCards=new Image[3],ammoSelection=new Image[3];
        static readonly string[] AmmoNames={"穿透弹","反弹弹","激光弹"};
        static readonly string[] AmmoStats={"34 伤害 / 0.30s","18 伤害 / 0.12s","82 伤害 / 0.60s"};
        static readonly string[] AmmoUses={"穿透墙体与多个敌人\n纵队 / 掩体后目标","最多 3 次折射\n利用墙面 / 清扫拐角","瞬间击破护盾\n远距单发 / 不可穿透"};
        static readonly string[] GunStatus={"穿透弹 · 34 伤害 / 0.30s","反弹弹 · 18 伤害 / 0.12s","激光弹 · 82 伤害 / 0.60s"};
        bool wasOpen;int claimedMove=-1,lastAmmo=-2,lastClaim=-1;float selectionAt=-100,claimAt=-100;
        readonly Image[] moveCards=new Image[2];
        FrontierAmmoCarousel024 carousel;
        CanvasGroup objectiveFade,statusFade,panelFade;Image missionBacking,navigationBacking,weaponBacking;Text routeHint;
        void Awake()
        {
            gameObject.AddComponent<FrontierGuidance>();
            gameObject.AddComponent<FieldGlassBackdrop024>();
            objective=Text("Frontier objective",transform,new Vector2(355,-104),new Vector2(650,34),"",22,FieldUI.Paper,TextAnchor.MiddleLeft,anchor:new Vector2(0,1));
            status=Text("Selected ammunition",transform,new Vector2(208,292),new Vector2(320,36),"",20,FieldUI.Paper,TextAnchor.MiddleLeft,anchor:new Vector2(0,0));
            objectiveFade=objective.gameObject.AddComponent<CanvasGroup>();statusFade=status.gameObject.AddComponent<CanvasGroup>();
            objectiveFade.alpha=statusFade.alpha=0;objectiveFade.blocksRaycasts=statusFade.blocksRaycasts=false;
            objective.raycastTarget=status.raycastTarget=false;
            missionBacking=AddTextBacking(objective,"Mission contrast");
            missionBacking.rectTransform.anchoredPosition=new Vector2(355,-106);
            missionBacking.rectTransform.sizeDelta=new Vector2(690,164);
            missionBacking.transform.SetAsFirstSibling();
            weaponBacking=AddTextBacking(status,"Weapon contrast");
            weaponBacking.gameObject.AddComponent<HUDVisibilityMirror>().source=status.gameObject;
            // FrontEnd owns CanvasGroups on direct HUD children. Keep the terminal's
            // animation on an inner layer so its menu gate cannot overwrite fade state.
            panel=Rect("Safe terminal",transform,Vector2.zero,Vector2.zero).gameObject;
            var rect=panel.GetComponent<RectTransform>();rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;
            var terminalLayer=Box("Terminal transition",panel.transform,Vector2.zero,Vector2.zero,new Color(.006f,.010f,.019f,.64f));
            terminalLayer.rectTransform.anchorMin=Vector2.zero;terminalLayer.rectTransform.anchorMax=Vector2.one;terminalLayer.raycastTarget=true;
            panelFade=terminalLayer.gameObject.AddComponent<CanvasGroup>();panelFade.alpha=0;panelFade.blocksRaycasts=panelFade.interactable=false;
            var shell=FieldUI.Panel("Terminal chassis",terminalLayer.transform,Vector2.zero,new Vector2(1230,796),FieldUI.Cyan);
            FieldUI.SetRole(shell,FieldSurfaceRole.Shell);
            shell.color=new Color(.019f,.031f,.049f,.72f);
            shell.GetComponentInChildren<FieldGlassGraphic>().SetQuietInterior(.98f);
            Box("Header hairline",shell.transform,new Vector2(0,239),new Vector2(1120,1),new Color(.58f,.72f,.8f,.16f));
            Box("Deployment hairline",shell.transform,new Vector2(0,-307),new Vector2(1100,1),new Color(.58f,.72f,.8f,.14f));
            Text("Terminal code",shell.transform,new Vector2(-340,361),new Vector2(470,25),"FRONTIER / 安全补给协议",20,FieldUI.Cyan,TextAnchor.MiddleLeft);
            Text("Terminal connection",shell.transform,new Vector2(382,361),new Vector2(370,25),"● 安全连接 / 战场暂停",20,FieldUI.Cyan,TextAnchor.MiddleRight);
            heading=Text("Terminal heading",shell.transform,new Vector2(-285,306),new Vector2(550,56),"战术整备",40,FieldUI.Paper,TextAnchor.MiddleLeft);
            Text("Terminal stages",shell.transform,new Vector2(365,307),new Vector2(370,46),"01 装备  /  02 升级",23,FieldUI.Cyan,TextAnchor.MiddleRight);
            caption=Text("Terminal caption",shell.transform,new Vector2(0,261),new Vector2(1120,34),"",23,FieldUI.Muted,TextAnchor.MiddleLeft);
            Text("Ammunition step",shell.transform,new Vector2(-315,216),new Vector2(470,28),"01 / 弹药装配",23,FieldUI.Cyan,TextAnchor.MiddleLeft);
            Text("Ammunition gesture",shell.transform,new Vector2(245,216),new Vector2(610,28),"← 横向拖动 / 滚轮切换 · 松开即装备 →",21,FieldUI.Paper,TextAnchor.MiddleRight);
            var track=Box("Ammunition optical track",shell.transform,new Vector2(0,27),new Vector2(1120,372),Color.clear);
            track.raycastTarget=true;track.gameObject.AddComponent<RectMask2D>();carousel=track.gameObject.AddComponent<FrontierAmmoCarousel024>();
            var cardRects=new RectTransform[3];
            for(int i=0;i<3;i++)
            {
                int n=i;Color accent=WeaponLoadout.UpgradeColor((AmmoUpgrade)i);
                var c=FieldUI.Panel("Ammo card "+i,track.transform,new Vector2((i-1)*372,0),new Vector2(350,338),accent);ammoCards[i]=c;
                FieldUI.SetRole(c,FieldSurfaceRole.Recessed);c.GetComponentInChildren<FieldGlassGraphic>().SetQuietInterior(.91f);c.raycastTarget=true;cardRects[i]=c.rectTransform;
                // This independent backing is above the optical layer and below every glyph.
                // Keep a 15px optical perimeter, but never transmit scene detail into text.
                Box("Ammunition quiet lining",c.transform,Vector2.zero,new Vector2(320,308),new Color(.039f,.058f,.080f,.88f));
                var glazeRect=Rect("Continuous card reflection",c.transform,Vector2.zero,new Vector2(346,334));
                var glaze=glazeRect.gameObject.AddComponent<FieldGlassGraphic>();glaze.role=FieldSurfaceRole.Recessed;glaze.color=Color.white;glaze.SetCoating(.9f);
                ammoSelection[i]=Box("Selected indicator",c.transform,new Vector2(0,164),new Vector2(306,2),FieldUI.Cyan);
                ammoTag[i]=Text("Equipment state",c.transform,new Vector2(0,143),new Vector2(312,24),"",20,FieldUI.Muted,TextAnchor.MiddleRight);
                Text("Ammo identifier",c.transform,new Vector2(-93,143),new Vector2(120,24),"0"+(i+1)+" / 弹药",20,accent,TextAnchor.MiddleLeft);
                ammoLabel[i]=Text("Ammo name",c.transform,new Vector2(0,105),new Vector2(306,42),AmmoNames[i],32,FieldUI.Paper,TextAnchor.MiddleLeft);
                var well=Box("Ballistics recess",c.transform,new Vector2(0,44),new Vector2(308,76),new Color(.013f,.024f,.035f,.99f));
                FieldUI.Decorate(well,accent);FieldUI.SetRole(well,FieldSurfaceRole.Recessed);
                Box("Recess lip",well.transform,new Vector2(0,38),new Vector2(288,1),new Color(.48f,.60f,.69f,.25f));
                var diagram=Rect("Ballistic schematic",well.transform,Vector2.zero,new Vector2(294,72)).gameObject.AddComponent<FrontierAmmoGraphic>();diagram.kind=i;diagram.color=accent;diagram.raycastTarget=false;
                Text("Impact magnitude",c.transform,new Vector2(-78,-17),new Vector2(154,38),new[]{"34","18","82"}[i]+" <size=20>伤害</size>",30,accent,TextAnchor.MiddleLeft);
                Text("Fire interval",c.transform,new Vector2(92,-17),new Vector2(122,34),new[]{"0.30s","0.12s","0.60s"}[i]+" / 发",20,FieldUI.Muted,TextAnchor.MiddleRight);
                Text("Tactical advantage",c.transform,new Vector2(0,-68),new Vector2(306,60),AmmoUses[i].Replace("\n","\n<size=20>")+"</size>",23,FieldUI.Paper,TextAnchor.MiddleLeft);
                ammo[i]=FieldUI.Action(c.transform,"Equip ammunition "+i,new Vector2(0,-134),new Vector2(306,46),"",()=>ArenaGame.Instance.Frontier.SelectAmmo((AmmoUpgrade)n));
                ammoAction[i]=ammo[i].transform.Find("Label").GetComponent<Text>();
            }
            carousel.Configure(cardRects);
            upgradeHeading=Text("Mobility heading",shell.transform,new Vector2(0,-170),new Vector2(1100,32),"",23,FieldUI.Amber,TextAnchor.MiddleLeft);
            for(int i=0;i<2;i++)
            {
                int n=i;var c=FieldUI.Panel("Mobility card "+i,shell.transform,new Vector2((i-.5f)*562,-241),new Vector2(540,98),FieldUI.Cyan);
                moveCards[i]=c;FieldUI.SetRole(c,FieldSurfaceRole.Recessed);c.GetComponentInChildren<FieldGlassGraphic>().SetQuietInterior(.95f);
                Box("Mobility quiet lining",c.transform,Vector2.zero,new Vector2(518,76),new Color(.033f,.053f,.075f,.91f));
                Icon("Mobility symbol",c.transform,new Vector2(-226,0),42,i==0?7:4,FieldUI.Cyan);
                moveLabel[i]=Text("Mobility description",c.transform,new Vector2(-35,1),new Vector2(306,70),"",20,FieldUI.Paper,TextAnchor.MiddleLeft);
                mobility[i]=FieldUI.Action(c.transform,"Claim mobility "+i,new Vector2(190,0),new Vector2(132,62),"",()=>{if(ArenaGame.Instance.Frontier.SelectMobility(n==0?AmmoUpgrade.Jump:AmmoUpgrade.DashCapacity))claimedMove=n;});
                moveAction[i]=mobility[i].transform.Find("Label").GetComponent<Text>();moveAction[i].fontSize=23;
            }
            next=FieldUI.Action(shell.transform,"Deploy",new Vector2(333,-334),new Vector2(434,58),"",()=>ArenaGame.Instance.Frontier.LeaveTerminal(),true);
            nextHint=Text("Next requirement",shell.transform,new Vector2(-245,-334),new Vector2(600,58),"",23,FieldUI.Muted,TextAnchor.MiddleLeft);
            terminalFooter=Text("Terminal footer",shell.transform,new Vector2(0,-373),new Vector2(1080,26),"1 / 2 / 3 切换弹种    ·    4 / 5 领取机动升级    ·    Enter 出发",20,FieldUI.Muted);
            panel.SetActive(false);
        }
        void Start()
        {
            EnsureNavigationContrast();
            // Separate mission, encounter and map into three non-overlapping top lanes.
            var encounter=transform.Find("Elite Encounter");
            if(encounter)
            {
                var r=encounter.GetComponent<RectTransform>();r.anchoredPosition=new Vector2(195,-112);r.sizeDelta=new Vector2(550,76);
                encounter.Find("Elite Title").GetComponent<RectTransform>().sizeDelta=new Vector2(510,24);
                encounter.Find("Elite Move").GetComponent<RectTransform>().sizeDelta=new Vector2(520,28);
                encounter.Find("Elite Health Track").GetComponent<RectTransform>().sizeDelta=new Vector2(490,5);
                var fill=encounter.Find("Elite Health").GetComponent<RectTransform>();fill.anchoredPosition=new Vector2(-245,-2);fill.sizeDelta=new Vector2(490,5);
            }
        }
        void EnsureNavigationContrast()
        {
            if(routeHint&&navigationBacking)return;
            // The slash is part of the object's name, not a Transform.Find hierarchy.
            // Guidance may also finish Awake after this HUD, so retry until it exists.
            foreach(Transform child in transform)
                if(child.name=="Route / interaction"){routeHint=child.GetComponent<Text>();break;}
            if(!routeHint)return;
            routeHint.rectTransform.anchorMin=routeHint.rectTransform.anchorMax=new Vector2(0,1);
            routeHint.rectTransform.anchoredPosition=new Vector2(355,-150);
            routeHint.rectTransform.sizeDelta=new Vector2(650,54);routeHint.fontSize=23;routeHint.alignment=TextAnchor.MiddleLeft;
            var existing=transform.Find("Navigation contrast");
            navigationBacking=existing?existing.GetComponent<Image>():AddTextBacking(routeHint,"Navigation contrast");
        }
        static Image AddTextBacking(Text label,string name)
        {
            var plate=Box(name,label.transform.parent,label.rectTransform.anchoredPosition,label.rectTransform.sizeDelta+new Vector2(22,2),new Color(.01f,.025f,.04f,.83f),label.rectTransform.anchorMin);
            plate.transform.SetSiblingIndex(label.transform.GetSiblingIndex());plate.raycastTarget=false;return plate;
        }
        void LateUpdate()
        {
            EnsureNavigationContrast();
            var g=ArenaGame.Instance;if(!g||!g.Frontier)return;var f=g.Frontier;var l=g.player.weapon.Loadout;
            float blend=1-Mathf.Exp(-Time.unscaledDeltaTime*12);
            float visible=g.IsPlaying&&!g.Cinema.FinaleActive&&!f.TerminalOpen?1:0;
            objectiveFade.alpha=Mathf.Lerp(objectiveFade.alpha,visible,blend);statusFade.alpha=objectiveFade.alpha;
            objective.text=f.Objective;
            Color objectiveColor=f.AwaitingTerminal||f.BossResupply?FieldUI.Cyan:f.ArrivalPending||f.Boss&&f.Boss.Charging?FieldUI.Amber:FieldUI.Paper;
            objective.color=Color.Lerp(objective.color,objectiveColor,blend);
            missionBacking.color=new Color(.01f,.025f,.04f,.83f*objectiveFade.alpha);
            if(navigationBacking)navigationBacking.color=Color.clear;
            status.rectTransform.anchoredPosition=new Vector2(208,g.hud.HelpOpen?370:292);
            if(weaponBacking){weaponBacking.rectTransform.anchoredPosition=status.rectTransform.anchoredPosition;weaponBacking.color=new Color(.01f,.025f,.04f,.87f*statusFade.alpha);}
            status.text="武器 / "+(l.SelectedAmmo<0?"基础射击":AmmoNames[l.SelectedAmmo]);
            panelFade.alpha=Mathf.Lerp(panelFade.alpha,f.TerminalOpen?1:0,blend);
            panelFade.interactable=panelFade.blocksRaycasts=f.TerminalOpen;
            panel.SetActive(f.TerminalOpen||panelFade.alpha>.01f);
            if(!f.TerminalOpen){wasOpen=false;return;}
            if(!wasOpen){claimedMove=-1;lastClaim=-1;lastAmmo=-2;wasOpen=true;}
            if(lastAmmo!=l.SelectedAmmo){selectionAt=Time.unscaledTime;lastAmmo=l.SelectedAmmo;}
            if(lastClaim!=claimedMove){claimAt=Time.unscaledTime;lastClaim=claimedMove;}
            panel.transform.SetAsLastSibling();
            caption.text=f.BossTerminal?"阶段间安全窗口 / 自由切换弹种后，返回守卫战":f.Revisiting?"已领取本站机动升级 / 可自由更换弹药后离开":"完成弹药装配与一次机动升级，继续区域行动";
            for(int i=0;i<3;i++)
            {
                bool selected=l.SelectedAmmo==i;
                ammoSelection[i].gameObject.SetActive(selected);
                float settle=CombatPresentation.FlashGain<=0?1:Mathf.Clamp01((Time.unscaledTime-selectionAt)/.24f);
                ammoCards[i].color=selected?Color.Lerp(new Color(.11f,.16f,.21f,.78f),new Color(.035f,.065f,.09f,.72f),settle):new Color(.025f,.039f,.065f,.57f);
                ammoSelection[i].color=Color.Lerp(new Color(.62f,.88f,.91f),FieldUI.Cyan,settle);
                ammoTag[i].text=selected?"● 已装备":"可替换";ammoTag[i].color=selected?FieldUI.Cyan:FieldUI.Muted;
                ammoAction[i].text=selected?"[ "+(i+1)+" ] 已装备":"[ "+(i+1)+" ] 装备弹药";
                var colors=ammo[i].colors;colors.normalColor=selected?new Color(.08f,.33f,.38f):new Color(.055f,.10f,.13f);ammo[i].colors=colors;
            }
            upgradeHeading.text="02 / 机动升级     "+(f.MobilityPending?"剩余 1 次 · 二选一":f.BossTerminal?"本次窗口不提供升级":"剩余 0 次 · 本站已领取");
            upgradeHeading.color=f.MobilityPending?FieldUI.Amber:FieldUI.Muted;
            moveLabel[0].text="<size=23>跳跃 Lv."+l.Jump+(l.Jump>=2?" / 喷气已解锁":" → Lv."+(l.Jump+1))+"</size>\n"+(l.Jump>=2?"空中长按空格 / 落地补充能量":"Lv.2 解锁喷气与垂直爬升");
            moveLabel[1].text="<size=23>冲刺容量 "+g.player.Dash.MaxCharges+" / 3</size>\n"+(l.CanUpgrade(AmmoUpgrade.DashCapacity)?"升级增加一次储备冲刺":"已达到最大容量");
            for(int i=0;i<2;i++)
            {
                moveCards[i].color=claimedMove==i?Color.Lerp(new Color(.08f,.16f,.18f,1),new Color(.034f,.073f,.09f,1),CombatPresentation.FlashGain<=0?1:Mathf.Clamp01((Time.unscaledTime-claimAt)/.3f)):new Color(.026f,.039f,.055f,1);
                bool can=l.CanUpgrade(i==0?AmmoUpgrade.Jump:AmmoUpgrade.DashCapacity);
                mobility[i].interactable=f.MobilityPending&&can;
                moveAction[i].text=f.MobilityPending?(can?"[ "+(i+4)+" ]\n领取升级":"已满级"):claimedMove==i?"已领取":f.BossTerminal?"本次无升级":"额度已用尽";
                moveAction[i].fontSize=mobility[i].interactable?23:20;
                moveAction[i].color=mobility[i].interactable?FieldUI.Paper:new Color(.83f,.87f,.90f);
            }
            terminalFooter.text=f.MobilityPending?"1 / 2 / 3 切换弹种    ·    4 / 5 领取机动升级    ·    Enter 出发":"1 / 2 / 3 自由切换弹种    ·    机动升级额度已结算    ·    Enter 出发";
            bool hasAmmo=f.AmmoChosen||l.SelectedAmmo>=0;
            next.interactable=!f.MobilityPending&&hasAmmo;
            next.transform.Find("Label").GetComponent<Text>().text=next.interactable?"Enter / 确认装配并出发  →":"完成整备后出发";
            next.transform.Find("Label").GetComponent<Text>().color=next.interactable?new Color(.02f,.07f,.09f):new Color(.85f,.89f,.92f);
            nextHint.text=!hasAmmo?"下一步 / 拖动或点击装配弹药":f.MobilityPending?"弹药已装配 / 还需领取一项机动升级":"装配完成 / 随时可以返回战场";
            nextHint.color=next.interactable?FieldUI.Cyan:FieldUI.Amber;
            if(Input.GetKeyDown(KeyCode.Alpha1))f.SelectAmmo(AmmoUpgrade.Piercing);
            if(Input.GetKeyDown(KeyCode.Alpha2))f.SelectAmmo(AmmoUpgrade.Ricochet);
            if(Input.GetKeyDown(KeyCode.Alpha3))f.SelectAmmo(AmmoUpgrade.Laser);
            if(Input.GetKeyDown(KeyCode.Alpha4)&&f.SelectMobility(AmmoUpgrade.Jump))claimedMove=0;
            if(Input.GetKeyDown(KeyCode.Alpha5)&&f.SelectMobility(AmmoUpgrade.DashCapacity))claimedMove=1;
            if(Input.GetKeyDown(KeyCode.Return))f.LeaveTerminal();
        }
    }
    // Small original vector diagrams communicate each weapon's trajectory without generated assets.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FrontierAmmoGraphic:MaskableGraphic
    {
        public int kind;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            void Line(Vector2 a,Vector2 b,float width,Color c)
            {
                var n=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f;int k=vh.currentVertCount;
                vh.AddVert(a-n,c,Vector2.zero);vh.AddVert(a+n,c,Vector2.zero);vh.AddVert(b+n,c,Vector2.zero);vh.AddVert(b-n,c,Vector2.zero);vh.AddTriangle(k,k+1,k+2);vh.AddTriangle(k,k+2,k+3);
            }
            void Hull(float x,float y,float w,float h,Color tint)
            {
                Vector2 a=new Vector2(x-w*.5f,y-h*.5f),b=new Vector2(x-w*.5f,y+h*.5f),c=new Vector2(x+w*.5f,y+h*.5f),d=new Vector2(x+w*.5f,y-h*.5f);
                int k=vh.currentVertCount;vh.AddVert(a,new Color(tint.r*.28f,tint.g*.28f,tint.b*.28f,1),Vector2.zero);vh.AddVert(b,tint,Vector2.zero);vh.AddVert(c,tint,Vector2.zero);vh.AddVert(d,new Color(tint.r*.5f,tint.g*.5f,tint.b*.5f,1),Vector2.zero);vh.AddTriangle(k,k+1,k+2);vh.AddTriangle(k,k+2,k+3);
                Line(c,c+new Vector2(5,4),1,tint);Line(b+new Vector2(5,4),c+new Vector2(5,4),1,tint);Line(b,b+new Vector2(5,4),1,tint);
            }
            void Beam(Vector2 a,Vector2 b)
            {Line(a,b,10,new Color(color.r,color.g,color.b,.10f));Line(a,b,5,new Color(color.r,color.g,color.b,.24f));Line(a,b,2.5f,color);Line(a,b,.7f,new Color(.85f,1,1,.95f));}
            void Target(float x,float y,bool broken=false)
            {
                Color c=new Color(.35f,.48f,.56f,.85f);
                Hull(x,y,14,28,c);
                if(broken){Line(new Vector2(x-12,y+14),new Vector2(x-17,y+22),2,color);Line(new Vector2(x+12,y-14),new Vector2(x+18,y-21),2,color);}
            }
            var grid=new Color(.24f,.40f,.5f,.10f);
            for(int x=-140;x<=140;x+=20)Line(new Vector2(x,-32),new Vector2(x,32),.6f,grid);
            for(int y=-30;y<=30;y+=15)Line(new Vector2(-145,y),new Vector2(145,y),.6f,grid);
            Hull(-132,0,18,16,new Color(.36f,.55f,.65f,1));
            if(kind==0){Target(-34,0);Target(31,0);Target(96,0);Beam(new Vector2(-120,0),new Vector2(135,0));Line(new Vector2(119,-7),new Vector2(135,0),3,color);Line(new Vector2(119,7),new Vector2(135,0),3,color);}
            else if(kind==1){Color wall=new Color(.42f,.55f,.62f,.55f);Line(new Vector2(-65,24),new Vector2(44,24),3,wall);Line(new Vector2(-4,-24),new Vector2(109,-24),3,wall);Beam(new Vector2(-120,0),new Vector2(-37,20));Beam(new Vector2(-37,20),new Vector2(41,-20));Beam(new Vector2(41,-20),new Vector2(121,12));Hull(-37,20,6,6,color);Hull(41,-20,6,6,color);Target(123,12);}
            else{Target(83,0,true);Line(new Vector2(-135,0),new Vector2(132,0),8,new Color(color.r,color.g,color.b,.18f));Beam(new Vector2(-120,0),new Vector2(132,0));for(int i=0;i<24;i++){float a=i*Mathf.PI/12,b=(i+1)*Mathf.PI/12;Line(new Vector2(-102+Mathf.Cos(a)*8,Mathf.Sin(a)*22),new Vector2(-102+Mathf.Cos(b)*8,Mathf.Sin(b)*22),1.5f,new Color(color.r,color.g,color.b,.65f));};Line(new Vector2(66,-18),new Vector2(57,-25),3,color);Line(new Vector2(98,18),new Vector2(108,26),3,color);}
        }
    }
}
