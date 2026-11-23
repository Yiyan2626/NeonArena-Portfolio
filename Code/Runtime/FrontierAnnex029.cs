using UnityEngine;

namespace NeonArena
{
    // Authored scene contract, shared by route validation and capture tools.
    // The annex uses the existing campaign encounter, terminal and restart lifecycle.
    public sealed class FrontierAnnex029 : MonoBehaviour
    {
        public const string EncounterName="维修回廊 · 直进 / 绕侧";
        // The captured building's south wall ends at x=-9.46, z=-88.72.
        // Enter diagonally around that actual facade end instead of crossing its one-sided face.
        public static readonly Vector3 Entrance=new Vector3(-8.4f,.12f,-88);
        public static readonly Vector3 Decision=new Vector3(-10,.12f,-95);
        public static readonly Vector3 Flank=new Vector3(-10,.12f,-102);
        public static readonly Vector3 FlankEnd=new Vector3(14,.12f,-102);
        public static readonly Vector3 Convergence=new Vector3(14,.12f,-95);
        public static readonly Vector3 Exit=new Vector3(14,.12f,-88);
        public static Vector3[] DirectRoute=>new[]{Entrance,Decision,Convergence,Exit};
        public static Vector3[] CoveredRoute=>new[]{Entrance,Decision,Flank,FlankEnd,Convergence,Exit};
        public int originalEnemyBudget;
        public int encounterIndex;
    }
}
