using UnityEngine;

namespace NeonArena
{
    // All geometry, materials and particle buffers are allocated once. Effects never own combat state.
    public sealed class CombatVfx027 : MonoBehaviour
    {
        public static CombatVfx027 Instance { get; private set; }
        public const int RibbonCapacity = 64;
        sealed class Ribbon
        {
            public LineRenderer line;
            public Vector3 origin, axis, across;
            public Color color;
            public float age, life, radius, width, phase;
            public int kind;
        }
        readonly Ribbon[] ribbons = new Ribbon[RibbonCapacity];
        readonly ParticleSystem[] pools = new ParticleSystem[6];
        readonly Material[] materials = new Material[7];
        readonly int[] impacts = new int[4], travels = new int[4];
        int cursor;
        Mesh shard;
        public Material DissolveMaterial { get; private set; }
        bool paused;
        public int DeathsPresented { get; private set; }
        public int BouncesPresented { get; private set; }
        public int LaserPulses { get; private set; }
        public int ActiveRibbons { get { int n=0; foreach(var r in ribbons) if(r!=null&&r.line.enabled)n++; return n; } }
        public int ActiveParticles { get { int n=0; foreach(var p in pools)if(p)n+=p.particleCount; return n; } }
        public int ImpactsForAmmo(int ammo) => impacts[Mathf.Clamp(ammo+1,0,3)];
        public int TravelForAmmo(int ammo) => travels[Mathf.Clamp(ammo+1,0,3)];

        void Awake()
        {
            Instance=this;
            DissolveMaterial=new Material(Resources.Load<Shader>("ArmorDissolve027"));
            string[] sprites={"spark_05","spark_01","smoke_01","light_01","flare_01","spark_01"};
            int[] limits={420,240,72,260,80,180};
            for(int i=0;i<pools.Length;i++)
            {
                materials[i]=FrontierCombatFx.SpriteMaterial(sprites[i],i!=2&&i!=5);
                pools[i]=FrontierCombatFx.CreatePool(transform,"027 "+sprites[i]+" layer "+i,materials[i],limits[i]);
                var main=pools[i].main; main.gravityModifier=i==0?.3f:i==5?1.2f:0;
                var renderer=pools[i].GetComponent<ParticleSystemRenderer>();
                if(i==0||i==1){renderer.renderMode=ParticleSystemRenderMode.Stretch;renderer.velocityScale=.045f;renderer.lengthScale=i==0?2.1f:1.2f;}
                if(i==2){var size=pools[i].sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,.5f,1,1.9f));}
            }
            // Faceted, tumbling metal fragments: silhouette stays dark against the electrical corona.
            shard=new Mesh{name="027 fractured armor sliver"};
            shard.vertices=new[]{new Vector3(-.45f,-.12f,0),new Vector3(.55f,-.18f,0),new Vector3(.1f,.5f,0),new Vector3(0,0,.22f)};
            shard.triangles=new[]{0,2,1,0,1,3,1,2,3,2,0,3};shard.RecalculateNormals();shard.RecalculateBounds();
            var debris=pools[5].GetComponent<ParticleSystemRenderer>();debris.renderMode=ParticleSystemRenderMode.Mesh;debris.mesh=shard;
            var rotation=pools[5].rotationOverLifetime;rotation.enabled=true;rotation.separateAxes=true;
            rotation.x=new ParticleSystem.MinMaxCurve(-8,8);rotation.y=new ParticleSystem.MinMaxCurve(-5,5);rotation.z=new ParticleSystem.MinMaxCurve(-7,7);
            // Mesh UVs use the central opaque texel instead of the transparent sprite corners.
            shard.uv=new[]{Vector2.one*.5f,Vector2.one*.5f,Vector2.one*.5f,Vector2.one*.5f};
            materials[6]=new Material(Shader.Find("NeonArena/Beam"));materials[6].color=Color.white;
            for(int i=0;i<ribbons.Length;i++)
            {
                var line=RuntimeArt.Line(transform,"027 pooled discharge "+i,materials[6],.02f);
                line.positionCount=18;line.enabled=false;line.numCapVertices=2;
                ribbons[i]=new Ribbon{line=line};
            }
        }
        void Emit(ParticleSystem ps,Vector3 p,Vector3 v,Color color,float size,float life)
        {
            if(ArenaGame.Instance&&ArenaGame.Instance.State==ArenaState.Paused)return;
            if(ps!=pools[2]&&ps!=pools[5])color.a*=.20f+.80f*CombatPresentation.FlashGain;
            if(ps==pools[4])color.a*=CombatPresentation.FlashGain;
            if(!ps.isPlaying)ps.Play();
            ps.Emit(new ParticleSystem.EmitParams{position=p,velocity=v,startColor=color,startSize=size,startLifetime=life,rotation3D=Random.insideUnitSphere*180},1);
        }
        Ribbon Add(Vector3 p,Vector3 axis,Color c,float radius,float life,float width,int kind)
        {
            var r=ribbons[cursor++%ribbons.Length];r.origin=p;r.axis=axis.sqrMagnitude>.01f?axis.normalized:Vector3.up;
            r.across=Vector3.Cross(r.axis,Mathf.Abs(r.axis.y)>.9f?Vector3.right:Vector3.up).normalized;
            r.color=c;r.radius=radius;r.life=life;r.width=width;r.kind=kind;r.phase=Random.Range(0,6.28f);r.age=0;
            r.line.enabled=true;Draw(r);return r;
        }
        public void Travel(Vector3 from,Vector3 to,Vector3 direction,Color c,int ammo)
        {
            if(ArenaGame.Instance&&ArenaGame.Instance.State==ArenaState.Paused)return;
            if((to-from).sqrMagnitude<.0001f)return;
            travels[Mathf.Clamp(ammo+1,0,3)]++;
            if(ammo==0)
            {
                // Piercing: a long needle wrapped by two counter-rotating rails.
                Add(to-direction*.38f,direction,c,.23f,.11f,.019f,3);
                Add(to-direction*.38f,direction,new Color(.8f,1,1,.8f),.15f,.08f,.012f,3);
                Emit(pools[1],to,-direction*.8f,c,.065f,.12f);
            }
            else if(ammo==1)
            {
                // Ricochet: separated charged flecks, never a duplicate straight laser.
                Emit(pools[3],to,Vector3.zero,c,.075f,.19f);
                Emit(pools[1],Vector3.Lerp(from,to,.5f),Vector3.zero,c,.045f,.14f);
            }
            else Emit(pools[0],to,-direction*2,new Color(1,.65f,.3f,.72f),.032f,.075f);
        }
        public void Impact(Vector3 p,Vector3 normal,Vector3 incoming,Color c,int ammo,bool bounce)
        {
            if(ArenaGame.Instance&&ArenaGame.Instance.State==ArenaState.Paused)return;
            impacts[Mathf.Clamp(ammo+1,0,3)]++;
            normal=normal.sqrMagnitude>.1f?normal.normalized:-incoming.normalized;
            p+=normal*.025f;
            Emit(pools[4],p,normal*.2f,Color.Lerp(c,Color.white,.45f),ammo==2?.30f:.17f,.075f);
            int count=ammo==2?16:ammo==0?12:8;
            for(int i=0;i<count;i++)
            {
                Vector3 spread=Random.insideUnitSphere; if(Vector3.Dot(spread,normal)<0)spread=-spread;
                Vector3 v=(normal*.65f+spread)*Random.Range(2,6);
                Emit(pools[0],p,v,c,Random.Range(.035f,.075f),Random.Range(.13f,.32f));
            }
            Emit(pools[2],p,normal*.7f+Vector3.up*.3f,new Color(.22f,.28f,.3f,.23f),.20f,.44f);
            for(int i=0;i<3;i++)
            {
                Vector3 fan=(normal+Random.insideUnitSphere*.65f).normalized;
                Add(p,fan,Color.Lerp(c,Color.white,.35f),.34f,.20f,.032f,4);
            }
            if(ammo==0)
            {
                Add(p,incoming,c,.34f,.20f,.022f,1);
                // The exit jet is narrow and points downrange.
                for(int i=0;i<6;i++)Emit(pools[1],p,incoming*Random.Range(4,8)+Random.insideUnitSphere*.5f,c,.045f,.20f);
            }
            if(ammo==2){Add(p,normal,c,.45f,.25f,.027f,1);Add(p,normal,Color.white,.27f,.13f,.018f,2);}
            if(bounce)
            {
                BouncesPresented++;
                Vector3 reflected=Vector3.Reflect(incoming,normal).normalized;
                Add(p,normal,c,.36f,.23f,.025f,2);
                for(int i=0;i<7;i++)Emit(pools[1],p,reflected*Random.Range(3,7)+Random.insideUnitSphere*.35f,c,.06f,.24f);
            }
        }
        public void Death(Vector3 p,Vector3 direction,bool elite)
        {
            if(ArenaGame.Instance&&ArenaGame.Instance.State==ArenaState.Paused)return;
            DeathsPresented++;float scale=elite?1.3f:1;
            Color hot=new Color(1,.55f,.16f,.85f),ion=new Color(.28f,.86f,1,.75f);
            // Three readable beats: core rupture, broken electric loops, heavy shell and cooling embers.
            Emit(pools[4],p,Vector3.zero,new Color(1,.83f,.5f,.6f),.43f*scale,.09f);
            Add(p,Vector3.up,hot,.8f*scale,.54f,.055f,1);
            // A fragmented core has open gaps; there is never an opaque luminous sphere.
            for(int i=0;i<3;i++)
            {
                Vector3 axis=Quaternion.AngleAxis(i*60,direction)*Vector3.up;
                Add(p,axis,i==1?hot:ion,.42f*scale,.38f,.060f,5);
            }
            Vector3 side=Vector3.Cross(direction,Vector3.up).normalized;
            if(side.sqrMagnitude<.1f)side=Vector3.right;
            for(int i=0;i<9;i++)
            {
                float a=i*Mathf.PI*2/9;
                Vector3 ray=(side*Mathf.Cos(a)+Vector3.up*Mathf.Sin(a)+direction*.18f).normalized;
                Add(p+ray*.12f,ray,i%3==0?ion:hot,Random.Range(.65f,1.05f)*scale,Random.Range(.36f,.56f),.055f,4);
            }
            for(int i=0;i<22;i++)
            {
                Vector3 v=Random.onUnitSphere;v.y=Mathf.Abs(v.y)*.65f+.2f;v=(v*Random.Range(1.6f,4.4f)+direction*.7f)*scale;
                Emit(pools[0],p,v,i%3==0?ion:hot,Random.Range(.065f,.13f),Random.Range(.32f,.65f));
                if(i<12)Emit(pools[5],p+Random.insideUnitSphere*.15f,v*.8f,new Color(.42f,.46f,.49f,.95f),Random.Range(.20f,.36f)*scale,Random.Range(.62f,.95f));
                if(i<9)Emit(pools[3],p+Random.insideUnitSphere*.24f,v*.45f,hot,Random.Range(.07f,.13f),Random.Range(.55f,.95f));
            }
            for(int i=0;i<3;i++)Emit(pools[2],p+Random.insideUnitSphere*.22f,Vector3.up*Random.Range(.35f,.65f),new Color(.18f,.23f,.25f,.20f),.40f*scale,.65f);
        }
        public void Laser(Vector3 from,Vector3 to)
        {
            if(ArenaGame.Instance&&ArenaGame.Instance.State==ArenaState.Paused)return;
            LaserPulses++;Vector3 delta=to-from;float length=delta.magnitude;if(length<.01f)return;
            Vector3 axis=delta/length;Color c=new Color(1,.58f,.14f,.8f);
            Add(from,axis,c,length,.14f,.085f,0);
            Add(from,axis,new Color(1,.94f,.77f,1),length,.075f,.028f,0);
            // Finite pulse chambers recede along the actual hit-tested beam.
            for(int i=0;i<Mathf.Min(10,Mathf.FloorToInt(length/2));i++)Add(from+axis*(1+i*2),axis,c,.18f,.18f,.016f,1);
        }
        void Draw(Ribbon r)
        {
            float t=Mathf.Clamp01(r.age/r.life),fade=r.kind>=4?Mathf.Clamp01((1-t)*1.7f):(1-t)*(1-t);
            Color c=r.color;c.a*=fade*(r.kind==0?1:.8f)*(.20f+.80f*CombatPresentation.FlashGain);
            r.line.startColor=r.line.endColor=c;r.line.widthMultiplier=r.width*(1-t*.6f);
            Vector3 up=Vector3.Cross(r.axis,r.across);
            for(int i=0;i<18;i++)
            {
                float u=i/17f;Vector3 p;
                if(r.kind==0)p=r.origin+r.axis*(u*r.radius);
                else if(r.kind==4)
                {
                    // Separated outward shards keep their directional silhouette through the decay.
                    float distance=r.radius*(t*.8f+u*(1-t*.55f));
                    p=r.origin+r.axis*distance+r.across*Mathf.Sin(u*9.42f+r.phase)*.025f*Mathf.Sin(u*Mathf.PI);
                }
                else if(r.kind==3)
                {float a=u*9.42f+r.phase;float rad=r.radius*.26f*(1-t);p=r.origin+r.axis*(u*.72f)+(r.across*Mathf.Cos(a)+up*Mathf.Sin(a))*rad;}
                else
                {
                    float a=r.phase+u*(r.kind==5?2.1f:r.kind==2?4.6f:5.65f);float rad=r.radius*(.3f+t*.95f);
                    if(r.kind==2||r.kind==5)rad*=1+Mathf.Sin(i*2.4f+r.phase)*.19f;
                    p=r.origin+(r.across*Mathf.Cos(a)+up*Mathf.Sin(a))*rad;
                }
                r.line.SetPosition(i,p);
            }
        }
        void LateUpdate()
        {
            var game=ArenaGame.Instance;bool shouldPause=game&&game.State==ArenaState.Paused;
            if(shouldPause!=paused)
            {paused=shouldPause;foreach(var ps in pools){if(paused)ps.Pause();else if(ps.particleCount>0)ps.Play();}}
            if(paused)return;
            foreach(var r in ribbons)if(r.line.enabled){r.age+=Time.deltaTime;if(r.age>=r.life)r.line.enabled=false;else Draw(r);}
        }
        public void Clear()
        {
            foreach(var r in ribbons)if(r!=null)r.line.enabled=false;
            foreach(var p in pools)if(p)p.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            for(int i=0;i<4;i++)impacts[i]=travels[i]=0;
            DeathsPresented=BouncesPresented=LaserPulses=cursor=0;paused=false;
        }
        void OnDestroy(){if(Instance==this)Instance=null;foreach(var m in materials)if(m)Destroy(m);if(shard)Destroy(shard);if(DissolveMaterial)Destroy(DissolveMaterial);}
    }
}
