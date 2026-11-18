using System;
using System.IO;
using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// Papa's 10th Birthday!'s short reaction sounds: a children's cheer on winning
    /// a tattletail minigame, a boo on losing one. Files live in Sounds/ next to the
    /// mod's DLL (children_yay.wav / children_boo.wav) as plain PCM WAVs — same
    /// reasoning as BirthdayMusic.cs (this Unity version can't decode MP3 at
    /// runtime). These are short, so unlike the looping music they're decoded once
    /// into a real AudioClip up front and just played with PlayOneShot from then on.
    /// Missing files are silently skipped, same as the music.
    /// </summary>
    public static class PartySoundEffects
    {
        private static AudioSource _source;
        private static AudioClip _yay;
        private static AudioClip _boo;
        private static bool _triedYay;
        private static bool _triedBoo;

        private static string SoundsDir
        {
            get { return Path.Combine(Path.GetDirectoryName(typeof(KaleidoscopeEndlessPlugin).Assembly.Location), "Sounds"); }
        }

        public static void PlayYay() { Play(ref _yay, ref _triedYay, "children_yay.wav"); }
        public static void PlayBoo() { Play(ref _boo, ref _triedBoo, "children_boo.wav"); }

        private static void Play(ref AudioClip clip, ref bool tried, string fileName)
        {
            if (!tried)
            {
                tried = true;
                try { clip = LoadClip(Path.Combine(SoundsDir, fileName)); }
                catch (Exception e)
                {
                    KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Party] failed to load '" + fileName + "': " + e.Message);
                }
                if (clip == null)
                    KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Party] No '" + fileName + "' in Sounds/ — that reaction stays silent.");
            }
            if (clip == null) return;

            if (_source == null)
            {
                var go = new GameObject("KE_PartySfx");
                UnityEngine.Object.DontDestroyOnLoad(go);
                _source = go.AddComponent<AudioSource>();
                _source.spatialBlend = 0f; // 2D — a UI-style reaction, not from a world position
                _source.playOnAwake = false;
            }
            _source.PlayOneShot(clip);
        }

        // A fully-decoded (non-streaming) load — these clips are short, so unlike
        // BirthdayMusic's streamed music there's no need to keep reading from disk.
        private static AudioClip LoadClip(string path)
        {
            if (!File.Exists(path)) return null;
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length < 44 || bytes[0] != 'R' || bytes[1] != 'I' || bytes[2] != 'F' || bytes[3] != 'F')
            {
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Party] '" + path + "' isn't a RIFF/WAV file.");
                return null;
            }

            int pos = 12, channels = 0, format = 0, sampleRate = 0, bits = 0, dataStart = -1, dataLength = 0;
            while (pos + 8 <= bytes.Length)
            {
                string id = System.Text.Encoding.ASCII.GetString(bytes, pos, 4);
                int size = BitConverter.ToInt32(bytes, pos + 4);
                if (id == "fmt ")
                {
                    format = BitConverter.ToInt16(bytes, pos + 8);
                    channels = BitConverter.ToInt16(bytes, pos + 10);
                    sampleRate = BitConverter.ToInt32(bytes, pos + 12);
                    bits = BitConverter.ToInt16(bytes, pos + 22);
                    if (format == unchecked((short)0xFFFE) && size >= 26) format = BitConverter.ToInt16(bytes, pos + 32);
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

            int bytesPerSample = bits / 8;
            bool supported = (format == 1 && (bits == 8 || bits == 16 || bits == 24 || bits == 32)) || (format == 3 && bits == 32);
            if (dataStart < 0 || channels < 1 || sampleRate <= 0 || !supported)
            {
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Party] Unsupported WAV '" + path + "' (format " + format + ", " + bits + "-bit, " + channels + "ch).");
                return null;
            }

            int totalSamples = dataLength / bytesPerSample;
            int frames = totalSamples / channels;
            var samples = new float[totalSamples];
            for (int i = 0; i < totalSamples; i++)
            {
                int o = dataStart + i * bytesPerSample;
                switch (bytesPerSample)
                {
                    case 1: samples[i] = (bytes[o] - 128) / 128f; break;
                    case 2: samples[i] = BitConverter.ToInt16(bytes, o) / 32768f; break;
                    case 3: samples[i] = ((bytes[o] | (bytes[o + 1] << 8) | (bytes[o + 2] << 16)) << 8 >> 8) / 8388608f; break;
                    default: samples[i] = format == 3 ? BitConverter.ToSingle(bytes, o) : BitConverter.ToInt32(bytes, o) / 2147483648f; break;
                }
            }

            var clip = AudioClip.Create(Path.GetFileNameWithoutExtension(path), frames, channels, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
