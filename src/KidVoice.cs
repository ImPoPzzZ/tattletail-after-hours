using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// The kids' voices, synthesized (no recordings): a buzzing "throat" at the voice's pitch, shaped
    /// by three mouth resonances (formants) into vowels, plus breath noise. Each sound is a handful
    /// of keyframes (pitch, loudness, voicing, breath, vowel), so there are lots of them, and every
    /// one plays a little differently each time.
    ///   Jump      "hup!"
    ///   Land      "oof" (after a jump)
    ///   SeeMama   screams, shrieks, yelps, gasps, heavy breathing, panting, whimpers, "no no no"...
    ///   Terrified Mama's Gaze: trembling whimpers and shaky breaths
    ///   Caught    a scream
    /// The kid who makes a sound hears it; the other player hears it from where the kid is.
    /// None of it counts on the hunter's kill meter: it's only sound.
    /// </summary>
    public static class KidVoice
    {
        public enum Kind { Jump = 0, Land = 1, SeeMama = 2, Terrified = 3, Caught = 4 }

        public static readonly string[] Types = { "Bright", "Soft", "Raspy", "Squeaky", "Deep" };
        // Per voice type: base pitch (Hz), formant scale (a smaller mouth rings higher), breathiness, rasp.
        private static readonly float[,] TYPE =
        {
            { 300f, 1.25f, 0.15f, 0f }, { 285f, 1.2f, 0.4f, 0f }, { 250f, 1.15f, 0.3f, 0.5f }, { 370f, 1.35f, 0.12f, 0f }, { 125f, 1.0f, 0.22f, 0.2f },
        };

        // Vowels: the first four formants (adult Hz; scaled per voice).
        private const int A = 0, UH = 1, EE = 2, OO = 3, EH = 4, AE = 5, MM = 6, OH = 7;
        private static readonly float[,] VOWEL =
        {
            { 800, 1200, 2600, 3600 }, { 620, 1200, 2550, 3600 }, { 300, 2300, 3000, 3700 }, { 320, 800, 2300, 3500 },
            { 550, 1800, 2550, 3600 }, { 700, 1700, 2500, 3600 }, { 280, 1100, 2300, 3400 }, { 500, 900, 2400, 3500 },
        };

        // ── Who sounds like what ────────────────────────────────────────────
        private static void Params(KidGender g, string spec, out int type, out float pitch)
        {
            switch (g)
            {
                case KidGender.Girl: type = 1; pitch = 0.25f; break;
                case KidGender.Dez: type = 3; pitch = -0.1f; break;
                case KidGender.Kaz: type = 4; pitch = 0f; break;
                case KidGender.Rat: type = 2; pitch = -0.15f; break; // raspy
                case KidGender.Plague: type = 2; pitch = -0.32f; break; // a low rasp behind the mask
                case KidGender.Fish: type = 4; pitch = -0.12f; break;   // deep, for a man that size
                case KidGender.Akurrio: type = 0; pitch = -0.05f; break; // bright, a little low
                case KidGender.Custom:
                    var s = CharacterSpec.FromString(spec);
                    type = s.Voice; pitch = s.VoicePitch / 100f;
                    break;
                default: type = 0; pitch = 0f; break;
            }
        }

        private static void Local(out int type, out float pitch)
        {
            KidGender g = KidSkins.Gender;
            Params(g, g == KidGender.Custom ? CharacterSpec.LocalString : "", out type, out pitch);
        }

        private static void Remote(out int type, out float pitch)
        {
            Params(KidSkins.GenderOf(VersusNet.RemoteSkin), VersusNet.RemoteSpec, out type, out pitch);
        }

        /// <summary>Where this player is a kid who makes sounds: the Arcade, and any run as the kid (Endless, Papa's party, the child in Versus).</summary>
        public static bool Active
        {
            get { return HubMode.Active || BeachMode.Active || (!VersusMode.IsMama && EndlessRunManager.Instance != null && EndlessRunManager.Instance.RunActive); }
        }

        // ── Seeing Mama: a scream, a gasp, heavy breathing... when she comes into view, and
        // now and then while she stays in it. Versus (the Mama player's body) and Endless (the AI Mama).
        private const float SEE_DIST = 14f;
        private static float _mamaSeenAt = -100f, _nextReaction;

        public static void WatchFor(GameObject mama)
        {
            Camera cam = Camera.main;
            if (mama == null || cam == null || !Active) return;
            Vector3 chest = mama.transform.position + Vector3.up * 1.4f;
            if (Vector3.Distance(cam.transform.position, chest) > SEE_DIST) return;
            Vector3 vp = cam.WorldToViewportPoint(chest);
            if (vp.z <= 0f || vp.x < 0.05f || vp.x > 0.95f || vp.y < 0.05f || vp.y > 0.95f) return;
            RaycastHit hit;
            if (Physics.Linecast(cam.transform.position, chest, out hit, ~0, QueryTriggerInteraction.Ignore) && !hit.transform.IsChildOf(mama.transform)) return; // a wall
            bool fresh = Time.time - _mamaSeenAt > 4f; // she just came into view
            _mamaSeenAt = Time.time;
            if (Time.time < _nextReaction || !(fresh || UnityEngine.Random.value < Time.deltaTime / 9f)) return;
            _nextReaction = Time.time + 6f;
            Say(Kind.SeeMama);
        }

        /// <summary>Every frame (plugin Update): the AI Mama outside Versus (Versus watches the Mama player's body itself).</summary>
        public static void Tick()
        {
            if (VersusMode.Role != VersusRole.None || MamaController.Instance == null || !Active || HubMode.Active) return;
            WatchFor(MamaController.Instance.GetActiveMama());
        }

        // ── Playing ──────────────────────────────────────────────────────────
        /// <summary>This player's kid makes a sound: they hear it, and the other player hears it from them.</summary>
        public static void Say(Kind kind, float seconds = 2f)
        {
            int seed = UnityEngine.Random.Range(1, 1 << 20);
            int type; float pitch;
            Local(out type, out pitch);
            bool noisy;
            Play(Make(kind, seed, type, pitch, seconds, out noisy), Vector3.zero, true);
            VersusNet.SendVoice((int)kind, seed, seconds);
        }

        /// <summary>The other player's kid made a sound (VersusNet event): heard from where they are.</summary>
        public static void Hear(int kind, int seed, float seconds)
        {
            Vector3 pos; float yaw; bool present;
            int ctx = VersusNet.LocalPose(out pos, out yaw, out present);
            if (ctx == VersusNet.CTX_NONE || ctx != VersusNet.RemoteCtx) return; // not in the same place (or not where you can see each other)
            var kid = VersusAvatars.VisiblePerson;
            Vector3 at = (kid != null ? kid.transform.position : VersusNet.RemotePos) + Vector3.up * 0.9f;
            int type; float pitch;
            Remote(out type, out pitch);
            bool noisy;
            Play(Make((Kind)kind, seed, type, pitch, seconds, out noisy), at, false);
        }

        /// <summary>The other player's kid, at a spot (Mama's kill: they scream as she gets them).</summary>
        public static void RemoteAt(Kind kind, Vector3 at)
        {
            int type; float pitch;
            Remote(out type, out pitch);
            bool noisy;
            Play(Make(kind, UnityEngine.Random.Range(1, 1 << 20), type, pitch, 2f, out noisy), at, false);
        }

        /// <summary>This player's kid, heard only here (their own scream when Mama gets them: she plays hers).</summary>
        public static void Mine(Kind kind)
        {
            int type; float pitch;
            Local(out type, out pitch);
            bool noisy;
            Play(Make(kind, UnityEngine.Random.Range(1, 1 << 20), type, pitch, 2f, out noisy), Vector3.zero, true);
        }

        /// <summary>The Character Creator's test buttons: just to hear it.</summary>
        public static void Preview(Kind kind, int type, float pitch)
        {
            bool noisy;
            Play(Make(kind, UnityEngine.Random.Range(1, 1 << 20), type, pitch, 2f, out noisy), Vector3.zero, true);
        }

        private static void Play(AudioClip clip, Vector3 at, bool mine)
        {
            if (clip == null) return;
            var go = new GameObject("KE_KidVoice");
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.dopplerLevel = 0f;
            if (mine)
            {
                src.spatialBlend = 0f; // in your head (not on the camera: the jumpscare can switch cameras off)
                src.volume = 0.7f;
            }
            else
            {
                go.transform.position = at;
                src.spatialBlend = 1f;
                src.rolloffMode = AudioRolloffMode.Linear;
                src.minDistance = 1.5f;
                src.maxDistance = 30f; // a scream carries through the house
                src.volume = 1f;
            }
            src.Play();
            Object.Destroy(go, clip.length + 0.2f);
            Object.Destroy(clip, clip.length + 0.3f);
        }

        // ── The sounds ───────────────────────────────────────────────────────
        private class Take
        {
            public readonly List<float> T = new List<float>();
            public readonly List<float[]> K = new List<float[]>(); // pitch, loudness, voicing, breath, vowel
            public float Vib, VibRate = 6f, Trem, TremRate = 8f, Jitter = 0.01f, Drive = 1f, Loud = 1f, PitchMul = 1f;
            public bool Noisy;

            public void Key(float t, float pitch, float amp, float voiced, float breath, int vowel)
            {
                if (T.Count > 0 && t <= T[T.Count - 1]) t = T[T.Count - 1] + 0.001f; // always forward
                T.Add(t);
                K.Add(new float[] { pitch, amp, voiced, breath, vowel });
            }

            public float End { get { return T.Count > 0 ? T[T.Count - 1] : 0f; } }

            /// <summary>One syllable: rise, hold, fall, from one vowel and pitch to another.</summary>
            public void Syl(float at, float dur, float p0, float p1, float amp, int v0, int v1, float voiced, float breath, float attack = 0.03f)
            {
                Key(at, p0, 0f, voiced, breath, v0);
                Key(at + attack, p0, amp, voiced, breath, v0);
                Key(at + dur * 0.7f, Mathf.Lerp(p0, p1, 0.7f), amp * 0.85f, voiced, breath, v1);
                Key(at + dur, p1, 0f, voiced * 0.6f, breath, v1);
            }

            /// <summary>A breath in or out: air through the mouth, a little voice in it.</summary>
            public void Breath(float at, float dur, float amp, int vowel, float pitch, float voiced)
            {
                Key(at, pitch, 0f, voiced, 1f, vowel);
                Key(at + dur * 0.45f, pitch, amp, voiced, 1f, vowel);
                Key(at + dur, pitch * 0.95f, 0f, voiced, 1f, vowel);
            }
        }

        private static AudioClip Make(Kind kind, int seed, int type, float pitchSlider, float seconds, out bool noisy)
        {
            var r = new System.Random(seed);
            var t = new Take();
            t.PitchMul = 0.94f + (float)r.NextDouble() * 0.14f;
            switch (kind)
            {
                case Kind.Jump: Jump(t, r); break;
                case Kind.Land: Land(t, r); break;
                case Kind.SeeMama: SeeMama(t, r); break;
                case Kind.Terrified: Terrified(t, r, Mathf.Clamp(seconds, 0.8f, 6f)); break;
                default: Scream(t, r, 1.3f + (float)r.NextDouble() * 0.4f, 2.3f); t.Drive = 3f; break;
            }
            noisy = t.Noisy;
            type = Mathf.Clamp(type, 0, Types.Length - 1);
            float f0 = TYPE[type, 0] * Mathf.Pow(2f, Mathf.Clamp(pitchSlider, -1f, 1f) * 0.6f);
            return Render(t, f0, TYPE[type, 1], TYPE[type, 2], TYPE[type, 3], seed);
        }

        private static float Rand(System.Random r, float a, float b) { return a + (float)r.NextDouble() * (b - a); }

        private static void Jump(Take t, System.Random r)
        {
            t.Loud = 0.5f;
            t.Noisy = true; // one small noise
            switch (r.Next(4))
            {
                case 0: // "hup!"
                    t.Key(0f, 1f, 0f, 0f, 0.9f, UH); t.Key(0.02f, 1f, 0.5f, 0f, 0.9f, UH);
                    t.Key(0.045f, 1.05f, 1f, 1f, 0.2f, UH); t.Key(0.12f, 1.15f, 0.7f, 1f, 0.2f, MM); t.Key(0.16f, 1.1f, 0f, 1f, 0.2f, MM);
                    break;
                case 1: // "hn!"
                    t.Syl(0f, 0.15f, 1.05f, 1.2f, 1f, MM, MM, 1f, 0.25f, 0.02f);
                    break;
                case 2: // "hah!"
                    t.Key(0f, 1f, 0f, 0f, 1f, A); t.Key(0.02f, 1f, 0.6f, 0.3f, 0.9f, A);
                    t.Key(0.05f, 1.1f, 1f, 1f, 0.3f, A); t.Key(0.13f, 1.05f, 0.5f, 0.6f, 0.6f, A); t.Key(0.18f, 1f, 0f, 0f, 0.6f, A);
                    break;
                default: // "heh!"
                    t.Syl(0f, 0.14f, 1.1f, 1.2f, 1f, EH, EH, 1f, 0.35f, 0.02f);
                    break;
            }
        }

        private static void Land(Take t, System.Random r)
        {
            t.Loud = 0.55f;
            switch (r.Next(4))
            {
                case 0: // "oof"
                    t.Key(0f, 0.95f, 0f, 0f, 1f, OO); t.Key(0.02f, 0.95f, 0.7f, 0.2f, 0.8f, OO);
                    t.Key(0.05f, 0.9f, 1f, 1f, 0.3f, OO); t.Key(0.15f, 0.78f, 0.6f, 0.8f, 0.4f, OO); t.Key(0.22f, 0.72f, 0f, 0f, 0.5f, OO);
                    break;
                case 1: // "uhh"
                    t.Syl(0f, 0.2f, 0.95f, 0.75f, 1f, UH, UH, 1f, 0.35f, 0.02f);
                    break;
                case 2: // "hmph"
                    t.Syl(0f, 0.12f, 1f, 0.85f, 1f, MM, MM, 1f, 0.2f, 0.02f);
                    t.Breath(0.12f, 0.1f, 0.6f, UH, 0.85f, 0f);
                    break;
                default: // "uff"
                    t.Syl(0f, 0.1f, 1f, 0.9f, 1f, UH, UH, 1f, 0.4f, 0.015f);
                    t.Breath(0.1f, 0.12f, 0.5f, OO, 0.9f, 0f);
                    break;
            }
        }

        private static void Scream(Take t, System.Random r, float dur, float high)
        {
            t.Loud = 1f;
            t.Noisy = true;
            t.Vib = 0.035f; t.VibRate = Rand(r, 5.5f, 7.5f); t.Jitter = 0.04f; t.Drive = 2.5f;
            int v = r.Next(2) == 0 ? A : AE;
            t.Key(0f, high * 0.75f, 0f, 1f, 0.5f, A);
            t.Key(0.05f, high * 0.9f, 1f, 1f, 0.3f, A);
            t.Key(dur * 0.4f, high, 1f, 1f, 0.3f, v);
            t.Key(dur - 0.25f, high * 0.95f, 0.9f, 1f, 0.35f, v);
            t.Key(dur, high * 0.78f, 0f, 0.5f, 0.6f, A);
        }

        private static void Gasp(Take t, float at, float dur, float pitch)
        {
            t.Key(at, pitch, 0f, 0.1f, 1f, A);
            t.Key(at + dur * 0.2f, pitch, 0.4f, 0.15f, 1f, A);
            t.Key(at + dur * 0.85f, pitch * 1.1f, 1f, 0.25f, 1f, EH);
            t.Key(at + dur * 0.92f, pitch * 1.1f, 0f, 0f, 1f, EH); // caught in the throat
        }

        private static void SeeMama(Take t, System.Random r)
        {
            switch (r.Next(14))
            {
                case 0: case 1: // a scream
                    Scream(t, r, Rand(r, 0.9f, 1.3f), Rand(r, 1.9f, 2.3f));
                    break;
                case 2: // "eeek!"
                    t.Noisy = true; t.Drive = 2f; t.Jitter = 0.03f;
                    t.Syl(0f, Rand(r, 0.3f, 0.45f), 2.4f, 2.5f, 1f, EE, EE, 1f, 0.2f, 0.02f);
                    break;
                case 3: // "ah!"
                    t.Noisy = true; t.Drive = 1.6f;
                    t.Syl(0f, 0.28f, 2.1f, 1.6f, 1f, A, UH, 1f, 0.25f, 0.015f);
                    break;
                case 4: // a gasp
                    t.Loud = 0.7f;
                    Gasp(t, 0f, Rand(r, 0.4f, 0.55f), 1.7f);
                    break;
                case 5: // two gasps
                    t.Loud = 0.7f;
                    Gasp(t, 0f, 0.32f, 1.6f); Gasp(t, 0.36f, 0.4f, 1.75f);
                    break;
                case 6: // heavy breathing
                {
                    t.Loud = 0.6f;
                    float at = 0f;
                    for (int i = 0; i < 4; i++)
                    {
                        t.Breath(at, Rand(r, 0.35f, 0.45f), 0.6f, A, 1.3f, 0.05f);
                        at = t.End + 0.03f;
                        t.Breath(at, Rand(r, 0.3f, 0.4f), 0.75f, UH, 1.2f, 0.18f);
                        at = t.End + Rand(r, 0.05f, 0.12f);
                    }
                    break;
                }
                case 7: // panting
                {
                    t.Loud = 0.6f;
                    float at = 0f;
                    for (int i = 0; i < 8; i++) { t.Breath(at, 0.13f, i % 2 == 0 ? 0.6f : 0.75f, i % 2 == 0 ? A : UH, 1.5f, 0.1f); at = t.End + 0.02f; }
                    break;
                }
                case 8: // a whimper
                    t.Loud = 0.7f; t.Trem = 0.5f; t.TremRate = 9f; t.Vib = 0.05f; t.VibRate = 9f;
                    t.Syl(0f, 0.35f, 1.6f, 1.35f, 0.8f, MM, UH, 1f, 0.35f, 0.05f);
                    t.Syl(0.4f, 0.4f, 1.55f, 1.1f, 0.8f, MM, UH, 1f, 0.4f, 0.05f);
                    break;
                case 9: // a scream held in: "mmMMM!"
                    t.Noisy = true; t.Drive = 2.2f; t.Vib = 0.03f;
                    t.Key(0f, 1.4f, 0f, 1f, 0.2f, MM); t.Key(0.1f, 1.7f, 0.8f, 1f, 0.2f, MM);
                    t.Key(0.6f, 2f, 1f, 1f, 0.2f, MM); t.Key(0.9f, 1.8f, 0f, 1f, 0.3f, MM);
                    break;
                case 10: // "no no no"
                {
                    t.Loud = 0.85f; t.Jitter = 0.02f;
                    float p = 1.55f, at = 0f;
                    for (int i = 0; i < 3; i++)
                    {
                        t.Key(at, p, 0f, 1f, 0.2f, MM); t.Key(at + 0.04f, p, 0.5f, 1f, 0.2f, MM);
                        t.Key(at + 0.08f, p * 1.05f, 1f, 1f, 0.25f, OH); t.Key(at + 0.2f, p * 1.02f, 0.7f, 1f, 0.3f, OO); t.Key(at + 0.24f, p, 0f, 1f, 0.3f, OO);
                        at = t.End + 0.07f;
                        p *= 1.1f;
                    }
                    break;
                }
                case 11: // a shaky breath in
                    t.Loud = 0.65f; t.Trem = 0.7f; t.TremRate = 11f;
                    t.Breath(0f, 0.8f, 0.8f, A, 1.7f, 0.15f);
                    break;
                case 12: // a short scream, then panting
                    Scream(t, r, 0.5f, 2.1f);
                    for (int i = 0; i < 3; i++) t.Breath(t.End + 0.05f, 0.16f, 0.55f, i % 2 == 0 ? A : UH, 1.5f, 0.1f);
                    break;
                default: // "uh-oh..."
                    t.Loud = 0.8f;
                    t.Syl(0f, 0.18f, 1.5f, 1.5f, 1f, UH, UH, 1f, 0.3f, 0.02f);
                    t.Syl(0.24f, 0.32f, 1.25f, 1.1f, 1f, OH, OO, 1f, 0.35f, 0.02f);
                    break;
            }
        }

        private static void Terrified(Take t, System.Random r, float seconds)
        {
            t.Loud = 0.75f; t.Trem = 0.55f; t.TremRate = Rand(r, 6.5f, 8.5f); t.Vib = 0.08f; t.VibRate = t.TremRate; t.Jitter = 0.03f;
            int style = r.Next(3);
            float at = 0f;
            while (at < seconds - 0.25f)
            {
                if (style == 1 || r.Next(4) == 0) // shaky breaths
                {
                    t.Breath(at, 0.4f, 0.7f, A, 1.6f, 0.2f);
                    at = t.End + 0.04f;
                    t.Breath(at, 0.3f, 0.6f, UH, 1.4f, 0.25f);
                }
                else if (style == 2) t.Syl(at, 0.45f, 1.5f, 1.3f, 0.8f, MM, MM, 1f, 0.3f, 0.06f); // "nnn..."
                else t.Syl(at, 0.3f, Rand(r, 1.4f, 1.6f), 1.25f, 0.9f, UH, UH, 1f, 0.45f, 0.04f); // "uh... uh..."
                at = t.End + Rand(r, 0.04f, 0.12f);
            }
        }

        // ── Synthesis ────────────────────────────────────────────────────────
        private const int SR = 22050;

        private static AudioClip Render(Take tk, float f0Base, float fScale, float breathy, float rasp, int seed)
        {
            if (tk.T.Count < 2) return null;
            int n = (int)((tk.End + 0.05f) * SR);
            var data = new float[n];
            var rnd = new System.Random(seed * 31 + 7);
            var y1 = new float[4]; var y2 = new float[4];
            var ca = new float[4]; var cb = new float[4]; var cc = new float[4];
            float lastNoise = 0f, lips = 0f, phase = 0f, glot = 0f, jit = 0f, pitch = 1f, amp = 0f, voiced = 0f, breath = 0f, peak = 1e-6f;
            int period = 0, seg = 0, last = tk.T.Count - 1;
            const float TAU = 6.2831853f;

            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SR;
                if ((i & 31) == 0) // the keyframes change slowly: update every 32 samples
                {
                    while (seg < last - 1 && t > tk.T[seg + 1]) seg++;
                    int s1 = Mathf.Min(seg + 1, last);
                    float t0 = tk.T[seg], t1 = tk.T[s1];
                    float k = t1 > t0 ? Mathf.Clamp01((t - t0) / (t1 - t0)) : 1f;
                    if (t > tk.T[last]) k = 1f;
                    float[] a = tk.K[seg], b = tk.K[s1];
                    pitch = Mathf.Lerp(a[0], b[0], k);
                    amp = Mathf.Lerp(a[1], b[1], k);
                    voiced = Mathf.Lerp(a[2], b[2], k);
                    breath = Mathf.Lerp(a[3], b[3], k);
                    for (int f = 0; f < 4; f++)
                    {
                        float F = Mathf.Min(Mathf.Lerp(VOWEL[(int)a[4], f], VOWEL[(int)b[4], f], k) * fScale, SR * 0.45f);
                        float bw = (f == 0 ? 90f : f == 1 ? 120f : f == 2 ? 180f : 250f) * (1f + breath);
                        cc[f] = -Mathf.Exp(-TAU * bw / SR);
                        cb[f] = 2f * Mathf.Exp(-Mathf.PI * bw / SR) * Mathf.Cos(TAU * F / SR);
                        ca[f] = 1f - cb[f] - cc[f]; // unity gain at 0 Hz, so the three can be chained
                    }
                }

                jit += ((float)rnd.NextDouble() - 0.5f) * tk.Jitter;
                jit *= 0.98f;
                float f0 = f0Base * pitch * tk.PitchMul * (1f + tk.Vib * Mathf.Sin(TAU * tk.VibRate * t) + jit);
                float dt = Mathf.Clamp(f0 / SR, 0.001f, 0.45f);
                phase += dt;
                if (phase >= 1f) { phase -= 1f; period++; }
                float saw = 2f * phase - 1f - PolyBlep(phase, dt);
                glot += (saw - glot) * 0.6f; // softer highs, like a throat
                float src = glot;
                if (rasp > 0f && (period & 1) == 1) src *= 1f - rasp * 0.6f; // rough, uneven cycles
                float noise = (float)rnd.NextDouble() * 2f - 1f;
                float hiss = noise - lastNoise; // air at the lips (the "hhh" in a breath)
                lastNoise = noise;

                float x = voiced * src + (breath + breathy * voiced) * noise * 0.5f;
                for (int f = 0; f < 4; f++)
                {
                    float y = ca[f] * x + cb[f] * y1[f] + cc[f] * y2[f];
                    y2[f] = y1[f]; y1[f] = y;
                    x = y;
                }
                float trem = 1f - tk.Trem * (0.5f + 0.5f * Mathf.Sin(TAU * tk.TremRate * t));
                float o = x - 0.92f * lips; // sound leaving the lips is brighter than in the throat
                lips = x;
                float s = (o + breath * hiss * 0.12f) * amp * trem;
                data[i] = s;
                if (Mathf.Abs(s) > peak) peak = Mathf.Abs(s);
            }

            // Level it, strain it (screams), level it again, and no clicks at the ends.
            float drive = Mathf.Max(1f, tk.Drive), norm = (float)System.Math.Tanh(drive), peak2 = 1e-6f;
            for (int i = 0; i < n; i++)
            {
                float s = data[i] / peak;
                if (drive > 1f) s = (float)System.Math.Tanh(s * drive) / norm;
                data[i] = s;
                if (Mathf.Abs(s) > peak2) peak2 = Mathf.Abs(s);
            }
            int fade = SR / 200;
            float gain = 0.9f * tk.Loud / peak2;
            for (int i = 0; i < n; i++)
            {
                float e = Mathf.Min(1f, Mathf.Min(i, n - 1 - i) / (float)fade);
                data[i] *= gain * e;
            }

            var clip = AudioClip.Create("KE_KidVoice", n, 1, SR, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static float PolyBlep(float t, float dt)
        {
            if (t < dt) { t /= dt; return t + t - t * t - 1f; }
            if (t > 1f - dt) { t = (t - 1f) / dt; return t * t + t + t + 1f; }
            return 0f;
        }
    }

    // Jumping and landing (from a jump), on the game's own jump/land sounds.
    [HarmonyPatch(typeof(FirstPersonController), "PlayJumpSound")]
    public static class Patch_FPS_PlayJumpSound_Voice
    {
        private static float _next;
        static void Postfix(FirstPersonController __instance)
        {
            if (__instance != RM.fpsController || !KidVoice.Active || Time.time < _next) return;
            _next = Time.time + 0.3f;
            KidVoice.Say(KidVoice.Kind.Jump);
        }
    }

    [HarmonyPatch(typeof(FirstPersonController), "PlayLandingSound")]
    public static class Patch_FPS_PlayLandingSound_Voice
    {
        private static float _next;
        static void Postfix(FirstPersonController __instance, bool ___m_Jumping)
        {
            if (!___m_Jumping || __instance != RM.fpsController || !KidVoice.Active || Time.time < _next) return; // only after a jump, not every step down
            _next = Time.time + 0.4f;
            KidVoice.Say(KidVoice.Kind.Land);
        }
    }
}
