using UnityEngine;
using UnityEngine.UI;
using static NeonArena.ArenaTheme;

namespace NeonArena
{
    public sealed class UpgradeHUD : MonoBehaviour
    {
        GameObject panel, combat;
        Text title, subtitle, countdown, objective, dashText, muteText;
        readonly Text[] headings=new Text[7],levels=new Text[7],descriptions=new Text[7],actions=new Text[7],cycleLabels=new Text[4];
        readonly Button[] buttons=new Button[7];
        readonly Image[] bars=new Image[4],slots=new Image[4];
        readonly string[] names={"穿透弹","反射弹","激光束","生命强化","冲刺储备","冲刺恢复","跳跃模块"};
        readonly string[] english={"PIERCING","RICOCHET","LASER","VITALITY","DASH CAPACITY","DASH RECOVERY","JUMP / JETPACK"};
        float nextRefresh;
        ArenaState lastState=(ArenaState)(-1);
        void Awake()
        {
            objective=Text("Objective",transform,new Vector2(0,-116),new Vector2(1280,32),"",16,Muted,anchor:new Vector2(.5f,1));
            combat=new GameObject("Combat Modules",typeof(RectTransform));combat.transform.SetParent(transform,false);var c=combat.GetComponent<RectTransform>();c.anchorMin=Vector2.zero;c.anchorMax=Vector2.one;c.offsetMin=c.offsetMax=Vector2.zero;
            for(int i=0;i<4;i++)
            {
                Color color=i==0?Paper:WeaponLoadout.UpgradeColor((AmmoUpgrade)(i-1));
                var tile=Box("Module "+i,combat.transform,new Vector2(500+i*200,81),new Vector2(186,106),new Color(.035f,.045f,.065f,.92f),Vector2.zero);
                Icon("Module Icon",tile.transform,new Vector2(-62,17),32,i==0?6:i-1,color);
                cycleLabels[i]=Text("Module State",tile.transform,new Vector2(13,8),new Vector2(132,74),"",14,color);
                Box("Track",tile.transform,new Vector2(0,-32),new Vector2(158,4),new Color(.14f,.18f,.21f));
                bars[i]=Box("Meter",tile.transform,new Vector2(-79,-32),new Vector2(158,4),color);bars[i].rectTransform.pivot=new Vector2(0,.5f);
            }
            var dash=Box("Dash",combat.transform,new Vector2(-195,81),new Vector2(330,106),new Color(.035f,.045f,.065f,.92f),new Vector2(1,0));
            Icon("Dash Icon",dash.transform,new Vector2(-126,9),38,4,Gold);
            dashText=Text("Dash State",dash.transform,new Vector2(15,21),new Vector2(225,52),"",16,Paper);
            for(int i=0;i<4;i++)slots[i]=Box("Charge "+i,dash.transform,new Vector2(-80+i*59,-29),new Vector2(50,7),Gold);
            muteText=Text("Audio State",transform,new Vector2(187,166),new Vector2(314,24),"",12,Muted,TextAnchor.MiddleLeft,Vector2.zero);
            countdown=Text("Countdown",transform,new Vector2(0,85),new Vector2(760,150),"",31,Gold);
            panel=Box("Upgrade selection",transform,Vector2.zero,Vector2.zero,new Color(.018f,.025f,.04f,.95f)).gameObject;
            var pr=panel.GetComponent<RectTransform>();pr.anchorMin=Vector2.zero;pr.anchorMax=Vector2.one;panel.GetComponent<Image>().raycastTarget=true;
            Text("Eyebrow",panel.transform,new Vector2(0,344),new Vector2(1200,26),"FIELD REWARDS   /   CHOOSE YOUR NEXT ADVANTAGE",13,Gold);
            title=Text("Reward Title",panel.transform,new Vector2(0,291),new Vector2(1240,64),"",36,Paper);
            subtitle=Text("Reward Subtitle",panel.transform,new Vector2(0,246),new Vector2(1200,30),"",16,Muted);
            for(int i=0;i<7;i++)
            {
                int index=i;Color accent=WeaponLoadout.UpgradeColor((AmmoUpgrade)i);
                var border=Box("Card Border "+i,panel.transform,new Vector2((i-3f)*218,-15),new Vector2(208,470),new Color(.45f,.5f,.54f,.5f));
                var card=Box("Card "+i,border.transform,Vector2.zero,new Vector2(204,466),Paper);
                var header=Box("Color Field",card.transform,new Vector2(0,155),new Vector2(204,156),Color.Lerp(Ink,accent,.34f));
                header.gameObject.AddComponent<RectMask2D>();
                var diagonal=Box("Diagonal",header.transform,new Vector2(48,-42),new Vector2(184,2),new Color(1,1,1,.2f));diagonal.rectTransform.localRotation=Quaternion.Euler(0,0,27);
                Icon("Ability",header.transform,new Vector2(0,-6),100,i==6?7:i,new Color(.96f,.94f,.84f));
                Text("Category",header.transform,new Vector2(0,58),new Vector2(210,24),i<3?"武器模块  /  WEAPON":"生存强化  /  SURVIVAL",11,Paper);
                headings[i]=Text("Type",card.transform,new Vector2(0,50),new Vector2(212,32),names[i],21,Ink);
                levels[i]=Text("Level",card.transform,new Vector2(0,20),new Vector2(206,22),"",13,new Color(.4f,.36f,.26f));
                descriptions[i]=Text("Description",card.transform,new Vector2(0,-62),new Vector2(182,125),"",15,new Color(.22f,.25f,.29f),TextAnchor.UpperLeft);
                Text("English",card.transform,new Vector2(0,-141),new Vector2(212,20),english[i],10,new Color(.47f,.47f,.43f));
                var action=Box("Choose",card.transform,new Vector2(0,-193),new Vector2(184,46),Gold);action.raycastTarget=true;
                buttons[i]=action.gameObject.AddComponent<Button>();buttons[i].targetGraphic=action;buttons[i].onClick.AddListener(()=>ArenaGame.Instance.ChooseUpgrade((AmmoUpgrade)index));
                var colors=buttons[i].colors;colors.highlightedColor=new Color(1,1,.85f);colors.pressedColor=new Color(.7f,.65f,.5f);colors.disabledColor=new Color(.42f,.42f,.4f);buttons[i].colors=colors;
                actions[i]=Text("Choice",action.transform,Vector2.zero,new Vector2(180,44),"",17,Ink);
                var hover=action.gameObject.AddComponent<RewardHover>();hover.card=border.rectTransform;hover.border=border;
            }
            Text("Reward Footer",panel.transform,new Vector2(0,-294),new Vector2(1410,35),"选择一项并保留已有能力  ·  穿透破盾 / 反射折返 / 激光被盾阻挡  ·  自由瞄准 + 横向冲刺可绕后",15,Muted);
            Text("Reward Controls",panel.transform,new Vector2(0,-336),new Vector2(1200,28),"数字 1—7 快速选择    /    冲刺最多储备 4 次，恢复最低 0.8 秒",13,Gold);
            panel.SetActive(false);
        }
        void Start()
        {
            if(ArenaGame.Instance.Frontier){panel.SetActive(false);combat.SetActive(false);objective.gameObject.SetActive(false);countdown.gameObject.SetActive(false);muteText.gameObject.SetActive(false);enabled=false;return;}
            for(int i=0;i<4;i++)
            {
                var tile=combat.transform.Find("Module "+i).GetComponent<RectTransform>();tile.localScale=Vector3.one*.78f;tile.anchoredPosition=new Vector2(400+i*155,63);
            }
            var dash=combat.transform.Find("Dash").GetComponent<RectTransform>();dash.localScale=Vector3.one*.8f;dash.anchoredPosition=new Vector2(-145,63);
            objective.rectTransform.anchoredPosition=new Vector2(0,-84);
        }
        void LateUpdate()
        {
            var game=ArenaGame.Instance;if(!game||!game.player)return;
            if(lastState==game.State && Time.unscaledTime<nextRefresh)return;lastState=game.State;nextRefresh=Time.unscaledTime+.06f;
            var w=game.player.weapon;var l=w.Loadout;var dash=game.player.Dash;
            bool play=(game.State==ArenaState.Playing||game.State==ArenaState.Paused) && !game.Cinema.FinaleActive;combat.SetActive(play);
            WriteCycle(0,"普通弹",w.StandardCycle,true,0);WriteCycle(1,"穿透",w.PierceCycle,l.Piercing>0,l.Piercing);WriteCycle(2,"反射",w.RicochetCycle,l.Ricochet>0,l.Ricochet);WriteCycle(3,"激光",w.LaserCycle,l.Laser>0,l.Laser);
            if(game.player.Overheat.Active)
            {
                cycleLabels[0].text="激光刀\n"+(game.player.Overheat.Cooldown>0?"斩击间隔 "+game.player.Overheat.Cooldown.ToString("0.0")+"s":"左键瞬移斩");
                Fill(bars[0],1-game.player.Overheat.Cooldown/PlayerOverheat.StrikeInterval);
                for(int i=1;i<4;i++){cycleLabels[i].text="枪械模块\n过热中收起";Fill(bars[i],0);}
            }
            dashText.text=(dash.IsActive?"冲刺中 · 无敌": "SHIFT  冲刺")+"\n"+dash.Charges+" / "+dash.MaxCharges+(dash.CooldownLeft>0?"  ·  "+dash.CooldownLeft.ToString("0.0")+"s":"  ·  就绪");
            for(int i=0;i<4;i++)slots[i].color=i<dash.Charges?Gold:i<dash.MaxCharges?new Color(.35f,.33f,.25f):new Color(.1f,.13f,.16f);
            muteText.text=ArenaAudio.Instance && ArenaAudio.Instance.Muted?"音效已静音 · F3 开启":"立体声战斗音效 · F3 静音";muteText.gameObject.SetActive(play && ArenaAudio.Instance && ArenaAudio.Instance.Muted);
            objective.gameObject.SetActive(game.IsPlaying && !game.Cinema.FinaleActive);
            objective.text=game.Operation.Enabled?game.Operation.Objective:"清除敌人  "+game.ClearedWaves+" / "+game.CampaignLength;
            for(int i=1;i<4;i++)combat.transform.Find("Module "+i).gameObject.SetActive((i==1?l.Piercing:i==2?l.Ricochet:l.Laser)>0);

            if(game.player.Overheat.Active)objective.text="激光刀  ·  18 米内可见敌人  ·  自动斩击近处可见敌人  ·  无安全落点时无法瞬移";
            countdown.gameObject.SetActive(game.State==ArenaState.Intermission);countdown.text="下一关  /  "+game.Wave.ToString("00")+"\n"+Mathf.CeilToInt(game.Countdown);
            panel.SetActive(game.State==ArenaState.Reward);if(!panel.activeSelf)return;
            panel.transform.SetAsLastSibling();
            title.text="第 "+game.ClearedWaves.ToString("00")+" 关完成  ·  战术补给";subtitle.text="生命已恢复 25 点   /   选择下一段战斗的优势";
            for(int i=0;i<7;i++)
            {
                var type=(AmmoUpgrade)i;int rank=l.Level(type);bool can=l.CanUpgrade(type);
                levels[i].text=!can?"已达上限":rank==0?(i<3?"解锁新模块":"首次强化"):"Lv."+rank+"  →  Lv."+(rank+1);
                descriptions[i].text=Describe(l,type);actions[i].text=!can?"已满级":"[ "+(i+1)+" ]  选择  →";
                buttons[i].interactable=game.RewardInputReady&&can;
            }
        }
        string Describe(WeaponLoadout l,AmmoUpgrade type)
        {
            int next=l.Level(type)+1;
            if(type==AmmoUpgrade.Jump)return l.Jump==0?"强化跳跃\n跳高约 1.6 → 2.6 米\n\n下一级解锁喷气背包":"喷气背包\n空中按住空格升空\n能量 2.6 秒，落地恢复\n\n能量耗尽后自然下落";
            if(type==AmmoUpgrade.Vitality)return "生命上限 +20\n立即恢复 20 点生命\n\n保留武器与冲刺强化";
            if(type==AmmoUpgrade.DashCapacity)return "储备 "+(1+l.DashCapacity)+" → "+Mathf.Min(4,2+l.DashCapacity)+" 次冲刺\n新增次数立即可用\n\n逐次恢复 · 最多 4 次";
            if(type==AmmoUpgrade.DashRecovery)return "恢复 "+(2-.15f*l.DashRecovery).ToString("0.00")+" → "+Mathf.Max(.8f,2-.15f*next).ToString("0.00")+"s\n每次缩短 0.15 秒\n\n不延长无敌时间";
            string text=type==AmmoUpgrade.Piercing?"单发最多命中 "+(1+Mathf.Min(12,next+1))+" 个敌人\n盾牌伤害 ×3\n持续 1.6s / 冷却 1.3s":type==AmmoUpgrade.Ricochet?"最多反弹 "+Mathf.Min(8,next+1)+" 次\n墙体与完整盾牌可反弹\n持续 2.0s / 冷却 1.6s":"额外持续光束，叠加弹种\n完整盾牌会阻挡光束\n持续 1.25s / 冷却 2.2s";
            return text+"\n\n武器伤害 ×"+(l.DamageMultiplier+.12f).ToString("0.00");
        }
        void WriteCycle(int i,string name,BurstCycle cycle,bool unlocked,int rank)
        {cycleLabels[i].text=name+(rank>0?" Lv."+rank:"")+"\n"+(!unlocked?"未解锁":cycle.CooldownLeft>0?"冷却 "+cycle.CooldownLeft.ToString("0.0")+"s":"发射 "+cycle.Remaining.ToString("0.0")+"s");Fill(bars[i],!unlocked?0:cycle.CooldownLeft>0?1-cycle.CooldownLeft/cycle.Recovery:cycle.Remaining/cycle.Duration);}
    }
}
