using UnityEngine;
namespace NeonArena
{
    public static class RicochetSolver
    {
        // Acceptance/debug helper: mirror a target across an actual collider plane, then verify the complete bank shot.
        public static bool TryBank(Vector3 origin,EnemyController target,out Vector3 aim)
        {
            aim=Vector3.zero;if(!target||!target.IsAlive)return false;Vector3 body=target.transform.position+Vector3.up*(target.Elite.IsElite?1.75f:.75f);float best=70;
            foreach(var wall in Object.FindObjectsOfType<Collider>())
            {
                // Bounds is only a broad phase. Collider.Raycast below tests the actual surface;
                // ClosestPoint is unsupported on the city's non-convex mesh colliders.
                if(!wall.enabled||wall.isTrigger||wall.gameObject.layer!=9||wall.bounds.size.y<1.2f||Vector3.Distance(wall.bounds.ClosestPoint(origin),origin)>16)continue;
                for(int source=0;source<2;source++)
                {
                    Vector3 from=source==0?origin:body;Vector3 towards=wall.bounds.center-from;
                    if(!wall.Raycast(new Ray(from,towards.normalized),out var plane,30))continue;
                    float side=Vector3.Dot(body-plane.point,plane.normal);Vector3 mirror=body-2*side*plane.normal;
                    Vector3 direction=(mirror-origin).normalized;
                    if(!Physics.SphereCast(origin,.09f,direction,out var first,40,(1<<8)|(1<<9),QueryTriggerInteraction.Ignore)||first.collider!=wall)continue;
                    Vector3 center=origin+direction*first.distance;
                    Vector3 reflected=Vector3.Reflect(direction,first.normal).normalized;
                    if(!Physics.SphereCast(center+first.normal*.025f,.09f,reflected,out var second,40,(1<<8)|(1<<9),QueryTriggerInteraction.Ignore))continue;
                    if(second.collider.GetComponentInParent<EnemyController>()!=target||second.collider.GetComponent<EnemyShield>())continue;
                    float length=first.distance+second.distance;if(length>=best)continue;
                    best=length;aim=origin+direction*(first.distance+1);
                }
            }
            return best<70;
        }
    }
}
