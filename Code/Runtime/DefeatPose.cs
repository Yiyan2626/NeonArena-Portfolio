using UnityEngine;

namespace NeonArena
{
    public sealed class DefeatPose : MonoBehaviour
    {
        static int active;
        MaterialPropertyBlock dissolve; Material[] originalMaterials; Vector3[] jointOrigins;
        float time;CharacterModel model;Quaternion initial;Vector3 origin,initialScale,drift;
        public static void Play(CharacterMotion motion)
        {
            if(!motion||!motion.Model||active>=8)return;
            var copy=Instantiate(motion.Model,motion.Model.transform.position,motion.Model.transform.rotation);copy.transform.localScale=motion.Model.transform.lossyScale;
            foreach(var extra in copy.GetComponentsInChildren<SkinnedMeshRenderer>())if(extra!=copy.skin)Destroy(extra.gameObject);
            var death=copy.gameObject.AddComponent<DefeatPose>();death.model=copy;death.initial=copy.transform.rotation;death.origin=copy.transform.position;death.initialScale=copy.transform.localScale;
            var game=ArenaGame.Instance;death.drift=game?Vector3.ProjectOnPlane(copy.transform.position-game.player.transform.position,Vector3.up).normalized:Vector3.forward;
            if(Physics.SphereCast(death.origin+Vector3.up*.5f,.4f,death.drift,out var hit,1.2f,1<<9,QueryTriggerInteraction.Ignore))death.drift*=Mathf.Clamp01(hit.distance/1.2f);
            death.PrepareBreakup();
            active++;
        }
        void PrepareBreakup()
        {
            if(!CombatVfx027.Instance||!model.skin)return;
            originalMaterials=model.skin.sharedMaterials;var replacements=new Material[originalMaterials.Length];
            for(int i=0;i<replacements.Length;i++)replacements[i]=CombatVfx027.Instance.DissolveMaterial;
            model.skin.sharedMaterials=replacements;dissolve=new MaterialPropertyBlock();
            if(!model.imported&&model.joints!=null){jointOrigins=new Vector3[model.joints.Length];for(int i=0;i<jointOrigins.Length;i++)jointOrigins[i]=model.joints[i].localPosition;}
        }
        void Update()
        {
            var game=ArenaGame.Instance;if(!game||game.State==ArenaState.Ready||game.State==ArenaState.Dead){Destroy(gameObject);return;}
            if(!game.IsPlaying){if(game.State!=ArenaState.Paused)Destroy(gameObject);return;}
            time+=Time.deltaTime;
            transform.rotation=initial*Quaternion.Euler(Mathf.Lerp(0,-78,time/.28f),0,Mathf.Sin(time*13)*5);
            transform.position=origin+drift*Mathf.Min(.85f,time*3)+Vector3.up*(Mathf.Sin(Mathf.Min(1,time/.5f)*Mathf.PI)*.24f);
            if(dissolve!=null)
            {
                float split=Mathf.Clamp01((time-.10f)/.4f);
                if(jointOrigins!=null)for(int i=0;i<jointOrigins.Length;i++)
                {
                    if(i!=2&&i!=3&&i!=6&&i!=9&&i!=12)continue;
                    float side=i==3||i==9?-1:1;
                    model.joints[i].localPosition=jointOrigins[i]+new Vector3(i==2?0:side*.22f,i==2?.18f:0,.06f)*split;
                }
                for(int i=0;i<originalMaterials.Length;i++)
                {
                    var original=originalMaterials[i];dissolve.Clear();
                    if(original&&original.mainTexture)dissolve.SetTexture("_MainTex",original.mainTexture);
                    dissolve.SetColor("_Color",original&&original.HasProperty("_Color")?original.color:Color.white);
                    dissolve.SetFloat("_Dissolve",Mathf.Clamp01((time-.35f)/.65f));dissolve.SetFloat("_FlashGain",CombatPresentation.FlashGain);
                    model.skin.SetPropertyBlock(dissolve,i);
                }
            }
            if(!game.Vision.CanObserve(transform.position)||time>1.02f)Destroy(gameObject);
        }
        void OnDestroy(){active=Mathf.Max(0,active-1);}
    }
}
