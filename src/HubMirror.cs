using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// A real mirror on the Arcade's wall (next to the OUTFITS machine): the room, the other
    /// player, and you — your own kid in your outfit, which only the mirror sees (you're first
    /// person). A second camera sits at your eye's reflection behind the wall and films the
    /// room through the mirror's frame (an off-center frustum fitted to its corners, its near
    /// plane on the glass so nothing behind the wall gets in); the picture goes on the glass,
    /// flipped left-to-right like a mirror. Only rendered while the glass is on screen.
    /// </summary>
    public class HubMirror : MonoBehaviour
    {
        public const int SELF_LAYER = 30; // your mirror body: the mirror's camera sees it, your own doesn't

        private Transform _holder;
        private Camera _cam;
        private RenderTexture _tex;
        private Material _glass;
        private GameObject _self;
        private string _selfKey;
        private static bool _rendering;

        private static Interactable _prompt;

        public static void ShowPrompt() { SetPrompt(true); }
        public static void HidePrompt() { SetPrompt(false); }
        private static void SetPrompt(bool on)
        {
            if (_prompt == null) return;
            _prompt.conditions.newStateOnConditionsMet = on ? Interactable.State.Enabled : Interactable.State.Hidden;
            if (on) _prompt.SetStateEnabled(); else _prompt.SetStateHidden();
        }

        public static void Build(Transform room, Vector3 localPos, float yaw, float width, float height)
        {
            var holder = new GameObject("KE_Mirror").transform;
            holder.SetParent(room, false);
            holder.localPosition = localPos;
            holder.localRotation = Quaternion.Euler(0f, yaw, 0f); // the glass faces local +z

            Material frame = Mat.Lit(new Color(0.78f, 0.8f, 0.86f), 0.7f);
            const float F = 0.07f;
            Piece(holder, new Vector3(0f, 0f, 0f), new Vector3(width + 2f * F, height + 2f * F, 0.04f), Mat.Lit(new Color(0.05f, 0.05f, 0.06f), 0.2f));
            Piece(holder, new Vector3(0f, (height + F) / 2f, 0.03f), new Vector3(width + 2f * F, F, 0.05f), frame);
            Piece(holder, new Vector3(0f, -(height + F) / 2f, 0.03f), new Vector3(width + 2f * F, F, 0.05f), frame);
            Piece(holder, new Vector3((width + F) / 2f, 0f, 0.03f), new Vector3(F, height, 0.05f), frame);
            Piece(holder, new Vector3(-(width + F) / 2f, 0f, 0.03f), new Vector3(F, height, 0.05f), frame);

            // "Remember who you are": while you're a blank mannequin, the mirror opens OUTFITS (Identity).
            var hit = holder.gameObject.AddComponent<BoxCollider>();
            hit.center = new Vector3(0f, 0f, 0.03f);
            hit.size = new Vector3(width + 0.14f, height + 0.14f, 0.1f);
            _prompt = PartyGameRunner.MakeInteractable(holder.gameObject, "Remember who you are", 0.6f);
            HubLayout.Register(holder.gameObject, HubMachine.Mirror);
            if (Identity.Forgotten) ShowPrompt(); else HidePrompt();

            var glass = GameObject.CreatePrimitive(PrimitiveType.Quad); // a Quad shows its -z side: turned around to face the room
            glass.name = "Glass";
            Object.Destroy(glass.GetComponent<Collider>());
            glass.transform.SetParent(holder, false);
            glass.transform.localPosition = new Vector3(0f, 0f, 0.025f);
            glass.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            glass.transform.localScale = new Vector3(width, height, 1f);

            var m = glass.AddComponent<HubMirror>();
            m._holder = holder;
            m._tex = new RenderTexture(Mathf.RoundToInt(1024f * width / height), 1024, 24);
            m._tex.antiAliasing = 2;
            // The game's build has no "Unlit/Texture" shader: the Standard shader, black, glowing with the
            // picture (so the room's light doesn't darken or shine on it). Its tiling flips the emission too.
            m._glass = Mat.Lit(Color.black, 0f);
            m._glass.mainTexture = m._tex;
            m._glass.EnableKeyword("_EMISSION");
            m._glass.SetTexture("_EmissionMap", m._tex);
            m._glass.SetColor("_EmissionColor", Color.white);
            m._glass.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            glass.GetComponent<Renderer>().material = m._glass;

            var camGO = new GameObject("KE_MirrorCamera");
            camGO.transform.SetParent(holder, false);
            m._cam = camGO.AddComponent<Camera>();
            m._cam.enabled = false; // rendered by hand, when the glass is seen
            m._cam.targetTexture = m._tex;
        }

        private static void Piece(Transform holder, Vector3 pos, Vector3 size, Material mat)
        {
            var go = Mat.Box(holder, "MirrorFrame", pos, size, mat);
            Object.Destroy(go.GetComponent<Collider>());
        }

        private void OnDestroy()
        {
            if (_tex != null) _tex.Release();
            if (_self != null) Destroy(_self);
            if (Camera.main != null) Camera.main.cullingMask |= 1 << SELF_LAYER;
        }

        // ── You, in the mirror ───────────────────────────────────────────────
        private void LateUpdate()
        {
            var main = Camera.main;
            if (main != null) main.cullingMask &= ~(1 << SELF_LAYER);
            var fps = RM.fpsController;
            if (fps == null) return;

            string key = KidSkins.Code + "|" + (KidSkins.Gender == KidGender.Custom ? CharacterSpec.LocalString : "");
            if (_self == null || key != _selfKey) // a new kid or outfit (from the OUTFITS machine next to it)
            {
                if (_self != null) Destroy(_self);
                _self = Identity.Forgotten ? ChildModel.Build("KE_MirrorSelf", KidGender.Boy, KidSkins.MANNEQUIN) : ChildModel.Build("KE_MirrorSelf", KidSkins.Gender, KidSkins.Selected);
                _selfKey = key;
            }
            _self.transform.position = WorldRays.FeetOf(fps.transform.position); // jumps show too
            _self.transform.rotation = Quaternion.Euler(0f, fps.transform.eulerAngles.y, 0f);
            foreach (var t in _self.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = SELF_LAYER; // (the face rebuilds its parts)
            foreach (var r in _self.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // ── The reflection ───────────────────────────────────────────────────
        private void OnWillRenderObject()
        {
            Camera main = Camera.main;
            if (_rendering || main == null || Camera.current != main) return;
            _rendering = true;
            try { Reflect(main); }
            finally { _rendering = false; }
        }

        private void Reflect(Camera main)
        {
            Vector3 n = _holder.forward, center = transform.position, eye = main.transform.position;
            float d = Vector3.Dot(eye - center, n);
            if (d < 0.05f) return; // behind the wall

            _cam.transform.position = eye - 2f * d * n; // your eye's reflection
            _cam.transform.rotation = Quaternion.LookRotation(n, _holder.up);

            // The frustum through the glass's corners, starting just past the glass.
            Vector3 a = _cam.transform.InverseTransformPoint(transform.TransformPoint(new Vector3(-0.5f, -0.5f, 0f)));
            Vector3 b = _cam.transform.InverseTransformPoint(transform.TransformPoint(new Vector3(0.5f, 0.5f, 0f)));
            float near = d + 0.02f, far = Mathf.Max(near + 1f, main.farClipPlane), k = near / d;
            _cam.nearClipPlane = near;
            _cam.farClipPlane = far;
            _cam.projectionMatrix = OffCenter(Mathf.Min(a.x, b.x) * k, Mathf.Max(a.x, b.x) * k, Mathf.Min(a.y, b.y) * k, Mathf.Max(a.y, b.y) * k, near, far);

            // Whichever way the picture came out, the glass shows it mirrored the right way round.
            bool fx = a.x > b.x, fy = a.y > b.y;
            _glass.mainTextureScale = new Vector2(fx ? -1f : 1f, fy ? -1f : 1f);
            _glass.mainTextureOffset = new Vector2(fx ? 1f : 0f, fy ? 1f : 0f);

            _cam.clearFlags = main.clearFlags;
            _cam.backgroundColor = main.backgroundColor;
            _cam.cullingMask = (main.cullingMask | (1 << SELF_LAYER)) & ~(1 << PreviewStage.LAYER);
            _cam.Render();
        }

        private static Matrix4x4 OffCenter(float l, float r, float b, float t, float near, float far)
        {
            var m = new Matrix4x4();
            m[0, 0] = 2f * near / (r - l); m[0, 2] = (r + l) / (r - l);
            m[1, 1] = 2f * near / (t - b); m[1, 2] = (t + b) / (t - b);
            m[2, 2] = -(far + near) / (far - near); m[2, 3] = -(2f * far * near) / (far - near);
            m[3, 2] = -1f;
            return m;
        }
    }
}
