using UnityEngine;
namespace NeonArena
{
    // Camera-only cutaway. The physical collider and enemy visibility remain solid.
    public sealed class CameraOccluder : MonoBehaviour
    {
        static readonly System.Collections.Generic.List<CameraOccluder> imported=new System.Collections.Generic.List<CameraOccluder>();
        Renderer surface; MaterialPropertyBlock block; float requestedUntil,supportUntil,fade=1;
        public float Fade => fade;
        public bool SupportingPlayer => Time.unscaledTime < supportUntil;
        public bool hideInside;
        public Bounds interiorBounds;
        void Awake(){surface=GetComponent<Renderer>();block=new MaterialPropertyBlock();}
        public void Reveal(){if(!SupportingPlayer)requestedUntil=Time.unscaledTime+.12f;}
        // A ramp is both an occluder and the player's footing. Never cut away that footing.
        public void ProtectFooting()
        {
            supportUntil=Time.unscaledTime+.18f;requestedUntil=0;fade=1;
            if(surface){surface.GetPropertyBlock(block);block.SetFloat("_CameraFade",1);surface.SetPropertyBlock(block);surface.forceRenderingOff=false;}
        }
        void OnEnable(){if(GetComponent<Renderer>())imported.Add(this);}
        public static void RevealImportedAlong(Vector3 eye,Vector3 target)
        {
            Vector3 delta=target-eye;var ray=new Ray(eye,delta.normalized);float length=delta.magnitude;
            // Captured mesh colliders can omit facade triangles. Fade conservatively without changing physics.
            foreach(var item in imported)
            {
                if(!item||!item.surface)continue;
                var bounds=item.surface.bounds;
                // Captured plazas may combine several floor heights in one mesh. Their bounding
                // box can contain the torso even when the actual triangles are all below it.
                if(bounds.max.y<target.y+.4f&&bounds.size.y<Mathf.Min(bounds.size.x,bounds.size.z)*.15f)continue;
                if(bounds.IntersectRay(ray,out float distance)&&distance<length)item.Reveal();
            }
        }
        void LateUpdate()
        {
            if(!surface)return;
            var game=ArenaGame.Instance;bool playing=game&&(game.IsPlaying||game.State==ArenaState.Paused);
            float target=playing&&!SupportingPlayer&&Time.unscaledTime<requestedUntil?0:1;
            float next=Mathf.MoveTowards(fade,target,Time.unscaledDeltaTime*(target<fade?12:3));
            if(!Mathf.Approximately(next,fade)){fade=next;surface.GetPropertyBlock(block);block.SetFloat("_CameraFade",fade);surface.SetPropertyBlock(block);}
            bool backwards=false;
            if(GetComponent<TextMesh>()&&game&&game.player&&game.player.viewCamera)
                backwards=Vector3.Dot(transform.forward,game.player.viewCamera.transform.position-transform.position)>0;
            surface.forceRenderingOff=fade<=.01f||backwards||(hideInside&&game&&game.player&&interiorBounds.Contains(game.player.transform.position));
        }
        void OnDisable(){imported.Remove(this);fade=1;if(surface){surface.SetPropertyBlock(null);surface.forceRenderingOff=false;}}
    }
}
