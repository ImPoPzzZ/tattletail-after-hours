using System;
using System.IO;
using System.IO.Compression;
using System.Media;
using System.Reflection;

namespace AfterHoursInstaller
{
    /// <summary>The installer's sounds: a bubbly pop on clicks (made here) and the mod's own kids' "Yayyyy!" when it's installed.</summary>
    public static class Sounds
    {
        private const int RATE = 22050;
        private static SoundPlayer _pop, _yay;

        public static void Pop() { Play(ref _pop, () => Tone(0.09f, 0.3f, t => 900f - 4200f * t, 55f)); }

        public static void Yay()
        {
            Play(ref _yay, () =>
            {
                using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip"))
                using (var zip = new ZipArchive(s, ZipArchiveMode.Read))
                using (var wav = zip.GetEntry("BepInEx/plugins/KaleidoscopeEndless/Sounds/children_yay.wav").Open())
                {
                    var ms = new MemoryStream();
                    wav.CopyTo(ms);
                    ms.Position = 0;
                    return ms;
                }
            });
        }

        private static void Play(ref SoundPlayer player, Func<Stream> make)
        {
            try
            {
                if (player == null) { player = new SoundPlayer(make()); player.Load(); }
                player.Play();
            }
            catch { } // no sound device, or whatever: sounds are a bonus
        }

        /// <summary>A short sine blip: duration, loudness, pitch over time (0..1), how fast it dies away.</summary>
        private static Stream Tone(float seconds, float volume, Func<float, float> pitch, float decay)
        {
            int n = (int)(RATE * seconds);
            var ms = new MemoryStream();
            var w = new BinaryWriter(ms);
            w.Write(new[] { 'R', 'I', 'F', 'F' }); w.Write(36 + n * 2); w.Write(new[] { 'W', 'A', 'V', 'E' });
            w.Write(new[] { 'f', 'm', 't', ' ' }); w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(RATE); w.Write(RATE * 2); w.Write((short)2); w.Write((short)16);
            w.Write(new[] { 'd', 'a', 't', 'a' }); w.Write(n * 2);
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)RATE, k = i / (float)n;
                phase += 2 * Math.PI * pitch(k) / RATE;
                float env = Math.Min(1f, t * 600f) * (float)Math.Exp(-t * decay);
                w.Write((short)(Math.Sin(phase) * env * volume * 32767));
            }
            w.Flush();
            ms.Position = 0;
            return ms;
        }
    }
}
