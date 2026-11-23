using UnityEngine;
namespace NeonArena
{
    // Serialized baseline makes repeatable image/performance comparisons non-destructive.
    public sealed class RiftEnvironment019 : MonoBehaviour
    {
        public GameObject originalApron, pilotRoot, fullRoot;
        public Transform GeometryRoot;
        public Transform[] ruins;
        public Vector3[] originalPositions, pilotPositions, fullPositions;
        public Renderer[] originalEdgeRenderers;
        public bool[] originalRendererEnabled, hideForPilot;
        public int mode=1;
        public void SetMode(int value)
        {
            mode=Mathf.Clamp(value,0,2);
            if(originalApron)originalApron.SetActive(mode==0);
            if(pilotRoot)pilotRoot.SetActive(mode==1);
            if(fullRoot)fullRoot.SetActive(mode==2);
            if(originalEdgeRenderers!=null)for(int i=0;i<originalEdgeRenderers.Length;i++)if(originalEdgeRenderers[i])originalEdgeRenderers[i].enabled=originalRendererEnabled[i] && !(mode==2 || (mode==1 && hideForPilot[i]));
            for(int i=0;i<ruins.Length;i++)if(ruins[i])ruins[i].position=mode==0?originalPositions[i]:mode==1?pilotPositions[i]:fullPositions[i];
        }
    }
}
