using UnityEngine;
using UnityEngine.UI;

namespace NeonArena
{
    [DefaultExecutionOrder(220)]
    public sealed class CombatPresentation : MonoBehaviour
    {
        public static float FlashGain { get; private set; }=1;
        public static string ModeName=>FlashGain==1?"标准":FlashGain>0?"柔和":"无闪光";
        GameObject elitePanel;
        Text eliteTitle,eliteAction,entry,threat,setting;
        Image eliteFill,edgeLeft,edgeRight;
        float refreshAt;
        void Awake()
        {
            FlashGain=PlayerPrefs.GetFloat("ArenaFlashGain",1);
            elitePanel=ArenaTheme.Box("Elite Encounter",transform,new Vector2(0,-192),new Vector2(650,76),new Color(.06f,.035f,.025f,.94f),new Vector2(.5f,1)).gameObject;
            FieldUI.Decorate(elitePanel.GetComponent<Image>(),FieldUI.Amber);
            eliteTitle=ArenaTheme.Text("Elite Title",elitePanel.transform,new Vector2(0,19),new Vector2(590,24),"AEGIS / 熔核守卫",23,ArenaTheme.Gold);
            ArenaTheme.Box("Elite Health Track",elitePanel.transform,new Vector2(0,-2),new Vector2(570,5),ArenaTheme.Muted);
            eliteFill=ArenaTheme.Box("Elite Health",elitePanel.transform,new Vector2(-285,-2),new Vector2(570,5),new Color(1,.32f,.08f));eliteFill.rectTransform.pivot=new Vector2(0,.5f);
            eliteAction=ArenaTheme.Text("Elite Move",elitePanel.transform,new Vector2(0,-22),new Vector2(615,28),"",20,ArenaTheme.Paper);
            entry=ArenaTheme.Text("Overheat Entry",transform,new Vector2(0,-278),new Vector2(830,50),"",28,Color.cyan,anchor:new Vector2(.5f,1));
            threat=ArenaTheme.Text("Visible Offscreen Threat",transform,new Vector2(0,218),new Vector2(780,30),"",23,FieldUI.Amber,anchor:new Vector2(.5f,0));
            setting=ArenaTheme.Text("Flash Setting",transform,new Vector2(184,197),new Vector2(322,22),"",11,ArenaTheme.Muted,anchor:Vector2.zero);
            edgeLeft=ArenaTheme.Box("Heat Left",transform,new Vector2(3,0),new Vector2(6,900),Color.clear,new Vector2(0,.5f));
            edgeRight=ArenaTheme.Box("Heat Right",transform,new Vector2(-3,0),new Vector2(6,900),Color.clear,new Vector2(1,.5f));
        }
        public static void SetFlash(float gain){FlashGain=Mathf.Clamp01(gain);PlayerPrefs.SetFloat("ArenaFlashGain",FlashGain);}
        void Update(){if(Input.GetKeyDown(KeyCode.F4)){FlashGain=FlashGain==1?.25f:FlashGain>0?0:1;PlayerPrefs.SetFloat("ArenaFlashGain",FlashGain);}}
        void LateUpdate()
        {
            var game=ArenaGame.Instance;if(!game)return;
            bool playing=game.IsPlaying&&!game.Cinema.FinaleActive;var heat=game.player.Overheat;
            float t=Time.unscaledTime-heat.ActivatedAt;
            entry.text=playing&&heat.Active&&t<.8f?"超 载 启 动  /  OVERDRIVE":"";
            if(game.Frontier&&game.Cinema.IntroActive)entry.text="";
            entry.color=new Color(.2f,.95f,1,Mathf.Clamp01((.8f-t)*3));
            float alpha=playing&&heat.Active?.4f+.15f*Mathf.Sin(Time.time*3):0;
            edgeLeft.color=edgeRight.color=new Color(.03f,.8f,1,alpha*FlashGain);
            setting.text="";
            EliteWarden elite=null;
            foreach(var e in game.Enemies)if(e.IsAlive&&e.Elite.IsElite&&e.IsVisibleToPlayer){elite=e.Elite;break;}
            elitePanel.SetActive(playing&&elite);
            if(elite)
            {
                var health=elite.GetComponent<Health>();ArenaTheme.Fill(eliteFill,health.Current/health.maxHealth);
                eliteAction.text=elite.Phase==ElitePhase.Windup?elite.MoveName:elite.Phase==ElitePhase.Recovery?"核心暴露 · 集中火力 / 激光刀可打断":"重装守卫 · 注意地面预警";
                var frontierBoss=elite.GetComponent<FrontierBoss>();if(game.Frontier&&frontierBoss){eliteTitle.text="SCORCH / 重型守卫机甲";eliteAction.text=frontierBoss.Instruction;}
            }
            if(Time.unscaledTime<refreshAt)return;refreshAt=Time.unscaledTime+.1f;
            threat.text="";if(!playing)return;
            EnemyController nearest=null;float distance=16;
            foreach(var e in game.Enemies)
            {
                if(!e.IsAlive||!e.IsVisibleToPlayer||!game.Vision.CanObserve(e.transform.position))continue;
                Vector3 screen=game.player.viewCamera.WorldToViewportPoint(e.transform.position+Vector3.up);
                float d=Vector3.Distance(e.transform.position,game.player.transform.position);
                if(d<distance&&(screen.z<0||screen.x<.04f||screen.x>.96f||screen.y<.05f||screen.y>.95f)){distance=d;nearest=e;}
            }
            if(nearest)
            {
                Vector3 relative=game.player.viewCamera.transform.InverseTransformDirection(nearest.transform.position-game.player.transform.position);
                threat.text=(relative.x<0?"◀ 左侧":"右侧 ▶")+" 可见威胁  "+distance.ToString("0")+"m";
            }
        }
    }
}
