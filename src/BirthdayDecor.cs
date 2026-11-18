using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// Papa's 10th Birthday! set dressing, at two hand-marked spots:
    ///   mark 1 — a Mama (the real model, cloned): white fur, a monocle and
    ///            a top hat, standing facing the table;
    ///   mark 2 — a party table with a plate in the middle, a pizza and cupcakes.
    /// Everything is built in code (flat Unlit colors, no external assets).
    /// If the real Mama model can't be found, a simple stand-in Mama built
    /// from shapes is used so the spot is never empty.
    /// </summary>
    /// <summary>Cycles fur colors (Rainbow Papa).</summary>
    public class FurRainbow : MonoBehaviour
    {
        public List<Material> Mats;
        private void Update()
        {
            if (Mats == null) return;
            Color c = Gadgets.Rainbow(Time.unscaledTime * 0.2f);
            foreach (var m in Mats)
            {
                if (m == null) continue;
                m.color = c;
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c * BirthdayDecor.FUR_GLOW);
            }
        }
    }

    public static class BirthdayDecor
    {
        // E-key marks (playerPos) — the floor is found by a raycast below each.
        private static readonly Vector3 MamaMark = new Vector3(5.2f, 1.1f, -4.3f);
        private static readonly Vector3 TableMark = new Vector3(5.1f, 1.1f, -2.8f);

        private static readonly Color MamaWhite = new Color(0.85f, 0.85f, 0.83f); // matte white (lit, so it isn't a glowing light source)

        private static readonly System.Reflection.FieldInfo HeadField =
            typeof(Mama).GetField("headTransform", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        private static GameObject _root;
        private static Shader _unlit;
        private static Shader Unlit { get { return _unlit != null ? _unlit : (_unlit = Shader.Find("Unlit/Color")); } }

        public static void Spawn()
        {
            if (_root != null) Object.Destroy(_root);
            _root = new GameObject("KE_BirthdayDecor");

            try { BuildTable(_root.transform, SnapToFloor(TableMark)); }
            catch (System.Exception e) { KaleidoscopeEndlessPlugin.Log.LogError("KE: [Birthday] table failed: " + e.Message + "\n" + e.StackTrace); }

            BirthdayMusic.Start(); // the party song from the speaker
            PartyMaterialSpawner.Setup(_root); // ingredients appearing at the marked spots
            PartyGameRunner.Begin(_root);      // hotbar, crafting, feeding, and the two bars
            PartyMachineController.Setup(_root); // the 4 placed food machines (Popcorn, Hot Dog, Nachos, Waffle & Crepe)
            PartyTattletailController.Setup(_root); // the 7 party tattletails (Face Paint / Rock-Paper-Scissors / Balloon)

            try { BuildMama(_root.transform, SnapToFloor(MamaMark)); }
            catch (System.Exception e) { KaleidoscopeEndlessPlugin.Log.LogError("KE: [Birthday] Mama failed: " + e.Message + "\n" + e.StackTrace); }
        }

        // The marks are the player's position, a bit above the floor — find the real floor under it.
        private static Vector3 SnapToFloor(Vector3 mark)
        {
            RaycastHit hit;
            if (Physics.Raycast(mark + Vector3.up * 0.3f, Vector3.down, out hit, 3f, ~0, QueryTriggerInteraction.Ignore))
                return new Vector3(mark.x, hit.point.y, mark.z);
            return new Vector3(mark.x, mark.y - 0.5f, mark.z);
        }

        // ── helpers ───────────────────────────────────────────────────────────
        private static Shader _lit;
        private static Shader Lit
        {
            get
            {
                if (_lit == null) _lit = Shader.Find("Standard");
                if (_lit == null) _lit = Shader.Find("Diffuse");
                return _lit;
            }
        }

        // Matte and lit, so props react to the house's own lighting (and the
        // flashlight) like everything else — flat Unlit colors read as
        // glowing in this dark house and fed the game's bloom.
        private static Material Flat(Color c)
        {
            Shader sh = Lit != null ? Lit : Unlit;
            var m = new Material(sh);
            m.color = c;
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.05f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            return m;
        }

        private static GameObject Prim(PrimitiveType type, Transform parent, string name, Vector3 localPos, Vector3 localScale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            Collider col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            Renderer r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = Flat(color);
            return go;
        }

        // Unity's Cylinder is 2 tall and 1 wide: scale (diameter, height/2, diameter).
        private static GameObject Disc(Transform parent, string name, Vector3 localPos, float radius, float height, Color color)
        {
            return Prim(PrimitiveType.Cylinder, parent, name, localPos, new Vector3(radius * 2f, height * 0.5f, radius * 2f), color);
        }

        // ── the party table ───────────────────────────────────────────────────
        private static void BuildTable(Transform parent, Vector3 floorPos)
        {
            var table = new GameObject("KE_BirthdayTable");
            table.transform.SetParent(parent, false);
            table.transform.position = floorPos;

            Color wood = new Color(0.4f, 0.25f, 0.12f);
            Color darkWood = new Color(0.3f, 0.18f, 0.09f);
            Color cloth = new Color(0.75f, 0.35f, 0.5f);

            Transform t = table.transform;
            Prim(PrimitiveType.Cube, t, "Top", new Vector3(0f, 0.47f, 0f), new Vector3(1.6f, 0.06f, 1.0f), wood);
            Prim(PrimitiveType.Cube, t, "Cloth", new Vector3(0f, 0.51f, 0f), new Vector3(1.7f, 0.02f, 1.1f), cloth);
            foreach (float sx in new[] { -0.72f, 0.72f })
                foreach (float sz in new[] { -0.42f, 0.42f })
                    Prim(PrimitiveType.Cube, t, "Leg", new Vector3(sx, 0.22f, sz), new Vector3(0.07f, 0.44f, 0.07f), darkWood);

            // Solid, so the player can't walk through it.
            var col = table.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.27f, 0f);
            col.size = new Vector3(1.7f, 0.55f, 1.1f);

            float top = 0.525f; // top of the tablecloth

            // Plate in the middle.
            Disc(t, "Plate", new Vector3(0f, top + 0.006f, 0f), 0.21f, 0.012f, new Color(0.68f, 0.68f, 0.7f));
            Disc(t, "PlateInner", new Vector3(0f, top + 0.013f, 0f), 0.15f, 0.006f, new Color(0.6f, 0.6f, 0.64f));

            BuildPizza(t, new Vector3(-0.5f, top, 0f));
            BuildCupcakes(t, new Vector3(0.5f, top, 0f));
        }

        private static void BuildPizza(Transform parent, Vector3 center)
        {
            var pizza = new GameObject("Pizza");
            pizza.transform.SetParent(parent, false);
            pizza.transform.localPosition = center;
            Transform p = pizza.transform;

            Disc(p, "Crust", new Vector3(0f, 0.012f, 0f), 0.25f, 0.024f, new Color(0.62f, 0.42f, 0.2f));
            Disc(p, "Sauce", new Vector3(0f, 0.026f, 0f), 0.21f, 0.004f, new Color(0.6f, 0.15f, 0.1f));
            Disc(p, "Cheese", new Vector3(0f, 0.029f, 0f), 0.2f, 0.004f, new Color(0.72f, 0.58f, 0.2f));

            // Slice lines.
            for (int i = 0; i < 4; i++)
            {
                var line = Prim(PrimitiveType.Cube, p, "Slice", new Vector3(0f, 0.033f, 0f), new Vector3(0.4f, 0.002f, 0.006f), new Color(0.5f, 0.38f, 0.14f));
                line.transform.localRotation = Quaternion.Euler(0f, i * 45f, 0f);
            }

            // Pepperoni.
            Vector2[] spots = { new Vector2(0.09f, 0.03f), new Vector2(-0.07f, 0.08f), new Vector2(-0.1f, -0.05f), new Vector2(0.03f, -0.1f), new Vector2(0.13f, -0.06f), new Vector2(-0.02f, 0.02f), new Vector2(0.05f, 0.12f) };
            foreach (var s in spots)
                Disc(p, "Pepperoni", new Vector3(s.x, 0.034f, s.y), 0.03f, 0.004f, new Color(0.55f, 0.12f, 0.1f));
        }

        private static void BuildCupcakes(Transform parent, Vector3 center)
        {
            Color[] wrappers = { new Color(0.75f, 0.3f, 0.45f), new Color(0.3f, 0.5f, 0.75f), new Color(0.75f, 0.6f, 0.25f), new Color(0.4f, 0.65f, 0.4f) };
            Color[] frostings = { new Color(0.75f, 0.7f, 0.7f), new Color(0.75f, 0.5f, 0.6f), new Color(0.72f, 0.72f, 0.5f), new Color(0.5f, 0.7f, 0.72f) };
            Vector2[] offsets = { new Vector2(-0.09f, -0.09f), new Vector2(0.09f, -0.09f), new Vector2(-0.09f, 0.09f), new Vector2(0.09f, 0.09f) };

            for (int i = 0; i < 4; i++)
            {
                var cup = new GameObject("Cupcake");
                cup.transform.SetParent(parent, false);
                cup.transform.localPosition = center + new Vector3(offsets[i].x, 0f, offsets[i].y);
                Transform c = cup.transform;
                Disc(c, "Wrapper", new Vector3(0f, 0.03f, 0f), 0.04f, 0.06f, wrappers[i]);
                Prim(PrimitiveType.Sphere, c, "Frosting", new Vector3(0f, 0.075f, 0f), new Vector3(0.1f, 0.07f, 0.1f), frostings[i]);
                Prim(PrimitiveType.Sphere, c, "Cherry", new Vector3(0f, 0.115f, 0f), new Vector3(0.03f, 0.03f, 0.03f), new Color(0.7f, 0.1f, 0.12f));
            }
        }

        // ── Mama ──────────────────────────────────────────────────────────────
        private static void BuildMama(Transform parent, Vector3 floorPos)
        {
            GameObject source = MamaController.Instance != null ? MamaController.Instance.GetMamaModelSource() : null;
            GameObject clone;
            Transform head = null;

            if (source != null)
            {
                clone = Object.Instantiate(source);
                clone.name = "KE_BirthdayMama";
                clone.transform.SetParent(parent, true);

                var mamaScript = clone.GetComponentInChildren<Mama>(true);
                if (mamaScript != null && (object)HeadField != null) head = HeadField.GetValue(mamaScript) as Transform;

                MakeInert(clone);
                clone.transform.position = floorPos;
                clone.transform.rotation = Quaternion.LookRotation(Vector3.forward); // facing the table
                clone.SetActive(true);
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Birthday] Cloned the real Mama model '" + source.name + "'.");

                // The original stays hidden — she's decoration only in this mode.
                source.SetActive(false);
            }
            else
            {
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Birthday] Real Mama model not found — building a stand-in.");
                clone = BuildStandInMama(parent, floorPos);
            }

            LogRenderers(clone);
            TintFur(clone, MamaWhite);
            KaleidoscopeEndlessPlugin.Instance.StartCoroutine(FinishMama(clone, head, floorPos));
        }

        // Strips everything that could make the copy act like a live Mama (AI, colliders that kill, sounds, physics).
        internal static void MakeInert(GameObject clone) // also used for the Versus Mama body
        {
            foreach (var mb in clone.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || mb is FurShaderControllerV1) continue; // the fur controller drives the fur's look
                try { Object.DestroyImmediate(mb); } catch { }
            }
            foreach (var c in clone.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var rb in clone.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(rb);
            foreach (var a in clone.GetComponentsInChildren<AudioSource>(true)) Object.DestroyImmediate(a);
            foreach (var l in clone.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(l);
            foreach (var an in clone.GetComponentsInChildren<Animator>(true))
            {
                an.applyRootMotion = false;
                an.speed = 0f; // hold whatever pose the default state starts in
            }
        }

        private static void LogRenderers(GameObject root)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                var mats = new List<string>();
                foreach (var m in r.sharedMaterials)
                    mats.Add(m == null ? "null" : m.name + "(" + (m.shader != null ? m.shader.name : "no-shader") + ")");
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [BDAY-DIAG] renderer='" + r.gameObject.name + "' hasFur=" + (r.GetComponent<FurShaderControllerV1>() != null) + " materials=[" + string.Join(", ", mats.ToArray()) + "]");
            }
        }

        // The real model (MamaEvil): the fur is on 'bodyTop'/'bodyBottom' (shader
        // Custom/FurShaderV1) and the tail/tuft use the hair material. Swapped
        // to a plain matte white; the textured face and eyes are left alone.
        /// <summary>Recolors the fur (a Versus skin, Papa's white). Rainbow: the fur keeps cycling colors.</summary>
        internal const float FUR_GLOW = 0.12f; // just a tiny glow (Mama/Papa skins, Gold/Diamond Tattletail)

        internal static void TintFur(GameObject root, Color color, bool rainbow = false)
        {
            var tinted = new List<Material>();
            int count = 0;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                Material[] mats = r.materials; // per-renderer copies — can't touch the shared assets
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    Material m = mats[i];
                    if (m == null || m.shader == null) continue;
                    bool isFur = m.shader.name.IndexOf("Fur", System.StringComparison.OrdinalIgnoreCase) >= 0;
                    bool isHair = m.name.IndexOf("Hair", System.StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!isFur && !isHair) continue;
                    TintMaterial(m, color, 0.05f, FUR_GLOW);
                    tinted.Add(m);
                    changed = true;
                }
                if (changed) { r.materials = mats; count++; }
            }

            // Fur shells (if a controller draws them) get the same treatment.
            foreach (var fur in root.GetComponentsInChildren<FurShaderControllerV1>(true))
            {
                if (fur == null || fur.FurMaterial == null) continue;
                var copy = new Material(fur.FurMaterial);
                TintMaterial(copy, color, 0.05f, FUR_GLOW);
                tinted.Add(copy);
                fur.FurMaterial = copy;
            }
            if (rainbow) root.AddComponent<FurRainbow>().Mats = tinted;
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Birthday] Fur recolored (" + count + " fur/hair renderer(s)).");
        }

        /// <summary>Lit, matte fur in one color — how Mama's and Papa's skins (and Gold/Diamond Tattletail) look.</summary>
        /// <param name="glow">0..1: a faint self-glow in the same color (kept low, under the game's Bloom).</param>
        internal static void TintMaterial(Material m, Color color, float gloss = 0.05f, float glow = 0f)
        {
            Tint(m, color);
            if (m == null) return;
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", gloss);
            if (glow > 0f && m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", color * glow);
            }
        }

        private static void Tint(Material m, Color color)
        {
            if (m == null) return;
            Shader sh = Lit != null ? Lit : Unlit;
            if (sh != null) m.shader = sh;
            m.color = color;
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.05f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        }

        // Runs a few frames after spawning, once the paused animation has posed the model — then the bounds are real.
        private static IEnumerator FinishMama(GameObject clone, Transform head, Vector3 floorPos)
        {
            yield return null; yield return null; yield return null;
            if (clone == null) yield break;

            try
            {
                Bounds body;
                if (!TryBounds(clone, out body)) yield break;

                // Stand her on the floor.
                clone.transform.position += Vector3.up * (floorPos.y - body.min.y);
                TryBounds(clone, out body);

                // Solid, so the player can't walk through her.
                var col = clone.AddComponent<CapsuleCollider>();
                col.center = clone.transform.InverseTransformPoint(body.center);
                col.height = body.size.y / Mathf.Max(0.0001f, clone.transform.lossyScale.y);
                col.radius = Mathf.Min(body.extents.x, body.extents.z) * 0.6f / Mathf.Max(0.0001f, clone.transform.lossyScale.x);
                if (PartyGameRunner.Instance != null) PartyGameRunner.Instance.AttachPapa(clone); // hold to feed him

                AddPapaAccessories(clone);
            }
            catch (System.Exception e)
            {
                KaleidoscopeEndlessPlugin.Log.LogError("KE: [Birthday] FinishMama failed: " + e.Message + "\n" + e.StackTrace);
            }
        }

        /// <summary>Papa's look on any Mama clone (the party's Papa, Versus Papa): white fur now, and the
        /// top hat, monocle and beard a few frames later, once the model has taken its pose.</summary>
        internal static void DressAsPapa(GameObject clone) { DressAsPapa(clone, MamaWhite, false); }

        internal static void DressAsPapa(GameObject clone, Color fur, bool rainbow)
        {
            if (clone == null) return;
            TintFur(clone, fur, rainbow);
            KaleidoscopeEndlessPlugin.Instance.StartCoroutine(DressAsPapaLater(clone));
        }

        private static IEnumerator DressAsPapaLater(GameObject clone)
        {
            yield return null; yield return null; yield return null;
            if (clone == null) yield break;
            try { AddPapaAccessories(clone); }
            catch (System.Exception e) { KaleidoscopeEndlessPlugin.Log.LogError("KE: [Birthday] Papa's accessories failed: " + e.Message); }
        }

        /// <summary>The top hat, monocle and beard, sized and placed from the posed model's own head.</summary>
        private static void AddPapaAccessories(GameObject clone)
        {
            Bounds body;
            if (!TryBounds(clone, out body)) return;
                // The head = the textured face ('mesh') plus the eye sockets. The
                // headTransform's own bounds turned out too small to hang a hat on.
                Bounds faceB = body;
                bool haveFace = false;
                Vector3 eyePos = Vector3.zero;
                bool haveEye = false;
                foreach (var r in clone.GetComponentsInChildren<Renderer>(false))
                {
                    if (r == null || !r.enabled) continue;
                    string n = r.gameObject.name.ToLowerInvariant();
                    bool isFace = n == "mesh" || n.StartsWith("eyesocket");
                    if (isFace)
                    {
                        if (!haveFace) { faceB = r.bounds; haveFace = true; } else faceB.Encapsulate(r.bounds);
                    }
                    if (!haveEye && n.StartsWith("eyesocket")) { eyePos = r.bounds.center; haveEye = true; }
                }
                if (!haveFace)
                {
                    Vector3 c = new Vector3(body.center.x, body.max.y - body.size.y * 0.12f, body.center.z);
                    faceB = new Bounds(c, new Vector3(body.size.x * 0.4f, body.size.y * 0.24f, body.size.z * 0.4f));
                }

                // Facing: her own forward (she was placed looking at the table).
                Vector3 fwd = clone.transform.forward;
                fwd.y = 0f;
                fwd = fwd.sqrMagnitude < 0.0001f ? Vector3.forward : fwd.normalized;

                float w = Mathf.Max(0.05f, faceB.extents.x); // half the face's width
                if (!haveEye) eyePos = faceB.center + fwd * faceB.extents.z * 0.8f + Vector3.up * faceB.extents.y * 0.2f;

                // The hat sits on the real crown of the head. The face box's top
                // is only about eye level on this model, so it went right through
                // the monocle — use the actual surface height above the head's
                // centre, falling back to a point well above the eyes.
                float crownY;
                bool haveCrown = TryCrownY(clone, new Vector3(faceB.center.x, 0f, faceB.center.z), w * 0.6f, out crownY);
                if (!haveCrown || crownY < eyePos.y + w * 0.3f) crownY = Mathf.Max(faceB.max.y, eyePos.y + w * 0.9f);

                Vector3 eyeC; float eyeR; Vector3 eyeN;
                bool exactEye = TryEyeGeometry(clone, out eyeC, out eyeR, out eyeN);
                if (!exactEye) { eyeC = eyePos; eyeR = w * 0.25f; eyeN = fwd; }

                float hatW = w * 0.6f; // hat scale: clearly smaller than the head
                AddTopHat(clone.transform, new Vector3(faceB.center.x, crownY - hatW * 0.7f, faceB.center.z), hatW);
                // The red eye you can see is far bigger than that highlight (~0.065 radius):
                // ring it at head scale, standing just in front of the eye.
                Vector3 monoOut = (fwd + Vector3.up * 0.15f).normalized;
                AddMonocle(clone.transform, eyeC + fwd * (w * 0.2f), monoOut, w * 0.16f); // ring radius = 1.35 * this = ~0.06
                AddBeard(clone.transform, faceB, fwd, w);
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Birthday] eye exact=" + exactEye + " center " + eyeC.ToString("F3") + " radius " + eyeR.ToString("F3") + " normal " + eyeN.ToString("F3") + ", clone lossyScale " + clone.transform.lossyScale.ToString("F3") + ", hat width " + hatW.ToString("F3") + ".");
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Birthday] Mama dressed up — body size " + body.size.ToString("F3") + " min.y " + body.min.y.ToString("F3") + " max.y " + body.max.y.ToString("F3")
                    + ", face center " + faceB.center.ToString("F3") + " size " + faceB.size.ToString("F3")
                    + ", eye " + eyePos.ToString("F3") + " (found=" + haveEye + "), crownY " + crownY.ToString("F3") + " (mesh=" + haveCrown + "), forward " + fwd + ".");
        }

        // The highest point of the model's surface within `radius` (horizontally)
        // of `center` — i.e. where a hat should actually sit: the crown of the
        // head between the ears, not the top of some bounding box. Reads the
        // (baked, so posed) mesh vertices; returns false if none are readable.
        private static bool TryCrownY(GameObject root, Vector3 center, float radius, out float crownY)
        {
            crownY = float.MinValue;
            bool any = false;
            foreach (var r in root.GetComponentsInChildren<Renderer>(false))
            {
                if (r == null || !r.enabled) continue;
                string rn = r.gameObject.name.ToLowerInvariant();
                if (rn == "tuft" || rn == "tail") continue; // the yellow hair tuft sticks up past the head's dome
                Vector3[] verts = null;
                Matrix4x4 toWorld = r.transform.localToWorldMatrix;
                try
                {
                    var smr = r as SkinnedMeshRenderer;
                    if (smr != null)
                    {
                        var baked = new Mesh();
                        smr.BakeMesh(baked);
                        verts = baked.vertices;
                        // BakeMesh output is in the renderer's local space without its scale.
                        toWorld = Matrix4x4.TRS(r.transform.position, r.transform.rotation, Vector3.one);
                        Object.Destroy(baked);
                    }
                    else
                    {
                        var mf = r.GetComponent<MeshFilter>();
                        if (mf != null && mf.sharedMesh != null) verts = mf.sharedMesh.vertices;
                    }
                }
                catch { verts = null; }
                if (verts == null) continue;

                for (int i = 0; i < verts.Length; i++)
                {
                    Vector3 w = toWorld.MultiplyPoint3x4(verts[i]);
                    float dx = w.x - center.x, dz = w.z - center.z;
                    if (dx * dx + dz * dz > radius * radius) continue;
                    if (w.y > crownY) { crownY = w.y; any = true; }
                }
            }
            return any;
        }

        // The exact centre, size and outward direction of the glowing red eye,
        // read from its own mesh (submesh 0 = the eye material) instead of the
        // renderer's bounding box, which also covers the whole socket.
        private static bool TryEyeGeometry(GameObject root, out Vector3 center, out float radius, out Vector3 normal)
        {
            center = Vector3.zero; radius = 0f; normal = Vector3.forward;
            foreach (var r in root.GetComponentsInChildren<Renderer>(false))
            {
                if (r == null || !r.enabled) continue;
                if (!r.gameObject.name.ToLowerInvariant().StartsWith("eyesocket")) continue;
                try
                {
                    Mesh mesh; Matrix4x4 toWorld;
                    var smr = r as SkinnedMeshRenderer;
                    if (smr != null)
                    {
                        mesh = new Mesh();
                        smr.BakeMesh(mesh);
                        toWorld = Matrix4x4.TRS(r.transform.position, r.transform.rotation, Vector3.one);
                    }
                    else
                    {
                        var mf = r.GetComponent<MeshFilter>();
                        if (mf == null || mf.sharedMesh == null) continue;
                        mesh = mf.sharedMesh;
                        toWorld = r.transform.localToWorldMatrix;
                    }

                    int[] tris = mesh.GetTriangles(0);
                    Vector3[] verts = mesh.vertices;
                    Vector3[] norms = mesh.normals;
                    var used = new HashSet<int>(tris);
                    if (used.Count == 0) continue;

                    Vector3 sum = Vector3.zero, nsum = Vector3.zero;
                    foreach (int i in used)
                    {
                        sum += toWorld.MultiplyPoint3x4(verts[i]);
                        if (norms != null && i < norms.Length) nsum += toWorld.MultiplyVector(norms[i]);
                    }
                    center = sum / used.Count;
                    foreach (int i in used)
                        radius = Mathf.Max(radius, Vector3.Distance(center, toWorld.MultiplyPoint3x4(verts[i])));
                    normal = nsum.sqrMagnitude > 0.0001f ? nsum.normalized : Vector3.forward;
                    if (smr != null) Object.Destroy(mesh);
                    return radius > 0.001f;
                }
                catch { }
            }
            return false;
        }

        private static bool TryBounds(GameObject go, out Bounds bounds)
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

        // A plain black top hat: crown, brim and a red band.
        private static void AddTopHat(Transform parent, Vector3 headTop, float headW)
        {
            var hat = new GameObject("KE_TopHat");
            hat.transform.SetParent(parent, true);
            hat.transform.position = headTop + Vector3.down * headW * 0.1f;
            hat.transform.rotation = Quaternion.identity;
            Transform h = hat.transform;

            float crownR = headW * 0.7f, brimR = headW * 1.1f, crownH = headW * 1.0f;
            Color black = new Color(0.22f, 0.22f, 0.26f);
            DiscWorld(h, "Brim", Vector3.up * 0.015f * headW, brimR, headW * 0.06f, black);
            DiscWorld(h, "Crown", Vector3.up * (crownH * 0.5f), crownR, crownH, black);
            DiscWorld(h, "Band", Vector3.up * (headW * 0.2f), crownR * 1.03f, headW * 0.22f, new Color(0.7f, 0.15f, 0.2f));
        }

        // Like Disc(), but sizes in world units regardless of the parent's scale.
        private static void DiscWorld(Transform parent, string name, Vector3 worldOffsetFromParent, float radius, float height, Color color)
        {
            var go = Disc(parent, name, Vector3.zero, radius, height, color);
            go.transform.position = parent.position + worldOffsetFromParent;
            go.transform.localScale = Vector3.one;
            go.transform.rotation = Quaternion.identity;
            SetWorldScale(go.transform, new Vector3(radius * 2f, height * 0.5f, radius * 2f));
        }

        private static void SetWorldScale(Transform t, Vector3 worldScale)
        {
            Vector3 p = t.parent != null ? t.parent.lossyScale : Vector3.one;
            t.localScale = new Vector3(worldScale.x / Mathf.Max(0.0001f, p.x), worldScale.y / Mathf.Max(0.0001f, p.y), worldScale.z / Mathf.Max(0.0001f, p.z));
        }

        // A gold ring around the glowing eye (ring radius sized from the eye's own
        // radius, facing straight out of it) plus a short chain hanging down.
        private static void AddMonocle(Transform parent, Vector3 eyeWorld, Vector3 outward, float eyeRadius)
        {
            var mono = new GameObject("KE_Monocle");
            mono.transform.SetParent(parent, true);
            mono.transform.position = eyeWorld + outward.normalized * eyeRadius * 0.15f;
            mono.transform.rotation = Quaternion.LookRotation(outward);
            // Unit scale in world space so the ring's size doesn't depend on the model's own scale.
            SetWorldScale(mono.transform, Vector3.one);

            float R = eyeRadius * 1.35f, r = R * 0.09f;
            Color gold = new Color(0.85f, 0.68f, 0.2f);

            var ring = new GameObject("Ring");
            ring.transform.SetParent(mono.transform, false);
            ring.AddComponent<MeshFilter>().sharedMesh = MakeTorus(R, r, 28, 8);
            ring.AddComponent<MeshRenderer>().sharedMaterial = Flat(gold);

            // Chain: links curving down and back toward the body.
            for (int i = 1; i <= 8; i++)
            {
                var link = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                link.name = "Link";
                Object.Destroy(link.GetComponent<Collider>());
                link.transform.SetParent(mono.transform, false);
                float f = i / 8f;
                link.transform.localPosition = new Vector3(R * 0.15f * f, -R - f * R * 2.2f, -f * R * 0.8f);
                link.transform.localScale = Vector3.one * r * 1.7f;
                link.GetComponent<Renderer>().sharedMaterial = Flat(gold);
            }
        }

        // A simple gray beard: a wide puff along the jaw and a smaller tuft below it.
        private static void AddBeard(Transform parent, Bounds faceB, Vector3 facing, float w)
        {
            var beard = new GameObject("KE_Beard");
            beard.transform.SetParent(parent, true);
            // Low on the face, pushed out to the front surface so it isn't buried in the head.
            beard.transform.position = new Vector3(faceB.center.x, faceB.min.y + faceB.size.y * 0.34f, faceB.center.z) + facing * (faceB.extents.z * 0.92f);
            beard.transform.rotation = Quaternion.LookRotation(facing);

            Color gray = new Color(0.62f, 0.62f, 0.66f);
            var puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            puff.name = "Puff";
            Object.Destroy(puff.GetComponent<Collider>());
            puff.transform.SetParent(beard.transform, false);
            puff.transform.localPosition = Vector3.zero;
            SetWorldScale(puff.transform, new Vector3(w * 1.3f, w * 0.75f, w * 0.7f));
            puff.GetComponent<Renderer>().sharedMaterial = Flat(gray);

            var tuft = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            tuft.name = "Tuft";
            Object.Destroy(tuft.GetComponent<Collider>());
            tuft.transform.SetParent(beard.transform, false);
            tuft.transform.position = beard.transform.position + Vector3.down * w * 0.45f + facing * w * 0.03f;
            SetWorldScale(tuft.transform, new Vector3(w * 0.85f, w * 0.8f, w * 0.55f));
            tuft.GetComponent<Renderer>().sharedMaterial = Flat(gray);
        }

        // Torus in the XY plane (axis along Z).
        private static Mesh MakeTorus(float R, float r, int seg, int sides)
        {
            var verts = new Vector3[(seg + 1) * (sides + 1)];
            for (int i = 0; i <= seg; i++)
            {
                float u = (float)i / seg * Mathf.PI * 2f;
                for (int j = 0; j <= sides; j++)
                {
                    float v = (float)j / sides * Mathf.PI * 2f;
                    float rad = R + r * Mathf.Cos(v);
                    verts[i * (sides + 1) + j] = new Vector3(rad * Mathf.Cos(u), rad * Mathf.Sin(u), r * Mathf.Sin(v));
                }
            }
            var tris = new List<int>();
            for (int i = 0; i < seg; i++)
            {
                for (int j = 0; j < sides; j++)
                {
                    int a = i * (sides + 1) + j, b = (i + 1) * (sides + 1) + j, c = b + 1, d = a + 1;
                    tris.Add(a); tris.Add(b); tris.Add(c);
                    tris.Add(a); tris.Add(c); tris.Add(d);
                    tris.Add(a); tris.Add(c); tris.Add(b); // both faces — Unlit culls back faces otherwise
                    tris.Add(a); tris.Add(d); tris.Add(c);
                }
            }
            var mesh = new Mesh();
            mesh.vertices = verts;
            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ── stand-in Mama (only if the real model can't be found) ────────────
        private static GameObject BuildStandInMama(Transform parent, Vector3 floorPos)
        {
            var mama = new GameObject("KE_BirthdayMama");
            mama.transform.SetParent(parent, false);
            mama.transform.position = floorPos;
            mama.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            Transform m = mama.transform;
            Color fur = MamaWhite;

            Prim(PrimitiveType.Sphere, m, "Body", new Vector3(0f, 0.85f, 0f), new Vector3(0.75f, 0.95f, 0.55f), fur);
            Prim(PrimitiveType.Sphere, m, "Head", new Vector3(0f, 1.55f, 0f), new Vector3(0.5f, 0.5f, 0.48f), fur);
            Prim(PrimitiveType.Sphere, m, "EarL", new Vector3(-0.22f, 1.88f, 0f), new Vector3(0.18f, 0.28f, 0.1f), fur);
            Prim(PrimitiveType.Sphere, m, "EarR", new Vector3(0.22f, 1.88f, 0f), new Vector3(0.18f, 0.28f, 0.1f), fur);
            Prim(PrimitiveType.Sphere, m, "eyeL", new Vector3(-0.11f, 1.6f, 0.2f), new Vector3(0.08f, 0.08f, 0.05f), new Color(0.05f, 0.05f, 0.05f));
            Prim(PrimitiveType.Sphere, m, "eyeR", new Vector3(0.11f, 1.6f, 0.2f), new Vector3(0.08f, 0.08f, 0.05f), new Color(0.05f, 0.05f, 0.05f));
            Prim(PrimitiveType.Cylinder, m, "ArmL", new Vector3(-0.45f, 0.95f, 0f), new Vector3(0.14f, 0.4f, 0.14f), fur);
            Prim(PrimitiveType.Cylinder, m, "ArmR", new Vector3(0.45f, 0.95f, 0f), new Vector3(0.14f, 0.4f, 0.14f), fur);
            Prim(PrimitiveType.Cylinder, m, "LegL", new Vector3(-0.18f, 0.25f, 0f), new Vector3(0.18f, 0.25f, 0.18f), fur);
            Prim(PrimitiveType.Cylinder, m, "LegR", new Vector3(0.18f, 0.25f, 0f), new Vector3(0.18f, 0.25f, 0.18f), fur);
            return mama;
        }
    }
}
