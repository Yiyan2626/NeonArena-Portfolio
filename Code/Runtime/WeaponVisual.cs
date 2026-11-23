using UnityEngine;

namespace NeonArena
{
    public sealed class WeaponVisual : MonoBehaviour
    {
        public Transform piercingModule, ricochetModule, laserModule, rotor, muzzle;
        public Transform aimPivot, receiver;
        public Vector3 leftGripOffset=new Vector3(-.16f,-.05f,.57f);
        // Authored DetailKit rifle: butt back face, pistol grip and rear handguard.
        // These are mesh-space contact points, not offsets from an animated hand.
        static readonly Vector3 StockContact=new Vector3(0,-.055f,-.2675f);
        bool shouldered;
        public Vector3 ShoulderAnchor { get; private set; }
        public Vector3 StockWorld=>aimPivot?aimPivot.TransformPoint(StockContact+Vector3.back*recoil):transform.position;
        public Vector3 RightGrip=>aimPivot?aimPivot.TransformPoint((shouldered?new Vector3(-.06f,-.13f,.065f):new Vector3(-.06f,.03f,.23f))+Vector3.back*recoil):muzzle.position-muzzle.forward*.62f-muzzle.right*.06f+Vector3.up*.03f;
        public Vector3 LeftGrip=>aimPivot?aimPivot.TransformPoint((shouldered?new Vector3(-.15f,-.11f,.31f):leftGripOffset)+Vector3.back*recoil):muzzle.position-muzzle.forward*.28f-muzzle.right*.16f-Vector3.up*.05f;
        public void SeatAtShoulder(Vector3 shoulder,Vector3 target)
        {
            if(!aimPivot)return;
            if(!shouldered)
            {
                shouldered=true;recoil=0;ApplyRecoilPose();
            }
            ShoulderAnchor=shoulder;
            // Alternating rotation/translation converges the offset barrel ray while
            // preserving exact stock contact, including the receiver recoil sample.
            for(int pass=0;pass<4;pass++)
            {
                AimAt(target);
                aimPivot.position+=shoulder-StockWorld;
            }
        }
        public void AimAt(Vector3 point)
        {
            if(!aimPivot)return;
            Vector3 direction=point-aimPivot.position;
            if(direction.sqrMagnitude>.001f)aimPivot.rotation=Quaternion.LookRotation(direction,Vector3.up);
        }
        Vector3 MuzzlePosition(float z)=>aimPivot?new Vector3(0,0,z-.30f):new Vector3(.37f,.85f,z);
        public Renderer[] rankLights;
        WeaponLoadout current;
        MaterialPropertyBlock block;
        float recoil;
        Vector3 pierceHome, ricochetHome, laserHome;
        bool hasHomes;
        public void Recoil()
        {
            // A seated stock recoils through the thorax in RetargetedMotion. Sliding
            // the entire receiver here would detach the butt between Update and the
            // post-animation shoulder solve (and move the hands' targets twice).
            recoil=shouldered?0:.08f;
            // Firing may run after this component's Update. Keep rendered receiver,
            // muzzle and the two IK grip targets on the same recoil sample immediately.
            ApplyRecoilPose();
        }
        public void Apply(WeaponLoadout loadout)
        {
            if (block == null) block = new MaterialPropertyBlock();
            current = loadout;
            if (!hasHomes)
            { pierceHome=piercingModule.localPosition;ricochetHome=ricochetModule.localPosition;laserHome=laserModule.localPosition;hasHomes=true; }
            Configure(piercingModule, loadout.Piercing);
            Configure(ricochetModule, loadout.Ricochet);
            Configure(laserModule, loadout.Laser);
            if (muzzle) muzzle.localPosition = MuzzlePosition(loadout.Piercing > 0 ? 1.72f : 1.15f);
            if (rankLights == null) return;
            for (int i = 0; i < rankLights.Length; i++)
            {
                if (!rankLights[i]) continue;
                int rank = loadout.Level((AmmoUpgrade)Mathf.Min(2, i));
                Color color = WeaponLoadout.UpgradeColor((AmmoUpgrade)Mathf.Min(2, i));
                block.Clear(); block.SetColor("_Color", color);
                block.SetColor("_EmissionColor", color * (0.7f + Mathf.Min(4, rank) * 0.6f));
                rankLights[i].SetPropertyBlock(block);
            }
        }
        static void Configure(Transform module, int rank)
        {
            if (!module) return;
            module.gameObject.SetActive(rank > 0);
            module.localScale = Vector3.one * (1 + Mathf.Min(0.35f, Mathf.Max(0, rank - 1) * 0.06f));
        }
        void Update()
        {
            recoil = Mathf.MoveTowards(recoil, 0, Time.deltaTime * .7f);
            ApplyRecoilPose();
            if (rotor && current != null && current.Ricochet > 0 && ArenaGame.Instance && ArenaGame.Instance.IsPlaying)
                rotor.Rotate(Vector3.up, (60 + 15 * current.Ricochet) * Time.deltaTime, Space.Self);
        }
        void ApplyRecoilPose()
        {
            if (muzzle && current != null) muzzle.localPosition = MuzzlePosition((current.Piercing > 0 ? 1.72f : 1.15f) - recoil);
            if(receiver)receiver.localPosition=Vector3.back*recoil;
            if(hasHomes) { piercingModule.localPosition=pierceHome+Vector3.back*recoil;ricochetModule.localPosition=ricochetHome+Vector3.back*recoil*.5f;laserModule.localPosition=laserHome+Vector3.back*recoil*.35f; }
        }
    }
}
