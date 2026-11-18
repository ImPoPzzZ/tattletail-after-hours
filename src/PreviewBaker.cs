using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>Loads the shop's baked 3D preview renders (PNG files written by PreviewBaker) for the Shop screen.</summary>
    public static class PreviewLibrary
    {
        private static readonly Dictionary<string, Texture2D> _cache = new Dictionary<string, Texture2D>();

        private static string Dir { get { return Path.Combine(BepInEx.Paths.GameRootPath, "ShopPreviews"); } }

        public static string FileFor(string key)
        {
            // Party (Papa's 10th Birthday!) items reuse the same renders as their Endless twins.
            if (key.StartsWith("Party ")) key = key.Substring(6);
            if (key.StartsWith("Versus ")) key = key.Substring(7);
            if (key == "Color Gold" || key == "Color Diamond") key += " v4"; // fresh pictures for each remake (v2 caught a dropped shiny look; v4: real fur + sparkles)
            return Path.Combine(Dir, key.Replace(' ', '_').ToLowerInvariant() + ".png");
        }

        /// <summary>Colors, beams and the Diamond Flashlight get a 3D render; the other items are just described.</summary>
        public static bool HasPreviewSlot(ShopItem item)
        {
            return item.Category == ShopCategory.Color || item.Category == ShopCategory.Beam || item.Key == "Power Up Diamond Flashlight";
        }

        public static List<string> MissingKeys()
        {
            var missing = new List<string>();
            foreach (var item in Shop.Items)
                if (!item.Party && HasPreviewSlot(item) && !File.Exists(FileFor(item.Key))) missing.Add(item.Key);
            return missing;
        }

        public static Texture2D Get(string key)
        {
            Texture2D tex;
            if (_cache.TryGetValue(key, out tex) && tex != null) return tex;
            try
            {
                string path = FileFor(key);
                if (!File.Exists(path)) return null;
                tex = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                tex.LoadImage(File.ReadAllBytes(path));
                _cache[key] = tex;
                return tex;
            }
            catch (System.Exception e)
            {
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Preview] load failed for '" + key + "': " + e.Message);
                return null;
            }
        }
    }

    /// <summary>
    /// Produces the Shop's 3D previews. The first time a run starts (the real
    /// Tattletail and flashlight models only exist inside a level, not in the
    /// menu), clones of both are rendered once per shop color into PNGs under
    /// ShopPreviews/ next to Shop.txt — the Shop then just shows those files.
    /// Everything is built far below the map (y = -5000) so no game camera
    /// ever sees it, and the lights are range-limited points so nothing in
    /// the real scene lights up. Missing files are re-baked next launch.
    /// </summary>
    public static class PreviewBaker
    {
        private static readonly Vector3 RigPos = new Vector3(0f, -5000f, 0f);
        private static bool _started;

        private class Shot
        {
            public GameObject root;
            public Vector3 camPos;
            public Vector3 lookAt;
            public float fov = 28f;
            public Color background = new Color(0f, 0f, 0f, 0f);
        }

        /// <summary>Call from SceneBootstrap.StartEndless() BEFORE the player's own recolor is applied, so the templates are the pristine model.</summary>
        public static void BeginBake()
        {
            if (_started) return;
            var missing = PreviewLibrary.MissingKeys();
            if (missing.Count == 0) { _started = true; return; }
            if (RM.tattletail == null || RM.tattletail.tattletail == null || KaleidoscopeEndlessPlugin.Instance == null) return;

            _started = true;
            try
            {
                GameObject ttTemplate = MakeTemplate(RM.tattletail.tattletail);

                GameObject flTemplate = null;
                var glowNames = new HashSet<string>();
                if (RM.flashlight != null && RM.flashlight.flashlight != null)
                {
                    flTemplate = MakeTemplate(RM.flashlight.flashlight);
                    if (RM.flashlight.glowingRenderers != null)
                        foreach (var gr in RM.flashlight.glowingRenderers)
                            if (gr != null) glowNames.Add(gr.gameObject.name);
                }

                KaleidoscopeEndlessPlugin.Instance.StartCoroutine(BakeRoutine(ttTemplate, flTemplate, glowNames, missing));
            }
            catch (System.Exception e)
            {
                KaleidoscopeEndlessPlugin.Log.LogError("KE: [Preview] BeginBake failed: " + e.Message + "\n" + e.StackTrace);
            }
        }

        private static GameObject MakeTemplate(GameObject source)
        {
            GameObject t = Object.Instantiate(source, RigPos, source.transform.rotation);
            t.name = "KE_PreviewTemplate";
            foreach (Interactable existing in t.GetComponentsInChildren<Interactable>(true)) Object.DestroyImmediate(existing);
            foreach (Trigger trigger in t.GetComponentsInChildren<Trigger>(true)) Object.DestroyImmediate(trigger);
            t.SetActive(false);
            return t;
        }

        private static IEnumerator BakeRoutine(GameObject ttTemplate, GameObject flTemplate, HashSet<string> glowNames, List<string> keys)
        {
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Preview] Baking " + keys.Count + " shop preview(s)...");

            var camGO = new GameObject("KE_PreviewCam");
            camGO.transform.position = RigPos;
            var cam = camGO.AddComponent<Camera>();
            cam.enabled = false; // rendered by hand below
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 60f;
            cam.cullingMask = -1;
            var rt = new RenderTexture(320, 320, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;

            int baked = 0;
            foreach (string key in keys)
            {
                Shot shot = null;
                try { shot = BuildShot(key, ttTemplate, flTemplate, glowNames); }
                catch (System.Exception e) { KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Preview] build failed for '" + key + "': " + e.Message); }
                if (shot == null) continue;

                // Let the fur controller etc. run a couple of frames before capturing.
                yield return null;
                yield return null;
                yield return new WaitForEndOfFrame();

                try
                {
                    camGO.transform.position = shot.camPos;
                    camGO.transform.LookAt(shot.lookAt);
                    cam.fieldOfView = shot.fov;
                    cam.backgroundColor = shot.background;
                    cam.Render();

                    RenderTexture.active = rt;
                    var tex = new Texture2D(rt.width, rt.height, TextureFormat.ARGB32, false);
                    tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                    tex.Apply();
                    RenderTexture.active = null;

                    string path = PreviewLibrary.FileFor(key);
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllBytes(path, tex.EncodeToPNG());
                    Object.Destroy(tex);
                    baked++;
                }
                catch (System.Exception e)
                {
                    RenderTexture.active = null;
                    KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Preview] render failed for '" + key + "': " + e.Message);
                }

                if (shot.root != null) Object.Destroy(shot.root);
            }

            Object.Destroy(camGO);
            rt.Release();
            Object.Destroy(rt);
            if (ttTemplate != null) Object.Destroy(ttTemplate);
            if (flTemplate != null) Object.Destroy(flTemplate);
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Preview] Done — " + baked + "/" + keys.Count + " preview(s) written to ShopPreviews/.");
        }

        private static Shot BuildShot(string key, GameObject ttTemplate, GameObject flTemplate, HashSet<string> glowNames)
        {
            if (key.StartsWith("Color ")) return BuildTattletailShot(key, ttTemplate);
            if (key.StartsWith("Beam ")) return BuildBeamShot(key.Substring(5));
            if (key == "Power Up Diamond Flashlight") return BuildFlashlightShot(flTemplate, glowNames);
            return null;
        }

        private static Shot BuildTattletailShot(string key, GameObject template)
        {
            if (template == null) return null;
            GameObject clone = Object.Instantiate(template, RigPos, template.transform.rotation);
            clone.SetActive(true);

            switch (key)
            {
                case "Color Yellow": RM.recolorManager?.RecolorObject(clone, RecolorType.YELLOW); break;
                case "Color Teal":   RM.recolorManager?.RecolorObject(clone, RecolorType.TEAL); break;
                case "Color Black":  RM.recolorManager?.RecolorObject(clone, RecolorType.BLACK); break;
                case "Color White":  RM.recolorManager?.RecolorObject(clone, RecolorType.WHITE); break;
                case "Color Gold":   GoldRecolor.Apply(clone); break;
                case "Color Diamond": DiamondRecolor.Apply(clone); break;
            }

            Bounds b;
            if (!TryGetBounds(clone, out b)) { Object.Destroy(clone); return null; }

            // Face the camera toward whichever way the eyes point.
            Vector3 dir = clone.transform.forward;
            foreach (var r in clone.GetComponentsInChildren<Renderer>(false))
            {
                if (r.gameObject.name.ToLowerInvariant().Contains("eye"))
                {
                    Vector3 toEye = r.bounds.center - b.center;
                    toEye.y = 0f;
                    if (toEye.sqrMagnitude > 0.0001f) { dir = toEye.normalized; break; }
                }
            }
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
            dir.Normalize();

            var shot = new Shot { root = clone, lookAt = b.center };
            float dist = b.extents.magnitude / Mathf.Sin(shot.fov * 0.5f * Mathf.Deg2Rad) * 1.05f;
            shot.camPos = b.center + dir * dist + Vector3.up * dist * 0.08f;
            AddStudioLights(clone.transform, b.center, dir, dist);
            return shot;
        }

        private static Shot BuildFlashlightShot(GameObject template, HashSet<string> glowNames)
        {
            if (template == null) return null;
            GameObject clone = Object.Instantiate(template, RigPos, template.transform.rotation);
            clone.SetActive(true);

            Shader unlit = Shader.Find("Unlit/Color");
            foreach (var r in clone.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || glowNames.Contains(r.gameObject.name)) continue;
                Material[] mats = r.materials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    if (unlit != null) mats[i].shader = unlit;
                    mats[i].color = DiamondFlashlight.DiamondColor;
                }
                r.materials = mats;
            }

            Bounds b;
            if (!TryGetBounds(clone, out b)) { Object.Destroy(clone); return null; }

            Vector3 dir = (clone.transform.forward + clone.transform.right).normalized;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
            var shot = new Shot { root = clone, lookAt = b.center };
            float dist = b.extents.magnitude / Mathf.Sin(shot.fov * 0.5f * Mathf.Deg2Rad) * 1.1f;
            shot.camPos = b.center + (dir + Vector3.up * 0.4f).normalized * dist;
            AddStudioLights(clone.transform, b.center, dir, dist);
            return shot;
        }

        // A small lit test room: a spotlight in the beam's color lighting a wall,
        // a sphere and a cube — shows what the beam actually looks like.
        private static Shot BuildBeamShot(string colorName)
        {
            Color color;
            if (!FlashlightBeam.TryGetColor(colorName, out color)) return null;

            Shader lit = Shader.Find("Standard");
            if (lit == null) lit = Shader.Find("Diffuse");
            if (lit == null) return null;

            var root = new GameObject("KE_PreviewBeamRoom");
            root.transform.position = RigPos;

            Material mat = new Material(lit);
            mat.color = new Color(0.85f, 0.85f, 0.85f);

            var wall = GameObject.CreatePrimitive(PrimitiveType.Quad);
            wall.transform.SetParent(root.transform, false);
            wall.transform.localPosition = new Vector3(0f, 0f, 6f);
            wall.transform.localScale = new Vector3(14f, 9f, 1f);
            wall.GetComponent<Renderer>().sharedMaterial = mat;

            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.SetParent(root.transform, false);
            sphere.transform.localPosition = new Vector3(-1.3f, -0.9f, 4.4f);
            sphere.transform.localScale = Vector3.one * 1.4f;
            sphere.GetComponent<Renderer>().sharedMaterial = mat;

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(root.transform, false);
            cube.transform.localPosition = new Vector3(1.4f, -1.1f, 4.8f);
            cube.transform.localRotation = Quaternion.Euler(0f, 30f, 0f);
            cube.transform.localScale = Vector3.one * 1.2f;
            cube.GetComponent<Renderer>().sharedMaterial = mat;

            var lightGO = new GameObject("BeamLight");
            lightGO.transform.SetParent(root.transform, false);
            lightGO.transform.localPosition = Vector3.zero;
            lightGO.transform.localRotation = Quaternion.identity; // +Z, toward the wall
            var spot = lightGO.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.spotAngle = 55f;
            spot.range = 14f;
            spot.intensity = 4f;
            spot.color = color;

            return new Shot
            {
                root = root,
                camPos = RigPos + new Vector3(3.2f, 1.4f, -1.5f),
                lookAt = RigPos + new Vector3(0f, -0.3f, 4.8f),
                fov = 45f,
                background = new Color(0.03f, 0.03f, 0.05f, 1f),
            };
        }

        private static void AddStudioLights(Transform parent, Vector3 center, Vector3 dir, float dist)
        {
            Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;

            var keyGO = new GameObject("PreviewKey");
            keyGO.transform.SetParent(parent, true);
            keyGO.transform.position = center + dir * dist * 0.8f + right * dist * 0.7f + Vector3.up * dist * 0.8f;
            var key = keyGO.AddComponent<Light>();
            key.type = LightType.Point;
            key.range = dist * 4f;
            key.intensity = 1.3f;

            var fillGO = new GameObject("PreviewFill");
            fillGO.transform.SetParent(parent, true);
            fillGO.transform.position = center + dir * dist * 0.6f - right * dist * 0.9f + Vector3.up * dist * 0.1f;
            var fill = fillGO.AddComponent<Light>();
            fill.type = LightType.Point;
            fill.range = dist * 4f;
            fill.intensity = 0.6f;
        }

        private static bool TryGetBounds(GameObject go, out Bounds bounds)
        {
            bounds = new Bounds(go.transform.position, Vector3.zero);
            bool any = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>(false))
            {
                if (r == null || !r.enabled) continue;
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return any;
        }
    }
}
