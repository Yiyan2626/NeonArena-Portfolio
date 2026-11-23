using UnityEngine;
namespace NeonArena
{
    public sealed class RicochetGuide:MonoBehaviour
    {
        Weapon weapon;LineRenderer line;Material material;readonly Vector3[] points=new Vector3[5];
        void Awake(){weapon=GetComponent<Weapon>();material=new Material(Shader.Find("NeonArena/Beam"));material.color=new Color(.70f,.40f,1,.32f);line=RuntimeArt.Line(transform,"Ricochet trajectory preview",material,.018f);}
        void LateUpdate()
        {
            var g=ArenaGame.Instance;bool show=g&&g.Frontier&&g.IsPlaying&&weapon.Loadout.Ricochet>0&&!g.player.Overheat.Active;
            line.enabled=show;if(!show)return;
            Vector3 origin=weapon.muzzle.position;Vector3 direction=(g.player.AimPoint-origin).normalized;float remaining=70;points[0]=origin;int count=1;
            for(int bounce=0;bounce<=3;bounce++)
            {
                if(!Physics.Raycast(origin,direction,out var hit,remaining,(1<<8)|(1<<9),QueryTriggerInteraction.Ignore)){points[count++]=origin+direction*remaining;break;}
                points[count++]=hit.point;remaining-=hit.distance;
                var shield=hit.collider.GetComponent<EnemyShield>();if(hit.collider.GetComponentInParent<Health>()&&(!shield||!shield.IsIntact)||bounce==3||remaining<=0)break;
                origin=hit.point+hit.normal*.025f;direction=Vector3.Reflect(direction,hit.normal).normalized;
            }
            line.positionCount=count;for(int i=0;i<count;i++)line.SetPosition(i,points[i]);
        }
        void OnDestroy(){if(material)Destroy(material);}
    }
}
