using UnityEngine;

namespace NeonArena
{
    [RequireComponent(typeof(Health))]
    public sealed class HitFlash : MonoBehaviour
    {
        public Renderer[] surfaces;
        Health health;
        MaterialPropertyBlock block;
        float remaining;
        void Awake()
        {
            health = GetComponent<Health>();
            block = new MaterialPropertyBlock();
            health.Damaged += OnDamage;
        }
        void OnDestroy() { if (health) health.Damaged -= OnDamage; }
        void OnDamage(float _) { remaining = 0.09f; Apply(true); }
        void Update()
        {
            if (remaining <= 0) return;
            remaining -= Time.deltaTime;
            if (remaining <= 0) Apply(false);
        }
        void OnDisable() { remaining = 0; Apply(false); }
        void Apply(bool flash)
        {
            if (surfaces == null) return;
            foreach (var r in surfaces)
            {
                if (!r) continue;
                if (flash && CombatPresentation.FlashGain>0)
                {
                    r.GetPropertyBlock(block);
                    block.SetColor("_EmissionColor", new Color(.22f,.7f,.82f) * (.65f*CombatPresentation.FlashGain));
                    r.SetPropertyBlock(block);
                }
                else {r.GetPropertyBlock(block);block.SetColor("_EmissionColor",r.sharedMaterial&&r.sharedMaterial.HasProperty("_EmissionColor")?r.sharedMaterial.GetColor("_EmissionColor"):Color.black);r.SetPropertyBlock(block);}
            }
        }
    }
}
