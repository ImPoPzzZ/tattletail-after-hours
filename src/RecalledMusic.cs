using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// Recalled's music and sounds, all written in code (no audio files, nothing copyrighted), in toy instruments:
    /// a music box, ticking like a wind-up mechanism, a low drone.
    ///
    /// Each zone has one loop in three layers that play in step and fade with the danger (RcEnemies.Danger):
    ///   exploring: the drone and a sparse music box;
    ///   noticed:   a heartbeat, louder ticking, a high dissonance (the music box drops back);
    ///   fighting:  drums, a bass line, a fast arpeggio.
    /// The park has its own warm lullaby. Mama's fight plays that lullaby's tune in a minor key.
    /// Layers share a tempo and a key, so any mix of them fits. A boss speeds all three up together.
    /// </summary>
    public class RcMusic : MonoBehaviour
    {
        public const int J_STEP = 0, J_PICKUP = 1, J_QUEST = 2, J_ZIP = 3, J_LEVEL = 4, J_RARE = 5, J_SWING = 6, J_SHOT = 7, J_HIT = 8,
            J_SHOCK = 9, J_HEAL = 10, J_HURT = 11, J_DEATH = 12, J_BOSS = 13, J_BREAK = 14, J_POP = 15, J_SLAM = 16, J_COUNT = 17;

        private const int SR = 22050;
        private const float TAU = Mathf.PI * 2f;

        private AudioSource _explore, _tense, _fight, _home, _sfx, _voice;
        private readonly Dictionary<int, AudioClip[]> _zoneClips = new Dictionary<int, AudioClip[]>();
        private readonly AudioClip[] _jingles = new AudioClip[J_COUNT];
        private readonly float[] _jingleVol = new float[J_COUNT];
        private AudioClip[] _chatter;
        private int _playing = -99, _want = 0;
        private bool _park, _building;
        // The recorded tracks (RcTracks): loaded when the place needs them, dropped when it moves on. A missing file leaves that part to the synthesized music.
        private AudioSource _bgFile, _cmbFile;
        private readonly AudioClip[] _rec = new AudioClip[RcTracks.COUNT];
        private readonly bool[] _recLoading = new bool[RcTracks.COUNT], _recFailed = new bool[RcTracks.COUNT];

        private static readonly float[] Root = { 110f, 98f, 92.5f, 82.41f, 73.42f, 123.47f, 110f, 103.8f, 92.5f, 82.41f };  // darker as you go (the town's five, then Egypt's)
        private static readonly float[] Bpm = { 76f, 80f, 72f, 84f, 90f, 82f, 78f, 86f, 92f, 96f };
        private static readonly int[] Minor = { 0, 2, 3, 5, 7, 8, 10 }, Phrygian = { 0, 1, 3, 5, 7, 8, 10 }, Major = { 0, 2, 4, 5, 7, 9, 11 }, Hijaz = { 0, 1, 4, 5, 7, 8, 10 };
        // The treehouse lullaby, a note per beat (scale degrees; -1 holds).
        private static readonly int[] Lullaby = { 4, -1, 2, -1, 3, 2, 1, -1, 0, -1, 1, 2, 1, -1, -1, -1, 4, -1, 2, -1, 3, 2, 1, 0, 1, -1, 0, -1, 0, -1, -1, -1 };

        private void Awake()
        {
            _explore = Source(); _tense = Source(); _fight = Source(); _home = Source();
            _bgFile = Source(); _cmbFile = Source();
            _sfx = Source(); _sfx.loop = false;
            var v = new GameObject("KE_RcVoice");
            v.transform.SetParent(transform, false);
            _voice = v.AddComponent<AudioSource>();
            _voice.playOnAwake = false; _voice.spatialBlend = 1f; _voice.rolloffMode = AudioRolloffMode.Linear; _voice.minDistance = 2f; _voice.maxDistance = 32f;
            BuildJingles();
            _home.clip = RcWorlds.Egypt ? HomeThemeEgypt() : HomeTheme();
            _home.volume = 0f;
            _home.Play();
        }

        private AudioSource Source()
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false; s.loop = true; s.spatialBlend = 0f; s.volume = 0f;
            return s;
        }

        /// <summary>Where you are: a zone (0-4), the streets (-1), or the park.</summary>
        public void SetPlace(int zone, bool park)
        {
            _park = park;
            if (zone >= 0) _want = zone + 5 * RcWorlds.Now;   // (Egypt's zones have their own loops)
        }

        public void Jingle(int id)
        {
            if (id < 0 || id >= J_COUNT || _jingles[id] == null) return;
            _sfx.PlayOneShot(_jingles[id], _jingleVol[id]);
        }

        /// <summary>A stray's babble, from where it is. The color sets how high its voice is.</summary>
        public void Chatter(Vector3 world, int color)
        {
            if (_chatter == null || _voice.isPlaying) return;
            _voice.transform.position = world;
            _voice.pitch = 0.85f + (color % 7) * 0.07f + Random.Range(-0.04f, 0.04f);
            _voice.PlayOneShot(_chatter[Random.Range(0, _chatter.Length)], 0.7f);
        }

        private void Update()
        {
            // The zone's loop: made the first time you hear it (a layer a frame), kept after that.
            if (_want != _playing && !_building)
            {
                AudioClip[] clips;
                if (_zoneClips.TryGetValue(_want, out clips)) Switch(_want, clips);
                else StartCoroutine(BuildZone(_want));
            }
            int danger = Rc.Enemies != null ? Rc.Enemies.Danger : 0;
            bool boss = Rc.Enemies != null && Rc.Enemies.Boss != null;
            bool dead = Rc.Player != null && Rc.Player.Dead;
            float k = Time.deltaTime * 0.9f;
            _home.volume = Mathf.MoveTowards(_home.volume, _park && !dead ? 0.5f : 0f, k);
            _explore.volume = Mathf.MoveTowards(_explore.volume, _park || dead ? 0f : danger == 0 ? 0.5f : 0.2f, k);
            _tense.volume = Mathf.MoveTowards(_tense.volume, !_park && !dead && danger >= 1 ? 0.5f : 0f, k * 1.6f);
            _fight.volume = Mathf.MoveTowards(_fight.volume, !_park && !dead && danger >= 2 ? 0.55f : 0f, k * 2f);
            float pitch = Mathf.MoveTowards(_explore.pitch, boss ? 1.12f : 1f, Time.deltaTime * 0.2f);
            _explore.pitch = _tense.pitch = _fight.pitch = pitch;
            RecordedMusic(danger, dead);
        }

        // ── Recorded music ───────────────────────────────────────────────────
        private void WantTrack(int i)
        {
            if (_rec[i] != null || _recLoading[i] || _recFailed[i]) return;
            if (!RcTracks.Exists(i)) { _recFailed[i] = true; return; }
            _recLoading[i] = true;
            int track = i;
            StartCoroutine(RcTracks.Load(track, delegate (AudioClip clip)
            {
                _recLoading[track] = false;
                if (clip == null) _recFailed[track] = true; else _rec[track] = clip;
            }));
        }

        /// <summary>The recorded loop for where you are, over the synthesized layers it replaces: background (the park, the streets, each zone; the Facility has its own) and a combat track per zone that comes in when a fight starts.</summary>
        private void RecordedMusic(int danger, bool dead)
        {
            int bgT = (!_park && _want == 4) ? RcTracks.FACILITY_BG : RcTracks.BG;
            int cmT = RcTracks.COMBAT0 + Mathf.Clamp(_want, 0, 4);
            WantTrack(bgT); WantTrack(cmT);
            for (int i = 0; i < RcTracks.COUNT; i++) // (one background and one combat clip in memory at a time)
            {
                if (i == bgT || i == cmT || _rec[i] == null) continue;
                if (_bgFile.clip == _rec[i]) { _bgFile.Stop(); _bgFile.clip = null; }
                if (_cmbFile.clip == _rec[i]) { _cmbFile.Stop(); _cmbFile.clip = null; }
                Destroy(_rec[i]); _rec[i] = null;
            }
            AudioClip bg = _rec[bgT], cm = _rec[cmT];
            float k = Time.deltaTime * 0.9f;
            bool fighting = !_park && !dead && danger >= 2;
            if (bg != null)
            {
                _home.volume = 0f; _explore.volume = 0f; _tense.volume = 0f;
                if (_bgFile.clip != bg) { _bgFile.Stop(); _bgFile.clip = bg; _bgFile.volume = 0f; _bgFile.Play(); }
                float target = dead ? 0f : (fighting && cm != null) ? 0f : (!_park && danger == 1) ? 0.3f : 0.55f;
                _bgFile.volume = Mathf.MoveTowards(_bgFile.volume, target, k);
            }
            if (cm != null)
            {
                _fight.volume = 0f;
                float target = fighting ? 0.6f : 0f;
                if (_cmbFile.clip != cm) { _cmbFile.Stop(); _cmbFile.clip = cm; _cmbFile.volume = 0f; }
                if (target > 0f && !_cmbFile.isPlaying) { _cmbFile.volume = 0f; _cmbFile.Play(); } // (a fight starts the track from its beginning)
                _cmbFile.volume = Mathf.MoveTowards(_cmbFile.volume, target, k * 2f);
                if (target == 0f && _cmbFile.volume <= 0.001f && _cmbFile.isPlaying) _cmbFile.Stop();
            }
        }

        private void Switch(int zone, AudioClip[] clips)
        {
            _playing = zone;
            _explore.Stop(); _tense.Stop(); _fight.Stop();
            _explore.clip = clips[0]; _tense.clip = clips[1]; _fight.clip = clips[2];
            _explore.Play(); _tense.Play(); _fight.Play(); // (the same length, started together: they stay in step)
        }

        private IEnumerator BuildZone(int zone)
        {
            _building = true;
            var clips = new AudioClip[3];
            for (int layer = 0; layer < 3; layer++)
            {
                clips[layer] = Layer(zone, layer);
                yield return null;
            }
            _zoneClips[zone] = clips;
            _building = false;
        }

        // ── Writing the music ────────────────────────────────────────────────
        private static float Note(float root, int[] scale, int degree, int octave)
        {
            int n = scale.Length, o = degree >= 0 ? degree / n : -((-degree + n - 1) / n);
            int d = degree - o * n;
            return root * Mathf.Pow(2f, (scale[d] + 12 * (o + octave)) / 12f);
        }

        /// <summary>
        /// A sine made by turning a point round a circle a step a sample (two multiplies instead of a Sin call:
        /// these loops run for half a million samples, and a slow one is a hitch you'd feel).
        /// </summary>
        private struct Osc
        {
            private float _s, _c, _cs, _sn;
            private int _n;
            public Osc(float freq) { float w = TAU * freq / SR; _cs = Mathf.Cos(w); _sn = Mathf.Sin(w); _s = 0f; _c = 1f; _n = 0; }
            public float Next()
            {
                float s = _s * _cs + _c * _sn;
                _c = _c * _cs - _s * _sn;
                _s = s;
                if ((++_n & 1023) == 0) { float m = 1f / Mathf.Sqrt(_s * _s + _c * _c); _s *= m; _c *= m; } // (rounding would let it grow)
                return s;
            }
        }

        /// <summary>A music-box note: a bell of three partials that ring out. Wraps round the loop's end.</summary>
        private static void Box(float[] d, float at, float freq, float gain, float ring)
        {
            int start = (int)(at * SR), len = (int)(ring * 3.5f * SR);
            Osc a = new Osc(freq), b = new Osc(freq * 3.01f), c = new Osc(freq * 5.4f);
            float ea = 1f, eb = 0.4f, ec = 0.18f;
            float ka = Mathf.Exp(-1f / (ring * SR)), kb = Mathf.Exp(-1f / (ring * 0.35f * SR)), kc = Mathf.Exp(-1f / (ring * 0.15f * SR));
            for (int i = 0; i < len; i++)
            {
                float s = a.Next() * ea + b.Next() * eb + c.Next() * ec;
                ea *= ka; eb *= kb; ec *= kc;
                d[(start + i) % d.Length] += s * gain * (i < 40 ? i / 40f : 1f);
            }
        }

        /// <summary>A drum or a thump: a sine that falls in pitch, with some noise.</summary>
        private static void Thump(float[] d, float at, float f0, float f1, float len, float gain, float noise, System.Random rnd)
        {
            int start = (int)(at * SR), n = (int)(len * SR);
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)n, env = (1f - k) * (1f - k);
                phase += TAU * Mathf.Lerp(f0, f1, Mathf.Sqrt(k)) / SR;
                d[(start + i) % d.Length] += (Mathf.Sin(phase) * (1f - noise) + ((float)rnd.NextDouble() * 2f - 1f) * noise) * env * gain;
            }
        }

        private static void Tone(float[] d, float at, float freq, float len, float gain, float bright)
        {
            int start = (int)(at * SR), n = (int)(len * SR);
            Osc a = new Osc(freq), b = new Osc(freq * 3f), c = new Osc(freq * 5f);
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)n, env = (i < 60 ? i / 60f : 1f) * (n - i < 200 ? (n - i) / 200f : 1f) * (1f - k * 0.5f);
                d[(start + i) % d.Length] += (a.Next() + bright * b.Next() / 3f + bright * c.Next() / 5f) * env * gain;
            }
        }

        private static AudioClip Clip(string name, float[] d)
        {
            // Keep it from clipping when layers stack.
            float peak = 0.001f;
            for (int i = 0; i < d.Length; i++) { float a = d[i] < 0f ? -d[i] : d[i]; if (a > peak) peak = a; }
            if (peak > 0.95f) { float g = 0.95f / peak; for (int i = 0; i < d.Length; i++) d[i] *= g; }
            AudioClip c = AudioClip.Create(name, d.Length, 1, SR, false);
            c.SetData(d, 0);
            return c;
        }

        private static AudioClip Layer(int zone, int layer)
        {
            float beat = 60f / Bpm[zone], root = Root[zone];
            int beats = 32, n = (int)(beats * beat * SR);
            var d = new float[n];
            var rnd = new System.Random(100 + zone * 10 + layer);
            int[] scale = zone >= 5 ? Hijaz : zone >= 2 ? Phrygian : Minor;

            if (layer == 0)
            {
                // The drone: the root and its fifth, slowly breathing.
                // (Frequencies nudged to whole numbers of cycles over the loop, so it joins up without a click.)
                float loop = n / (float)SR;
                Osc o1 = new Osc(Mathf.Round(root * loop) / loop), o2 = new Osc(Mathf.Round(root * 1.498f * loop) / loop), o3 = new Osc(Mathf.Round(root * 0.5f * loop) / loop), slow = new Osc(2f / loop);
                for (int i = 0; i < n; i++)
                {
                    float breathe = 0.6f + 0.4f * slow.Next();
                    d[i] += o1.Next() * 0.09f + o2.Next() * 0.05f * breathe + o3.Next() * 0.1f;
                }
                // A sparse music box, slightly out of tune, with long silences.
                for (int b = 0; b < beats; b++)
                {
                    if (rnd.Next(100) > (b % 8 == 0 ? 85 : 32)) continue;
                    int degree = rnd.Next(7) + (rnd.Next(3) == 0 ? 7 : 0);
                    float detune = 1f + ((float)rnd.NextDouble() - 0.5f) * 0.012f;
                    Box(d, (b + (rnd.Next(2) == 0 ? 0f : 0.5f)) * beat, Note(root, scale, degree, 2) * detune, 0.2f, 0.55f);
                }
                // The wind-up mechanism, ticking.
                for (int b = 0; b < beats; b++) Thump(d, b * beat, 2400f, 1800f, 0.012f, 0.035f, 0.6f, rnd);
            }
            else if (layer == 1)
            {
                // A heartbeat, louder ticking, and two notes a semitone apart, high up.
                for (int b = 0; b < beats; b += 2)
                {
                    Thump(d, b * beat, 62f, 40f, 0.22f, 0.5f, 0.05f, rnd);
                    Thump(d, (b + 0.33f) * beat, 56f, 38f, 0.2f, 0.36f, 0.05f, rnd);
                }
                for (int b = 0; b < beats * 2; b++) Thump(d, b * 0.5f * beat, 2600f, 1900f, 0.014f, b % 2 == 0 ? 0.07f : 0.04f, 0.6f, rnd);
                float loop = n / (float)SR;
                Osc oa = new Osc(Mathf.Round(root * 8f * loop) / loop), ob = new Osc(Mathf.Round(root * 8f * 1.0595f * loop) / loop), slow = new Osc(6f / loop);
                for (int i = 0; i < n; i++)
                {
                    float trem = 0.5f + 0.5f * slow.Next();
                    d[i] += (oa.Next() + ob.Next()) * 0.028f * trem;
                }
            }
            else
            {
                // Drums.
                for (int b = 0; b < beats; b++)
                {
                    Thump(d, b * beat, 130f, 44f, 0.16f, 0.62f, 0.04f, rnd);
                    if (b % 2 == 1) Thump(d, b * beat, 320f, 180f, 0.14f, 0.34f, 0.85f, rnd);
                    Thump(d, (b + 0.5f) * beat, 6000f, 5000f, 0.03f, 0.1f, 0.95f, rnd);
                    if (b % 4 == 3) Thump(d, (b + 0.75f) * beat, 130f, 44f, 0.12f, 0.4f, 0.04f, rnd);
                }
                // The bass: eighth notes round the root.
                int[] bass = { 0, 0, 4, 0, 0, 0, 6, 4 };
                for (int e = 0; e < beats * 2; e++)
                    Tone(d, e * 0.5f * beat, Note(root, scale, bass[e % 8] + (e / 16 % 2 == 1 ? 3 : 0), 0), 0.45f * beat, 0.2f, 0.9f);
                // The lead. For Mama: the treehouse lullaby, in this key. For everyone else: an arpeggio.
                if (zone % 5 == 4)
                {
                    for (int b = 0; b < beats; b++)
                        if (Lullaby[b] >= 0) Box(d, b * beat, Note(root, scale, Lullaby[b], 3), 0.26f, 0.5f);
                }
                else
                {
                    int[] arp = { 0, 2, 4, 7, 4, 2, 0, 2, 5, 7, 5, 2, 3, 5, 7, 5 };
                    for (int e = 0; e < beats * 2; e++)
                        if (e % 8 != 7) Box(d, e * 0.5f * beat, Note(root, scale, arp[(e + (e / 16) * 3) % 16], 2), 0.13f, 0.2f);
                }
            }
            return Clip("KE_Rc_" + zone + "_" + layer, d);
        }

        /// <summary>Last Stop: the same lullaby, a little lower and in a desert mode, over a slow drum. The one warm place in Egypt.</summary>
        private static AudioClip HomeThemeEgypt()
        {
            const float beat = 60f / 62f, root = 196f;
            int beats = 32, n = (int)(beats * beat * SR);
            var d = new float[n];
            int[] chords = { 0, 3, 4, 0, 0, 3, 4, 0 };
            for (int bar = 0; bar < 8; bar++)
                for (int v = 0; v < 3; v++)
                    Tone(d, bar * 4f * beat, Note(root, Hijaz, chords[bar] + v * 2, -1), 4f * beat, 0.06f, 0.15f);
            for (int b = 0; b < beats; b++)
                if (Lullaby[b] >= 0) Box(d, b * beat, Note(root, Hijaz, Lullaby[b], 1), 0.3f, 0.7f);
            var rnd = new System.Random(5);
            for (int b = 0; b < beats; b += 2) Thump(d, b * beat, 90f, 50f, 0.14f, 0.2f, 0.05f, rnd);
            return Clip("KE_Rc_HomeE", d);
        }

        /// <summary>The treehouse: the one warm tune in town. A music box over soft chords.</summary>
        private static AudioClip HomeTheme()
        {
            const float beat = 60f / 66f, root = 220f;
            int beats = 32, n = (int)(beats * beat * SR);
            var d = new float[n];
            int[] chords = { 0, 5, 3, 4, 0, 5, 3, 0 }; // one a bar: I vi IV V ...
            for (int bar = 0; bar < 8; bar++)
                for (int v = 0; v < 3; v++)
                    Tone(d, bar * 4f * beat, Note(root, Major, chords[bar] + v * 2, -1), 4f * beat, 0.06f, 0.15f);
            for (int b = 0; b < beats; b++)
                if (Lullaby[b] >= 0) Box(d, b * beat, Note(root, Major, Lullaby[b], 1), 0.3f, 0.7f);
            // An octave echo, half a beat behind, very quiet.
            for (int b = 0; b < beats; b += 2)
                if (Lullaby[b] >= 0) Box(d, (b + 0.5f) * beat, Note(root, Major, Lullaby[b], 2), 0.07f, 0.4f);
            return Clip("KE_Rc_Home", d);
        }

        // ── Sounds ───────────────────────────────────────────────────────────
        private void BuildJingles()
        {
            var rnd = new System.Random(9);
            _jingles[J_STEP] = Notes("step", 0.05f, 0.5f, 0.2f, 660f); _jingleVol[J_STEP] = 0.35f;
            _jingles[J_PICKUP] = Notes("pickup", 0.07f, 0.35f, 0.25f, 880f, 1320f); _jingleVol[J_PICKUP] = 0.45f;
            _jingles[J_QUEST] = Notes("quest", 0.11f, 0.6f, 0.3f, 523f, 659f, 784f, 1047f); _jingleVol[J_QUEST] = 0.6f;
            _jingles[J_LEVEL] = Notes("level", 0.12f, 0.8f, 0.3f, 392f, 523f, 659f, 784f, 1047f, 1319f); _jingleVol[J_LEVEL] = 0.7f;
            _jingles[J_RARE] = Notes("rare", 0.09f, 0.7f, 0.2f, 1568f, 2093f, 2637f); _jingleVol[J_RARE] = 0.5f;
            _jingles[J_HEAL] = Notes("heal", 0.16f, 0.7f, 0.15f, 660f, 990f); _jingleVol[J_HEAL] = 0.5f;
            _jingles[J_DEATH] = Notes("death", 0.3f, 1.2f, 0.3f, 440f, 415f, 349f, 262f); _jingleVol[J_DEATH] = 0.7f;
            _jingles[J_BOSS] = Notes("boss", 0.16f, 1f, 0.35f, 392f, 523f, 392f, 659f, 784f, 1047f); _jingleVol[J_BOSS] = 0.75f;
            _jingles[J_HURT] = Notes("hurt", 0.09f, 0.18f, 0.6f, 196f, 185f); _jingleVol[J_HURT] = 0.55f;

            _jingles[J_ZIP] = Sweep("zip", 0.55f, 300f, 1400f, 0.75f, rnd); _jingleVol[J_ZIP] = 0.5f;
            _jingles[J_SWING] = Sweep("swing", 0.13f, 900f, 300f, 0.9f, rnd); _jingleVol[J_SWING] = 0.3f;
            _jingles[J_SHOT] = Sweep("shot", 0.09f, 1100f, 260f, 0.45f, rnd); _jingleVol[J_SHOT] = 0.4f;
            _jingles[J_HIT] = Sweep("hit", 0.11f, 190f, 80f, 0.4f, rnd); _jingleVol[J_HIT] = 0.55f;
            _jingles[J_SHOCK] = Sweep("shock", 0.34f, 1500f, 160f, 0.55f, rnd); _jingleVol[J_SHOCK] = 0.6f;
            _jingles[J_BREAK] = Sweep("break", 0.22f, 700f, 120f, 0.7f, rnd); _jingleVol[J_BREAK] = 0.55f;
            _jingles[J_POP] = Sweep("pop", 0.2f, 180f, 760f, 0.05f, rnd); _jingleVol[J_POP] = 0.7f;
            _jingles[J_SLAM] = Sweep("slam", 0.55f, 90f, 32f, 0.3f, rnd); _jingleVol[J_SLAM] = 0.9f;

            // Toy babble: a few syllables of a wobbling voice.
            _chatter = new AudioClip[4];
            for (int c = 0; c < _chatter.Length; c++)
            {
                int syllables = 3 + rnd.Next(4);
                var d = new float[(int)(syllables * 0.13f * SR) + 200];
                for (int s = 0; s < syllables; s++)
                {
                    float f = 520f + rnd.Next(380), len = 0.07f + (float)rnd.NextDouble() * 0.04f;
                    int start = (int)(s * 0.13f * SR), m = (int)(len * SR);
                    float phase = 0f;
                    for (int i = 0; i < m && start + i < d.Length; i++)
                    {
                        float k = i / (float)m;
                        phase += TAU * f * (1f + 0.12f * Mathf.Sin(k * TAU * 1.5f)) / SR;
                        d[start + i] += (Mathf.Sin(phase) + 0.4f * Mathf.Sin(phase * 2f) + 0.2f * Mathf.Sin(phase * 3f)) * Mathf.Sin(k * Mathf.PI) * 0.45f;
                    }
                }
                _chatter[c] = Clip("KE_Rc_Chatter" + c, d);
            }
        }

        private static AudioClip Notes(string name, float step, float ring, float bright, params float[] freqs)
        {
            var d = new float[(int)((freqs.Length * step + ring) * SR)];
            for (int i = 0; i < freqs.Length; i++)
            {
                int start = (int)(i * step * SR), n = (int)(ring * SR);
                float w = TAU * freqs[i] / SR;
                for (int k = 0; k < n && start + k < d.Length; k++)
                {
                    float t = k / (float)SR;
                    d[start + k] += (Mathf.Sin(w * k) + bright * Mathf.Sin(w * k * 3.01f)) * Mathf.Exp(-t / (ring * 0.3f)) * Mathf.Min(1f, k / 30f) * 0.5f;
                }
            }
            return Clip("KE_Rc_" + name, d);
        }

        private static AudioClip Sweep(string name, float len, float f0, float f1, float noise, System.Random rnd)
        {
            var d = new float[(int)(len * SR)];
            float phase = 0f, lp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float k = i / (float)d.Length, env = Mathf.Sin(Mathf.Min(1f, k * 8f) * Mathf.PI * 0.5f) * (1f - k) * (1f - k * 0.5f);
                phase += TAU * Mathf.Lerp(f0, f1, k) / SR;
                lp += (((float)rnd.NextDouble() * 2f - 1f) - lp) * 0.5f;
                d[i] = (Mathf.Sin(phase) * (1f - noise) + lp * noise) * env * 0.8f;
            }
            return Clip("KE_Rc_" + name, d);
        }
    }
}
