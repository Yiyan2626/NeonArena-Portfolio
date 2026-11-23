using UnityEngine;
namespace NeonArena
{
    // Four bounded particle pools use the CC0 Kenney sprites. Gameplay never depends on effects.
    public sealed class FrontierCombatFx:MonoBehaviour
    {
        public static FrontierCombatFx Instance{get;private set;}
        ParticleSystem glow,sparks,smoke,exhaust,shock;Light flash;float flashTime,dashRemainder;
        public int ShotsPresented{get;private set;}
        public int DashBursts{get;private set;}
        public static Material SpriteMaterial(string name,bool additive=true)
        {
            var m=new Material(Resources.Load<Shader>("Effects016/"+(additive?"ParticleAdditive":"ParticleAlpha")));
            m.mainTexture=Resources.Load<Texture2D>("Effects016/"+name);m.name="Kenney "+name;return m;
        }
        public static ParticleSystem CreatePool(Transform parent,string name,Material mat,int capacity=600)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);var ps=go.AddComponent<ParticleSystem>();
            var main=ps.main;main.playOnAwake=false;main.loop=false;main.maxParticles=capacity;main.simulationSpace=ParticleSystemSimulationSpace.World;
            var e=ps.emission;e.enabled=false;var shape=ps.shape;shape.enabled=false;
            var fade=ps.colorOverLifetime;fade.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(0,1)});fade.color=gradient;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial=mat;ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);return ps;
        }
        void Awake()
        {
            Instance=this;
            if(!GetComponent<CombatVfx027>())gameObject.AddComponent<CombatVfx027>();
            glow=CreatePool(transform,"Textured muzzle / hit glow",SpriteMaterial("flare_01"));
            sparks=CreatePool(transform,"Metal sparks",SpriteMaterial("spark_05"));
            var r=sparks.GetComponent<ParticleSystemRenderer>();r.renderMode=ParticleSystemRenderMode.Stretch;r.velocityScale=.12f;r.lengthScale=2;
            smoke=CreatePool(transform,"Soft muzzle smoke",SpriteMaterial("smoke_01",false),120);
            exhaust=CreatePool(transform,"Dash ion exhaust",SpriteMaterial("light_01"),300);
            shock=CreatePool(transform,"Dash ignition wave",SpriteMaterial("circle_03"),12);shock.GetComponent<ParticleSystemRenderer>().renderMode=ParticleSystemRenderMode.HorizontalBillboard;
            var growth=shock.sizeOverLifetime;growth.enabled=true;growth.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.EaseInOut(0,.55f,1,2.3f));
            var size=exhaust.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.EaseInOut(0,.45f,1,1.7f));
            flash=new GameObject("Muzzle transient light").AddComponent<Light>();flash.transform.SetParent(transform);flash.type=LightType.Point;flash.range=2.4f;flash.enabled=false;flash.shadows=LightShadows.None;
        }
        static void Emit(ParticleSystem ps,Vector3 p,Vector3 v,Color c,float size,float life,int count=1)
        {if(!ps.isPlaying)ps.Play();ps.Emit(new ParticleSystem.EmitParams{position=p,velocity=v,startColor=c,startSize=size,startLifetime=life,rotation=Random.Range(0,360)},count);}
        public void Shot(Transform muzzle,int ammo)
        {
            if(ArenaGame.Instance&&ArenaGame.Instance.State==ArenaState.Paused)return;
            ShotsPresented++;Color c=ammo<0?new Color(1,.65f,.26f):WeaponLoadout.UpgradeColor((AmmoUpgrade)ammo);
            c.a=CombatPresentation.FlashGain;
            Emit(glow,muzzle.position,muzzle.forward*.6f,c,ammo==2?.38f:.22f,ammo==2?.12f:.075f);
            for(int i=0;i<(ammo==2?10:5);i++)Emit(sparks,muzzle.position,muzzle.forward*Random.Range(3,8)+Random.insideUnitSphere*1.5f,c,Random.Range(.07f,.14f),.09f);
            Emit(smoke,muzzle.position,Vector3.up*.65f+muzzle.forward*.4f,new Color(.42f,.5f,.52f,.24f),.20f,.34f);
            flash.transform.position=muzzle.position;flash.color=c;flash.intensity=(ammo==2?2.2f:1.2f)*CombatPresentation.FlashGain;flash.enabled=CombatPresentation.FlashGain>0;flashTime=.045f;
        }
        public void Hit(Vector3 p,Color c,int count)
        {
            if(ArenaGame.Instance&&ArenaGame.Instance.State==ArenaState.Paused)return;
            c.a*=CombatPresentation.FlashGain;
            Emit(glow,p,Vector3.zero,c,Mathf.Clamp(count*.026f,.16f,.52f),.13f);
            for(int i=0;i<Mathf.Min(count,24);i++)Emit(sparks,p,Random.onUnitSphere*Random.Range(1.4f,5),c,Random.Range(.065f,.13f),Random.Range(.16f,.32f));
        }
        public void DashStart(Vector3 p,Vector3 direction)
        {
            DashBursts++;dashRemainder=0;
            Emit(shock,p+Vector3.up*.045f,Vector3.zero,new Color(.2f,.8f,1,.7f),1.25f,.24f);
            for(int i=0;i<16;i++)Emit(exhaust,p+Vector3.up*.6f,Random.insideUnitSphere*2-direction*4,new Color(.18f,.65f,1,.7f),.30f,.3f);
        }
        void LateUpdate()
        {
            if(ArenaGame.Instance&&ArenaGame.Instance.State==ArenaState.Paused)return;
            flashTime-=Time.deltaTime;if(flashTime<=0)flash.enabled=false;
            var g=ArenaGame.Instance;if(!g||!g.IsPlaying)return;var p=g.player;
            if(!p.Dash.IsActive)return;dashRemainder+=Time.deltaTime*120;
            int count=Mathf.Min(24,(int)dashRemainder);dashRemainder-=count;
            for(int i=0;i<count;i++)
            {
                Vector3 at=p.transform.position+Vector3.up*Random.Range(.35f,1.2f)+p.visual.right*Random.Range(-.25f,.25f);
                Emit(exhaust,at,-p.Dash.Direction*Random.Range(3,6),new Color(.14f,.7f,1,.75f),Random.Range(.22f,.42f),.28f);
            }
        }
        public void Clear()
        {
            if(glow)glow.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            if(sparks)sparks.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            if(smoke)smoke.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            if(exhaust)exhaust.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            if(shock)shock.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            if(flash)flash.enabled=false;flashTime=dashRemainder=0;
        }
        void OnDestroy()
        {
            if(Instance==this)Instance=null;
            foreach(var ps in new[]{glow,sparks,smoke,exhaust,shock})if(ps)Destroy(ps.GetComponent<ParticleSystemRenderer>().sharedMaterial);
        }
    }
}
