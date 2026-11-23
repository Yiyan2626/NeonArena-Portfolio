using UnityEngine;
using UnityEngine.UI;
namespace NeonArena
{
    public sealed class FrontierGuidance:MonoBehaviour
    {
        public Vector3 Target{get;private set;}
        public int BreadcrumbCount{get;private set;}
        public string Label{get;private set;}
        public bool HasGroundPath{get;private set;}
        Text arrow,caption,hint;GameObject marker;Transform[] crumbs=new Transform[14];Transform[] beacons;
        TextMesh[] names;Material pathMaterial;Mesh pathMesh;float nextPath;Vector3 lastTarget;
        CanvasGroup markerFade,hintFade;float visibility;bool wasActive;
        static bool AtJetEntry(ArenaGame g)=>g.Frontier.Sector==2&&!g.Frontier.RouteResolved&&g.player.weapon.Loadout.Jump>=2&&
            Vector3.ProjectOnPlane(g.player.transform.position-g.Frontier.JetEntry,Vector3.up).sqrMagnitude<3.2f*3.2f;
        public static Vector3 Goal(ArenaGame g)
        {
            var f=g.Frontier;
            if(f.BossResupply)
            {
                Vector3 best=f.bossPoint;float score=float.MaxValue;
                for(int i=3;i<f.terminals.Length;i++)
                {
                    var p=f.terminals[i].position;float distance=Vector3.Distance(g.player.transform.position,p);
                    if(!g.terrain.HasRoute(g.player.transform.position,p))distance+=1000;
                    if(distance<score){score=distance;best=p;}
                }
                return best;
            }
            if(f.Sector==2&&!f.RouteResolved)
                return AtJetEntry(g)&&g.player.transform.position.y>2?new Vector3(f.JetEntry.x,7.1f,-75.1f):f.LowerEntry;
            if(f.Sector==2&&f.UpperRouteChosen&&f.AwaitingTerminal&&g.player.transform.position.y>5)
                return g.player.transform.position.x<8?new Vector3(10,7.1f,-75.1f):new Vector3(10,.3f,-71.5f);
            return f.Destination;
        }
        void Awake()
        {
            marker=ArenaTheme.Rect("Objective direction",transform,Vector2.zero,new Vector2(230,90)).gameObject;
            // FrontEnd owns CanvasGroups on direct HUD children. Keep local marker visibility below that gate.
            var markerContent=ArenaTheme.Rect("Objective visibility",marker.transform,Vector2.zero,new Vector2(230,90));
            markerFade=markerContent.gameObject.AddComponent<CanvasGroup>();markerFade.blocksRaycasts=false;markerFade.interactable=false;markerFade.alpha=0;
            arrow=ArenaTheme.Text("Objective diamond",markerContent,new Vector2(0,17),new Vector2(80,42),"◇",31,ArenaTheme.Gold);
            caption=ArenaTheme.Text("Objective distance",markerContent,new Vector2(0,-18),new Vector2(230,42),"",15,ArenaTheme.Paper);
            hint=ArenaTheme.Text("Route / interaction",transform,new Vector2(0,-129),new Vector2(1160,50),"",17,ArenaTheme.Paper,anchor:new Vector2(.5f,1));
            hintFade=hint.gameObject.AddComponent<CanvasGroup>();hintFade.blocksRaycasts=false;hintFade.interactable=false;hintFade.alpha=0;
            arrow.raycastTarget=caption.raycastTarget=hint.raycastTarget=false;
            pathMaterial=new Material(Shader.Find("Sprites/Default")){color=new Color(.18f,.85f,.92f,.78f)};
            pathMesh=new Mesh{name="Navigation chevron"};pathMesh.vertices=new[]{new Vector3(-.25f,0,-.28f),new Vector3(0,0,.1f),new Vector3(.25f,0,-.28f),new Vector3(.25f,0,-.06f),new Vector3(0,0,.32f),new Vector3(-.25f,0,-.06f)};pathMesh.triangles=new[]{0,1,5,5,1,4,1,2,3,1,3,4};pathMesh.RecalculateNormals();
            for(int i=0;i<crumbs.Length;i++){var go=new GameObject("Ground navigation arrow");go.transform.SetParent(ArenaGame.Instance.transform);go.AddComponent<MeshFilter>().sharedMesh=pathMesh;go.AddComponent<MeshRenderer>().sharedMaterial=pathMaterial;crumbs[i]=go.transform;go.SetActive(false);}
            var f=ArenaGame.Instance.Frontier;beacons=new Transform[f.terminals.Length];names=new TextMesh[beacons.Length];
            for(int i=0;i<beacons.Length;i++)
            {
                var go=new GameObject("Terminal beacon "+i);go.transform.SetParent(f.terminals[i],false);go.transform.localPosition=Vector3.up*2.6f;beacons[i]=go.transform;
                var text=go.AddComponent<TextMesh>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=64;text.characterSize=.075f;text.anchor=TextAnchor.MiddleCenter;text.text="[ F ]\nLOADOUT";go.GetComponent<Renderer>().sharedMaterial=text.font.material;names[i]=text;
                var light=go.AddComponent<Light>();light.type=LightType.Point;light.range=3;light.intensity=1;light.color=new Color(.2f,.85f,1);light.shadows=LightShadows.None;
            }
        }
        public bool TerminalAvailable(int i)
        {
            return ArenaGame.Instance.Frontier.TerminalAvailable(i);
        }
        void LateUpdate()
        {
            var g=ArenaGame.Instance;if(!g||!g.Frontier)return;var f=g.Frontier;bool active=g.IsPlaying&&!g.Cinema.FinaleActive&&!f.TerminalOpen;
            float blend=1-Mathf.Exp(-Time.unscaledDeltaTime*12);
            visibility=Mathf.Lerp(visibility,active?1:0,blend);hintFade.alpha=visibility;
            if(!active)markerFade.alpha=Mathf.Lerp(markerFade.alpha,0,blend);
            pathMaterial.color=new Color(.18f,.85f,.92f,.64f*visibility);
            for(int i=0;i<beacons.Length;i++)
            {
                bool unlocked=TerminalAvailable(i);
                float proximity=Vector3.Distance(g.player.transform.position,f.terminals[i].position);
                bool current=f.AwaitingTerminal&&i==f.Sector||f.Sector==3&&i>=3;
                beacons[i].gameObject.SetActive(active&&unlocked&&(current||proximity<18));
                names[i].color=unlocked?new Color(.2f,.95f,1):new Color(.43f,.5f,.52f);
                bool usable=unlocked&&proximity<=3.5f&&f.TerminalBlockReason(i).Length==0;
                names[i].text=usable?"[ F ]\nLOADOUT":unlocked?"LOADOUT":"LOCKED";
                beacons[i].rotation=Quaternion.LookRotation(beacons[i].position-g.player.viewCamera.transform.position);
                beacons[i].GetComponent<Light>().enabled=unlocked;
            }
            if(!active){if(visibility<.02f)foreach(var c in crumbs)c.gameObject.SetActive(false);wasActive=false;return;}
            Target=Goal(g);float distance=Vector3.Distance(g.player.transform.position,Target);
            bool terminal=f.AwaitingTerminal||f.BossResupply;
            bool jet=AtJetEntry(g)&&g.player.transform.position.y>2;
            bool descent=f.Sector==2&&f.UpperRouteChosen&&f.AwaitingTerminal&&g.player.transform.position.y>5;
            Label=descent?"高架东端出口":terminal?"安全终端":jet?"上层落点":f.Sector==2&&!f.RouteResolved?"下层主路":f.ExitTrialActive?"出口追击":f.Sector==3?"中央控制室":f.CurrentEncounter!=null?f.CurrentEncounter.name:"行动目标";
            var canvas=GetComponentInParent<Canvas>().rootCanvas;var rect=canvas.GetComponent<RectTransform>();
            Vector3 v=g.player.viewCamera.WorldToViewportPoint(Target+Vector3.up*1.4f);
            Vector2 relative=new Vector2(v.x-.5f,v.y-.5f);bool onscreen=v.z>0&&Mathf.Abs(relative.x)<.42f&&Mathf.Abs(relative.y)<.34f;
            if(v.z<0)relative=-relative;if(relative.sqrMagnitude<.001f)relative=Vector2.down;
            if(!onscreen){float s=Mathf.Min(.42f/Mathf.Max(.001f,Mathf.Abs(relative.x)),.32f/Mathf.Max(.001f,Mathf.Abs(relative.y)));relative*=s;}
            // Never place navigation text over the reticle or the enemy being aimed at.
            bool center=onscreen&&Mathf.Abs(relative.x)<.14f&&Mathf.Abs(relative.y)<.16f;
            var playerScreen=g.player.viewCamera.WorldToViewportPoint(g.player.transform.position+Vector3.up);
            bool coversPlayer=playerScreen.z>0&&Mathf.Abs(v.x-playerScreen.x)<.1f&&Mathf.Abs(v.y-playerScreen.y)<.18f;
            float markerAlpha=center||coversPlayer?0:g.ActiveEnemies>0&&!terminal&&onscreen?.32f:1;
            markerFade.alpha=Mathf.Lerp(markerFade.alpha,markerAlpha,blend);
            var markerRect=marker.GetComponent<RectTransform>();var position=Vector2.Scale(relative,rect.rect.size);
            markerRect.anchoredPosition=wasActive?Vector2.Lerp(markerRect.anchoredPosition,position,blend):position;wasActive=true;
            arrow.text=onscreen?"◇":"▲";arrow.rectTransform.localRotation=Quaternion.Euler(0,0,onscreen?0:Mathf.Atan2(relative.y,relative.x)*Mathf.Rad2Deg-90);
            caption.text=Label+"\n"+distance.ToString("0")+" m";
            hint.text=descent?"沿高架向东 · 从前侧边缘落回地面终端":terminal?"跟随地面箭头 · 靠近安全终端按 F":jet?"可选上层 · 长按空格升至平台上方，再向落点移动":
                f.Sector==2&&!f.RouteResolved?(AtJetEntry(g)?"已到喷气入口 · 长按空格走上层 / 继续沿箭头走下层":g.player.weapon.Loadout.Jump>=2?"箭头：下层主路 · 左侧喷气入口：上层捷径":"下层主路无需升级 · 上层捷径需要跳跃 Lv.2"):
                CombatHint(g);
            hint.color=f.Boss&&f.Boss.Charging?FieldUI.Amber:FieldUI.Muted;
            for(int i=0;i<beacons.Length;i++)if(Vector3.Distance(g.player.transform.position,f.terminals[i].position)<3.5f)
            {
                string blocked=f.TerminalBlockReason(i);hint.text=blocked.Length==0?"[ F ] 打开安全终端 · 自由更换弹种":blocked;
                hint.color=blocked.Length==0?FieldUI.Cyan:FieldUI.Amber;break;
            }
            if(Time.unscaledTime>=nextPath||(lastTarget-Target).sqrMagnitude>4){nextPath=Time.unscaledTime+.7f;lastTarget=Target;UpdatePath(g);}
        }
        static string CombatHint(ArenaGame g)
        {
            var f=g.Frontier;
            if(f.Boss&&f.Boss.Charging)return "守卫锁定 · 离开预警线 / 保留冲刺";
            if(f.ArrivalPending)return "增援信号已标记 · 留出侧向移动空间";
            if(f.EncounterActive&&f.RegroupRemaining>0&&f.PendingEnemies>0)return "增援间隙 · 调整站位与射线";
            bool shield=false,ranged=false;
            foreach(var e in g.Enemies)if(e.IsAlive&&e.IsVisibleToPlayer){shield|=e.Shield&&e.Shield.IsIntact;ranged|=e.Ranged&&e.Ranged.IsRanged;}
            if(shield)return g.player.weapon.Loadout.SelectedAmmo==2?"护盾目标 · 激光单发破盾":"护盾目标 · 绕至侧后方 / 激光破盾";
            if(ranged)return "远程火力在场 · 利用掩体切断射线";
            if(g.ActiveEnemies>0)return f.ExitTrialActive&&g.player.weapon.Loadout.SelectedAmmo==0?"穿透已装备 · 将纵队压在同一射线上":"交战中 · 保持侧向移动";
            return "跟随地面箭头推进";
        }
        void UpdatePath(ArenaGame g)
        {
            var path=g.terrain.RouteCorners(g.player.transform.position,Target);BreadcrumbCount=0;
            HasGroundPath=path.Length>1;
            float carry=1.2f;
            for(int i=1;i<path.Length&&BreadcrumbCount<crumbs.Length;i++)
            {
                Vector3 delta=path[i]-path[i-1];float length=delta.magnitude;Vector3 dir=delta.normalized;
                while(carry<length&&BreadcrumbCount<crumbs.Length)
                {
                    var p=path[i-1]+dir*carry;
                    if(Physics.Raycast(p+Vector3.up*.45f,Vector3.down,out var hit,.9f,1<<9,QueryTriggerInteraction.Ignore))
                    {var c=crumbs[BreadcrumbCount++];c.position=hit.point+hit.normal*.035f;c.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(dir,hit.normal),hit.normal);c.gameObject.SetActive(true);}
                    carry+=2;
                }
                carry-=length;
            }
            for(int i=BreadcrumbCount;i<crumbs.Length;i++)crumbs[i].gameObject.SetActive(false);
        }
        void OnDestroy(){foreach(var c in crumbs)if(c)Destroy(c.gameObject);if(pathMaterial)Destroy(pathMaterial);if(pathMesh)Destroy(pathMesh);}
    }
}
