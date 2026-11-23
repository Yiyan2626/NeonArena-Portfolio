using System;
using UnityEngine;

namespace NeonArena
{
    // One authority for damage, invulnerability and death. Pool reuse resets all state.
    public sealed class Health : MonoBehaviour
    {
        [Min(1)] public float maxHealth = 100;
        [Min(0)] public float invulnerabilitySeconds;
        public float Current { get; private set; }
        public bool IsDead => Current <= 0;
        public bool DamageBlocked { get; set; }
        public event Action<float> Damaged;
        public event Action Died;
        float nextDamageAt;
        PlayerOverheat overheat;

        void Awake() => ResetHealth();
        public void ResetHealth()
        {
            Current = maxHealth;
            DamageBlocked = false;
            nextDamageAt = float.NegativeInfinity;
        }

        public bool TakeDamage(float amount)
        {
            if (!overheat) overheat = GetComponent<PlayerOverheat>();
            if (IsDead || DamageBlocked || (overheat && overheat.Active) || amount <= 0 || float.IsNaN(amount) || float.IsInfinity(amount)
                || Time.time < nextDamageAt) return false;
            var frontierBoss=GetComponent<FrontierBoss>();if(frontierBoss&&frontierBoss.enabled){amount=frontierBoss.LimitDamage(amount);if(amount<=0)return false;}
            Current = Mathf.Max(0, Current - amount);
            nextDamageAt = Time.time + invulnerabilitySeconds;
            Damaged?.Invoke(amount);
            if (IsDead) Died?.Invoke();
            return true;
        }
        public void Heal(float amount)
        {
            if (!IsDead && amount > 0 && !float.IsNaN(amount) && !float.IsInfinity(amount))
                Current = Mathf.Min(maxHealth, Current + amount);
        }
    }
}
