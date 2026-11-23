using UnityEngine;
using UnityEngine.UI;
namespace NeonArena
{
    public sealed class CombatFeedback : MonoBehaviour
    {
        public static CombatFeedback Instance {get;private set;}
        sealed class Popup { public Text text; public Vector3 point; public float age,amount; public int enemy; public bool kill; }
        readonly Popup[] slots=new Popup[32];int cursor,combo;float lastKill=-100,lastHit=-100;
        Text confirmation;RectTransform canvas;Material impactMaterial;
        readonly LineRenderer[] rings=new LineRenderer[12];readonly float[] ages=new float[12];int ringCursor;
        public int VisibleNumbers {get {int n=0;foreach(var p in slots)if(p!=null&&p.text.enabled)n++;return n;}}
        public int Combo=>combo;
        public int ConfirmedHits {get;private set;}
        void Awake(){Instance=this;}
        void Start()
        {
            canvas=ArenaGame.Instance.hud.GetComponent<RectTransform>();
            var root=ArenaTheme.Rect("Impact readout",canvas,Vector2.zero,new Vector2(1600,900));
            for(int i=0;i<slots.Length;i++)
            {
                var t=ArenaTheme.Text("Damage "+i,root,Vector2.zero,new Vector2(140,56),"",23,Color.white);
                var shadow=t.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(0,0,0,.9f);shadow.effectDistance=new Vector2(1,-2);
                t.enabled=false;slots[i]=new Popup{text=t};
            }
            confirmation=ArenaTheme.Text("Kill confirmation",root,new Vector2(0,115),new Vector2(400,45),"",22,ArenaTheme.Gold);
            impactMaterial=new Material(Shader.Find("NeonArena/BladeGlow"));impactMaterial.color=new Color(1,.58f,.15f,.8f);
            for(int i=0;i<rings.Length;i++){rings[i]=RuntimeArt.Line(transform,"Impact shock "+i,impactMaterial,.055f);rings[i].positionCount=33;rings[i].enabled=false;}
        }
        public void Hit(EnemyController enemy,float damage)
        {
            if(!canvas||!enemy.IsVisibleToPlayer||!ArenaGame.Instance.Vision.CanObserve(enemy.transform.position))return;
            bool kill=enemy.Health.IsDead;Vector3 point=enemy.transform.position+Vector3.up*(enemy.Elite.IsElite?2:1.25f);
            FrontierCombatReadability.Instance?.BodyImpact(enemy,point,kill);
            ConfirmedHits++;lastHit=Time.unscaledTime;
            Popup slot=null;int id=enemy.GetInstanceID();
            foreach(var s in slots)if(s.text.enabled&&s.enemy==id&&!s.kill&&s.age<.14f){slot=s;break;}
            if(slot==null){slot=slots[cursor++%slots.Length];slot.amount=0;}
            slot.point=point;slot.age=0;slot.amount+=damage;slot.enemy=id;slot.kill=kill;
            slot.text.text=Mathf.CeilToInt(slot.amount).ToString()+(kill?"  ×":"");slot.text.enabled=true;
            slot.text.fontSize=kill?29:23;
            var cam=ArenaGame.Instance.player.viewCamera.GetComponent<ArenaCamera>();cam.Kick(kill?.105f:.035f);
            if(kill&&CombatVfx027.Instance)
                CombatVfx027.Instance.Death(point,(point-ArenaGame.Instance.player.transform.position).normalized,enemy.Elite.IsElite);
            else if(!CombatVfx027.Instance)ImpactPool.Instance?.Emit(point,kill?new Color(1,.48f,.12f):new Color(.55f,.9f,1),kill?44:12);
            if(kill)
            {
                combo=Time.unscaledTime-lastKill<2.8f?combo+1:1;lastKill=Time.unscaledTime;
                ArenaAudio.Instance?.Play(SoundCue.BladeHit,point,.3f);
                if(CombatVfx027.Instance)return;
                int n=ringCursor++%rings.Length;ages[n]=0;rings[n].transform.position=enemy.transform.position+Vector3.up*.12f;rings[n].enabled=true;
            }
        }
        void LateUpdate()
        {
            var game=ArenaGame.Instance;if(!canvas||!game)return;
            bool active=game.IsPlaying;float dt=game.State==ArenaState.Paused?0:Time.unscaledDeltaTime;
            foreach(var s in slots)
            {
                if(!s.text.enabled)continue;s.age+=dt;if(s.age>.75f){s.text.enabled=false;continue;}
                var view=game.player.viewCamera.WorldToScreenPoint(s.point+Vector3.up*s.age*.8f);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas,view,null,out var local);
                s.text.rectTransform.anchoredPosition=local+new Vector2((s.enemy%3-1)*15,15);
                float alpha=active&&view.z>0?Mathf.Clamp01((.75f-s.age)*3):0;
                s.text.color=s.kill?new Color(1,.72f,.28f,alpha):new Color(.93f,1,1,alpha);
                s.text.transform.localScale=Vector3.one*(1+.3f*Mathf.Exp(-s.age*22));
            }
            float sinceKill=Time.unscaledTime-lastKill;
            confirmation.text=active&&sinceKill<1.3f?(combo>1?"连 杀  "+combo+"  /  CHAIN":"击 破  /  ELIMINATED"):"";
            confirmation.color=new Color(1,.76f,.35f,Mathf.Clamp01((1.3f-sinceKill)*3));
            for(int n=0;n<rings.Length;n++)
            {
                if(!rings[n].enabled)continue;ages[n]+=dt;
                if(ages[n]>.34f||!active){rings[n].enabled=false;continue;}
                float t=ages[n]/.34f;rings[n].widthMultiplier=.08f*(1-t)*CombatPresentation.FlashGain;
                for(int i=0;i<=32;i++){float a=i*Mathf.PI/16;rings[n].SetPosition(i,rings[n].transform.position+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*(.3f+t*1.7f));}
            }
        }
        public void Clear(){CombatVfx027.Instance?.Clear();foreach(var s in slots)if(s!=null)s.text.enabled=false;foreach(var r in rings)if(r)r.enabled=false;combo=ConfirmedHits=0;lastKill=lastHit=-100;}
        void OnDestroy(){if(Instance==this)Instance=null;if(impactMaterial)Destroy(impactMaterial);}
    }
}
