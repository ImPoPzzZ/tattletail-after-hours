using UnityEngine;
using UnityEngine.UI;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// Found footage: you carry a camcorder in your right hand (through its strap, like a real handycam),
    /// always recording. No HUD: the proof is the blinking red REC light and the flip-out screen, which is a
    /// window: it shows exactly what's behind it, lined up with your view, in night vision (a second camera
    /// at your eyes with the screen's field of view, lit by an infrared lamp only it sees, drawn green and
    /// grainy). Hold the right mouse button to raise it in front of your eye and look through it (Outlast).
    /// Built from primitives in camera space: the hand (palm, jointed fingers, thumb), a jacket sleeve and
    /// the camcorder (body, lens, viewfinder, hand strap, buttons, the flip-out screen).
    /// </summary>
    public partial class FactoryCamcorder : MonoBehaviour
    {
        private Transform _rig;            // follows the player's camera
        private Camera _lens;              // what the camcorder sees
        private Transform _lensT;
        private RenderTexture _feed;
        private Light _ir, _screenGlow;
        private Renderer _recLed;
        private RawImage _grain;
        private Texture2D _grainTex;
        private float _nextFrame, _raise, _nextExposureCheck, _startTime;
        private bool _dumped;

        // Diagnostics (the screen stayed dark): one frame from the lens saved as BepInEx/camcorder_frame.png, plus what it saw.
        private void DumpFrame()
        {
            try
            {
                var prev = RenderTexture.active;
                RenderTexture.active = _feed;
                var t = new Texture2D(_feed.width, _feed.height, TextureFormat.RGB24, false);
                t.ReadPixels(new Rect(0, 0, _feed.width, _feed.height), 0, 0, false);
                t.Apply(false);
                RenderTexture.active = prev;
                string path = System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "camcorder_frame.png");
                System.IO.File.WriteAllBytes(path, t.EncodeToPNG());
                Color32[] px = t.GetPixels32();
                float sum = 0f; int lit = 0;
                for (int i = 0; i < px.Length; i++) { float v = (px[i].r + px[i].g + px[i].b) / 765f; sum += v; if (v > 0.02f) lit++; }
                Destroy(t);
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Camcorder] Frame saved to " + path + ": average " + (sum / px.Length).ToString("F4") + ", " + lit + "/" + px.Length +
                    " pixels lit. Lens at " + _lensT.position + " looking " + _lensT.forward + ", mask " + _lens.cullingMask + ", path " + _lens.actualRenderingPath +
                    ", created " + _feed.IsCreated() + ", gain " + _gain.ToString("F1") + ", IR " + _ir.intensity + "/" + _ir.range + ".");
            }
            catch (System.Exception e) { KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Camcorder] Frame dump failed: " + e.Message); }
        }
        private bool _up, _wasPowered = true;
        private RawImage _feedImg;
        private UnityEngine.PostProcessing.PostProcessingProfile _profile;
        private bool _savedEyeAdaptation;

        // The screen glows by itself, and the game's camera boosts exposure by +2.5..+5.5 stops for its dark
        // house (its brightness setting), which turned the screen pure white. So the screen is dimmed by exactly
        // that boost, and the camera's auto-exposure is off while you're in the factory.
        private static readonly Color Green = new Color(0.55f, 1f, 0.6f);

        // Auto gain, like a real night-vision camcorder: the lens has none of the game's exposure boost, so its
        // raw picture is very dark. Twice a second the picture's average brightness is measured and the gain
        // follows it toward a readable level (noisy and bright in the dark, calmer where there's light).
        private float _gain = 8f;
        private Texture2D _probe;
        private const float TARGET = 0.42f; // night vision reads bright and contrasty

        private void MeasureGain()
        {
            if (_feed == null) return;
            if (_probe == null) _probe = new Texture2D(32, 24, TextureFormat.RGB24, false);
            RenderTexture small = RenderTexture.GetTemporary(32, 24, 0);
            Graphics.Blit(_feed, small);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = small;
            _probe.ReadPixels(new Rect(0, 0, 32, 24), 0, 0, false);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(small);
            Color32[] px = _probe.GetPixels32();
            float sum = 0f;
            for (int i = 0; i < px.Length; i++) sum += (px[i].r + px[i].g + px[i].b) / 765f;
            float avg = Mathf.Max(0.002f, sum / px.Length);
            float want = Mathf.Clamp(TARGET / avg, 1f, 60f);
            _gain = Mathf.Lerp(_gain, want, 0.5f);
            MatchExposure();
        }

        private void MatchExposure()
        {
            if (_feedImg == null) return;
            float stops = 0f;
            if (_profile != null) stops = _profile.colorGrading.settings.basic.postExposure;
            float k = Mathf.Pow(2f, -stops) * _gain; // the camcorder's own gain, minus the boost the game camera adds on top
            if (!_powered) _feedImg.color = Color.black;                                  // no battery, no picture
            else if (_night) _feedImg.color = new Color(Green.r * k, Green.g * k, Green.b * k, 1f);
            else _feedImg.color = new Color(0.95f, 0.93f, 0.9f, 1f);                      // regular: what you'd see, no boost
            float g = !_powered ? 0f : _night ? Mathf.Pow(2f, -stops) * Mathf.Clamp(_gain * 0.25f, 1f, 6f) : Mathf.Pow(2f, -stops) * 0.6f; // more gain, more visible grain
            if (_grain != null) _grain.color = new Color(g, g, g, 1f);
            float l = Mathf.Pow(2f, -stops) * 3f;
            if (_ledOn != null) _ledOn.color = new Color(Mathf.Min(1f, l), 0.05f * l, 0.03f * l);
        }
        private Material _ledOn, _ledOff;

        // Where the camcorder sits in the view: low on the right, and raised so its screen is centered
        // 5 cm in front of your eye and fills it (the screen's center is at (-0.078, 0.004, 0.0405) in the rig).
        private static readonly Vector3 LowPos = new Vector3(0.14f, -0.19f, 0.34f), UpPos = new Vector3(0.078f, -0.004f, 0.0095f);
        private static readonly Vector3 LowRot = new Vector3(4f, -14f, 4f), UpRot = new Vector3(0f, 0f, 0f);
        private const float SCREEN_FOV = 58f; // the screen's height seen from 5 cm: what's behind it lines up

        private void Start()
        {
            Camera main = FactoryController.PlayerCam;
            if (main == null) { KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Camcorder] No player camera."); Destroy(this); return; }
            _startTime = Time.time;
            if (main.nearClipPlane > 0.02f) main.nearClipPlane = 0.02f; // the camcorder comes right up to your eye
            _rig = new GameObject("KE_Camcorder").transform;
            _rig.SetParent(main.transform, false);
            _rig.localPosition = LowPos;
            _rig.localRotation = Quaternion.Euler(LowRot);
            var ppb = main.GetComponent<UnityEngine.PostProcessing.PostProcessingBehaviour>();
            if (ppb != null && ppb.profile != null)
            {
                _profile = ppb.profile;
                _savedEyeAdaptation = _profile.eyeAdaptation.enabled;
                _profile.eyeAdaptation.enabled = false; // auto-exposure off in here (restored when you leave)
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Factory] Camcorder: camera exposure +" + _profile.colorGrading.settings.basic.postExposure + " stops, auto-exposure was " + _savedEyeAdaptation + ".");
            }
            Build();
            // The camcorder and hand live on the "Ignore Raycast" layer, which the lens skips (or the screen would film itself).
            foreach (var t in _rig.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 2;
            // Everything but the camcorder itself. (Not a copy of the player camera's mask: the player's view is
            // several cameras stacked, and the one we hang the camcorder on may only draw what's in your hands.)
            if (_lens != null) _lens.cullingMask = ~(1 << 2);
            SetupBatteries();
            foreach (var c in main.transform.root.GetComponentsInChildren<Camera>(true))
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Factory] Player camera '" + c.name + "' mask=" + c.cullingMask + " depth=" + c.depth + (c == main ? " (camcorder hangs here)" : ""));
        }

        private void OnDestroy()
        {
            if (_profile != null) _profile.eyeAdaptation.enabled = _savedEyeAdaptation;
            if (_rig != null) Destroy(_rig.gameObject);
            if (_lensT != null) Destroy(_lensT.gameObject);
            if (_feed != null) { _feed.Release(); Destroy(_feed); }
            if (_probe != null) Destroy(_probe);
            if (Instance == this) Instance = null;
            if (_hudGo != null) Destroy(_hudGo);
        }

        private void LateUpdate()
        {
            if (_rig == null) return;
            // Right click raises it to your eye; right click again lowers it.
            if (Input.GetMouseButtonDown(1)) _up = !_up;
            _raise = Mathf.MoveTowards(_raise, _up ? 1f : 0f, Time.deltaTime * 4f);
            if (Time.time >= _nextExposureCheck) { _nextExposureCheck = Time.time + 0.5f; if (_night && _powered) MeasureGain(); else MatchExposure(); }
            float e = _raise * _raise * (3f - 2f * _raise);
            _rig.localPosition = Vector3.Lerp(LowPos, UpPos, e) + new Vector3(0f, Mathf.Sin(Time.time * 1.3f) * 0.002f, 0f); // a hand isn't a tripod
            _rig.localRotation = Quaternion.Slerp(Quaternion.Euler(LowRot), Quaternion.Euler(UpRot), e);
            ApplySwapPose();
            bool on = TickBattery();

            // Looking through it: left click switches night vision / regular.
            if (on && _raise > 0.9f && Input.GetMouseButtonDown(0)) { _night = !_night; if (_night) Noise(_click, 0.3f); else PlayOnly(_click, 0.3f); MatchExposure(); } // switching night vision ON is a noise (the game's icon, and Mama hears it)

            // REC: blinks once a second, as long as it has power.
            if (_recLed != null) _recLed.sharedMaterial = on && (Time.time % 1f) < 0.6f ? _ledOn : _ledOff;

            // The picture: 24 frames a second. Night vision lights it with the infrared lamp (only while the lens renders).
            if (on && Time.time >= _nextFrame && _lens != null)
            {
                _nextFrame = Time.time + 1f / 24f;
                Color amb = RenderSettings.ambientLight;
                if (_night) { RenderSettings.ambientLight = new Color(0.06f, 0.06f, 0.06f); _ir.enabled = true; }
                _lens.Render();
                _ir.enabled = false;
                RenderSettings.ambientLight = amb;
                if (_grain != null) _grain.uvRect = new Rect(Random.value, Random.value, 1.6f, 1.2f); // fresh grain every frame
            }
            if (_screenGlow != null) { _screenGlow.enabled = on; _screenGlow.color = _night ? new Color(0.4f, 1f, 0.5f) : new Color(0.8f, 0.85f, 0.9f); _screenGlow.intensity = 0.15f + Random.value * 0.03f; }
            if (_powered != _wasPowered) { _wasPowered = _powered; MatchExposure(); }
        }

        // ── The model ────────────────────────────────────────────────────────
        private static Material Mat(Color c, float gloss) { return Mat_(c, gloss); }
        private static Material Mat_(Color c, float gloss) { return global::KaleidoscopeEndless.Mat.Lit(c, gloss); }

        private static GameObject Part(Transform parent, PrimitiveType type, string name, Vector3 pos, Vector3 scale, Vector3 euler, Material m)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>()); // no physics: it's in your hand
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.transform.localRotation = Quaternion.Euler(euler);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        private void Build()
        {
            Material plastic = Mat(new Color(0.07f, 0.07f, 0.08f), 0.55f);
            Material grey = Mat(new Color(0.28f, 0.29f, 0.31f), 0.6f);
            Material glass = Mat(new Color(0.02f, 0.03f, 0.05f), 0.97f);
            Material skin = Mat(new Color(0.8f, 0.6f, 0.48f), 0.3f);
            Material sleeve = FTex.Get("fabric", 0.15f, new Color(0.22f, 0.26f, 0.2f), 0.05f, 0f, 1f).Mat; // an old green jacket
            _ledOn = new Material(Shader.Find("Unlit/Color")) { color = new Color(1f, 0.08f, 0.05f) };
            _ledOff = Mat(new Color(0.18f, 0.03f, 0.03f), 0.6f);

            // The camcorder (lens toward +z). Body 7 x 8 x 13 cm.
            var cam = new GameObject("Camcorder").transform;
            cam.SetParent(_rig, false);
            Part(cam, PrimitiveType.Cube, "Body", new Vector3(0f, 0f, 0f), new Vector3(0.068f, 0.078f, 0.13f), Vector3.zero, plastic);
            Part(cam, PrimitiveType.Cube, "Body top", new Vector3(0f, 0.045f, -0.01f), new Vector3(0.058f, 0.014f, 0.1f), Vector3.zero, plastic);
            Part(cam, PrimitiveType.Cylinder, "Lens barrel", new Vector3(0.005f, 0.008f, 0.085f), new Vector3(0.056f, 0.022f, 0.056f), new Vector3(90f, 0f, 0f), grey);
            Part(cam, PrimitiveType.Cylinder, "Lens hood", new Vector3(0.005f, 0.008f, 0.108f), new Vector3(0.064f, 0.006f, 0.064f), new Vector3(90f, 0f, 0f), plastic);
            Part(cam, PrimitiveType.Cylinder, "Lens glass", new Vector3(0.005f, 0.008f, 0.112f), new Vector3(0.046f, 0.002f, 0.046f), new Vector3(90f, 0f, 0f), glass);
            Part(cam, PrimitiveType.Cylinder, "Viewfinder", new Vector3(0.012f, 0.047f, -0.075f), new Vector3(0.024f, 0.025f, 0.024f), new Vector3(90f, 0f, 0f), plastic);
            Part(cam, PrimitiveType.Cube, "Strap", new Vector3(0.042f, 0f, 0f), new Vector3(0.012f, 0.05f, 0.1f), Vector3.zero, Mat(new Color(0.12f, 0.11f, 0.1f), 0.2f));
            Part(cam, PrimitiveType.Cube, "Record button", new Vector3(0.02f, -0.02f, -0.068f), new Vector3(0.014f, 0.014f, 0.006f), Vector3.zero, Mat(new Color(0.55f, 0.08f, 0.06f), 0.5f));
            Part(cam, PrimitiveType.Cube, "Zoom rocker", new Vector3(0.012f, 0.053f, -0.03f), new Vector3(0.018f, 0.006f, 0.01f), Vector3.zero, grey);
            Part(cam, PrimitiveType.Cube, "Tape door", new Vector3(0.0345f, -0.01f, -0.02f), new Vector3(0.002f, 0.05f, 0.07f), Vector3.zero, grey);
            _cam = cam;
            _pack = Part(cam, PrimitiveType.Cube, "Battery pack", new Vector3(0f, -0.016f, -0.074f), new Vector3(0.058f, 0.042f, 0.02f), Vector3.zero, Mat(new Color(0.16f, 0.16f, 0.17f), 0.4f)).transform;
            Part(_pack, PrimitiveType.Cube, "Battery label", new Vector3(0f, 0f, -0.51f), new Vector3(0.7f, 0.5f, 0.02f), Vector3.zero, Mat(new Color(0.55f, 0.56f, 0.58f), 0.3f));
            _packHome = _pack.localPosition;
            _recLed = Part(cam, PrimitiveType.Sphere, "REC light", new Vector3(-0.018f, 0.05f, 0.05f), new Vector3(0.006f, 0.006f, 0.006f), Vector3.zero, _ledOn).GetComponent<Renderer>();
            var brand = Part(cam, PrimitiveType.Cube, "Brand plate", new Vector3(-0.0345f, 0.02f, 0.03f), new Vector3(0.001f, 0.01f, 0.04f), Vector3.zero, grey);
            brand.name = "Brand plate";

            // The flip-out screen: hinged at the body's front-left edge, swung out to face you.
            var hinge = new GameObject("Screen hinge").transform;
            hinge.SetParent(cam, false);
            hinge.localPosition = new Vector3(-0.036f, 0.004f, 0.045f);
            hinge.localRotation = Quaternion.identity; // swung all the way out, facing straight back at you
            Part(hinge, PrimitiveType.Cube, "Screen frame", new Vector3(-0.042f, 0f, 0f), new Vector3(0.086f, 0.064f, 0.008f), Vector3.zero, plastic);
            BuildScreen(hinge);

            // The picture: a camera at your eyes looking where you look, with the screen's field of view, so the
            // screen shows exactly what's behind it. Plus its infrared lamp.
            var lensT = new GameObject("Lens").transform;
            lensT.SetParent(_rig.parent, false);
            lensT.localPosition = Vector3.zero;
            lensT.localRotation = Quaternion.identity;
            _lensT = lensT;
            _feed = new RenderTexture(512, 384, 24) { name = "KE_CamcorderFeed" };
            if (_feedImg != null) _feedImg.texture = _feed; // (the screen was built first: hook the picture up to it now)
            _lens = lensT.gameObject.AddComponent<Camera>();
            _lens.enabled = false;              // rendered by hand, 24 fps
            _lens.targetTexture = _feed;
            _lens.fieldOfView = SCREEN_FOV;

            _lens.nearClipPlane = 0.05f;
            _lens.farClipPlane = 80f;
            _lens.clearFlags = CameraClearFlags.SolidColor;
            _lens.backgroundColor = Color.black;
            _ir = lensT.gameObject.AddComponent<Light>();
            _ir.type = LightType.Spot;
            _ir.spotAngle = 75f;
            _ir.range = 14f;
            _ir.intensity = 1.8f;
            _ir.color = Color.white;
            _ir.shadows = LightShadows.None;
            _ir.enabled = false;

            // A little green light from the screen on your hand.
            var glow = new GameObject("Screen glow");
            glow.transform.SetParent(hinge, false);
            glow.transform.localPosition = new Vector3(-0.04f, 0f, -0.05f);
            _screenGlow = glow.AddComponent<Light>();
            _screenGlow.type = LightType.Point;
            _screenGlow.color = new Color(0.4f, 1f, 0.5f);
            _screenGlow.range = 0.3f;
            _screenGlow.intensity = 0.15f;

            // Your right hand, through the strap: palm flat against the camcorder's right side, fingers over
            // the top, thumb at the back by the record button; the wrist and sleeve run back and down out of view.
            var hand = new GameObject("Right hand").transform;
            hand.SetParent(cam, false);
            Part(hand, PrimitiveType.Cube, "Palm", new Vector3(0.058f, -0.004f, -0.004f), new Vector3(0.022f, 0.08f, 0.088f), Vector3.zero, skin);
            for (int i = 0; i < 4; i++)
            {
                float z = 0.031f - i * 0.021f;
                float len = i == 1 ? 1f : i == 3 ? 0.8f : 0.93f;
                Finger(hand, new Vector3(0.054f, 0.034f, z), new Vector3(0f, 0f, 90f), len, 58f, 42f, skin);
            }
            Finger(hand, new Vector3(0.05f, -0.006f, -0.052f), new Vector3(0f, 25f, 150f), 0.9f, -18f, 0f, skin); // the thumb
            Part(hand, PrimitiveType.Cylinder, "Wrist", new Vector3(0.07f, -0.032f, -0.07f), new Vector3(0.05f, 0.03f, 0.046f), new Vector3(68f, -18f, 0f), skin);
            Part(hand, PrimitiveType.Cylinder, "Sleeve", new Vector3(0.1f, -0.085f, -0.2f), new Vector3(0.076f, 0.13f, 0.07f), new Vector3(62f, -12f, 0f), sleeve);
            Part(hand, PrimitiveType.Cylinder, "Sleeve cuff", new Vector3(0.079f, -0.048f, -0.106f), new Vector3(0.079f, 0.012f, 0.074f), new Vector3(62f, -12f, 0f), sleeve);
        }

        // A finger: three segments from 'root' along the joint's local x, bending around local z.
        private static void Finger(Transform hand, Vector3 root, Vector3 baseEuler, float len, float bend1, float bend2, Material skin)
        {
            Transform j = new GameObject("Finger").transform;
            j.SetParent(hand, false);
            j.localPosition = root;
            j.localRotation = Quaternion.Euler(baseEuler);
            float[] seg = { 0.028f * len, 0.022f * len, 0.018f * len };
            float[] bend = { 0f, bend1, bend2 };
            for (int i = 0; i < 3; i++)
            {
                j = Joint(j, i == 0 ? Vector3.zero : new Vector3(seg[i - 1], 0f, 0f), bend[i]);
                Part(j, PrimitiveType.Capsule, "Knuckle", new Vector3(seg[i] * 0.5f, 0f, 0f), new Vector3(0.017f, seg[i] * 0.62f, 0.017f), new Vector3(0f, 0f, 90f), skin);
            }
        }

        private static Transform Joint(Transform parent, Vector3 at, float bend)
        {
            var t = new GameObject("Joint").transform;
            t.SetParent(parent, false);
            t.localPosition = at;
            t.localRotation = Quaternion.Euler(0f, 0f, bend);
            return t;
        }

        // The screen itself: the lens feed in night-vision green, with grain and a dark vignette.
        private void BuildScreen(Transform hinge)
        {
            var canvasGo = new GameObject("Screen", typeof(RectTransform));
            canvasGo.transform.SetParent(hinge, false);
            canvasGo.transform.localPosition = new Vector3(-0.042f, 0f, -0.0045f);
            canvasGo.transform.localRotation = Quaternion.identity;   // facing -z: back toward you
            canvasGo.transform.localScale = Vector3.one * 0.0001f;    // 1 unit = 0.1 mm
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            ((RectTransform)canvasGo.transform).sizeDelta = new Vector2(760f, 560f);

            var feed = Layer(canvasGo.transform, "Feed");
            feed.texture = _feed;
            _feedImg = feed;                                          // night-vision green; MatchExposure sets how bright

            _grainTex = new Texture2D(128, 128, TextureFormat.ARGB32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
            var rnd = new System.Random(3);
            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 128; x++)
                {
                    float v = (float)rnd.NextDouble();
                    _grainTex.SetPixel(x, y, new Color(0.35f, 0.55f, 0.38f, v > 0.72f ? (v - 0.72f) * 0.7f : 0f));
                }
            _grainTex.Apply();
            _grain = Layer(canvasGo.transform, "Grain");
            _grain.texture = _grainTex;
            MatchExposure();

            var vignette = new Texture2D(64, 48, TextureFormat.ARGB32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 48; y++)
                for (int x = 0; x < 64; x++)
                {
                    float dx = (x - 31.5f) / 31.5f, dy = (y - 23.5f) / 23.5f;
                    vignette.SetPixel(x, y, new Color(0f, 0f, 0f, Mathf.Clamp01((dx * dx + dy * dy - 0.35f) * 0.9f)));
                }
            vignette.Apply();
            Layer(canvasGo.transform, "Vignette").texture = vignette;
            BuildBatteryIcon(canvasGo.transform);
        }

        private static RawImage Layer(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<RawImage>();
            img.raycastTarget = false;
            return img;
        }
    }
}
