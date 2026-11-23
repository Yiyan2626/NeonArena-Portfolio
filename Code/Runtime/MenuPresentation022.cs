using UnityEngine;

namespace NeonArena
{
    // Attached only by FrontEnd; the original camera, city, and lighting stay intact.
    [RequireComponent(typeof(Camera))]
    public sealed class MenuPresentation022 : MonoBehaviour
    {
        Material grade;
        void OnEnable()
        {
            var shader=Resources.Load<Shader>("UI/MenuGrade022");
            if(shader&&shader.isSupported)grade=new Material(shader){hideFlags=HideFlags.HideAndDontSave};
        }
        void OnRenderImage(RenderTexture source,RenderTexture destination)
        {
            var game=ArenaGame.Instance;
            if(grade&&game&&game.State==ArenaState.Ready)Graphics.Blit(source,destination,grade);
            else Graphics.Blit(source,destination);
        }
        void OnDisable(){if(grade)Destroy(grade);grade=null;}
    }
}
