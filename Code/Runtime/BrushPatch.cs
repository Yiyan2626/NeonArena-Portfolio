using UnityEngine;

namespace NeonArena
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class BrushPatch : MonoBehaviour
    {
        BoxCollider volume;
        void Awake() => volume = GetComponent<BoxCollider>();
        public bool Contains(Vector3 point) { if(!isActiveAndEnabled)return false;if(!volume)volume=GetComponent<BoxCollider>();return volume.bounds.Contains(point+Vector3.up*.65f); }
        public bool Blocks(Vector3 from, Vector3 to)
        {
            if (!isActiveAndEnabled) return false;
            if (!volume) volume = GetComponent<BoxCollider>();
            return Blocks(volume.bounds,from,to);
        }
        public static bool Blocks(Bounds bounds,Vector3 from,Vector3 to)
        {
            from += Vector3.up * .65f; to += Vector3.up * .65f;
            Vector3 delta = to - from;
            if (delta.magnitude < 2.2f) return false;
            if (bounds.Contains(from) && bounds.Contains(to)) return false;
            if (bounds.Contains(to)) return true;
            if (bounds.Contains(from)) return delta.magnitude > 7;
            return bounds.IntersectRay(new Ray(from, delta.normalized), out float distance) && distance < delta.magnitude;
        }
    }
}
