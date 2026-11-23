using UnityEngine;

namespace NeonArena
{
    // Releasing a partially used burst starts its full cooldown; tapping cannot refill it for free.
    public sealed class BurstCycle
    {
        public float Duration { get; }
        public float Recovery { get; }
        public float Remaining { get; private set; }
        public float CooldownLeft { get; private set; }
        public BurstCycle(float duration, float recovery) { Duration = duration; Recovery = recovery; Reset(); }
        public void Reset() { Remaining = Duration; CooldownLeft = 0; }
        public float Advance(bool held, float dt)
        {
            if (dt <= 0 || float.IsNaN(dt) || float.IsInfinity(dt)) return 0;
            if (CooldownLeft > 0)
            {
                CooldownLeft = Mathf.Max(0, CooldownLeft - dt);
                if (CooldownLeft <= 0) Remaining = Duration;
                return 0;
            }
            if (!held)
            {
                if (Remaining < Duration) CooldownLeft = Recovery;
                return 0;
            }
            float active = Mathf.Min(dt, Remaining);
            Remaining = Mathf.Max(0, Remaining - active);
            if (Remaining <= 0) CooldownLeft = Recovery;
            return active;
        }
    }
}
