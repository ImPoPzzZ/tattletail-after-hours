using UnityEngine;
using UnityEngine.UI;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// The camcorder's battery. It drains (about five minutes a pack); under 15% it beeps every few seconds,
    /// and every beep is a noise. You carry spares (3 to start; more lie around the building). R swaps one in:
    /// you tilt the camcorder to its back, the empty pack slides out and drops (clack), a fresh one clicks in,
    /// and it boots with a beep. The picture is off while the battery is out. The clack and the click are
    /// noises too. The charge shows as a battery icon on the camcorder's own screen, like a real one.
    /// </summary>
    public partial class FactoryCamcorder
    {
        public static FactoryCamcorder Instance { get; private set; }

        private const float DRAIN_NIGHT = 1f / 200f; // night vision (the infrared lamp): a pack lasts ~3 minutes
        private const float DRAIN_DAY = 1f / 600f;   // regular: ~10 minutes
        private bool _night = true;                  // left click while looking through it switches
        private const float LOW = 0.15f;
        private const float SWAP_TIME = 2.1f;

        private float _charge = 1f;
        private int _spares = 3;
        private float _swapT = -1f;                 // < 0: not swapping
        private float _nextBeep;
        private bool _powered = true, _clacked, _clicked, _booted;
        private Transform _cam, _pack, _oldPack;
        private Vector3 _packHome;
        private float _jamUntil, _nextNightNoise;
        private const float NIGHT_NOISE_EVERY = 2f;
        private const float BATTERY_RESPAWN = 60f;   // a pack you take lies there again a minute later
        private readonly System.Collections.Generic.List<Interactable> _taken = new System.Collections.Generic.List<Interactable>();
        private readonly System.Collections.Generic.List<float> _takenAt = new System.Collections.Generic.List<float>();
        /// <summary>Versus: the hunter jams the camcorder; it is dead for this long, then comes back by itself.</summary>
        /// <summary>Raised to your eye with night vision on and powered (the Facility Endless objective counts this).</summary>
        /// <summary>Night vision is switched on and the camcorder has power (the Facility makes Tattletail see this as light).</summary>
        public bool NightOn { get { return _night && _powered; } }

        public bool NightLooking { get { return _night && _powered && _raise > 0.9f; } }

        public void Jam(float seconds) { _jamUntil = Time.time + seconds; _up = false; }

        private AudioSource _audio;
        private AudioClip _clack, _click, _beep, _boot;
        private Image[] _bars;
        private GameObject _batteryIcon;
        private readonly System.Collections.Generic.List<Interactable> _pickups = new System.Collections.Generic.List<Interactable>();

        /// <summary>Called every frame before the picture: drains, beeps, runs the swap. True if the camcorder has power.</summary>
        private bool TickBattery()
        {
            for (int i = _taken.Count - 1; i >= 0; i--)
                if (Time.time >= _takenAt[i])
                {
                    if (_taken[i] != null) { _taken[i].gameObject.SetActive(true); _pickups.Add(_taken[i]); }
                    _taken.RemoveAt(i); _takenAt.RemoveAt(i);
                }
            if (_swapT < 0f && Input.GetKeyDown(KeyCode.R))
            {
                if (_spares <= 0) PlayOnly(_beep, 0.25f);           // nothing to swap in: a dull little beep
                else if (_charge < 0.98f) StartSwap();
            }
            UpdateSpareHud();
            if (Time.time < _jamUntil) { UpdateSpareHud(); return _powered = false; }   // Versus: the hunter jammed it
            if (_swapT >= 0f) { TickSwap(); return _powered = false; }

            _charge = Mathf.Max(0f, _charge - (_night ? DRAIN_NIGHT : DRAIN_DAY) * Time.deltaTime);
            if (_charge <= 0f) return _powered = false;                 // dead: it stops recording until you swap
            if (_night && Time.time >= _nextNightNoise)               // night vision hums: a noise every 2 s while it is on
            {
                _nextNightNoise = Time.time + NIGHT_NOISE_EVERY;
                FactoryController.MakeNoise(RM.fpsController != null ? RM.fpsController.transform.position : Vector3.zero);
            }
            if (_night && _charge < LOW && Time.time >= _nextBeep)     // (regular mode doesn't beep)
            {
                _nextBeep = Time.time + 3f;
                Noise(_beep, 0.55f);                                 // low battery: beep... beep...
            }
            UpdateIcon();
            UpdateSpareHud();
            return _powered = true;
        }

        private void StartSwap()
        {
            _swapT = 0f;
            _clacked = _clicked = _booted = false;
            _up = false; // it comes down from your eye to change it
        }

        private void TickSwap()
        {
            _swapT += Time.deltaTime;
            float t = _swapT;
            // 0.35-1.0: the empty pack slides out the back and drops away.
            if (t >= 0.35f && !_clacked)
            {
                _clacked = true;
                Noise(_clack, 0.9f);
                _oldPack = Object.Instantiate(_pack.gameObject, _pack.parent).transform; // this one falls; the real one comes back full
                _oldPack.localPosition = _pack.localPosition;
                _pack.gameObject.SetActive(false);
            }
            if (_oldPack != null)
            {
                float o = Mathf.Clamp01((t - 0.35f) / 0.65f);
                _oldPack.localPosition = _packHome + new Vector3(0f, -0.9f * o * o * o, -0.06f * Mathf.Min(1f, o * 3f));
                _oldPack.localRotation = Quaternion.Euler(140f * o * o, 0f, 30f * o);
                if (o >= 1f) { Destroy(_oldPack.gameObject); _oldPack = null; }
            }
            // 1.05-1.45: the fresh one comes up from below and slides home; click.
            if (t >= 1.05f)
            {
                _pack.gameObject.SetActive(true);
                float n = Mathf.Clamp01((t - 1.05f) / 0.4f);
                float ne = 1f - (1f - n) * (1f - n);
                _pack.localPosition = _packHome + new Vector3(0.02f * (1f - ne), -0.1f * (1f - ne), -0.06f * (1f - ne));
                if (n >= 1f && !_clicked) { _clicked = true; Noise(_click, 0.9f); _charge = 1f; _spares--; }
            }
            if (t >= 1.85f && !_booted) { _booted = true; PlayOnly(_boot, 0.45f); }
            if (t >= SWAP_TIME) { _swapT = -1f; _pack.localPosition = _packHome; }
        }

        // The tilt that shows you the camcorder's back while you swap (added on top of the usual pose).
        private void ApplySwapPose()
        {
            if (_swapT < 0f || _rig == null) return;
            float t = _swapT;
            float w = t < 0.35f ? t / 0.35f : t > 1.55f ? Mathf.Clamp01((SWAP_TIME - t) / 0.55f) : 1f;
            w = w * w * (3f - 2f * w);
            _rig.localPosition += new Vector3(-0.07f, 0.06f, -0.02f) * w;
            _rig.localRotation *= Quaternion.Euler(-20f * w, 62f * w, 18f * w);
        }

        // ── Noise: a sound, and (once Mama is here) something she hears ──
        private void Noise(AudioClip clip, float volume)
        {
            PlayOnly(clip, volume);
            FactoryController.MakeNoise(RM.fpsController != null ? RM.fpsController.transform.position : Vector3.zero);
        }

        private void PlayOnly(AudioClip clip, float volume)
        {
            if (_audio != null && clip != null) _audio.PlayOneShot(clip, volume);
        }

        // ── Spare batteries around the building ──────────────────────────────
        // Top right of the screen: how many spares you carry, and how to use one.
        private Text _spareText;
        private GameObject _hudGo;

        private void BuildSpareHud()
        {
            Font font = MainMenuButtonInjector.RealFont != null ? MainMenuButtonInjector.RealFont : Resources.GetBuiltinResource<Font>("Arial.ttf");
            _hudGo = new GameObject("KE_CamcorderHud");
            var canvas = _hudGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300;
            var scaler = _hudGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            var go = new GameObject("Spares", typeof(RectTransform), typeof(Text), typeof(Shadow));
            go.transform.SetParent(_hudGo.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-40f, -32f);
            rt.sizeDelta = new Vector2(600f, 100f);
            _spareText = go.GetComponent<Text>();
            _spareText.font = font;
            _spareText.alignment = TextAnchor.UpperRight;
            _spareText.supportRichText = true;
            _spareText.raycastTarget = false;
            _spareText.color = new Color(0.92f, 0.9f, 0.84f);
            go.GetComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.85f);
        }

        private void UpdateSpareHud()
        {
            if (_spareText == null) return;
            bool urge = _spares > 0 && _swapT < 0f && (_charge < LOW || !_powered) && (Time.time % 1f) < 0.6f;
            _spareText.text = "<size=34>BATTERIES  x" + _spares + "</size>\n" +
                (_spares > 0 ? "<size=24>" + (urge ? "<color=#FF6A5A>" : "") + "Press R to swap batteries" + (urge ? "</color>" : "") + "</size>"
                             : "<size=24><color=#AAAAAA>No spare batteries</color></size>");
        }

        private void SetupBatteries()
        {
            Instance = this;
            BuildSpareHud();
            _audio = _rig.gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;
            _clack = Synth(0); _click = Synth(1); _beep = Synth(2); _boot = Synth(3);
            foreach (var go in FactoryLayout.CamBatteries)
                if (go != null) _pickups.Add(PartyGameRunner.MakeInteractable(go, "Take the camcorder battery", 0.4f));
        }

        public void OnInteract(Interactable it)
        {
            if (!_pickups.Contains(it)) return;
            _pickups.Remove(it);
            _taken.Add(it); _takenAt.Add(Time.time + BATTERY_RESPAWN);
            it.gameObject.SetActive(false);
            _spares++;
            PlayOnly(_click, 0.5f);
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Camcorder] Picked up a battery (" + _spares + " spare).");
        }

        // ── The battery icon on the camcorder's screen ───────────────────────
        private void BuildBatteryIcon(Transform screen)
        {
            _batteryIcon = new GameObject("Battery icon", typeof(RectTransform));
            _batteryIcon.transform.SetParent(screen, false);
            var rt = (RectTransform)_batteryIcon.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-95f, -50f);
            rt.sizeDelta = new Vector2(110f, 44f);
            Rect(_batteryIcon.transform, "Case", new Vector2(0f, 0f), new Vector2(100f, 44f), new Color(0.8f, 1f, 0.82f, 0.9f));
            Rect(_batteryIcon.transform, "Case inside", new Vector2(0f, 0f), new Vector2(90f, 34f), new Color(0f, 0f, 0f, 0.85f));
            Rect(_batteryIcon.transform, "Tip", new Vector2(55f, 0f), new Vector2(8f, 18f), new Color(0.8f, 1f, 0.82f, 0.9f));
            _bars = new Image[4];
            for (int i = 0; i < 4; i++) _bars[i] = Rect(_batteryIcon.transform, "Bar", new Vector2(-33f + i * 22f, 0f), new Vector2(18f, 26f), new Color(0.8f, 1f, 0.82f, 0.9f));
        }

        private static Image Rect(Transform parent, string name, Vector2 pos, Vector2 size, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        private void UpdateIcon()
        {
            if (_bars == null) return;
            int lit = Mathf.CeilToInt(_charge * 4f);
            bool blink = _charge < LOW && (Time.time % 0.8f) < 0.4f;
            for (int i = 0; i < 4; i++) _bars[i].enabled = i < lit && !blink;
        }

        // ── Sounds, made here: 0 clack (pack out), 1 click (pack in), 2 beep, 3 boot chirp ──
        private static AudioClip Synth(int kind)
        {
            const int SR = 22050;
            int len = kind == 3 ? SR / 3 : kind == 2 ? SR / 8 : SR / 6;
            var d = new float[len];
            var r = new System.Random(40 + kind);
            const float TAU = Mathf.PI * 2f;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)SR, v = 0f;
                if (kind == 0) // plastic clack: a noise burst and a hollow knock
                    v = ((float)r.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 90f) * 0.7f + Mathf.Sin(t * 240f * TAU) * Mathf.Exp(-t * 35f) * 0.5f;
                else if (kind == 1) // a sharp click, and a tick a moment later
                    v = ((float)r.NextDouble() * 2f - 1f) * (Mathf.Exp(-t * 400f) + (t > 0.04f ? Mathf.Exp(-(t - 0.04f) * 500f) * 0.6f : 0f)) * 0.8f;
                else if (kind == 2) // the low-battery beep
                    v = Mathf.Sin(t * 2200f * TAU) * (t < 0.1f ? 1f : 0f) * 0.35f;
                else // the boot chirp: two quick rising tones
                    v = (t < 0.12f ? Mathf.Sin(t * 1500f * TAU) : t > 0.16f && t < 0.3f ? Mathf.Sin(t * 2250f * TAU) : 0f) * 0.3f;
                d[i] = Mathf.Clamp(v, -1f, 1f);
            }
            var clip = AudioClip.Create("KE_Camcorder" + kind, len, 1, SR, false);
            clip.SetData(d, 0);
            return clip;
        }
    }
}

namespace KaleidoscopeEndless
{
    // A finished hold on a battery pack in the Facility goes to the camcorder (nothing routed it there before: packs could not be taken).
    [HarmonyLib.HarmonyPatch(typeof(Interactable), "OnInteractComplete")]
    public static class Patch_Interactable_OnInteractComplete_CamBattery
    {
        static void Postfix(Interactable __instance, bool fromAutoComplete)
        {
            if (fromAutoComplete || !FactoryMode.Active || __instance == null || OverlayPanel.BlocksWorld) return;
            if (FactoryCamcorder.Instance != null) FactoryCamcorder.Instance.OnInteract(__instance);
        }
    }
}

namespace KaleidoscopeEndless
{
    // Endless in the Facility: Mama is off, and the game's own OnNoise stops early with nobody to hear it (no noise icon). The icon is still shown.
    [HarmonyLib.HarmonyPatch(typeof(MamaManager), "OnNoise")]
    public static class Patch_MamaManager_OnNoise_Facility
    {
        static bool Prefix(bool bigNoise)
        {
            if (!FactoryMode.EndlessPreview) return true;
            if (MamaController.Instance != null && MamaController.Instance.GetActiveMama() != null) return true; // Mama is out: the game's own noise (she hears it)
            if (RM.hud != null) RM.hud.OnNoise(bigNoise);
            return false;
        }
    }
}

namespace KaleidoscopeEndless
{
    // Tattletail is scared of the dark when the lights are off and the flashlight's light is low (the game reads FlashlightController.GetLightPercentage):
    // he shakes first, and screams a little later. In the Facility the camcorder's night vision counts as light, so with it on that reading is full and he is calm;
    // with it off he shakes and then screams, the game's own way.
    [HarmonyLib.HarmonyPatch(typeof(FlashlightController), "GetLightPercentage")]
    public static class Patch_FlashlightController_GetLightPercentage_NightVision
    {
        static void Postfix(ref float __result)
        {
            if (!FactoryMode.EndlessPreview || FactoryCamcorder.Instance == null) return;
            if (FactoryCamcorder.Instance.NightOn && __result < 1f) __result = 1f;
        }
    }
}

namespace KaleidoscopeEndless
{
    // In the Facility "above ground" means above its own ground floor (it sits at y 200), so the basement is Mama's other level, like the house's.
    [HarmonyLib.HarmonyPatch(typeof(MamaManager), "IsAboveGround")]
    public static class Patch_MamaManager_IsAboveGround_Factory
    {
        static bool Prefix(Vector3 pos, ref bool __result)
        {
            if (!FactoryMode.Active) return true;
            __result = pos.y - FactoryController.Origin.y > -0.2f;
            return false;
        }
    }
}
