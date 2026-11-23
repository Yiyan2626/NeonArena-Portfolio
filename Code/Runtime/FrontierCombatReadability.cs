using UnityEngine;
using UnityEngine.UI;
namespace NeonArena
{
    // Bounded UI/particle feedback. It never reveals an enemy hidden by cover or brush.
    [DefaultExecutionOrder(320)]
    public sealed class FrontierCombatReadability:MonoBehaviour
    {
        public static FrontierCombatReadability Instance{get;private set;}
        public int BodyConfirmations{get;private set;}
        public int ShieldBreaks{get;private set;}
        public int VisibleThreats{get;private set;}
        public string LastImpact{get;private set;}
        RectTransform canvas,reticle;Text hitLabel;Image[] hitMarks=new Image[4];
        readonly Text[] threats=new Text[3];readonly Image[] bars=new Image[3];
        readonly EnemyController[] selected=new EnemyController[3];readonly float[] urgency=new float[3];
        ParticleSystem fragments;Material shardMaterial;float hitUntil;Color hitColor;
        void Awake(){Instance=this;}
        void Start()
        {
            canvas=ArenaGame.Instance.hud.GetComponent<RectTransform>();
            reticle=ArenaTheme.Rect("Impact confirmation",canvas,Vector2.zero,new Vector2(110,70));
            for(int i=0;i<4;i++)
            {
                float a=(45+i*90)*Mathf.Deg2Rad;
                hitMarks[i]=ArenaTheme.Box("Hit notch",reticle,new Vector2(Mathf.Cos(a),Mathf.Sin(a))*20,new Vector2(9,2),Color.clear);
                hitMarks[i].rectTransform.localRotation=Quaternion.Euler(0,0,45+i*90);
            }
            hitLabel=ArenaTheme.Text("Impact kind",reticle,new Vector2(0,-41),new Vector2(200,28),"",15,ArenaTheme.Paper);
            for(int i=0;i<3;i++)
            {
                threats[i]=ArenaTheme.Text("Attack warning "+i,canvas,Vector2.zero,new Vector2(215,52),"",16,new Color(1,.65f,.15f));
                var sh=threats[i].gameObject.AddComponent<Shadow>();sh.effectColor=new Color(0,0,0,.95f);sh.effectDistance=new Vector2(1,-2);
                bars[i]=ArenaTheme.Box("Attack countdown",threats[i].transform,new Vector2(-50,-26),new Vector2(100,3),Color.clear);bars[i].rectTransform.pivot=new Vector2(0,.5f);
            }
            shardMaterial=FrontierCombatFx.SpriteMaterial("spark_01");
            fragments=FrontierCombatFx.CreatePool(transform,"Shield energy fragments",shardMaterial,160);
            var r=fragments.GetComponent<ParticleSystemRenderer>();r.renderMode=ParticleSystemRenderMode.Stretch;r.velocityScale=.07f;r.lengthScale=1.5f;
        }
        void Confirm(string label,Color color,float duration)
        {LastImpact=label;hitColor=color;hitUntil=Time.unscaledTime+duration;}
        public void Clear()
        {
            hitUntil=0;LastImpact="";BodyConfirmations=ShieldBreaks=VisibleThreats=0;
            if(reticle)reticle.gameObject.SetActive(false);foreach(var t in threats)if(t)t.gameObject.SetActive(false);
            if(fragments)fragments.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        public void BodyImpact(EnemyController owner,Vector3 point,bool kill)
        {
            if(!owner.IsVisibleToPlayer)return;
            BodyConfirmations++;Confirm(kill?"击破":"命中",kill?new Color(1,.57f,.18f):new Color(.8f,1,1),kill?.3f:.12f);
        }
        public void ShieldImpact(EnemyController owner,Vector3 point,bool broken,bool reflected)
        {
            if(!owner.IsVisibleToPlayer)return;
            Confirm(broken?"破盾 · 压制窗口":reflected?"护盾反射":"护盾吸收",broken?new Color(.3f,.95f,1):new Color(.68f,.50f,1),broken?.5f:.2f);
            if(!broken)return;ShieldBreaks++;if(!fragments)return;
            fragments.Play();
            for(int i=0;i<28;i++)fragments.Emit(new ParticleSystem.EmitParams{position=point+Random.insideUnitSphere*.25f,velocity=owner.visual.forward*2+Random.onUnitSphere*Random.Range(2,5),startColor=new Color(.35f,.8f,1,.8f),startLifetime=Random.Range(.25f,.55f),startSize=Random.Range(.1f,.2f)},1);
        }
        static bool Warning(EnemyController e,out string label,out float remaining,out float duration)
        {
            label="";remaining=0;duration=1;
            var boss=e.GetComponent<FrontierBoss>();
            if(boss&&boss.enabled&&boss.Charging){label=boss.Stage==3?"炮击锁定":"重炮蓄能";remaining=boss.WarningRemaining;duration=boss.Stage==3?.95f:1.2f;}
            else if(e.Ranged.Charging){label=e.Ranged.HoldPosition?"狙击锁定":"射击蓄能";remaining=e.Ranged.WarningRemaining;duration=.85f;}
            else if(e.Charge&&e.Charge.Phase==ChargePhase.Telegraph){label="冲锋 · 横向闪避";remaining=e.Charge.PhaseRemaining;duration=e.Charge.windup;}
            else if(e.MeleePreparing){label="近身挥击";remaining=e.MeleeRemaining;duration=.42f;}
            return remaining>0;
        }
        void LateUpdate()
        {
            var g=ArenaGame.Instance;if(!canvas||!g)return;
            bool active=g.IsPlaying&&!g.Cinema.FinaleActive;
            bool shown=active&&Time.unscaledTime<hitUntil&&g.hud.crosshair.gameObject.activeSelf;
            reticle.gameObject.SetActive(shown);
            if(shown){reticle.position=g.hud.crosshair.position;hitLabel.text=LastImpact;hitLabel.color=hitColor;foreach(var mark in hitMarks)mark.color=hitColor;}
            for(int i=0;i<3;i++){selected[i]=null;urgency[i]=float.MaxValue;}
            if(active)foreach(var e in g.Enemies)
            {
                if(!e.IsAlive||!e.IsVisibleToPlayer||!g.Vision.CanObserve(e.VisibilityPoint)||!Warning(e,out _,out float seconds,out _))continue;
                for(int i=0;i<3;i++)if(seconds<urgency[i]){for(int j=2;j>i;j--){selected[j]=selected[j-1];urgency[j]=urgency[j-1];}selected[i]=e;urgency[i]=seconds;break;}
            }
            VisibleThreats=0;
            for(int i=0;i<3;i++)
            {
                var e=selected[i];threats[i].gameObject.SetActive(e);if(!e)continue;VisibleThreats++;
                Warning(e,out string label,out float seconds,out float duration);
                Vector3 screen=g.player.viewCamera.WorldToViewportPoint(e.transform.position+Vector3.up*(e.Elite.IsElite?3.8f:2.5f));
                Vector2 delta=new Vector2(screen.x-.5f,screen.y-.5f);bool outside=screen.z<0||Mathf.Abs(delta.x)>.36f||Mathf.Abs(delta.y)>.24f;
                if(screen.z<0)delta=-delta;if(delta.sqrMagnitude<.0001f)delta=Vector2.down;
                if(outside)delta*=Mathf.Min(.36f/Mathf.Max(.001f,Mathf.Abs(delta.x)),.24f/Mathf.Max(.001f,Mathf.Abs(delta.y)));
                var pos=Vector2.Scale(delta,canvas.rect.size);
                for(int j=0;j<i;j++)if(selected[j]&&Vector2.Distance(pos,threats[j].rectTransform.anchoredPosition)<120)pos.y-=60;
                threats[i].rectTransform.anchoredPosition=pos;
                string direction=outside?(delta.x<-.12f?"◀ ":delta.x>.12f?"▶ ":delta.y<0?"▼ ":"▲ "):"";
                threats[i].text=direction+label+"\n"+seconds.ToString("0.0")+"s";
                Color c=seconds<.25f?new Color(1,.25f,.12f):new Color(1,.72f,.18f);threats[i].color=bars[i].color=c;
                bars[i].rectTransform.sizeDelta=new Vector2(100*Mathf.Clamp01(seconds/duration),3);
            }
        }
        void OnDestroy(){if(Instance==this)Instance=null;if(shardMaterial)Destroy(shardMaterial);if(reticle)Destroy(reticle.gameObject);foreach(var t in threats)if(t)Destroy(t.gameObject);}
    }
}
