using UnityEngine;

namespace KaleidoscopeEndless
{
    public enum GadgetType { None, PlungeOLaunch, MusicBox, FortuneCookie, YoYo, FlashCamera, SpareBattery, ToyParrot } // append new ones: the equipped pick is saved as a number

    /// <summary>
    /// Endless-only gadgets: one is picked in the menu before a run (see
    /// GadgetSubmenu) and used in-run with the 1 key (see GadgetController).
    /// Separate from shop Power Ups — both work in the same run.
    /// </summary>
    public static class Gadgets
    {
        public static readonly GadgetType[] All = { GadgetType.PlungeOLaunch, GadgetType.MusicBox, GadgetType.FortuneCookie, GadgetType.YoYo, GadgetType.FlashCamera, GadgetType.SpareBattery, GadgetType.ToyParrot };

        // ── DLC gumballs ─────────────────────────────────────────────────────
        // Every gadget added AFTER a version ships is that version's DLC: it
        // comes out of the Gumball Machine as a rainbow gumball. When the next
        // version ships its own wave of DLC gadgets, move the old ones out of
        // this list — they lose the rainbow and become part of the regular pool.
        public const string DLC_VERSION = "3.0";
        private static readonly GadgetType[] DlcGadgets = { GadgetType.ToyParrot };

        public static bool IsDlc(GadgetType g) { return System.Array.IndexOf(DlcGadgets, g) >= 0; }

        /// <summary>A rainbow color for t (any number; loops every 1).</summary>
        public static Color Rainbow(float t) { return Color.HSVToRGB(Mathf.Repeat(t, 1f), 0.75f, 1f); }

        /// <summary>The menu pick (only ever an owned gadget, or None).</summary>
        public static GadgetType Selected
        {
            get { return (GadgetType)PlayerPrefs.GetInt(SELECTED_KEY, (int)GadgetType.None); }
            set { PlayerPrefs.SetInt(SELECTED_KEY, (int)value); PlayerPrefs.Save(); }
        }
        private const string SELECTED_KEY = "KE_GADGET"; // remembered between sessions

        // ── Ownership: gadgets come out of the hub's Gumball Machine ─────────
        public const int GUMBALL_PRICE = 150;

        /// <summary>Its line in Shop.txt (1 = owned).</summary>
        public static string OwnKey(GadgetType g) { return "Gadget " + g; }

        public static bool IsOwned(GadgetType g) { return g == GadgetType.None || Shop.IsOwned(OwnKey(g)); }

        /// <summary>Selected, or None if it isn't owned (e.g. an old pick from before gadgets were locked).</summary>
        public static GadgetType SelectedIfOwned { get { return IsOwned(Selected) ? Selected : GadgetType.None; } }

        public enum GumballResult { Got, NotEnoughShards, HaveThemAll }

        /// <summary>150 shards for a random gadget you don't have yet. Nothing is charged if you already have them all.</summary>
        public static GumballResult BuyGumball(out GadgetType got)
        {
            got = GadgetType.None;
            var pool = new System.Collections.Generic.List<GadgetType>();
            foreach (var g in All) if (!IsOwned(g)) pool.Add(g);
            if (pool.Count == 0) return GumballResult.HaveThemAll;
            if (Shards.Balance < GUMBALL_PRICE) return GumballResult.NotEnoughShards;

            got = pool[Random.Range(0, pool.Count)];
            Shop.SpendShards(GUMBALL_PRICE);
            Shop.Unlock(OwnKey(got));
            if (Selected == GadgetType.None) Selected = got; // first gadget: equip it right away
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Gumball] Got " + got + " for " + GUMBALL_PRICE + " shards.");
            return GumballResult.Got;
        }

        public static Color GumballColor(GadgetType g)
        {
            switch (g)
            {
                case GadgetType.PlungeOLaunch: return new Color(1f, 0.25f, 0.25f);
                case GadgetType.MusicBox: return new Color(0.4f, 0.6f, 1f);
                case GadgetType.FortuneCookie: return new Color(1f, 0.8f, 0.25f);
                case GadgetType.YoYo: return new Color(0.4f, 1f, 0.5f);
                case GadgetType.FlashCamera: return new Color(1f, 0.45f, 0.9f);
                case GadgetType.SpareBattery: return new Color(0.95f, 0.95f, 0.95f);
                case GadgetType.ToyParrot: return Rainbow(Time.unscaledTime * 0.35f); // DLC: rainbow
                default: return Color.white;
            }
        }
        /// <summary>What the current/next run actually uses — copied from Selected at launch, None for Papa's 10th Birthday!.</summary>
        public static GadgetType RunGadget = GadgetType.None;

        public const float PLUNGE_COOLDOWN = 60f;
        public const float MUSIC_BOX_COOLDOWN = 100f;
        /// <summary>Fortune Cookie's cooldown = this many times how long Tattletail's hunger lasts, so the fridge still matters.</summary>
        public const float FORTUNE_COOLDOWN_HUNGERS = 1.5f;
        public const float HUNGER_FULL_SECONDS = 60f; // the game's own value, before modifiers
        public const float YOYO_COOLDOWN = 60f;
        public const int YOYO_USES = 2;
        public const float CAMERA_COOLDOWN = 60f;
        public const float CAMERA_RANGE = 10f;     // Mama must be this close, in front of you, not behind a wall
        public const float CAMERA_RETURN_DELAY = 8f; // then she reappears somewhere else
        /// <summary>Spare Battery's cooldown = this many times how long a full Tattletail charge lasts in this run.</summary>
        public const float BATTERY_COOLDOWN_CHARGES = 2f;
        public const float BATTERY_FULL_CHARGE_SECONDS = 125f; // the game's own value, before modifiers
        public const float PARROT_COOLDOWN = 60f;
        public const float PARROT_COOLDOWN_VERSUS = 80f; // nerfed in Versus only (a real Mama player gets dragged around)
        public const float PARROT_DURATION = 15f;  // how long it cries before it disappears

        public const float MUSIC_BOX_PAUSE = 20f;   // Mama stays away this long
        public const float MUSIC_BOX_LINGER = 15f;  // then the tune stays "stuck in your head" this long

        public static string Name(GadgetType g)
        {
            switch (g)
            {
                case GadgetType.PlungeOLaunch: return "Plunge-O-Launch";
                case GadgetType.MusicBox: return "Music Box";
                case GadgetType.FortuneCookie: return "Fortune Cookie";
                case GadgetType.YoYo: return "Yo-Yo";
                case GadgetType.FlashCamera: return "Flash Camera";
                case GadgetType.SpareBattery: return "Spare Battery";
                case GadgetType.ToyParrot: return "Toy Parrot";
                default: return "None";
            }
        }

        public static string ShortName(GadgetType g)
        {
            switch (g)
            {
                case GadgetType.PlungeOLaunch: return "PLUNGE";
                case GadgetType.MusicBox: return "MUSIC\nBOX";
                case GadgetType.FortuneCookie: return "COOKIE";
                case GadgetType.YoYo: return "YO-YO";
                case GadgetType.FlashCamera: return "CAMERA";
                case GadgetType.SpareBattery: return "BATTERY";
                case GadgetType.ToyParrot: return "PARROT";
                default: return "";
            }
        }

        public static float Cooldown(GadgetType g)
        {
            switch (g)
            {
                case GadgetType.PlungeOLaunch: return PLUNGE_COOLDOWN;
                case GadgetType.MusicBox: return MUSIC_BOX_COOLDOWN;
                case GadgetType.FortuneCookie: return HUNGER_FULL_SECONDS * FORTUNE_COOLDOWN_HUNGERS; // the real one is worked out live (see GadgetController)
                case GadgetType.YoYo: return YOYO_COOLDOWN;
                case GadgetType.FlashCamera: return CAMERA_COOLDOWN;
                case GadgetType.ToyParrot: return VersusMode.IsSurvivor ? PARROT_COOLDOWN_VERSUS : PARROT_COOLDOWN;
                case GadgetType.SpareBattery: return BATTERY_FULL_CHARGE_SECONDS * BATTERY_COOLDOWN_CHARGES; // the real one is worked out live (see GadgetController)
                default: return 0f;
            }
        }

        /// <summary>Uses before it has to recharge. The cooldown starts on the first use and refills all of them.</summary>
        public static int Uses(GadgetType g) { return g == GadgetType.YoYo ? YOYO_USES : 1; }

        /// <summary>Player-facing description shown in the gadget menu.</summary>
        public static string Description(GadgetType g)
        {
            switch (g)
            {
                case GadgetType.PlungeOLaunch:
                    return "Aim at the FLOOR and press 1: a spring-loaded plunger sticks to it and yanks you straight there. " +
                           "Great for getting around fast, but the loud SQUELCH tells Mama where you are.\nCooldown: " + PLUNGE_COOLDOWN + "s";
                case GadgetType.MusicBox:
                    return "Press 1 to play a lullaby that sends Mama away for " + MUSIC_BOX_PAUSE + " seconds. " +
                           "The tune gets stuck in your head afterward: you move slower and everything sounds muffled for a while.\nCooldown: " + MUSIC_BOX_COOLDOWN + "s";
                case GadgetType.FortuneCookie:
                    return "Press 1 to feed Tattletail a fortune cookie (fills its hunger) and read your fortune. " +
                           "Sometimes it hints at a hidden gift coin. Sometimes it just knows things about this house." +
                           "\nCooldown: 1.5x as long as Tattletail's hunger lasts (about " + Mathf.RoundToInt(HUNGER_FULL_SECONDS * FORTUNE_COOLDOWN_HUNGERS) + "s, shorter with Always Hungry)";
                case GadgetType.YoYo:
                    return "Aim at the FLOOR and press 1: the yo-yo whips out and pulls you there. Shorter reach than the Plunge-O-Launch, " +
                           "but it's quiet: Mama won't hear it. " + YOYO_USES + " uses; the recharge starts after the first one.\nCooldown: " + YOYO_COOLDOWN + "s";
                case GadgetType.FlashCamera:
                    return "Press 1 when Mama is close and in front of you: the flash sends her away, and she turns up somewhere else a few seconds later. " +
                           "If she isn't there, the flash is wasted.\nCooldown: " + CAMERA_COOLDOWN + "s";
                case GadgetType.SpareBattery:
                    return "Press 1 to fully charge Tattletail on the spot, no charging station needed." +
                           "\nCooldown: twice as long as a full charge lasts (about " + Mathf.RoundToInt(BATTERY_FULL_CHARGE_SECONDS * BATTERY_COOLDOWN_CHARGES / 60f) + " minutes, shorter with Batteries Not Included)";
                case GadgetType.ToyParrot:
                    return "Press 1 to hold the parrot, aim at the floor, and press 1 again to set it down (right-click cancels). " +
                           "For " + PARROT_DURATION + " seconds it LOUDLY imitates a crying Tattletail: Mama goes to it and ignores you completely, " +
                           "then it disappears and she's back to hunting you.\nCooldown: " + PARROT_COOLDOWN + "s";
                default: return "No gadget. Get one from the GUMBALL MACHINE in the shop: " + GUMBALL_PRICE + " shards for a random gadget you don't have yet.";
            }
        }

        // ── Fortunes ─────────────────────────────────────────────────────────
        public const float FORTUNE_LOOT_CHANCE = 0.35f;

        public static readonly string[] LootFortunes =
        {
            "Good things hide where you least expect them.",
            "Fortune favors the brave. Check the corners.",
            "Someone left you a present.",
            "Something shiny is waiting for you.",
        };

        public static readonly string[] LoreFortunes =
        {
            "Mama was a good toy once. Everyone forgets that part.",
            "The kaleidoscope remembers every color it swallowed.",
            "Waygetter Electronics never recalled them all.",
            "Tattletail can hear you, even when you think it's asleep.",
            "Christmas 1998 never really ended in this house.",
            "Every tape you rewind takes her a little further back.",
            "Some doors are closed for a reason.",
            "Brush your friend before the dark gets hungry.",
            "She doesn't hate you. She just wants her babies back.",
            "Lucky numbers: 6, 10, 98.",
        };
    }

    /// <summary>Gadget sounds, generated in code — no audio files needed.</summary>
    public static class GadgetAudio
    {
        private const int RATE = 44100;
        private static AudioClip _lullaby, _squelch, _crack, _whirr, _shutter, _zap, _parrotCry;

        public static AudioClip Lullaby { get { return _lullaby != null ? _lullaby : (_lullaby = MakeLullaby()); } }
        public static AudioClip Squelch { get { return _squelch != null ? _squelch : (_squelch = MakeSquelch()); } }
        public static AudioClip Crack { get { return _crack != null ? _crack : (_crack = MakeCrack()); } }
        public static AudioClip Whirr { get { return _whirr != null ? _whirr : (_whirr = MakeWhirr()); } }
        public static AudioClip Shutter { get { return _shutter != null ? _shutter : (_shutter = MakeShutter()); } }
        public static AudioClip Zap { get { return _zap != null ? _zap : (_zap = MakeZap()); } }

        /// <summary>The game's own crying Tattletail (the parrot plays it pitched up), or a made-up wail if it can't be found.</summary>
        public static AudioClip ParrotCry { get { return _parrotCry != null ? _parrotCry : (_parrotCry = FindGameClip(new[] { "tt_k_cry", "tt_scared", "tt_me_scared", "tt_wheres_mama" }) ?? MakeWail()); } }

        private static AudioClip FindGameClip(string[] names)
        {
            foreach (string n in names)
            {
                try
                {
                    var item = AudioController.GetAudioItem(n);
                    if (item != null && item.subItems != null)
                        foreach (var sub in item.subItems)
                            if (sub != null && sub.Clip != null) { KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Parrot] Using the game's '" + n + "' sound."); return sub.Clip; }
                }
                catch { }
                foreach (var clip in Resources.FindObjectsOfTypeAll<AudioClip>())
                    if (clip != null && clip.name == n) { KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Parrot] Using the game's '" + n + "' clip."); return clip; }
            }
            KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Parrot] No crying sound found in the game — using a made-up wail.");
            return null;
        }

        // Fallback: a warbling, sobbing wail.
        private static AudioClip MakeWail()
        {
            int len = (int)(RATE * 2f);
            var data = new float[len];
            float phase = 0f;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)RATE;
                float sob = Mathf.Repeat(t, 0.5f);
                float f = Mathf.Lerp(900f, 520f, sob / 0.5f) + Mathf.Sin(t * 2f * Mathf.PI * 9f) * 40f;
                phase += 2f * Mathf.PI * f / RATE;
                float env = Mathf.Min(1f, sob * 30f) * Mathf.Clamp01((0.5f - sob) * 6f);
                data[i] = (Mathf.Sin(phase) + 0.3f * Mathf.Sin(phase * 2f)) * env * 0.35f;
            }
            var wail = AudioClip.Create("KE_Wail", len, 1, RATE, false);
            wail.SetData(data, 0);
            return wail;
        }

        // A short, original music-box tune on a C-major pentatonic scale.
        private static AudioClip MakeLullaby()
        {
            float[] notes = { 659.3f, 784f, 880f, 784f, 659.3f, 587.3f, 523.3f, 587.3f, 659.3f, 784f, 659.3f, 587.3f, 523.3f, 440f, 523.3f, 587.3f };
            const float noteLen = 0.38f;
            int per = (int)(RATE * noteLen);
            var data = new float[per * notes.Length];
            for (int n = 0; n < notes.Length; n++)
            {
                float f = notes[n];
                for (int i = 0; i < per; i++)
                {
                    float t = i / (float)RATE;
                    float env = Mathf.Exp(-t * 5f) * Mathf.Min(1f, t * 400f);
                    float s = Mathf.Sin(2f * Mathf.PI * f * t) + 0.35f * Mathf.Sin(2f * Mathf.PI * f * 2f * t) + 0.12f * Mathf.Sin(2f * Mathf.PI * f * 3.01f * t);
                    data[n * per + i] = s * env * 0.28f;
                }
            }
            var clip = AudioClip.Create("KE_Lullaby", data.Length, 1, RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        // Wet suction pop: a falling low tone plus a burst of noise.
        private static AudioClip MakeSquelch()
        {
            int len = (int)(RATE * 0.4f);
            var data = new float[len];
            var rng = new System.Random(7);
            float phase = 0f;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)RATE;
                float f = Mathf.Lerp(320f, 70f, t / 0.4f);
                phase += 2f * Mathf.PI * f / RATE;
                float env = Mathf.Exp(-t * 9f) * Mathf.Min(1f, t * 300f);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t * 25f);
                data[i] = (Mathf.Sin(phase) * 0.8f + noise * 0.5f) * env * 0.7f;
            }
            var clip = AudioClip.Create("KE_Squelch", len, 1, RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        // A soft, quick spinning whirr — the yo-yo (quiet on purpose).
        private static AudioClip MakeWhirr()
        {
            int len = (int)(RATE * 0.5f);
            var data = new float[len];
            float phase = 0f;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)RATE;
                float f = Mathf.Lerp(420f, 180f, t / 0.5f);
                phase += 2f * Mathf.PI * f / RATE;
                float wobble = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 28f * t); // the spin
                float env = Mathf.Min(1f, t * 60f) * Mathf.Clamp01((0.5f - t) * 6f);
                data[i] = Mathf.Sin(phase) * wobble * env * 0.25f;
            }
            var clip = AudioClip.Create("KE_Whirr", len, 1, RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        // Camera: a sharp shutter click, then the flash's high charging whine.
        private static AudioClip MakeShutter()
        {
            int len = (int)(RATE * 0.7f);
            var data = new float[len];
            var rng = new System.Random(11);
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)RATE;
                float click = (t < 0.012f || (t > 0.05f && t < 0.06f)) ? (float)(rng.NextDouble() * 2.0 - 1.0) * 0.9f : 0f;
                float whine = t > 0.08f ? Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(2400f, 5200f, (t - 0.08f) / 0.62f) * t) * 0.12f * Mathf.Clamp01((0.7f - t) * 4f) : 0f;
                data[i] = click + whine;
            }
            var clip = AudioClip.Create("KE_Shutter", len, 1, RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        // Battery: a snap into place and an electric hum that rises.
        private static AudioClip MakeZap()
        {
            int len = (int)(RATE * 0.6f);
            var data = new float[len];
            var rng = new System.Random(5);
            float phase = 0f;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)RATE;
                float snap = t < 0.02f ? (float)(rng.NextDouble() * 2.0 - 1.0) * 0.8f : 0f;
                phase += 2f * Mathf.PI * Mathf.Lerp(90f, 260f, t / 0.6f) / RATE;
                float buzz = Mathf.Sign(Mathf.Sin(phase)) * 0.12f * Mathf.Clamp01((t - 0.03f) * 20f) * Mathf.Clamp01((0.6f - t) * 5f);
                data[i] = snap + buzz;
            }
            var clip = AudioClip.Create("KE_Zap", len, 1, RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        // A few quick crunchy clicks — a cookie snapping open.
        private static AudioClip MakeCrack()
        {
            int len = (int)(RATE * 0.22f);
            var data = new float[len];
            var rng = new System.Random(3);
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)RATE;
                float burst = (t < 0.03f || (t > 0.07f && t < 0.09f) || (t > 0.13f && t < 0.145f)) ? 1f : 0.15f;
                data[i] = (float)(rng.NextDouble() * 2.0 - 1.0) * burst * Mathf.Exp(-t * 12f) * 0.6f;
            }
            var clip = AudioClip.Create("KE_Crack", len, 1, RATE, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
