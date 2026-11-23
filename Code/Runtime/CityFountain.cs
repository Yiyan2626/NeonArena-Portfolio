using UnityEngine;
namespace NeonArena
{
    public sealed class CityFountain:MonoBehaviour
    {
        ParticleSystem droplets,foam,ripples;Material dropMaterial,foamMaterial,ringMaterial;float budget;
        readonly Vector3[] jets={new Vector3(-3.25f,1.81f,1.03f),new Vector3(-1.63f,1.81f,-.85f),new Vector3(-.02f,1.81f,1.03f),new Vector3(1.61f,1.81f,-.85f),new Vector3(3.22f,1.81f,1.03f)};
        void Start()
        {
            dropMaterial=FrontierCombatFx.SpriteMaterial("spark_01",false);foamMaterial=FrontierCombatFx.SpriteMaterial("smoke_01",false);ringMaterial=FrontierCombatFx.SpriteMaterial("circle_03",false);
            droplets=FrontierCombatFx.CreatePool(transform,"Fountain continuous jets",dropMaterial,900);var m=droplets.main;m.gravityModifier=1;
            var r=droplets.GetComponent<ParticleSystemRenderer>();r.renderMode=ParticleSystemRenderMode.Stretch;r.lengthScale=2;r.velocityScale=.03f;
            foam=FrontierCombatFx.CreatePool(transform,"Fountain splash / mist",foamMaterial,180);
            ripples=FrontierCombatFx.CreatePool(transform,"Fountain surface ripples",ringMaterial,120);ripples.GetComponent<ParticleSystemRenderer>().renderMode=ParticleSystemRenderMode.HorizontalBillboard;
            var size=ripples.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,.2f,1,1.5f));
        }
        void Update()
        {
            if(!droplets)return;float dt=Mathf.Min(Time.deltaTime,.04f);budget+=dt*110;int n=(int)budget;budget-=n;
            if(!droplets.isPlaying){droplets.Play();foam.Play();ripples.Play();}
            for(int j=0;j<jets.Length;j++)for(int i=0;i<n;i++)
            {
                float speed=5.0f+.18f*Mathf.Sin(Time.time*1.7f+j);Vector3 v=Vector3.up*speed+Vector3.forward*(j%2==0?-.5f:.5f)+Random.insideUnitSphere*.11f;
                droplets.Emit(new ParticleSystem.EmitParams{position=jets[j],velocity=v,startLifetime=1.05f,startSize=Random.Range(.045f,.075f),startColor=new Color(.68f,.9f,.95f,.8f)},1);
                if(i!=0)continue;var at=jets[j]+Vector3.forward*(j%2==0?-.53f:.53f);at.y=1.81f;
                foam.Emit(new ParticleSystem.EmitParams{position=at,velocity=Vector3.up*.15f+Random.insideUnitSphere*.12f,startLifetime=.4f,startSize=.26f,startColor=new Color(.72f,.91f,.96f,.22f)},1);
                if(Time.frameCount%4==0)ripples.Emit(new ParticleSystem.EmitParams{position=at,velocity=Vector3.zero,startLifetime=.8f,startSize=.65f,startColor=new Color(.72f,.93f,.99f,.34f)},1);
            }
            Shader.SetGlobalFloat("_WaterLocalUvX",Time.time*.017f);Shader.SetGlobalFloat("_WaterLocalUvZ",Time.time*.009f);
            Shader.SetGlobalFloat("_WaterLocalUvNX",Time.time*.027f);Shader.SetGlobalFloat("_WaterLocalUvNZ",-Time.time*.019f);
        }
        void OnDestroy(){foreach(var m in new[]{dropMaterial,foamMaterial,ringMaterial})if(m)Destroy(m);}
    }
}
