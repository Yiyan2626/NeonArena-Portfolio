using UnityEngine;

namespace NeonArena
{
    // Fixed-size pool; pickups require a clear same-floor path.
    public sealed class HealthDrops : MonoBehaviour
    {
        public const float DropChance = .25f, HealAmount = 25;
        public int Spawned { get; private set; }
        public int Collected { get; private set; }
        public int ActiveCount { get { int n = 0; foreach (var p in packs) if (p.root.activeSelf) n++; return n; } }
        sealed class Pack { public GameObject root; public Vector3 floor; public float life; }
        readonly Pack[] packs = new Pack[20];
        Material metal, green;
        void Awake()
        {
            metal = RuntimeArt.Material(new Color(.09f,.17f,.19f));
            green = RuntimeArt.Material(new Color(.15f,1,.53f), 1.8f);
            for (int i = 0; i < packs.Length; i++)
            {
                var root = new GameObject("Medical Cell"); root.transform.SetParent(transform);
                var source=Resources.Load<GameObject>("Frontier/MedicalCell");
                if(ArenaGame.Instance&&ArenaGame.Instance.Frontier&&source)Instantiate(source,root.transform,false);
                else {
                RuntimeArt.Box(root.transform, "Case", Vector3.zero, new Vector3(.55f,.38f,.38f), metal);
                RuntimeArt.Box(root.transform, "Cross H", new Vector3(0,0,-.205f), new Vector3(.38f,.09f,.04f), green);
                RuntimeArt.Box(root.transform, "Cross V", new Vector3(0,0,-.205f), new Vector3(.09f,.28f,.04f), green);
                RuntimeArt.Box(root.transform, "Cross Back H", new Vector3(0,0,.205f), new Vector3(.38f,.09f,.04f), green);
                RuntimeArt.Box(root.transform, "Cross Back V", new Vector3(0,0,.205f), new Vector3(.09f,.28f,.04f), green);
                RuntimeArt.Box(root.transform, "Cap", new Vector3(0,.21f,0), new Vector3(.3f,.05f,.26f), green);
                }
                root.SetActive(false); packs[i] = new Pack { root = root };
            }
        }
        public bool TryDrop(Vector3 position, float roll)
        {
            if (roll >= DropChance || roll < 0 || !ArenaTerrain.TryGround(position, out var floor, 8)) return false;
            foreach (var p in packs)
            {
                if (p.root.activeSelf) continue;
                p.floor = floor; p.life = 35; p.root.transform.position = floor + Vector3.up * .55f;
                p.root.SetActive(true); Spawned++; return true;
            }
            return false;
        }
        void Update() => Tick(Time.deltaTime);
        public void Tick(float dt)
        {
            var game = ArenaGame.Instance;
            if (!game || !game.IsPlaying || game.Cinema.FinaleActive || dt <= 0) return;
            var player = game.player;
            foreach (var p in packs)
            {
                if (!p.root.activeSelf) continue;
                p.life -= dt;
                if (p.life <= 0) { p.root.SetActive(false); continue; }
                p.root.transform.Rotate(0, 70 * dt, 0);
                p.root.transform.position = p.floor + Vector3.up * (.55f + Mathf.Sin(p.life * 4) * .09f);
                Vector3 delta = player.transform.position - p.floor;
                if (Mathf.Abs(delta.y) > .9f || delta.sqrMagnitude > 1.35f * 1.35f || player.Health.IsDead || player.Health.Current >= player.Health.maxHealth) continue;
                if (Physics.Linecast(p.floor + Vector3.up * .6f, player.transform.position + Vector3.up * .6f, 1 << 9, QueryTriggerInteraction.Ignore)) continue;
                player.Health.Heal(HealAmount); Collected++; p.root.SetActive(false);
                ImpactPool.Instance?.Emit(player.transform.position + Vector3.up, new Color(.2f,1,.55f), 22);
                ArenaAudio.Instance?.Play(SoundCue.Heal);
            }
        }
        public void Clear(bool resetCounters = false)
        { foreach (var p in packs) p.root.SetActive(false); if (resetCounters) Spawned = Collected = 0; }
        void OnDestroy() { Destroy(metal); Destroy(green); }
    }
    public static class RuntimeArt
    {
        public static Material Material(Color color, float emission = 0)
        {
            var m = new Material(Shader.Find("NeonArena/VisionLit")); m.color = color;
            if (emission > 0) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", color * emission); }
            return m;
        }
        public static Transform Box(Transform root, string name, Vector3 position, Vector3 scale, Material material)
        {
            var o = GameObject.CreatePrimitive(PrimitiveType.Cube); o.name = name;
            o.transform.SetParent(root, false); o.transform.localPosition = position; o.transform.localScale = scale;
            var collider = o.GetComponent<Collider>(); collider.enabled = false; Object.Destroy(collider);
            o.GetComponent<Renderer>().sharedMaterial = material; return o.transform;
        }
        public static LineRenderer Line(Transform parent, string name, Material material, float width)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>(); line.transform.SetParent(parent,false);
            line.sharedMaterial = material; line.widthMultiplier = width; line.numCapVertices = 4;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.enabled = false; return line;
        }
    }
}
