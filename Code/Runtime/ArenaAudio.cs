using UnityEngine;

namespace NeonArena
{
    public enum SoundCue { Pulse, Pierce, RicochetShot, LaserStart, LaserLoop, LaserEnd, FleshHit, ShieldHit, ShieldPierce, ShieldReflect, ShieldBreak, LaserBlock, WallBounce, WallHit, ChargeWarn, ChargeRush, ChargeImpact, Hurt, Kill, Dash, Clear, Select, Hover, Lock, Heal, Overheat, Blink, SlashA, SlashB, SlashC, BladeHit, Finisher, Jump, Land, EliteHammer, EliteRam, EliteRing, EliteImpact, JetIgnite, JetLoop, JetEnd }

    // Bounded voices, category rate limits and player-relative stereo keep dense combat readable.
    public sealed class ArenaAudio : MonoBehaviour
    {
        public static ArenaAudio Instance { get; private set; }
        readonly AudioSource[] voices = new AudioSource[20];
        readonly AudioClip[] clips = new AudioClip[System.Enum.GetValues(typeof(SoundCue)).Length];
        readonly float[] nextAllowed = new float[System.Enum.GetValues(typeof(SoundCue)).Length];
        readonly int[] counts = new int[System.Enum.GetValues(typeof(SoundCue)).Length];
        AudioSource laser;
        AudioSource jet;
        int cursor,shotCursor;
        bool laserRequested, wasPlaying;
        float duckUntil;
        public bool Muted { get; private set; }
        public bool LaserAudible => laserRequested;
        public SoundCue LastCue { get; private set; }
        public int Count(SoundCue cue) => counts[(int)cue];
        public AudioClip Clip(SoundCue cue) => clips[(int)cue];
        void Awake()
        {
            Instance = this;
            for (int i = 0; i < clips.Length; i++) clips[i] = Resources.Load<AudioClip>("Audio/" + (SoundCue)i);
            for (int i = 0; i < voices.Length; i++) { voices[i] = gameObject.AddComponent<AudioSource>(); voices[i].playOnAwake = false; voices[i].spatialBlend = 0; }
            laser = gameObject.AddComponent<AudioSource>(); laser.playOnAwake = false; laser.loop = true; laser.clip = Clip(SoundCue.LaserLoop); laser.volume = .16f;
            jet=gameObject.AddComponent<AudioSource>();jet.playOnAwake=false;jet.loop=true;jet.clip=Clip(SoundCue.JetLoop);jet.volume=.10f;
        }
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F3)) SetMuted(!Muted);
            bool playing = ArenaGame.Instance && ArenaGame.Instance.IsPlaying;
            if (!playing && wasPlaying)
            {
                foreach (var voice in voices)
                    if (voice.clip != Clip(SoundCue.Clear) && voice.clip != Clip(SoundCue.Select) && voice.clip != Clip(SoundCue.Hover)) voice.Stop();
                SetLaser(false, false);
            }
            laser.volume = Time.unscaledTime < duckUntil ? .055f : .16f;
            bool thrust=playing&&ArenaGame.Instance.player.Flight.Thrusting;
            if(thrust&&!jet.isPlaying){Play(SoundCue.JetIgnite);jet.Play();counts[(int)SoundCue.JetLoop]++;}
            else if(!thrust&&jet.isPlaying){jet.Stop();if(playing)Play(SoundCue.JetEnd);}
            wasPlaying = playing;
        }
        public void SetMuted(bool muted)
        { Muted = muted; foreach (var voice in voices) voice.mute = muted; laser.mute = muted;if(jet)jet.mute=muted; }
        public bool Play(SoundCue cue, Vector3? position = null, float gain = 1)
        {
            int index = (int)cue;
            if (!clips[index] || Time.unscaledTime < nextAllowed[index]) return false;
            float interval = cue == SoundCue.LaserBlock ? .22f : cue == SoundCue.ChargeWarn ? .18f : cue == SoundCue.Hover ? .075f : .035f;
            nextAllowed[index] = Time.unscaledTime + interval;
            LastCue = cue; counts[index]++;
            bool weapon=cue==SoundCue.Pulse||cue==SoundCue.Pierce||cue==SoundCue.RicochetShot||cue==SoundCue.LaserStart;
            // Reserve four voices for the weapon: dense hit effects cannot cut its attack short.
            var voice = voices[weapon?shotCursor++%4:4+cursor++%(voices.Length-4)]; voice.Stop(); voice.clip = clips[index];
            voice.pitch = cue == SoundCue.ChargeWarn || cue == SoundCue.Select || cue == SoundCue.Clear ? 1 : weapon?Random.Range(.985f,1.015f):Random.Range(.97f, 1.03f);
            float volume = weapon ? (cue==SoundCue.LaserStart?.46f:cue==SoundCue.Pierce?.42f:cue==SoundCue.RicochetShot?.33f:.38f) : .4f;
            float pan = 0;
            var game = ArenaGame.Instance;
            if (position.HasValue && game && game.player)
            {
                Vector3 delta = position.Value - game.player.transform.position;
                volume *= Mathf.Lerp(1, .25f, Mathf.Clamp01(delta.magnitude / 24));
                pan = Mathf.Clamp(Vector3.Dot(delta, game.player.viewCamera.transform.right) / 12, -.8f, .8f);
            }
            if (cue == SoundCue.Hurt || cue == SoundCue.ChargeImpact || cue == SoundCue.ShieldBreak) duckUntil = Time.unscaledTime + .16f;
            voice.volume = volume * gain; voice.panStereo = pan; voice.Play(); return true;
        }
        public void PlayShot(bool pierce = false, bool ricochet = false)
        {
            Play(pierce ? SoundCue.Pierce : ricochet ? SoundCue.RicochetShot : SoundCue.Pulse);
            if (pierce && ricochet) Play(SoundCue.RicochetShot, null, .45f);
        }
        public void SetLaser(bool active, bool releaseSound = true)
        {
            if (active == laserRequested) return;
            laserRequested = active;
            if (active) { Play(SoundCue.LaserStart); counts[(int)SoundCue.LaserLoop]++; laser.Play(); }
            else { laser.Stop(); if (releaseSound) Play(SoundCue.LaserEnd, null, .5f); }
        }
        public void PlayHurt() => Play(SoundCue.Hurt);
        public void PlayKill() => Play(SoundCue.Kill, null, 1.0f);
        public void PlayDash() => Play(SoundCue.Dash);
        public void PlayClear() => Play(SoundCue.Clear);
        public void ResetSession()
        { foreach (var voice in voices) voice.Stop(); SetLaser(false, false);if(jet)jet.Stop(); for (int i = 0; i < nextAllowed.Length; i++) nextAllowed[i] = 0; }
        void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
