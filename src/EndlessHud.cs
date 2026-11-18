using UnityEngine;
using UnityEngine.UI;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// Small hand-built overlay canvas (same from-scratch-Canvas recipe the
    /// old StatsUI used, not a cloned vanilla panel — nothing here needs
    /// vanilla's busy background) for a top-right run timer, visible
    /// whenever RunActive. Created once per Endless run from
    /// SceneBootstrap, same lifecycle as EndlessRunManager/MamaController.
    /// PhoneObjective's task text goes through the normal HUD quest-text
    /// channel (RM.hud.SetNewQuest via EndlessRunManager), not a separate
    /// overlay here — this stays a single small corner readout.
    /// </summary>
    public class EndlessHud : MonoBehaviour
    {
        public static EndlessHud Instance { get; private set; }

        private Text _timerText;
        private Text _powerUpStatusText;
        private Text _abilityHintText;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            BuildUI();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            var run = EndlessRunManager.Instance;
            bool active = run != null && run.RunActive && !run.BirthdayMode; // no run timer in Papa's 10th Birthday!
            if (_timerText == null) return;

            _timerText.gameObject.SetActive(active && !EndlessTraits.HideHud);
            if (!active) return;

            int mins = (int)(run.RunTime / 60);
            int secs = (int)(run.RunTime % 60);
            _timerText.text = string.Format("{0:D2}:{1:D2}", mins, secs);

            UpdatePowerUpStatus();
        }

        // Below the run timer: Strong Battery's countdown takes priority
        // over Totems (both are Power Up state, but only one is likely to
        // matter at a glance at any given moment) — falls back to showing
        // held totems, then hides entirely once neither applies.
        private void UpdatePowerUpStatus()
        {
            if (_powerUpStatusText == null) return;
            var pu = PowerUpObjective.Instance;
            if (pu == null)
            {
                _powerUpStatusText.gameObject.SetActive(false);
                return;
            }

            if (pu.StrongBatteryTimeRemaining > 0f)
            {
                _powerUpStatusText.gameObject.SetActive(true);
                _powerUpStatusText.text = "Strong Battery: " + Mathf.CeilToInt(pu.StrongBatteryTimeRemaining) + "s";
            }
            else if (pu.TotemCount > 0)
            {
                _powerUpStatusText.gameObject.SetActive(true);
                _powerUpStatusText.text = "Totems: " + pu.TotemCount;
            }
            else
            {
                _powerUpStatusText.gameObject.SetActive(false);
            }
        }

        /// <summary>Shown while standing near an uncollected Power Up coin — see PowerUpObjective.</summary>
        public void ShowAbilityHint(string abilityName)
        {
            if (_abilityHintText == null) return;
            _abilityHintText.gameObject.SetActive(true);
            _abilityHintText.text = abilityName;
        }

        public void HideAbilityHint()
        {
            if (_abilityHintText == null) return;
            _abilityHintText.gameObject.SetActive(false);
        }

        private void BuildUI()
        {
            var canvasGO = new GameObject("KE_EndlessHudCanvas");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900; // below StatsUI-style full-screen panels, above normal gameplay
            canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

            _timerText = MakeText(canvasGO, "TimerText",
                new Vector2(0.82f, 0.90f), new Vector2(0.99f, 0.98f),
                22, new Color(0.9f, 0.85f, 0.65f, 0.9f), TextAnchor.UpperRight);
            _timerText.gameObject.SetActive(false);

            _powerUpStatusText = MakeText(canvasGO, "PowerUpStatusText",
                new Vector2(0.72f, 0.855f), new Vector2(0.99f, 0.895f),
                16, new Color(0.65f, 0.9f, 0.7f, 0.9f), TextAnchor.UpperRight);
            _powerUpStatusText.gameObject.SetActive(false);

            _abilityHintText = MakeText(canvasGO, "AbilityHintText",
                new Vector2(0.3f, 0.1f), new Vector2(0.7f, 0.16f),
                24, new Color(1f, 0.9f, 0.6f, 0.95f), TextAnchor.LowerCenter);
            _abilityHintText.gameObject.SetActive(false);
        }

        private static Font GetFont()
        {
            Font f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (f != null) return f;
            var existing = Object.FindObjectOfType<Text>();
            return existing != null ? existing.font : null;
        }

        private Text MakeText(GameObject parent, string name, Vector2 anchorMin, Vector2 anchorMax, int fontSize, Color color, TextAnchor alignment)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<Text>();
            t.font = GetFont();
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = alignment;
            return t;
        }
    }
}
