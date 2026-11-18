using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// A live 3D preview for menus: a model far above everything on its own layer, lit by
    /// lights that only touch that layer, filmed by its own camera into a texture that a
    /// RawImage shows. Used by the OUTFITS screen (the kids) and the VERSUS SHOP (Mama and
    /// Papa skins). Whatever's shown spins slowly.
    /// </summary>
    public class PreviewStage
    {
        public const int LAYER = 31;

        private readonly GameObject _rig;
        private readonly Camera _cam;
        private readonly RenderTexture _tex;
        private GameObject _subject;

        public Texture Texture { get { return _tex; } }
        public GameObject Subject { get { return _subject; } }

        public PreviewStage(Transform owner, Vector3 worldPos, int width, int height)
        {
            _rig = new GameObject("KE_PreviewStage");
            _rig.transform.SetParent(owner, false);
            _rig.transform.position = worldPos;

            _tex = new RenderTexture(width, height, 16);
            var camGO = new GameObject("PreviewCamera");
            camGO.transform.SetParent(_rig.transform, false);
            _cam = camGO.AddComponent<Camera>();
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.07f, 0.06f, 0.09f);
            _cam.cullingMask = 1 << LAYER;
            _cam.fieldOfView = 30f;
            _cam.farClipPlane = 30f;
            _cam.targetTexture = _tex;
            _cam.enabled = false;

            Light(new Vector3(1.2f, 1.8f, 2.0f), new Color(1f, 0.95f, 0.88f), 1.6f);  // key
            Light(new Vector3(-1.5f, 1.2f, 1.2f), new Color(0.75f, 0.82f, 1f), 0.8f); // fill
            Light(new Vector3(0f, 1.6f, -1.6f), new Color(0.9f, 0.9f, 1f), 0.9f);     // rim
        }

        private void Light(Vector3 localPos, Color color, float intensity)
        {
            var go = new GameObject("PreviewLight");
            go.transform.SetParent(_rig.transform, false);
            go.transform.localPosition = localPos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = 8f;
            l.color = color;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            l.renderMode = LightRenderMode.ForcePixel;
            l.cullingMask = 1 << LAYER;
        }

        /// <summary>Puts a model on the stage (replacing the last one), framed for its height.</summary>
        public void Show(GameObject subject, float height, float spin = 30f)
        {
            Quaternion keep = _subject != null ? _subject.transform.localRotation : Quaternion.Euler(0f, 20f, 0f);
            Clear();
            _subject = subject;
            if (subject == null) return;
            subject.transform.SetParent(_rig.transform, false);
            subject.transform.localPosition = Vector3.zero;
            subject.transform.localRotation = keep;
            subject.AddComponent<Turntable>().DegreesPerSecond = spin;
            Relayer();
            Frame(height * 0.5f, height * 1.2f); // the whole height in view, a little room around it (the creator re-aims after)
        }

        /// <summary>Points the camera at a height, showing viewHeight meters top to bottom (the creator zooms onto the face).</summary>
        public void Frame(float centerY, float viewHeight)
        {
            float dist = viewHeight * 0.5f / Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            _cam.transform.localPosition = new Vector3(0f, centerY + viewHeight * 0.05f, dist);
            _cam.transform.LookAt(_rig.transform.position + Vector3.up * centerY);
            for (int i = 0; i < _rig.transform.childCount; i++)
            {
                var l = _rig.transform.GetChild(i).GetComponent<Light>();
                if (l != null) l.range = dist + 4f;
            }
        }

        /// <summary>Turntable speed; 0 turns the model to face the camera.</summary>
        public void Spin(float degreesPerSecond)
        {
            if (_subject == null) return;
            var t = _subject.GetComponent<Turntable>();
            if (t != null) t.DegreesPerSecond = degreesPerSecond;
            if (degreesPerSecond == 0f) _subject.transform.localRotation = Quaternion.Euler(0f, 12f, 0f);
        }

        public void Clear()
        {
            if (_subject != null) Object.Destroy(_subject);
            _subject = null;
        }

        /// <summary>Call every frame: films while the menu is open, and pulls late-added parts (Papa's hat) onto the stage's layer.</summary>
        public void Tick(bool visible)
        {
            _cam.enabled = visible && _subject != null;
            if (_cam.enabled) Relayer();
        }

        private void Relayer()
        {
            if (_subject == null) return;
            foreach (var t in _subject.GetComponentsInChildren<Transform>(true))
                if (t.gameObject.layer != LAYER) t.gameObject.layer = LAYER;
        }
    }
}
