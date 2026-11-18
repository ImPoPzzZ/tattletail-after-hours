using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// Summer Vacation's music, synthesized like the party tune: a short steel-drum jingle when a
    /// day starts, a calypso loop while the bar is open, and a low drone while Mama judges you.
    /// Plays on its own 2D source; nothing plays between days (just the waves).
    /// </summary>
    public class BeachMusic : MonoBehaviour
    {
        private const int RATE = 22050;
        private static AudioClip _intro, _work, _drone;
        private AudioSource _src;
        private bool _workAfterIntro;
        private float _fade = -1f;

        public static BeachMusic Create(GameObject host)
        {
            var m = host.AddComponent<BeachMusic>();
            m._src = host.AddComponent<AudioSource>();
            m._src.spatialBlend = 0f;
            m._src.playOnAwake = false;
            return m;
        }

        /// <summary>The day-start jingle, then the work loop.</summary>
        public void PlayDay()
        {
            Play(Intro(), false, 0.55f);
            _workAfterIntro = true;
        }

        public void PlayWork() { Play(Work(), true, 0.32f); }
        public static AudioClip WorkClip() { return Work(); }   // the trailer plays these itself
        public static AudioClip DroneClip() { return Drone(); }
        public void PlayDrone() { Play(Drone(), true, 0.6f); }
        public void FadeOut() { if (_src.isPlaying) _fade = 1.5f; _workAfterIntro = false; }
        public void Stop() { _src.Stop(); _workAfterIntro = false; _fade = -1f; }

        private void Play(AudioClip clip, bool loop, float volume)
        {
            _fade = -1f;
            _workAfterIntro = false;
            _src.Stop();
            _src.clip = clip;
            _src.loop = loop;
            _src.volume = volume;
            _src.Play();
        }

        private void Update()
        {
            if (_workAfterIntro && !_src.isPlaying) PlayWork();
            if (_fade >= 0f)
            {
                _fade -= Time.unscaledDeltaTime;
                _src.volume *= 0.94f;
                if (_fade <= 0f) { _src.Stop(); _fade = -1f; }
            }
        }

        // ── The tunes ────────────────────────────────────────────────────────
        private const int R = -1; // rest

        /// <summary>Day start: a quick run up to a ringing chord, steel drums over a shaker.</summary>
        private static AudioClip Intro()
        {
            if (_intro != null) return _intro;
            float beat = 60f / 128f;
            int six = (int)(beat / 4f * RATE);
            var buf = new float[(int)(RATE * 3.2f)];
            int[] run = { 60, 64, 67, 72, 64, 67, 72, 76 };
            for (int i = 0; i < run.Length; i++) Pan(buf, i * six, six * 2, Freq(run[i]), 0.22f);
            int hit = run.Length * six + six;
            foreach (int n in new[] { 72, 76, 79, 84 }) Pan(buf, hit, RATE * 2, Freq(n), 0.16f, 2.2f);
            Tone(buf, hit, RATE * 2, Freq(48), 0.18f, 2.5f);
            var rng = new System.Random(4);
            for (int i = 0; i < run.Length + 2; i++) Noise(buf, i * six, (int)(0.03f * RATE), 0.04f, 120f, rng);
            Noise(buf, hit, (int)(0.6f * RATE), 0.08f, 6f, rng); // a soft crash
            return _intro = Finish("KE_BeachIntro", buf);
        }

        // Calypso: 16 bars in C (C F G C Am F G C, twice), steel-drum lead, bouncy bass, off-beat chords, shaker and clave.
        private static readonly int[] Roots = { 48, 41, 43, 48, 45, 41, 43, 48 };
        private static readonly int[][] Chords = { new[] { 64, 67, 72 }, new[] { 65, 69, 72 }, new[] { 62, 67, 71 }, new[] { 64, 67, 72 }, new[] { 64, 69, 72 }, new[] { 65, 69, 72 }, new[] { 62, 65, 71 }, new[] { 64, 67, 72 } };
        private static readonly int[] Melody =
        {
            72, R, 76, R, 79, 76, R, 72,     77, R, 81, R, 77, 76, 74, R,
            74, R, 79, R, 83, 81, 79, R,     76, R, 72, R, 76, R, R, R,
            76, R, 81, R, 84, 81, 79, R,     77, R, 76, R, 74, 72, 74, R,
            79, R, 77, R, 74, R, 71, R,      72, R, R, 67, 72, R, R, R,
        };
        private static readonly int[] MelodyB =
        {
            79, 79, R, 76, R, 79, 81, R,     81, R, 77, R, 81, 84, R, 81,
            79, R, 74, R, 79, 83, 86, R,     84, R, 79, R, 76, R, 72, R,
            81, R, 84, 81, R, 76, 72, R,     77, 81, R, 77, 74, R, 77, R,
            79, 77, 74, 71, 74, R, 79, R,    84, R, 79, R, 72, R, R, R,
        };

        private static AudioClip Work()
        {
            if (_work != null) return _work;
            float beat = 60f / 116f;
            int beatS = (int)(beat * RATE), eighth = beatS / 2, six = beatS / 4, barS = beatS * 4;
            const int BARS = 16;
            var buf = new float[barS * BARS];
            var rng = new System.Random(7);
            int[] clave = { 0, 3, 6, 10, 12 }; // son clave, in sixteenths over a bar
            for (int bar = 0; bar < BARS; bar++)
            {
                int b0 = bar * barS, k = bar % 8;
                // Bass: root, the fifth on the "and" of 2, root on 3, fifth on the "and" of 4.
                int[] bassAt = { 0, 3, 4, 7 };
                for (int i = 0; i < bassAt.Length; i++)
                    Tone(buf, b0 + bassAt[i] * eighth, eighth, Freq(Roots[k] + (i % 2 == 1 ? 7 : 0)), 0.17f, 7f);
                // Off-beat chord skank.
                for (int e = 1; e < 8; e += 2)
                    foreach (int n in Chords[k]) Tri(buf, b0 + e * eighth, eighth / 2, Freq(n), 0.035f);
                // Lead: the tune, then its answer.
                int[] mel = bar < 8 ? Melody : MelodyB;
                for (int e = 0; e < 8; e++)
                {
                    int note = mel[k * 8 + e];
                    if (note == R) continue;
                    Pan(buf, b0 + e * eighth, eighth * 2, Freq(note), 0.15f);
                }
                // Percussion: kick on 1 and 3, shaker on sixteenths, clave.
                AddKick(buf, b0); AddKick(buf, b0 + 2 * beatS);
                for (int s = 0; s < 16; s++) Noise(buf, b0 + s * six, (int)(0.025f * RATE), s % 2 == 0 ? 0.03f : 0.05f, 150f, rng);
                foreach (int c in clave) Tone(buf, b0 + c * six, (int)(0.05f * RATE), 2500f, 0.05f, 60f);
            }
            return _work = Finish("KE_BeachWork", buf);
        }

        /// <summary>Mama's verdict: a low, beating drone with a breath of noise.</summary>
        private static AudioClip Drone()
        {
            if (_drone != null) return _drone;
            int len = RATE * 8; // 8 s: 55 Hz and 57.5 Hz both loop cleanly
            var buf = new float[len];
            var rng = new System.Random(13);
            float lp = 0f;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)RATE;
                lp += (((float)rng.NextDouble() * 2f - 1f) - lp) * 0.02f;
                buf[i] = (Mathf.Sin(2f * Mathf.PI * 55f * t) + Mathf.Sin(2f * Mathf.PI * 57.5f * t) * 0.8f + Mathf.Sin(2f * Mathf.PI * 110f * t) * 0.2f) * 0.22f + lp * 0.6f;
            }
            return _drone = Finish("KE_BeachDrone", buf);
        }

        // ── Instruments ──────────────────────────────────────────────────────
        private static float Freq(int midi) { return 440f * Mathf.Pow(2f, (midi - 69) / 12f); }

        /// <summary>A steel drum: a bright, slightly inharmonic ring with a quick attack.</summary>
        private static void Pan(float[] buf, int start, int length, float f, float amp, float decay = 4.5f)
        {
            for (int i = 0; i < length && start + i < buf.Length; i++)
            {
                float t = i / (float)RATE, p = 2f * Mathf.PI * f * t;
                float s = Mathf.Sin(p) + 0.5f * Mathf.Sin(2f * p) * Mathf.Exp(-t * 6f) + 0.22f * Mathf.Sin(3.01f * p) * Mathf.Exp(-t * 10f);
                float env = Mathf.Min(1f, t * 400f) * Mathf.Exp(-t * decay) * Mathf.Min(1f, (length - i) / (0.005f * RATE));
                buf[start + i] += s * amp * env;
            }
        }

        private static void Tone(float[] buf, int start, int length, float f, float amp, float decay)
        {
            for (int i = 0; i < length && start + i < buf.Length; i++)
            {
                float t = i / (float)RATE, p = 2f * Mathf.PI * f * t;
                float env = Mathf.Min(1f, t * 300f) * Mathf.Exp(-t * decay) * Mathf.Min(1f, (length - i) / (0.004f * RATE));
                buf[start + i] += (Mathf.Sin(p) + 0.25f * Mathf.Sin(2f * p)) * amp * env;
            }
        }

        private static void Tri(float[] buf, int start, int length, float f, float amp)
        {
            for (int i = 0; i < length && start + i < buf.Length; i++)
            {
                float t = i / (float)RATE, p = 2f * Mathf.PI * f * t;
                float env = Mathf.Min(1f, t * 300f) * Mathf.Exp(-t * 18f) * Mathf.Min(1f, (length - i) / (0.004f * RATE));
                buf[start + i] += (Mathf.Sin(p) - Mathf.Sin(3f * p) / 9f) * amp * env;
            }
        }

        private static void AddKick(float[] buf, int start)
        {
            int len = (int)(0.16f * RATE);
            float phase = 0f;
            for (int i = 0; i < len && start + i < buf.Length; i++)
            {
                float t = i / (float)RATE;
                phase += 2f * Mathf.PI * Mathf.Lerp(120f, 48f, Mathf.Min(1f, t / 0.1f)) / RATE;
                buf[start + i] += Mathf.Sin(phase) * Mathf.Exp(-t * 18f) * 0.35f;
            }
        }

        private static void Noise(float[] buf, int start, int len, float amp, float decay, System.Random rng)
        {
            for (int i = 0; i < len && start + i < buf.Length; i++)
                buf[start + i] += (float)(rng.NextDouble() * 2.0 - 1.0) * amp * Mathf.Exp(-(i / (float)RATE) * decay);
        }

        private static AudioClip Finish(string name, float[] buf)
        {
            for (int i = 0; i < buf.Length; i++) buf[i] = buf[i] / (1f + Mathf.Abs(buf[i])); // soft clip
            var clip = AudioClip.Create(name, buf.Length, 1, RATE, false);
            clip.SetData(buf, 0);
            return clip;
        }
    }
}
