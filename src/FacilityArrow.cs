using UnityEngine;
using UnityEngine.UI;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// Endless in the Facility: an arrow on the screen pointing at the nearest objective (in a Chaos Round, the nearest of the objectives
    /// that are on). It also says how far, and whether it is upstairs or downstairs.
    /// </summary>
    public class FacilityArrow : MonoBehaviour
    {
        private RectTransform _arrow;
        private Text _label;
        private GameObject _ui;

        private void Start()
        {
            _ui = new GameObject("KE_FacilityArrow");
            _ui.transform.SetParent(transform, false);
            var canvas = _ui.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 270;
            var scaler = _ui.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var a = new GameObject("Arrow", typeof(RectTransform), typeof(Image), typeof(Outline));
            a.transform.SetParent(_ui.transform, false);
            _arrow = (RectTransform)a.transform;
            _arrow.anchorMin = _arrow.anchorMax = new Vector2(0.5f, 0f);
            _arrow.pivot = new Vector2(0.5f, 0.5f);
            _arrow.anchoredPosition = new Vector2(0f, 190f);
            _arrow.sizeDelta = new Vector2(70f, 70f);
            var img = a.GetComponent<Image>();
            img.sprite = ArrowSprite();
            img.color = new Color(1f, 0.85f, 0.3f, 0.95f);
            img.raycastTarget = false;
            a.GetComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);

            var t = new GameObject("Label", typeof(RectTransform), typeof(Text), typeof(Shadow));
            t.transform.SetParent(_ui.transform, false);
            var rt = (RectTransform)t.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 135f);
            rt.sizeDelta = new Vector2(500f, 40f);
            _label = t.GetComponent<Text>();
            _label.font = MainMenuButtonInjector.RealFont != null ? MainMenuButtonInjector.RealFont : Resources.GetBuiltinResource<Font>("Arial.ttf");
            _label.fontSize = 26;
            _label.alignment = TextAnchor.MiddleCenter;
            _label.color = new Color(1f, 0.9f, 0.5f);
            _label.raycastTarget = false;
            t.GetComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.9f);
            _ui.SetActive(false);
        }

        private static Sprite _sprite;

        private static Sprite ArrowSprite()
        {
            if (_sprite != null) return _sprite;
            const int N = 64;
            var tex = new Texture2D(N, N, TextureFormat.ARGB32, false);
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    // an arrow pointing up: a head (triangle) over a short shaft
                    float u = (x + 0.5f) / N - 0.5f, v = (y + 0.5f) / N;
                    bool head = v > 0.45f && Mathf.Abs(u) < (1f - v) * 0.9f;
                    bool shaft = v <= 0.5f && v > 0.05f && Mathf.Abs(u) < 0.11f;
                    tex.SetPixel(x, y, head || shaft ? Color.white : Color.clear);
                }
            tex.Apply();
            _sprite = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f));
            return _sprite;
        }

        private static void Add(System.Collections.Generic.List<Vector3> list, Vector3? p)
        {
            if (p.HasValue) list.Add(p.Value);
        }

        private void Update()
        {
            var run = EndlessRunManager.Instance;
            var fps = RM.fpsController;
            if (_ui == null) return;
            if (run == null || !run.RunActive || fps == null || OverlayPanel.AnyOpen) { _ui.SetActive(false); return; }

            // everything that is an objective right now (a Chaos Round has two)
            var targets = new System.Collections.Generic.List<Vector3>();
            if (VaseObjective.Instance != null) targets.AddRange(VaseObjective.Instance.RemainingPositions());
            if (CandleObjective.Instance != null) targets.AddRange(CandleObjective.Instance.RemainingPositions());
            if (TattletailFriendObjective.Instance != null) targets.AddRange(TattletailFriendObjective.Instance.RemainingPositions());
            if (TapeObjective.Instance != null) targets.AddRange(TapeObjective.Instance.RemainingPositions());
            if (WashingMachineObjective.Instance != null) Add(targets, WashingMachineObjective.Instance.ActivePosition);
            if (SpeakerObjective.Instance != null) Add(targets, SpeakerObjective.Instance.ActivePosition);
            if (FuseBoxObjective.Instance != null) Add(targets, FuseBoxObjective.Instance.ActivePosition);
            if (PhoneObjective.Instance != null) Add(targets, PhoneObjective.Instance.ActivePosition);
            if (targets.Count == 0) { _ui.SetActive(false); return; }

            Vector3 me = fps.transform.position;
            Vector3 best = targets[0];
            float bestScore = float.MaxValue;
            for (int i = 0; i < targets.Count; i++)
            {
                Vector3 d = targets[i] - me;
                float score = new Vector2(d.x, d.z).magnitude + Mathf.Abs(d.y) * 2f; // (a floor away counts as further)
                if (score < bestScore) { bestScore = score; best = targets[i]; }
            }

            Vector3 flat = best - me;
            float dy = flat.y;
            flat.y = 0f;
            Vector3 fwd = fps.transform.forward;
            fwd.y = 0f;
            float angle = flat.sqrMagnitude < 0.01f || fwd.sqrMagnitude < 0.0001f ? 0f : Vector3.Angle(fwd, flat) * Mathf.Sign(Vector3.Cross(fwd, flat).y);
            _arrow.localRotation = Quaternion.Euler(0f, 0f, -angle); // (turns clockwise on the screen when the target is to the right)
            string floor = dy > 2f ? "  (upstairs)" : dy < -2f ? "  (downstairs)" : "";
            _label.text = Mathf.RoundToInt(new Vector2(best.x - me.x, best.z - me.z).magnitude) + " m" + floor;
            _ui.SetActive(true);
        }
    }
}
