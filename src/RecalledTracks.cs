using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// Recalled's recorded music: seven PCM WAVs in Sounds/ next to the DLL (this Unity can't decode MP3 at runtime, so the
    /// mp3s are converted with tools/convert_to_wav.ps1). The clips are 16-bit PCM turned into floats a slice a frame,
    /// so loading one never hitches. A missing file just means that part keeps the music written in code (RcMusic).
    /// Index: 0 background, 1..5 combat for zones 0..4 (suburbs, mall, school, junkyard, Facility), 6 the Facility's background.
    /// </summary>
    public static class RcTracks
    {
        public const int BG = 0, COMBAT0 = 1, FACILITY_BG = 6, COUNT = 7;
        private static readonly string[] Files =
        {
            "rc_bg.wav", "rc_combat_0.wav", "rc_combat_1.wav", "rc_combat_2.wav", "rc_combat_3.wav", "rc_combat_4.wav", "rc_facility_bg.wav"
        };
        private const int SLICE = 400000; // samples converted per frame

        private static string Dir
        {
            get { return Path.Combine(Path.GetDirectoryName(typeof(KaleidoscopeEndlessPlugin).Assembly.Location), "Sounds"); }
        }

        public static string PathOf(int track) { return Path.Combine(Dir, Files[track]); }
        public static bool Exists(int track) { return track >= 0 && track < COUNT && File.Exists(PathOf(track)); }

        /// <summary>Loads a track over several frames. done(null) if the file is missing or isn't a plain PCM WAV.</summary>
        public static IEnumerator Load(int track, ClipHandler done)
        {
            string path = PathOf(track);
            byte[] bytes = null;
            try { if (File.Exists(path)) bytes = File.ReadAllBytes(path); }
            catch (Exception e) { KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Recalled] couldn't read '" + path + "': " + e.Message); }
            if (bytes == null || bytes.Length < 44 || bytes[0] != 'R' || bytes[1] != 'I' || bytes[2] != 'F' || bytes[3] != 'F') { done(null); yield break; }
            yield return null;

            int pos = 12, channels = 0, format = 0, rate = 0, bits = 0, dataStart = -1, dataLength = 0;
            while (pos + 8 <= bytes.Length)
            {
                string id = System.Text.Encoding.ASCII.GetString(bytes, pos, 4);
                int size = BitConverter.ToInt32(bytes, pos + 4);
                if (id == "fmt ")
                {
                    format = BitConverter.ToInt16(bytes, pos + 8);
                    channels = BitConverter.ToInt16(bytes, pos + 10);
                    rate = BitConverter.ToInt32(bytes, pos + 12);
                    bits = BitConverter.ToInt16(bytes, pos + 22);
                }
                else if (id == "data")
                {
                    dataStart = pos + 8;
                    int remaining = bytes.Length - dataStart;
                    dataLength = (size <= 0 || size > remaining) ? remaining : size;
                    break;
                }
                pos += 8 + size + (size % 2);
            }
            if (dataStart < 0 || channels < 1 || rate <= 0 || format != 1 || bits != 16)
            {
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Recalled] '" + path + "' isn't 16-bit PCM (format " + format + ", " + bits + "-bit).");
                done(null);
                yield break;
            }

            int total = dataLength / 2, frames = total / channels;
            var samples = new float[frames * channels];
            for (int start = 0; start < samples.Length; start += SLICE)
            {
                int end = Math.Min(samples.Length, start + SLICE);
                for (int i = start; i < end; i++) samples[i] = BitConverter.ToInt16(bytes, dataStart + i * 2) / 32768f;
                yield return null;
            }
            var clip = AudioClip.Create("KE_" + Path.GetFileNameWithoutExtension(path), frames, channels, rate, false);
            clip.SetData(samples, 0);
            done(clip);
        }

        public delegate void ClipHandler(AudioClip clip);
    }
}
