using UnityEngine;

namespace NeonArena
{
    // A single time-scale owner. Camera shots never overwrite the player's saved orbit.
    public sealed class CombatCinema : MonoBehaviour
    {
        public bool FinaleActive => finaleLeft > 0;
        public bool ShotActive => shotLeft > 0;
        public bool IntroActive=>introLeft>0;
        public float Progress => ShotActive ? 1 - shotLeft / shotLength : 0;
        public int Finales { get; private set; }
        public float Letterbox => ShotActive ? (FinaleActive ? 1 : .4f) : 0;
        float finaleLeft, shotLeft, shotLength;
        float introLeft;
        Vector3 point;
        float baseYaw;
        int variant;
        Material glow;
        LineRenderer echo;
        readonly Transform[] fragments = new Transform[8];
        public void Initialize()
        {
            glow = RuntimeArt.Material(new Color(1,.64f,.14f), 2);
            echo = RuntimeArt.Line(transform,"Finishing Shockwave",glow,.075f);
            echo.loop = true; echo.positionCount = 64;
            for(int i=0;i<fragments.Length;i++)
            {
                fragments[i]=RuntimeArt.Box(transform,"Defeat Fragment",Vector3.zero,new Vector3(.16f,.1f,.3f),glow);
                fragments[i].gameObject.SetActive(false);
            }
        }
        public void Strike(Vector3 targetPoint, int style)
        {
            if (FinaleActive||IntroActive) return;
            Begin(targetPoint, style, .22f);
        }
        void Begin(Vector3 targetPoint, int style, float duration)
        {
            point = targetPoint + Vector3.up * .85f; variant = style;
            shotLength = shotLeft = duration;
            baseYaw = ArenaGame.Instance.player.viewCamera.GetComponent<ArenaCamera>().yaw;
        }
        public void OverheatEntry(Vector3 swordPoint){introLeft=1.15f;Begin(swordPoint-Vector3.up*.85f,3,1.15f);ApplyClock();}
        public void Finish(Vector3 targetPoint)
        {
            if (FinaleActive) return;
            Finales++; finaleLeft = 1.05f; Begin(targetPoint, Finales % 3, finaleLeft);
            ArenaAudio.Instance?.Play(SoundCue.Finisher);
            ApplyClock();
        }
        // dt is game time; presentation duration is real time excluding pause.
        public void Tick(float dt)
        {
            var game = ArenaGame.Instance;
            if (!game || !game.IsPlaying || dt <= 0) { ApplyClock(); return; }
            float realDt = dt / Mathf.Max(.01f, Time.timeScale);
            finaleLeft = Mathf.Max(0, finaleLeft - realDt);
            introLeft=Mathf.Max(0,introLeft-realDt);
            shotLeft = Mathf.Max(0, shotLeft - realDt);
            if (echo)
            {
                echo.enabled = FinaleActive;
                if (FinaleActive)
                {
                    float progress = 1 - shotLeft / shotLength;
                    echo.widthMultiplier = .12f * (1 - progress);
                    for (int i = 0; i < 64; i++)
                    {
                        float a = i * Mathf.PI * 2 / 64;
                        echo.SetPosition(i, point + new Vector3(Mathf.Cos(a),.2f,Mathf.Sin(a)) * (1 + progress * 4));
                    }
                }
            }
            for(int i=0;i<fragments.Length;i++)
            {
                fragments[i].gameObject.SetActive(FinaleActive);
                if(!FinaleActive)continue;
                float a=i*Mathf.PI/4, t=Progress;
                fragments[i].position=point+new Vector3(Mathf.Cos(a)*t*2,Mathf.Sin(t*Mathf.PI)*1.1f,Mathf.Sin(a)*t*2);
                fragments[i].rotation=Quaternion.Euler(t*200,i*45,t*150);
            }
            ApplyClock();
        }
        public void ApplyClock()
        {
            var game = ArenaGame.Instance;
            Time.timeScale = game && game.State == ArenaState.Paused ? 0 : FinaleActive ? .2f : IntroActive ? .16f : 1;
        }
        public void Render(Camera view)
        {
            if (!ShotActive) return;
            var player = ArenaGame.Instance.player;
            if(IntroActive)
            {
                Vector3 focus=player.Overheat.BladeFocus;float introEnvelope=Mathf.Sin(Mathf.Clamp01(Progress)*Mathf.PI);
                view.transform.LookAt(Vector3.Lerp(view.transform.position+view.transform.forward*5,focus,introEnvelope));
                view.fieldOfView=Mathf.Lerp(view.fieldOfView,24,introEnvelope);return;
            }
            // A distant gun kill frames the defeated enemy, not empty space between actors.
            Vector3 middle = FinaleActive ? point : Vector3.Lerp(player.transform.position + Vector3.up, point, .55f);
            float t = Mathf.Clamp01(1 - shotLeft / shotLength);
            float envelope = Mathf.SmoothStep(0,1,Mathf.Min(t * 8, (1 - t) * 5)) * (FinaleActive?1:.3f);
            float yaw = baseYaw + (variant == 1 ? -1 : 1) * Mathf.Lerp(8,24,t);
            Quaternion rotation = Quaternion.Euler(variant == 2 ? 52 : 32,yaw,0);
            float distance = FinaleActive ? 8.5f : 11;
            Vector3 back = rotation * Vector3.back;
            if (Physics.CheckSphere(middle,.3f,1<<9,QueryTriggerInteraction.Ignore)) return;
            if (Physics.SphereCast(middle,.3f,back,out var hit,distance,1<<9,QueryTriggerInteraction.Ignore)) distance = hit.distance - .2f;
            if (distance < 4) return;
            Vector3 destination = middle + back * distance;
            Vector3 blended = Vector3.Lerp(view.transform.position,destination,envelope);
            // The blend itself must not cross a balcony wall or bridge deck.
            if (Physics.Linecast(middle,blended,1<<9,QueryTriggerInteraction.Ignore)) return;
            view.transform.position = blended;
            view.transform.rotation = Quaternion.Slerp(view.transform.rotation,rotation,envelope);
            view.fieldOfView = Mathf.Lerp(view.fieldOfView,FinaleActive ? 43 : 48,envelope);
        }
        public void ResetCinema(bool counters = false)
        { finaleLeft = shotLeft = introLeft = 0; if (echo) echo.enabled = false; foreach(var f in fragments)if(f)f.gameObject.SetActive(false); if (counters) Finales = 0; Time.timeScale = 1; }
        void OnDestroy() { Time.timeScale = 1; if (glow) Destroy(glow); }
    }
}
