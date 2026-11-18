using System;
using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// Papa's 10th Birthday! music: an original, upbeat party loop composed and
    /// synthesized here in code (bass, arpeggiated chords, lead melody, drums),
    /// so there's no audio file to ship and nothing copyrighted in it. Plays
    /// from the speaker, looping, positioned in the room so it gets louder as
    /// you walk toward it.
    /// </summary>
    public static class BirthdayMusic
    {
        // The speaker's mark: standing on top of it at (4.3, 1.8, -6.9); its top surface is a bit lower.
        private static readonly Vector3 SpeakerPos = new Vector3(4.3f, 1.25f, -6.95f);
        private const float VOLUME = 0.8f;

        private static GameObject _go;
        private static AudioClip _clip;

        public static void Start()
        {
            Stop();
            try
            {
                if (_clip == null) _clip = PartyTune.Build();

                _go = new GameObject("KE_BirthdayMusic");
                _go.transform.position = SpeakerPos;
                var src = _go.AddComponent<AudioSource>();
                src.clip = _clip;
                src.loop = true;
                src.volume = VOLUME;
                src.spatialBlend = 1f; // fully 3D — it comes from the speaker
                src.rolloffMode = AudioRolloffMode.Linear;
                src.minDistance = 2f;
                src.maxDistance = 30f;
                src.playOnAwake = false;
                src.Play();

                MuteStoryMusic();
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Music] Playing the party tune (" + _clip.length.ToString("F1") + "s loop) from the speaker at " + SpeakerPos + ".");
            }
            catch (Exception e)
            {
                KaleidoscopeEndlessPlugin.Log.LogError("KE: [Music] Failed: " + e.Message + "\n" + e.StackTrace);
            }
        }

        public static void Stop()
        {
            if (_go != null) UnityEngine.Object.Destroy(_go);
            _go = null;
        }

        // The level's own looping Christmas music sits right next to the speaker — silence it so the party song is the only thing playing.
        private static void MuteStoryMusic()
        {
            foreach (var a in UnityEngine.Object.FindObjectsOfType<AudioSource>())
            {
                if (a == null || a.clip == null) continue;
                if (a.clip.name.IndexOf("music_christmas", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    a.Stop();
                    a.mute = true;
                }
            }
        }
    }

    /// <summary>The party song itself — an original 8-bar loop in C major (C–G–Am–F), 124 BPM.</summary>
    public static class PartyTune
    {
        private const int RATE = 22050;
        private const float BPM = 124f;
        private const int REST = -1;

        // Chord roots (MIDI) and chord tones per bar, 8 bars.
        private static readonly int[] Roots = { 48, 43, 45, 41, 48, 43, 45, 41 };
        private static readonly int[][] Chords =
        {
            new[] { 60, 64, 67 }, new[] { 59, 62, 67 }, new[] { 57, 60, 64 }, new[] { 57, 60, 65 },
            new[] { 60, 64, 67 }, new[] { 59, 62, 67 }, new[] { 57, 60, 64 }, new[] { 57, 60, 65 },
        };

        // Lead melody, 8 eighth-notes per bar.
        private static readonly int[] Melody =
        {
            72, REST, 76, 79,   76, REST, 74, 72,
            74, REST, 71, 74,   79, REST, 77, 74,
            72, REST, 76, 72,   69, REST, 72, 76,
            77, 76, 74, 72,     74, REST, REST, REST,
            79, REST, 79, 81,   79, 76, 72, REST,
            74, REST, 74, 77,   79, REST, 77, 74,
            76, REST, 72, 76,   81, REST, 79, 76,
            77, REST, 76, 74,   72, REST, REST, REST,
        };

        public static AudioClip Build()
        {
            float beat = 60f / BPM;
            int beatSamples = (int)(beat * RATE);
            int eighth = beatSamples / 2;
            int sixteenth = beatSamples / 4;
            int bars = Roots.Length;
            int barSamples = beatSamples * 4;
            var buf = new float[barSamples * bars];
            var rng = new System.Random(10);

            for (int bar = 0; bar < bars; bar++)
            {
                int barStart = bar * barSamples;

                // Bass: bouncy root / octave eighth notes.
                for (int e = 0; e < 8; e++)
                {
                    int note = Roots[bar] + (e % 4 == 2 ? 12 : 0);
                    AddTone(buf, barStart + e * eighth, eighth, Freq(note), 0.16f, 6f, Wave.Soft);
                }

                // Chords: a quiet arpeggio on sixteenths.
                int[] chord = Chords[bar];
                for (int s = 0; s < 16; s++)
                {
                    int note = chord[s % 3] + (s % 6 >= 3 ? 12 : 0);
                    AddTone(buf, barStart + s * sixteenth, sixteenth, Freq(note), 0.05f, 14f, Wave.Triangle);
                }

                // Lead melody.
                for (int e = 0; e < 8; e++)
                {
                    int note = Melody[bar * 8 + e];
                    if (note == REST) continue;
                    // Hold a note into following rests (up to 2 eighths) so phrases sing a little.
                    int len = eighth;
                    if (e + 1 < 8 && Melody[bar * 8 + e + 1] == REST) len += eighth;
                    AddTone(buf, barStart + e * eighth, len, Freq(note), 0.11f, 3.5f, Wave.Pulse);
                }

                // Drums: kick on 1 & 3, clap on 2 & 4, hats on the off-beats.
                for (int b = 0; b < 4; b++)
                {
                    int t = barStart + b * beatSamples;
                    if (b % 2 == 0) AddKick(buf, t);
                    else AddNoise(buf, t, (int)(0.12f * RATE), 0.16f, 22f, rng);
                    AddNoise(buf, t + eighth, (int)(0.04f * RATE), 0.05f, 90f, rng);
                }
            }

            // Gentle soft-clip so stacked voices never crackle.
            for (int i = 0; i < buf.Length; i++)
            {
                float v = buf[i];
                buf[i] = v / (1f + Mathf.Abs(v));
            }

            var clip = AudioClip.Create("KE_PartyTune", buf.Length, 1, RATE, false);
            clip.SetData(buf, 0);
            return clip;
        }

        private enum Wave { Soft, Triangle, Pulse }

        private static float Freq(int midi) { return 440f * Mathf.Pow(2f, (midi - 69) / 12f); }

        private static void AddTone(float[] buf, int start, int length, float freq, float amp, float decay, Wave wave)
        {
            for (int i = 0; i < length && start + i < buf.Length; i++)
            {
                float t = i / (float)RATE;
                float p = 2f * Mathf.PI * freq * t;
                float s;
                switch (wave)
                {
                    case Wave.Triangle:
                        s = Mathf.Sin(p) - Mathf.Sin(3f * p) / 9f + Mathf.Sin(5f * p) / 25f;
                        break;
                    case Wave.Pulse:
                        // Band-limited square-ish lead with a touch of vibrato.
                        float vib = 1f + 0.004f * Mathf.Sin(2f * Mathf.PI * 5.5f * t);
                        float q = p * vib;
                        s = Mathf.Sin(q) + Mathf.Sin(3f * q) / 3f + Mathf.Sin(5f * q) / 5f + Mathf.Sin(7f * q) / 7f;
                        s *= 0.8f;
                        break;
                    default:
                        s = Mathf.Sin(p) + 0.3f * Mathf.Sin(2f * p);
                        break;
                }
                float env = Mathf.Min(1f, t * 250f) * Mathf.Exp(-t * decay);
                // Short fade at the note's end to avoid clicks.
                float tail = Mathf.Min(1f, (length - i) / (0.004f * RATE));
                buf[start + i] += s * amp * env * tail;
            }
        }

        private static void AddKick(float[] buf, int start)
        {
            int len = (int)(0.18f * RATE);
            float phase = 0f;
            for (int i = 0; i < len && start + i < buf.Length; i++)
            {
                float t = i / (float)RATE;
                float f = Mathf.Lerp(130f, 45f, Mathf.Min(1f, t / 0.12f));
                phase += 2f * Mathf.PI * f / RATE;
                buf[start + i] += Mathf.Sin(phase) * Mathf.Exp(-t * 16f) * 0.45f;
            }
        }

        private static void AddNoise(float[] buf, int start, int len, float amp, float decay, System.Random rng)
        {
            for (int i = 0; i < len && start + i < buf.Length; i++)
            {
                float t = i / (float)RATE;
                buf[start + i] += (float)(rng.NextDouble() * 2.0 - 1.0) * amp * Mathf.Exp(-t * decay);
            }
        }
    }
}
