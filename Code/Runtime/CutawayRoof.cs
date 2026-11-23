using UnityEngine;
using System.Collections.Generic;
namespace NeonArena
{
    public sealed class CutawayRoof : MonoBehaviour
    {
        Renderer surface; BoxCollider volume;
        readonly List<Renderer> decorations=new List<Renderer>();
        public void RegisterDecorations(Renderer[] candidates)
        {
            foreach(var r in candidates)
            {
                // Registration runs before the first physics sync; use the authored transform.
                var p=transform.InverseTransformPoint(r.transform.position)-volume.center;
                var half=volume.size*.5f;
                if(p.y>-half.y && Mathf.Abs(p.x)<half.x && Mathf.Abs(p.z)<half.z)decorations.Add(r);
            }
        }
        public bool IsCutaway
        {
            get
            {
                var game=ArenaGame.Instance;if(!game||!volume)return false;
                Vector3 p=game.player.transform.position;var b=volume.bounds;
                return p.y<b.min.y && p.x>b.min.x-1 && p.x<b.max.x+1 && p.z>b.min.z-1 && p.z<b.max.z+1;
            }
        }
        void Awake(){surface=GetComponent<Renderer>();volume=GetComponent<BoxCollider>();}
        void Start(){if(name.StartsWith("Hall") && transform.parent)RegisterDecorations(transform.parent.GetComponentsInChildren<Renderer>());}
        void LateUpdate()
        {
            bool visible=!IsCutaway;
            if(surface)surface.enabled=visible;
            foreach(var r in decorations)if(r)r.enabled=visible;
        }
    }
}
