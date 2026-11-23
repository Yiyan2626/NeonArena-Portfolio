using UnityEngine;

namespace NeonArena
{
    public sealed class ProjectilePool : MonoBehaviour
    {
        public Projectile prefab;
        [Range(8, 128)] public int capacity = 40;
        Projectile[] items;
        int cursor;
        public int ActiveCount
        {
            get { int count = 0; foreach (var p in items) if (p.InFlight) count++; return count; }
        }

        void Awake()
        {
            items = new Projectile[capacity];
            for (int i = 0; i < capacity; i++)
            {
                items[i] = Instantiate(prefab, transform);
                items[i].gameObject.SetActive(false);
            }
        }

        public bool TryLaunch(Vector3 position, Vector3 heading, float speed, float damage, ShotProfile? profile = null)
        {
            if (heading.sqrMagnitude < 0.001f) return false;
            for (int n = 0; n < items.Length; n++)
            {
                int i = (cursor + n) % items.Length;
                if (items[i].InFlight) continue;
                items[i].Launch(position, heading, speed, damage, profile);
                cursor = (i + 1) % items.Length;
                return true;
            }
            return false;
        }

        public void Clear() { foreach (var p in items) p.Retire(); cursor = 0; }
    }
}
