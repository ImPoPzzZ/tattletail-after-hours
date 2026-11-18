using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// Loads the mod's sticker art (HUD event badges, objective icons, and
    /// Mama's Scrapbook collectibles) from Stickers/ next to the DLL — plain
    /// PNGs, same "read a real asset file off disk" approach as
    /// PartySoundEffects.cs/BirthdayMusic.cs use for audio, just images
    /// instead. Loaded lazily and cached by name so each file only ever
    /// gets read and decoded once. A missing file just means that spot
    /// silently shows nothing, same as a missing sound effect.
    /// </summary>
    public static class StickerImages
    {
        private static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();
        private static readonly HashSet<string> _missingLogged = new HashSet<string>();

        private static string StickersDir
        {
            get { return Path.Combine(Path.GetDirectoryName(typeof(KaleidoscopeEndlessPlugin).Assembly.Location), "Stickers"); }
        }

        /// <summary>Returns the cached/loaded Sprite for a sticker name (no ".png"), or null if the file's missing/unreadable.</summary>
        public static Sprite Get(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            Sprite sprite;
            if (_cache.TryGetValue(name, out sprite)) return sprite;

            sprite = Load(name);
            _cache[name] = sprite; // cache the miss too, so a missing file is only logged once
            return sprite;
        }

        private static Sprite Load(string name)
        {
            string path = Path.Combine(StickersDir, name + ".png");
            if (!File.Exists(path))
            {
                if (_missingLogged.Add(name))
                    KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Sticker] '" + name + ".png' not found in Stickers/ — that spot stays empty.");
                return null;
            }

            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!tex.LoadImage(bytes))
                {
                    KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Sticker] '" + name + ".png' failed to decode.");
                    return null;
                }
                tex.filterMode = FilterMode.Bilinear;
                HardenAlpha(tex);
                return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            }
            catch (Exception e)
            {
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Sticker] '" + name + ".png' load failed: " + e.Message);
                return null;
            }
        }

        // The source sheet's die-cut edges have a band of semi-transparent
        // anti-aliased pixels around every sticker (confirmed while cropping
        // — a meaningful chunk of pixels sit between fully transparent and
        // fully opaque). Composited over a dark menu/HUD background, that
        // soft edge reads as a faint grey/white halo — i.e. exactly the
        // "background" a small sticker shouldn't have. Hard-cutting alpha
        // at a threshold turns that halo into a crisp, fully-transparent
        // edge instead.
        private static void HardenAlpha(Texture2D tex)
        {
            const byte THRESHOLD = 120;
            Color32[] px = tex.GetPixels32();
            for (int i = 0; i < px.Length; i++)
            {
                if (px[i].a < THRESHOLD) px[i] = new Color32(0, 0, 0, 0);
                else px[i].a = 255;
            }
            tex.SetPixels32(px);
            tex.Apply(false);
        }
    }
}
