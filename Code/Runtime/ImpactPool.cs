using UnityEngine;

namespace NeonArena
{
    public sealed class ImpactPool : MonoBehaviour
    {
        public static ImpactPool Instance { get; private set; }
        public ParticleSystem particles;
        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }
        public void Emit(Vector3 point, Color color, int count)
        {
            if(FrontierCombatFx.Instance){FrontierCombatFx.Instance.Hit(point,color,count);return;}
            for (int i = 0; i < count; i++)
            {
                Vector3 velocity = Random.onUnitSphere * Random.Range(1.5f, 5f);
                velocity.y = Mathf.Abs(velocity.y) + 0.4f;
                var p = new ParticleSystem.EmitParams
                {
                    position = point, velocity = velocity, startColor = color,
                    startSize = Random.Range(0.045f, 0.13f), startLifetime = Random.Range(0.15f, 0.4f)
                };
                particles.Emit(p, 1);
            }
        }
        public void Clear() { if(particles)particles.Clear(); CombatVfx027.Instance?.Clear(); FrontierCombatFx.Instance?.Clear(); }
    }
}
