using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace NeonArena
{
    // The bridge and its underpass are separate navigation polygons.
    public sealed class ArenaTerrain : MonoBehaviour
    {
        public GameObject[] layouts;
        public BrushPatch[] brush;
        public int LayoutIndex { get; private set; }
        static readonly string[] Names = { "入门庭院", "掩体草径", "高台回廊", "下沉据点", "双层街区", "熔核工坊", "屋顶伏击", "复合要塞" };
        public StageExpansion Expansion { get; private set; }
        public int Complexity { get; private set; } = 1;
        public string LayoutName => satelliteCity ? "白区卫星城 / " + (Complexity < 3 ? "中心广场" : Complexity < 5 ? "双层街巷" : "全域围猎") : Names[Complexity-1];
        public const float HalfWidth = 52, HalfDepth = 44;
        public bool satelliteCity;
        public Vector2 extent = new Vector2(HalfWidth, HalfDepth);
        public Vector3 entry = new Vector3(0,.08f,-2);
        public static float WorldHalfWidth => ArenaGame.Instance && ArenaGame.Instance.terrain ? ArenaGame.Instance.terrain.extent.x : HalfWidth;
        public static float WorldHalfDepth => ArenaGame.Instance && ArenaGame.Instance.terrain ? ArenaGame.Instance.terrain.extent.y : HalfDepth;
        NavMeshDataInstance navigation;
        NavMeshData data;
        NavMeshPath path;
        void Awake() => path = new NavMeshPath();
        readonly Vector3[] corners = new Vector3[64];
        public bool TryEncounterPoint(Vector3 candidate,Vector3 target,out Vector3 point,out float routeLength)
        {
            point=candidate;routeLength=0;
            if(!NavMesh.SamplePosition(candidate,out var nav,2,NavMesh.AllAreas))return false;
            if(!TryGround(nav.position+Vector3.up*.25f,out point,1))return false;
            if(Vector3.Distance(point,nav.position)>.5f)return false;
            if(Physics.CheckCapsule(point+Vector3.up*.55f,point+Vector3.up*1.45f,.5f,1<<9,QueryTriggerInteraction.Ignore))return false;
            if(!HasRoute(point,target))return false;
            int count=path.GetCornersNonAlloc(corners);
            for(int i=1;i<count;i++)routeLength+=Vector3.Distance(corners[i-1],corners[i]);
            return true;
        }
        public void Apply(int wave)
        {
            if(satelliteCity)
            {
                bool first=Complexity==1 && !navigation.valid;
                Complexity=Mathf.Clamp(wave,1,8);
                Physics.SyncTransforms();if(first || !navigation.valid)RebuildNavigation();
                ArenaGame.Instance?.Vision?.Invalidate();return;
            }
            if(!Expansion){Expansion=gameObject.AddComponent<StageExpansion>();Expansion.Initialize();}
            Complexity=Mathf.Clamp(wave,1,8);Expansion.Apply(wave);
            GetComponent<SatelliteDistrict>()?.Apply(wave);
            LayoutIndex = Mathf.Clamp(wave-1,0,layouts.Length-1);
            for (int i = 0; i < layouts.Length; i++) layouts[i].SetActive(i == LayoutIndex);
            Physics.SyncTransforms(); RebuildNavigation();
            ArenaGame.Instance?.Vision?.Invalidate();
        }
        public bool CanSee(Vector3 player, Vector3 enemy)
        {
            if (brush != null) foreach (var patch in brush) if (patch && patch.Blocks(player, enemy)) return false;
            if(Expansion!=null)foreach(var patch in Expansion.Patches)if(patch && patch.Blocks(player,enemy))return false;
            return true;
        }
        public bool InBrush(Vector3 point)
        {
            if(brush!=null)foreach(var b in brush)if(b && b.Contains(point))return true;
            if(Expansion!=null)foreach(var b in Expansion.Patches)if(b && b.Contains(point))return true;
            return false;
        }
        public void CaptureBrushBounds(List<Bounds> result)
        {
            result.Clear();
            if(brush!=null)foreach(var patch in brush)if(patch && patch.isActiveAndEnabled)result.Add(patch.GetComponent<BoxCollider>().bounds);
            if(Expansion!=null)foreach(var patch in Expansion.Patches)if(patch && patch.isActiveAndEnabled)result.Add(patch.GetComponent<BoxCollider>().bounds);
        }
        public void RebuildNavigation()
        {
            if (navigation.valid) navigation.Remove();
            if (data) Destroy(data);
            var sources = new List<NavMeshBuildSource>();
            foreach (var box in FindObjectsOfType<BoxCollider>())
            {
                if (!box.enabled || box.isTrigger || box.gameObject.layer != 9) continue;
                sources.Add(new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box,
                    transform = box.transform.localToWorldMatrix * Matrix4x4.Translate(box.center), size = box.size, area = 0 });
            }
            foreach(var mesh in FindObjectsOfType<MeshCollider>())
            {
                if(!mesh.enabled || mesh.isTrigger || mesh.gameObject.layer!=9 || !mesh.sharedMesh)continue;
                sources.Add(new NavMeshBuildSource { shape=NavMeshBuildSourceShape.Mesh, sourceObject=mesh.sharedMesh,
                    transform=mesh.transform.localToWorldMatrix, area=0 });
            }
            var settings = NavMesh.GetSettingsByIndex(0);
            settings.agentRadius = .5f; settings.agentHeight = 1.8f; settings.agentClimb = .35f; settings.agentSlope = 40;
            settings.overrideVoxelSize = true; settings.voxelSize = .15f;
            data = NavMeshBuilder.BuildNavMeshData(settings, sources, new Bounds(Vector3.up * 16, new Vector3(extent.x*2+4, 48, extent.y*2+4)), Vector3.zero, Quaternion.identity);
            if (data) navigation = NavMesh.AddNavMeshData(data);

        }
        public bool HasRoute(Vector3 from, Vector3 target)
        {
            return NavMesh.SamplePosition(from, out var a, 2, NavMesh.AllAreas) &&
                NavMesh.SamplePosition(target, out var b, 3, NavMesh.AllAreas) &&
                NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
        }
        public Vector3[] RouteCorners(Vector3 from,Vector3 target)
        { return HasRoute(from,target)?path.corners:System.Array.Empty<Vector3>(); }
        public Vector3 Steer(Vector3 from, Vector3 target)
        {
            // Recalculate from the current position: reusing corners by quantized cells
            // can point backward after rounding a wall corner and create oscillation.
            if (!HasRoute(from,target)) return Vector3.zero;
            int count = path.GetCornersNonAlloc(corners);
            for (int i = 1; i < count; i++)
            {
                Vector3 delta = corners[i] - from; delta.y = 0;
                if (delta.sqrMagnitude > .2f * .2f) return delta.normalized;
            }
            return Vector3.ProjectOnPlane(target - from, Vector3.up).normalized;
        }
        public static bool TryGround(Vector3 point, out Vector3 ground, float drop = 3)
        {
            if (Physics.Raycast(point + Vector3.up * .6f, Vector3.down, out var hit, drop + .6f, 1 << 9, QueryTriggerInteraction.Ignore) && hit.normal.y > .72f)
            { ground = hit.point + Vector3.up * .04f; return true; }
            ground = point; return false;
        }
        void OnDestroy() { if (navigation.valid) navigation.Remove(); if (data) Destroy(data); }
    }
}
