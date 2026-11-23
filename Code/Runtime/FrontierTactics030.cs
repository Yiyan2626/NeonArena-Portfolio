using UnityEngine;

namespace NeonArena
{
    // A firing aperture, not a route gate. Its permanent sill/header keep the same
    // walking topology in either state; only combat visibility changes.
    public sealed class FrontierTactics030 : MonoBehaviour
    {
        public static readonly Vector3 ShutterCenter = new Vector3(2, 1.36f, -98);
        public static readonly Vector3 ControlPosition = new Vector3(-.7f, .04f, -101.15f);
        public static readonly Vector3 ControlStand = new Vector3(-.7f, .12f, -102.5f);
        public static readonly Vector3 FiringStand = new Vector3(2, .12f, -102);
        public static readonly Vector3 NorthTarget = new Vector3(2, .12f, -95);
        public static readonly Vector3 NorthRearTarget = new Vector3(2, .12f, -93.8f);
        public const float WindowAimHeight = 1.35f;
        public const float InteractionRange = 3;
        public const float Travel = 1.22f;
        public const float Duration = .85f;
        public Transform shutter, control;
        public Vector3 closedPosition;
        public GameObject replacedPanel;
        public TextMesh status;
        public TextMesh instruction;
        public Light statusLight;
        public int encounterIndex;
        public bool IsOpen => openness >= 1;
        public bool IsMoving => Mathf.Abs(openness - target) > .0001f;
        public float Openness => openness;
        public int Interactions { get; private set; }
        float openness, target;

        void Awake()
        {
            if (status)
            {
                status.font = ArenaTheme.Font;
                status.GetComponent<Renderer>().sharedMaterial = status.font.material;
            }
            if (instruction)
            {
                instruction.font = ArenaTheme.Font;
                instruction.GetComponent<Renderer>().sharedMaterial = instruction.font.material;
            }
            ResetTactics();
        }
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F)) TryInteract();
            Simulate(Time.deltaTime);
        }
        public string BlockReason()
        {
            var g = ArenaGame.Instance;
            if (!g || !g.IsPlaying || !g.player || g.player.Health.IsDead ||
                g.Cinema.FinaleActive || g.Frontier && g.Frontier.TerminalOpen)
                return "当前无法操作";
            if (!control || !shutter) return "设备离线";
            if (IsMoving) return "百叶窗移动中";
            if (Vector3.Distance(g.player.transform.position, control.position) > InteractionRange)
                return "靠近控制台 · F 操作";
            Vector3 from = g.player.transform.position + Vector3.up * .85f;
            Vector3 to = control.position + Vector3.up * .9f;
            foreach (var hit in Physics.RaycastAll(from, (to - from).normalized, Vector3.Distance(from, to), 1 << 9, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(control)) return "控制台被遮挡 · 绕至正面";
            // A loadout terminal owns F whenever it could also accept this press.
            if (g.Frontier)
                for (int i = 0; i < g.Frontier.terminals.Length; i++)
                    if (g.Frontier.TerminalBlockReason(i).Length == 0) return "安全终端优先";
            return "";
        }
        public bool TryInteract()
        {
            if (BlockReason().Length != 0) return false;
            target = IsOpen ? 0 : 1;
            Interactions++;
            ArenaAudio.Instance?.Play(SoundCue.Select, control.position, .75f);
            RefreshStatus();
            return true;
        }
        public void Simulate(float dt)
        {
            var g = ArenaGame.Instance;
            if (!g || !g.IsPlaying || g.Cinema.FinaleActive || dt <= 0 || float.IsNaN(dt) || float.IsInfinity(dt) || !IsMoving) return;
            openness = Mathf.MoveTowards(openness, target, dt / Duration);
            // The actual colliders move with the rendered assembly, including during travel.
            shutter.position = closedPosition + Vector3.up * (Mathf.SmoothStep(0, 1, openness) * Travel);
            Physics.SyncTransforms();
            g.Vision?.Invalidate();
            if (!IsMoving)
            {
                ArenaAudio.Instance?.Play(SoundCue.WallHit, shutter.position, .3f);
                RefreshStatus();
            }
        }
        public void ResetTactics()
        {
            openness = target = 0;
            Interactions = 0;
            if (shutter) shutter.position = closedPosition;
            Physics.SyncTransforms();
            ArenaGame.Instance?.Vision?.Invalidate();
            RefreshStatus();
        }
        void LateUpdate()
        {
            if (!status) return;
            var g = ArenaGame.Instance;
            bool near = g && g.IsPlaying && !g.Cinema.FinaleActive &&
                Vector3.Distance(g.player.transform.position, control.position) < 9 && CameraSeesControl(g);
            status.gameObject.SetActive(near);
            if (instruction) instruction.gameObject.SetActive(false);
            if (!near) return;
            status.transform.rotation = Quaternion.LookRotation(status.transform.position - g.player.viewCamera.transform.position);
            RefreshStatus();
            if (instruction && Vector3.Distance(g.player.transform.position, control.position) < 3)
            {
                var screen = g.player.viewCamera.WorldToViewportPoint(instruction.transform.position);
                // The explanation belongs beside the console, never across the aiming area.
                bool offAim = screen.z > 0 && (Mathf.Abs(screen.x - .5f) > .16f || Mathf.Abs(screen.y - .5f) > .13f);
                instruction.gameObject.SetActive(offAim);
                instruction.transform.rotation = status.transform.rotation;
                instruction.text = IsOpen ? "直射已通 · 留意还击" : "穿透可隔板 · 也可绕侧";
                instruction.color = new Color(.76f, .83f, .81f);
            }
        }
        bool CameraSeesControl(ArenaGame g)
        {
            Vector3 from = g.player.viewCamera.transform.position;
            Vector3 to = control.position + Vector3.up * 1.25f;
            foreach (var hit in Physics.RaycastAll(from, (to - from).normalized, Vector3.Distance(from, to), 1 << 9, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(control)) return false;
            return true;
        }
        void RefreshStatus()
        {
            Color color = IsMoving ? new Color(1, .68f, .22f) : IsOpen ? new Color(1, .59f, .2f) : new Color(.24f, .94f, .87f);
            if (statusLight) statusLight.color = color;
            if (!status) return;
            string state = IsMoving ? "百叶窗 · 移动中" : IsOpen ? "百叶窗 · 开启" : "百叶窗 · 关闭";
            string action = BlockReason();
            status.text = state + (action.Length == 0 ? IsOpen ? "\n[ F ] 关窗" : "\n[ F ] 开窗" : "");
            status.color = color;
        }
    }
}
