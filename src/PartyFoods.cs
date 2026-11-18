using System.Collections.Generic;
using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// Papa's 10th Birthday! party foods and the ingredients they're made of, all
    /// built in code from simple shapes (matte, lit — like the other party props).
    /// Every model stands on y = 0 with its front toward +Z and fits in roughly a
    /// 1 x 1 unit box. The FOODS are the party stations; the INGREDIENTS are the
    /// materials that will be scattered at marked spots and crafted into them.
    /// </summary>
    public static class PartyFoods
    {
        // Crafting recipes (two ingredients each) live in PartyItems.

        public static readonly string[] FoodNames =
        {
            "Popcorn", "Cotton Candy", "Ice Cream", "Hot Dog Roller Grill",
            "Nacho Cheese Warmer", "Candy Apple", "Waffle & Crepe Maker", "Pancakes",
        };

        public static readonly string[] IngredientNames =
        {
            "Corn Kernels", "Butter", "Sugar", "Food Coloring", "Cream", "Cones", "Hot Dogs",
            "Buns", "Nacho Chips", "Cheese", "Apples", "Flour", "Milk", "Eggs",
        };

        /// <summary>Builds a food or ingredient by name at a world position/rotation. Returns null for an unknown name.</summary>
        public static GameObject Build(string name, Vector3 worldPos, Quaternion rotation, Transform parent = null)
        {
            var root = new GameObject("KE_Party_" + name);
            if (parent != null) root.transform.SetParent(parent, false);
            root.transform.position = worldPos;
            root.transform.rotation = rotation;
            Transform t = root.transform;

            switch (name)
            {
                case "Pinata": Pinata(t, false); break;
                case "Golden Pinata": Pinata(t, true); break;
                case "Popcorn": Popcorn(t); break;
                case "Hot Dog": HotDogGrill(t); break;
                case "Nachos": NachoWarmer(t); break;
                case "Loaded Hot Dog": HotDogGrill(t); break;
                case "Loaded Nachos": NachoWarmer(t); break;
                case "Waffle & Crepe": WaffleCrepe(t); break;

                // in-between products made by crafting
                case "Salted Butter": SaltedButter(t); break;
                case "Ice Cream Base": BowlOf(t, C(0.86f, 0.86f, 0.9f), null); break;
                case "Vanilla Base": BowlOf(t, C(0.85f, 0.8f, 0.6f), C(0.25f, 0.12f, 0.05f)); break;
                case "Ice Cream Mix": BowlOf(t, C(0.9f, 0.85f, 0.7f), C(0.85f, 0.7f, 0.5f)); break;
                case "Mustard Dog": HotDogInBun(t, true); break;
                case "Cheesy Nachos": CheesyNachos(t); break;
                case "Apple on a Stick": AppleOnStick(t); break;
                case "Batter": BowlOf(t, C(0.85f, 0.75f, 0.5f), null); break;
                case "Waffle Batter": BowlOf(t, C(0.85f, 0.68f, 0.3f), C(0.9f, 0.85f, 0.4f)); break;
                case "Pancake Stack": PancakeStack(t); break;
                case "Cotton Candy": CottonCandy(t); break;
                case "Ice Cream": IceCream(t); break;
                case "Hot Dog Roller Grill": HotDogGrill(t); break;
                case "Nacho Cheese Warmer": NachoWarmer(t); break;
                case "Candy Apple": CandyApples(t); break;
                case "Waffle & Crepe Maker": WaffleCrepe(t); break;
                case "Pancakes": Pancakes(t); break;

                case "Corn Kernels": CornKernels(t); break;
                case "Butter": Butter(t); break;
                case "Salt": Salt(t); break;
                case "Sugar": Sugar(t); break;
                case "Food Coloring": FoodColoring(t); break;
                case "Milk": Milk(t); break;
                case "Cream": Cream(t); break;
                case "Vanilla": Vanilla(t); break;
                case "Cones": Cones(t); break;
                case "Hot Dogs": HotDogPack(t); break;
                case "Buns": Buns(t); break;
                case "Mustard": Squeeze(t, new Color(0.85f, 0.7f, 0.1f)); break;
                case "Ketchup": Squeeze(t, new Color(0.75f, 0.12f, 0.1f)); break;
                case "Cheese": Cheese(t); break;
                case "Nacho Chips": NachoBag(t); break;
                case "Jalapenos": Jalapenos(t); break;
                case "Apples": AppleCrate(t); break;
                case "Sticks": StickBundle(t); break;
                case "Flour": Flour(t); break;
                case "Eggs": EggCarton(t); break;
                case "Syrup": Syrup(t); break;
                default:
                    Object.Destroy(root);
                    return null;
            }
            return root;
        }

        /// <summary>
        /// The small, hand/hotbar-sized version of a food — what you actually get
        /// from a machine or a piñata, as opposed to Build()'s big station model
        /// (a popper cart, a roller grill...). Falls back to Build() for foods
        /// that were never a station to begin with (Ice Cream, Pancakes, etc).
        /// </summary>
        public static GameObject BuildHeld(string name, Vector3 worldPos, Quaternion rotation, Transform parent = null)
        {
            var root = new GameObject("KE_PartyHeld_" + name);
            if (parent != null) root.transform.SetParent(parent, false);
            root.transform.position = worldPos;
            root.transform.rotation = rotation;
            Transform t = root.transform;

            switch (name)
            {
                case "Popcorn": PopcornCup(t); break;
                case "Hot Dog": HotDogInBun(t, false); break;
                case "Nachos": CheesyNachos(t); break;
                case "Waffle & Crepe": WaffleServed(t); break;
                default:
                    Object.Destroy(root);
                    return Build(name, worldPos, rotation, parent);
            }
            return root;
        }

        // ══ shape helpers (positions are the shape's CENTER, local to the model) ══
        private static Shader _lit;
        private static Shader Lit
        {
            get
            {
                if (_lit == null) _lit = Shader.Find("Standard");
                if (_lit == null) _lit = Shader.Find("Diffuse");
                if (_lit == null) _lit = Shader.Find("Unlit/Color");
                return _lit;
            }
        }

        private static Material Mat(Color c, float gloss = 0.05f)
        {
            var m = new Material(Lit);
            m.color = c;
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", gloss);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            return m;
        }

        private static GameObject Shape(PrimitiveType type, Transform p, Vector3 pos, Vector3 scale, Color c, float gloss = 0.05f)
        {
            var go = GameObject.CreatePrimitive(type);
            go.transform.SetParent(p, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            Collider col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            go.GetComponent<Renderer>().sharedMaterial = Mat(c, gloss);
            return go;
        }

        private static GameObject Box(Transform p, Vector3 pos, Vector3 size, Color c, float gloss = 0.05f)
        { return Shape(PrimitiveType.Cube, p, pos, size, c, gloss); }

        // Upright cylinder. (Unity's is 2 tall, 1 wide.)
        private static GameObject Cyl(Transform p, Vector3 pos, float radius, float height, Color c, float gloss = 0.05f)
        { return Shape(PrimitiveType.Cylinder, p, pos, new Vector3(radius * 2f, height * 0.5f, radius * 2f), c, gloss); }

        private static GameObject Sph(Transform p, Vector3 pos, Vector3 diameters, Color c, float gloss = 0.05f)
        { return Shape(PrimitiveType.Sphere, p, pos, diameters, c, gloss); }

        private static GameObject Sph(Transform p, Vector3 pos, float diameter, Color c, float gloss = 0.05f)
        { return Shape(PrimitiveType.Sphere, p, pos, Vector3.one * diameter, c, gloss); }

        // Capsule with its long axis rotated by `euler` (default along local Y).
        private static GameObject Caps(Transform p, Vector3 pos, float radius, float length, Vector3 euler, Color c, float gloss = 0.05f)
        {
            var go = Shape(PrimitiveType.Capsule, p, pos, new Vector3(radius * 2f, length * 0.5f, radius * 2f), c, gloss);
            go.transform.localRotation = Quaternion.Euler(euler);
            return go;
        }

        // Cone with its base at the local origin of the shape and apex up (apexDown flips it).
        private static GameObject Cone(Transform p, Vector3 pos, float radius, float height, Color c, bool apexDown = false)
        {
            var go = new GameObject("Cone");
            go.transform.SetParent(p, false);
            go.transform.localPosition = pos;
            if (apexDown) go.transform.localRotation = Quaternion.Euler(180f, 0f, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = ConeMesh(radius, height, 20);
            go.AddComponent<MeshRenderer>().sharedMaterial = Mat(c);
            return go;
        }

        private static Mesh ConeMesh(float radius, float height, int sides)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            verts.Add(new Vector3(0f, height * 0.5f, 0f));   // 0 apex (centered vertically on the origin)
            verts.Add(new Vector3(0f, -height * 0.5f, 0f));  // 1 base centre
            for (int i = 0; i < sides; i++)
            {
                float a = (float)i / sides * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(a) * radius, -height * 0.5f, Mathf.Sin(a) * radius));
            }
            for (int i = 0; i < sides; i++)
            {
                int a = 2 + i, b = 2 + (i + 1) % sides;
                tris.Add(0); tris.Add(b); tris.Add(a);   // side, outward
                tris.Add(0); tris.Add(a); tris.Add(b);   // and inward (both faces)
                tris.Add(1); tris.Add(a); tris.Add(b);   // base
                tris.Add(1); tris.Add(b); tris.Add(a);
            }
            var m = new Mesh();
            m.vertices = verts.ToArray();
            m.triangles = tris.ToArray();
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        private static Color C(float r, float g, float b) { return new Color(r, g, b); }

        // Palette
        private static readonly Color Steel = new Color(0.6f, 0.62f, 0.66f);
        private static readonly Color DarkSteel = new Color(0.25f, 0.26f, 0.3f);
        private static readonly Color Chrome = new Color(0.72f, 0.74f, 0.78f);
        private static readonly Color Red = new Color(0.7f, 0.12f, 0.12f);
        private static readonly Color Gold = new Color(0.8f, 0.62f, 0.15f);
        private static readonly Color CreamCol = new Color(0.85f, 0.82f, 0.65f);
        private static readonly Color Wood = new Color(0.55f, 0.38f, 0.2f);
        private static readonly Color PaperWhite = new Color(0.82f, 0.8f, 0.74f);

        // ══ THE PARTY FOODS ══════════════════════════════════════════════════
        private static void Popcorn(Transform t)
        {
            // Red cart with a glass-topped popper full of popcorn.
            Box(t, new Vector3(0f, 0.25f, 0f), new Vector3(0.55f, 0.5f, 0.45f), Red);
            Box(t, new Vector3(0f, 0.51f, 0f), new Vector3(0.58f, 0.04f, 0.48f), Gold);
            foreach (float sx in new[] { -0.26f, 0.26f })
                foreach (float sz in new[] { -0.2f, 0.2f })
                    Box(t, new Vector3(sx, 0.74f, sz), new Vector3(0.03f, 0.42f, 0.03f), Chrome, 0.3f);
            Box(t, new Vector3(0f, 0.97f, 0f), new Vector3(0.6f, 0.05f, 0.5f), Red);
            Box(t, new Vector3(0f, 1.01f, 0f), new Vector3(0.62f, 0.02f, 0.52f), Gold);
            Cyl(t, new Vector3(0f, 0.86f, 0f), 0.06f, 0.02f, DarkSteel);        // the kettle on its chain
            // Popcorn heaped inside.
            var rnd = new System.Random(7);
            for (int i = 0; i < 44; i++)
            {
                float x = ((float)rnd.NextDouble() - 0.5f) * 0.42f;
                float z = ((float)rnd.NextDouble() - 0.5f) * 0.32f;
                float heap = 1f - (Mathf.Abs(x) / 0.21f) * 0.5f;
                float y = 0.55f + (float)rnd.NextDouble() * 0.28f * heap;
                float d = 0.05f + (float)rnd.NextDouble() * 0.03f;
                bool butter = rnd.NextDouble() > 0.6;
                Sph(t, new Vector3(x, y, z), d, butter ? C(0.86f, 0.76f, 0.4f) : C(0.88f, 0.84f, 0.7f));
            }
            foreach (float sx in new[] { -0.3f, 0.3f })
            {
                var w = Cyl(t, new Vector3(sx, 0.09f, 0.16f), 0.09f, 0.04f, C(0.15f, 0.15f, 0.17f));
                w.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
        }

        private static void CottonCandy(Transform t)
        {
            // Spinner machine with a big pink floss cloud, plus a finished stick.
            Cyl(t, new Vector3(0f, 0.17f, 0f), 0.27f, 0.34f, Steel, 0.2f);
            Cyl(t, new Vector3(0f, 0.4f, 0f), 0.4f, 0.1f, C(0.75f, 0.77f, 0.8f), 0.25f);
            Cyl(t, new Vector3(0f, 0.47f, 0f), 0.09f, 0.06f, DarkSteel);
            var rnd = new System.Random(11);
            for (int i = 0; i < 14; i++)
            {
                Vector3 o = new Vector3(((float)rnd.NextDouble() - 0.5f) * 0.4f, ((float)rnd.NextDouble() - 0.4f) * 0.08f, ((float)rnd.NextDouble() - 0.5f) * 0.4f);
                Sph(t, new Vector3(0f, 0.43f, 0f) + o, 0.14f + (float)rnd.NextDouble() * 0.08f, i % 2 == 0 ? C(0.82f, 0.5f, 0.64f) : C(0.78f, 0.62f, 0.75f));
            }
            // A finished cotton candy on a paper cone.
            Cyl(t, new Vector3(0.55f, 0.16f, 0f), 0.006f, 0.34f, Wood);
            Sph(t, new Vector3(0.55f, 0.42f, 0f), new Vector3(0.24f, 0.26f, 0.24f), C(0.55f, 0.65f, 0.85f));
            Sph(t, new Vector3(0.6f, 0.46f, 0.03f), 0.16f, C(0.82f, 0.5f, 0.64f));
        }

        private static void IceCream(Transform t)
        {
            // Pedestal holding a tall cone with three scoops.
            Cyl(t, new Vector3(0f, 0.03f, 0f), 0.14f, 0.06f, DarkSteel);
            Cyl(t, new Vector3(0f, 0.2f, 0f), 0.03f, 0.3f, Chrome, 0.3f);
            Cyl(t, new Vector3(0f, 0.36f, 0f), 0.07f, 0.03f, DarkSteel);
            Cone(t, new Vector3(0f, 0.56f, 0f), 0.085f, 0.36f, C(0.75f, 0.5f, 0.2f), true); // waffle cone, apex down
            Sph(t, new Vector3(0f, 0.75f, 0f), new Vector3(0.19f, 0.17f, 0.19f), C(0.6f, 0.3f, 0.2f));   // chocolate
            Sph(t, new Vector3(0f, 0.85f, 0f), new Vector3(0.18f, 0.16f, 0.18f), C(0.85f, 0.8f, 0.7f));  // vanilla
            Sph(t, new Vector3(0f, 0.94f, 0f), new Vector3(0.16f, 0.15f, 0.16f), C(0.85f, 0.5f, 0.6f));  // strawberry
            Sph(t, new Vector3(0f, 1.03f, 0f), 0.05f, C(0.75f, 0.08f, 0.1f), 0.4f);                       // cherry
            var rnd = new System.Random(3);
            Color[] sprinkle = { C(0.8f, 0.3f, 0.3f), C(0.3f, 0.5f, 0.8f), C(0.8f, 0.7f, 0.2f), C(0.4f, 0.7f, 0.4f) };
            for (int i = 0; i < 14; i++)
            {
                float a = (float)rnd.NextDouble() * Mathf.PI * 2f;
                float r = 0.045f + (float)rnd.NextDouble() * 0.03f;
                var s = Box(t, new Vector3(Mathf.Cos(a) * r, 0.955f + (float)rnd.NextDouble() * 0.03f, Mathf.Sin(a) * r), new Vector3(0.012f, 0.004f, 0.004f), sprinkle[i % 4]);
                s.transform.localRotation = Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f);
            }
        }

        private static void HotDogGrill(Transform t)
        {
            // Roller grill: steel base, chrome rollers, a row of hot dogs, a glass hood frame.
            Box(t, new Vector3(0f, 0.1f, 0f), new Vector3(0.72f, 0.2f, 0.4f), Steel, 0.2f);
            Box(t, new Vector3(0f, 0.205f, 0f), new Vector3(0.72f, 0.012f, 0.4f), DarkSteel);
            for (int i = 0; i < 6; i++)
            {
                float z = -0.15f + i * 0.06f;
                var roller = Cyl(t, new Vector3(0f, 0.225f, z), 0.018f, 0.68f, Chrome, 0.4f);
                roller.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
            for (int i = 0; i < 5; i++)
            {
                float z = -0.12f + i * 0.06f;
                Caps(t, new Vector3(0f, 0.262f, z), 0.028f, 0.5f, new Vector3(0f, 0f, 90f), C(0.65f, 0.25f, 0.15f), 0.3f);
            }
            // Hood: four posts, roof bar, and a heat-lamp strip.
            foreach (float sx in new[] { -0.35f, 0.35f })
                foreach (float sz in new[] { -0.19f, 0.19f })
                    Box(t, new Vector3(sx, 0.4f, sz), new Vector3(0.02f, 0.4f, 0.02f), Chrome, 0.3f);
            Box(t, new Vector3(0f, 0.6f, 0f), new Vector3(0.74f, 0.03f, 0.42f), Steel, 0.2f);
            Box(t, new Vector3(0f, 0.575f, 0f), new Vector3(0.6f, 0.012f, 0.05f), C(0.85f, 0.5f, 0.2f));
            // Control knobs.
            foreach (float sx in new[] { -0.2f, 0f, 0.2f })
            {
                var k = Cyl(t, new Vector3(sx, 0.1f, 0.205f), 0.025f, 0.02f, C(0.15f, 0.15f, 0.17f));
                k.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
        }

        private static void NachoWarmer(Transform t)
        {
            // Cheese warmer with a pump, and a tray of nachos in front.
            Box(t, new Vector3(0f, 0.08f, -0.1f), new Vector3(0.36f, 0.16f, 0.32f), Red);
            Cyl(t, new Vector3(0f, 0.27f, -0.1f), 0.13f, 0.22f, Steel, 0.25f);
            Cyl(t, new Vector3(0f, 0.36f, -0.1f), 0.115f, 0.06f, C(0.9f, 0.6f, 0.1f));           // cheese, open top
            Cyl(t, new Vector3(0.09f, 0.5f, -0.1f), 0.014f, 0.3f, Chrome, 0.4f);                   // pump column
            Box(t, new Vector3(0.09f, 0.66f, -0.1f), new Vector3(0.04f, 0.03f, 0.16f), DarkSteel); // handle bar
            Sph(t, new Vector3(0.09f, 0.66f, -0.19f), 0.04f, C(0.15f, 0.15f, 0.17f));
            var nozzle = Cyl(t, new Vector3(0.09f, 0.36f, 0.02f), 0.012f, 0.1f, Chrome, 0.4f);
            nozzle.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            // Nacho tray.
            Box(t, new Vector3(0f, 0.012f, 0.34f), new Vector3(0.36f, 0.024f, 0.24f), C(0.75f, 0.2f, 0.15f));
            var rnd = new System.Random(5);
            for (int i = 0; i < 12; i++)
            {
                float x = ((float)rnd.NextDouble() - 0.5f) * 0.28f, z = 0.34f + ((float)rnd.NextDouble() - 0.5f) * 0.16f;
                var chip = Box(t, new Vector3(x, 0.03f + (float)rnd.NextDouble() * 0.02f, z), new Vector3(0.06f, 0.008f, 0.06f), C(0.78f, 0.6f, 0.25f));
                chip.transform.localRotation = Quaternion.Euler((float)rnd.NextDouble() * 12f, 45f + (float)rnd.NextDouble() * 20f, (float)rnd.NextDouble() * 12f);
            }
            for (int i = 0; i < 4; i++)
                Cyl(t, new Vector3(-0.09f + i * 0.06f, 0.045f, 0.34f + (i % 2 == 0 ? 0.03f : -0.03f)), 0.03f, 0.008f, C(0.9f, 0.6f, 0.1f));
        }

        private static void CandyApples(Transform t)
        {
            // A wooden display board with three glossy red candy apples on sticks.
            Box(t, new Vector3(0f, 0.02f, 0f), new Vector3(0.55f, 0.04f, 0.25f), Wood);
            for (int i = 0; i < 3; i++)
            {
                float x = -0.18f + i * 0.18f;
                Cyl(t, new Vector3(x, 0.16f, 0f), 0.008f, 0.24f, C(0.75f, 0.6f, 0.35f));                      // stick
                Sph(t, new Vector3(x, 0.14f, 0f), new Vector3(0.15f, 0.13f, 0.15f), C(0.72f, 0.05f, 0.09f), 0.6f);
                Sph(t, new Vector3(x - 0.03f, 0.18f, 0.04f), 0.03f, C(0.9f, 0.6f, 0.6f), 0.7f);                 // shine
                Cyl(t, new Vector3(x, 0.205f, 0f), 0.014f, 0.02f, C(0.35f, 0.2f, 0.1f));                        // stem
            }
        }

        private static void WaffleCrepe(Transform t)
        {
            // Shared base: a round crepe plate on the left, a hinged waffle iron on the right.
            Box(t, new Vector3(0f, 0.04f, 0f), new Vector3(0.78f, 0.08f, 0.42f), DarkSteel, 0.2f);
            Cyl(t, new Vector3(-0.2f, 0.09f, 0f), 0.16f, 0.02f, C(0.12f, 0.12f, 0.14f), 0.3f);   // crepe plate
            Cyl(t, new Vector3(-0.2f, 0.105f, 0f), 0.135f, 0.012f, C(0.78f, 0.62f, 0.38f));       // crepe
            // Spreader: handle + T bar.
            var h = Cyl(t, new Vector3(-0.2f, 0.13f, 0.06f), 0.008f, 0.22f, Wood);
            h.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Box(t, new Vector3(-0.2f, 0.115f, 0.06f), new Vector3(0.005f, 0.012f, 0.13f), Wood);
            // Waffle iron: bottom plate with a golden waffle, open lid.
            Box(t, new Vector3(0.22f, 0.11f, 0f), new Vector3(0.3f, 0.06f, 0.3f), C(0.15f, 0.15f, 0.17f), 0.3f);
            Box(t, new Vector3(0.22f, 0.148f, 0f), new Vector3(0.26f, 0.018f, 0.26f), C(0.75f, 0.55f, 0.2f));
            for (int i = 0; i < 5; i++)
            {
                float o = -0.1f + i * 0.05f;
                Box(t, new Vector3(0.22f + o, 0.16f, 0f), new Vector3(0.008f, 0.006f, 0.26f), C(0.55f, 0.38f, 0.12f));
                Box(t, new Vector3(0.22f, 0.16f, o), new Vector3(0.26f, 0.006f, 0.008f), C(0.55f, 0.38f, 0.12f));
            }
            var lid = new GameObject("Lid");
            lid.transform.SetParent(t, false);
            lid.transform.localPosition = new Vector3(0.22f, 0.14f, -0.15f);   // hinge at the back edge
            lid.transform.localRotation = Quaternion.Euler(-70f, 0f, 0f);
            Box(lid.transform, new Vector3(0f, 0.03f, 0.15f), new Vector3(0.3f, 0.06f, 0.3f), C(0.18f, 0.18f, 0.2f), 0.3f);
            Cyl(t, new Vector3(0.22f, 0.11f, 0.17f), 0.014f, 0.1f, C(0.15f, 0.15f, 0.17f)).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }

        private static void Pancakes(Transform t)
        {
            Cyl(t, new Vector3(0f, 0.01f, 0f), 0.19f, 0.02f, C(0.8f, 0.8f, 0.82f), 0.3f);
            Cyl(t, new Vector3(0f, 0.025f, 0f), 0.15f, 0.006f, C(0.7f, 0.72f, 0.78f));
            var rnd = new System.Random(2);
            for (int i = 0; i < 5; i++)
            {
                var p = Cyl(t, new Vector3(((float)rnd.NextDouble() - 0.5f) * 0.02f, 0.045f + i * 0.03f, ((float)rnd.NextDouble() - 0.5f) * 0.02f), 0.13f, 0.028f, i % 2 == 0 ? C(0.75f, 0.55f, 0.28f) : C(0.68f, 0.48f, 0.24f));
                p.transform.localRotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
            }
            float top = 0.045f + 4 * 0.03f + 0.014f;
            Cyl(t, new Vector3(0f, top + 0.003f, 0f), 0.095f, 0.006f, C(0.35f, 0.17f, 0.05f), 0.5f);        // syrup on top
            Box(t, new Vector3(0.01f, top + 0.02f, 0f), new Vector3(0.05f, 0.03f, 0.05f), C(0.88f, 0.8f, 0.4f)); // butter
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f + 0.5f;
                Box(t, new Vector3(Mathf.Cos(a) * 0.115f, top - 0.05f, Mathf.Sin(a) * 0.115f), new Vector3(0.025f, 0.09f, 0.025f), C(0.35f, 0.17f, 0.05f), 0.5f); // drips
            }
            for (int i = 0; i < 5; i++)
                Sph(t, new Vector3(Mathf.Cos(i * 1.3f) * 0.07f, top + 0.012f, Mathf.Sin(i * 1.3f) * 0.07f), 0.022f, C(0.2f, 0.25f, 0.55f));
        }

        // ══ THE PIÑATA ═══════════════════════════════════════════════════════
        // A striped donkey piñata hanging from a rope (body centre 1.1 up, rope to 1.95).
        private static void Pinata(Transform t, bool golden)
        {
            Color[] stripes = golden
                ? new[] { C(0.95f, 0.8f, 0.2f), C(0.8f, 0.6f, 0.1f), C(1f, 0.9f, 0.5f), C(0.85f, 0.65f, 0.15f), C(0.7f, 0.5f, 0.08f) }
                : new[] { C(0.8f, 0.2f, 0.25f), C(0.9f, 0.7f, 0.15f), C(0.25f, 0.5f, 0.8f), C(0.3f, 0.7f, 0.35f), C(0.85f, 0.4f, 0.65f) };
            // Body: rings of fringe along a horizontal axis.
            for (int i = 0; i < 5; i++)
            {
                var ring = Cyl(t, new Vector3(-0.2f + i * 0.1f, 1.1f, 0f), 0.17f - Mathf.Abs(i - 2) * 0.012f, 0.1f, stripes[i]);
                ring.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
            // Head, snout, ears.
            Sph(t, new Vector3(0.36f, 1.2f, 0f), 0.24f, stripes[1]);
            Sph(t, new Vector3(0.48f, 1.15f, 0f), new Vector3(0.14f, 0.11f, 0.11f), stripes[4]);
            Box(t, new Vector3(0.33f, 1.38f, 0.07f), new Vector3(0.04f, 0.16f, 0.05f), stripes[2]);
            Box(t, new Vector3(0.33f, 1.38f, -0.07f), new Vector3(0.04f, 0.16f, 0.05f), stripes[2]);
            // Legs and tail.
            foreach (float lx in new[] { -0.2f, 0.2f })
                foreach (float lz in new[] { -0.09f, 0.09f })
                    Cyl(t, new Vector3(lx, 0.9f, lz), 0.03f, 0.2f, stripes[(lx > 0 ? 0 : 3)]);
            Box(t, new Vector3(-0.32f, 1.18f, 0f), new Vector3(0.04f, 0.12f, 0.04f), stripes[0]);
            // Fringe flecks.
            var rnd = new System.Random(21);
            for (int i = 0; i < 16; i++)
                Sph(t, new Vector3(-0.22f + (float)rnd.NextDouble() * 0.5f, 1.1f + ((float)rnd.NextDouble() - 0.5f) * 0.36f, ((float)rnd.NextDouble() - 0.5f) * 0.36f), 0.03f, stripes[i % 5]);
            // Rope and hook.
            Cyl(t, new Vector3(0f, 1.55f, 0f), 0.006f, 0.8f, C(0.6f, 0.5f, 0.35f));
            Sph(t, new Vector3(0f, 1.96f, 0f), 0.05f, C(0.3f, 0.3f, 0.32f));
        }

        // ══ IN-BETWEEN PRODUCTS ══════════════════════════════════════════════
        private static void SaltedButter(Transform t)
        {
            Box(t, new Vector3(0f, 0.03f, 0f), new Vector3(0.16f, 0.06f, 0.07f), C(0.85f, 0.75f, 0.35f));
            var rnd = new System.Random(8);
            for (int i = 0; i < 12; i++)
                Sph(t, new Vector3(((float)rnd.NextDouble() - 0.5f) * 0.14f, 0.062f, ((float)rnd.NextDouble() - 0.5f) * 0.06f), 0.008f, C(0.9f, 0.9f, 0.92f));
        }

        // A white bowl with something in it (and optional flecks).
        private static void BowlOf(Transform t, Color content, Color? speck)
        {
            Cyl(t, new Vector3(0f, 0.04f, 0f), 0.12f, 0.08f, C(0.85f, 0.85f, 0.88f), 0.3f);
            Cyl(t, new Vector3(0f, 0.082f, 0f), 0.105f, 0.008f, content, 0.2f);
            if (speck.HasValue)
            {
                var rnd = new System.Random(13);
                for (int i = 0; i < 14; i++)
                {
                    float a = (float)rnd.NextDouble() * Mathf.PI * 2f, r = (float)rnd.NextDouble() * 0.09f;
                    Sph(t, new Vector3(Mathf.Cos(a) * r, 0.088f, Mathf.Sin(a) * r), 0.012f, speck.Value);
                }
            }
        }

        private static void HotDogInBun(Transform t, bool mustard)
        {
            Caps(t, new Vector3(0f, 0.04f, 0f), 0.045f, 0.24f, new Vector3(0f, 0f, 90f), C(0.8f, 0.6f, 0.35f));
            Caps(t, new Vector3(0f, 0.075f, 0f), 0.03f, 0.27f, new Vector3(0f, 0f, 90f), C(0.7f, 0.28f, 0.18f), 0.3f);
            if (mustard)
                for (int i = 0; i < 7; i++)
                    Box(t, new Vector3(-0.09f + i * 0.03f, 0.105f, 0f), new Vector3(0.02f, 0.008f, 0.03f), C(0.85f, 0.7f, 0.1f)).transform.localRotation = Quaternion.Euler(0f, (i % 2 == 0 ? 30f : -30f), 0f);
        }

        private static void CheesyNachos(Transform t)
        {
            Box(t, new Vector3(0f, 0.012f, 0f), new Vector3(0.26f, 0.024f, 0.18f), C(0.75f, 0.2f, 0.15f));
            var rnd = new System.Random(5);
            for (int i = 0; i < 10; i++)
            {
                var chip = Box(t, new Vector3(((float)rnd.NextDouble() - 0.5f) * 0.2f, 0.03f + (float)rnd.NextDouble() * 0.02f, ((float)rnd.NextDouble() - 0.5f) * 0.12f), new Vector3(0.055f, 0.008f, 0.055f), C(0.78f, 0.6f, 0.25f));
                chip.transform.localRotation = Quaternion.Euler((float)rnd.NextDouble() * 12f, 45f + (float)rnd.NextDouble() * 20f, (float)rnd.NextDouble() * 12f);
            }
            for (int i = 0; i < 5; i++)
                Cyl(t, new Vector3(-0.08f + i * 0.04f, 0.05f, (i % 2 == 0 ? 0.03f : -0.03f)), 0.028f, 0.008f, C(0.9f, 0.6f, 0.1f));
        }

        private static void AppleOnStick(Transform t)
        {
            Cyl(t, new Vector3(0f, 0.13f, 0f), 0.008f, 0.26f, C(0.75f, 0.6f, 0.35f));
            Sph(t, new Vector3(0f, 0.16f, 0f), new Vector3(0.15f, 0.13f, 0.15f), C(0.7f, 0.1f, 0.1f), 0.3f);
            Cyl(t, new Vector3(0f, 0.228f, 0f), 0.014f, 0.02f, C(0.35f, 0.2f, 0.1f));
        }

        private static void PancakeStack(Transform t)
        {
            Cyl(t, new Vector3(0f, 0.01f, 0f), 0.16f, 0.02f, C(0.8f, 0.8f, 0.82f), 0.3f);
            for (int i = 0; i < 3; i++)
                Cyl(t, new Vector3(0f, 0.035f + i * 0.03f, 0f), 0.12f, 0.028f, i % 2 == 0 ? C(0.75f, 0.55f, 0.28f) : C(0.68f, 0.48f, 0.24f));
        }

        // A striped paper popcorn cup, filled and slightly overflowing — what you actually carry, not the cart.
        private static void PopcornCup(Transform t)
        {
            Color paperRed = C(0.75f, 0.15f, 0.15f), paperWhite = C(0.9f, 0.88f, 0.82f);
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * 360f;
                var stripe = Box(t, Vector3.zero, new Vector3(0.02f, 0.16f, 0.09f), (i % 2 == 0) ? paperRed : paperWhite);
                stripe.transform.localPosition = Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0.08f, 0.075f);
                stripe.transform.localRotation = Quaternion.Euler(0f, a, 0f);
            }
            Cyl(t, new Vector3(0f, 0.005f, 0f), 0.06f, 0.01f, paperRed);
            var rnd = new System.Random(17);
            for (int i = 0; i < 22; i++)
            {
                float a = (float)rnd.NextDouble() * Mathf.PI * 2f, r = (float)rnd.NextDouble() * 0.075f;
                float y = 0.16f + (float)rnd.NextDouble() * 0.07f * (1f - r / 0.08f);
                bool butter = rnd.NextDouble() > 0.6;
                Sph(t, new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r), 0.035f + (float)rnd.NextDouble() * 0.02f, butter ? C(0.86f, 0.76f, 0.4f) : C(0.88f, 0.84f, 0.7f));
            }
        }

        // A plate with a folded waffle (or rolled crepe) and a syrup drizzle — the actual serving, not the maker.
        private static void WaffleServed(Transform t)
        {
            Cyl(t, new Vector3(0f, 0.01f, 0f), 0.17f, 0.02f, C(0.8f, 0.8f, 0.82f), 0.3f);
            Box(t, new Vector3(-0.04f, 0.035f, 0f), new Vector3(0.2f, 0.03f, 0.18f), C(0.75f, 0.55f, 0.2f));
            for (int i = 0; i < 4; i++)
            {
                float o = -0.075f + i * 0.05f;
                Box(t, new Vector3(-0.04f + o, 0.052f, 0f), new Vector3(0.007f, 0.006f, 0.16f), C(0.55f, 0.38f, 0.12f));
                Box(t, new Vector3(-0.04f, 0.052f, o), new Vector3(0.18f, 0.006f, 0.007f), C(0.55f, 0.38f, 0.12f));
            }
            var crepe = Cyl(t, new Vector3(0.09f, 0.045f, 0f), 0.075f, 0.008f, C(0.85f, 0.72f, 0.4f));
            Cyl(t, new Vector3(-0.04f, 0.058f, 0f), 0.03f, 0.006f, C(0.85f, 0.75f, 0.4f), 0.3f);
            for (int i = 0; i < 3; i++)
                Box(t, new Vector3(-0.09f + i * 0.02f, 0.04f, 0.06f), new Vector3(0.012f, 0.05f, 0.012f), C(0.35f, 0.17f, 0.05f), 0.5f);
            for (int i = 0; i < 3; i++)
                Sph(t, new Vector3(0.09f + Mathf.Cos(i * 2.1f) * 0.05f, 0.06f, Mathf.Sin(i * 2.1f) * 0.05f), 0.02f, C(0.2f, 0.25f, 0.55f));
        }

        // ══ THE INGREDIENTS ══════════════════════════════════════════════════
        private static void CornKernels(Transform t)
        {
            Cyl(t, new Vector3(0f, 0.14f, 0f), 0.12f, 0.28f, C(0.6f, 0.5f, 0.3f));
            Sph(t, new Vector3(0f, 0.3f, 0f), new Vector3(0.12f, 0.08f, 0.12f), C(0.55f, 0.45f, 0.27f));
            var rnd = new System.Random(9);
            for (int i = 0; i < 16; i++)
            {
                float a = (float)rnd.NextDouble() * Mathf.PI * 2f, r = 0.11f + (float)rnd.NextDouble() * 0.09f;
                Sph(t, new Vector3(Mathf.Cos(a) * r, 0.008f, Mathf.Sin(a) * r), new Vector3(0.014f, 0.01f, 0.018f), C(0.85f, 0.7f, 0.1f));
            }
        }

        private static void Butter(Transform t)
        {
            Box(t, new Vector3(0f, 0.03f, 0f), new Vector3(0.16f, 0.06f, 0.07f), C(0.85f, 0.75f, 0.35f));
            Box(t, new Vector3(-0.03f, 0.033f, 0f), new Vector3(0.11f, 0.062f, 0.074f), PaperWhite);   // half-wrapped
            Box(t, new Vector3(-0.03f, 0.065f, 0f), new Vector3(0.06f, 0.004f, 0.05f), C(0.75f, 0.2f, 0.2f));
        }

        private static void Salt(Transform t)
        {
            Cyl(t, new Vector3(0f, 0.07f, 0f), 0.05f, 0.14f, C(0.85f, 0.85f, 0.88f), 0.3f);
            Box(t, new Vector3(0f, 0.07f, 0f), new Vector3(0.102f, 0.04f, 0.102f), C(0.3f, 0.45f, 0.75f));
            Cyl(t, new Vector3(0f, 0.155f, 0f), 0.052f, 0.03f, Chrome, 0.4f);
            for (int i = 0; i < 5; i++)
                Sph(t, new Vector3(Mathf.Cos(i * 1.26f) * 0.02f, 0.172f, Mathf.Sin(i * 1.26f) * 0.02f), 0.008f, C(0.15f, 0.15f, 0.17f));
        }

        private static void Sugar(Transform t)
        {
            Box(t, new Vector3(0f, 0.11f, 0f), new Vector3(0.16f, 0.22f, 0.08f), C(0.85f, 0.85f, 0.88f));
            Box(t, new Vector3(0f, 0.11f, 0.041f), new Vector3(0.16f, 0.06f, 0.004f), C(0.3f, 0.5f, 0.8f));
            Box(t, new Vector3(0f, 0.225f, 0f), new Vector3(0.16f, 0.02f, 0.08f), C(0.78f, 0.78f, 0.82f));
        }

        private static void FoodColoring(Transform t)
        {
            Color[] cols = { C(0.75f, 0.12f, 0.15f), C(0.15f, 0.3f, 0.75f), C(0.2f, 0.6f, 0.25f) };
            for (int i = 0; i < 3; i++)
            {
                float x = -0.06f + i * 0.06f;
                Cyl(t, new Vector3(x, 0.045f, 0f), 0.025f, 0.09f, cols[i], 0.4f);
                Cyl(t, new Vector3(x, 0.098f, 0f), 0.014f, 0.024f, C(0.88f, 0.88f, 0.9f));
                Sph(t, new Vector3(x, 0.118f, 0f), new Vector3(0.016f, 0.02f, 0.016f), C(0.88f, 0.88f, 0.9f));
            }
        }

        private static void Milk(Transform t) { Carton(t, C(0.85f, 0.85f, 0.88f), C(0.35f, 0.55f, 0.85f)); }
        private static void Cream(Transform t) { Carton(t, C(0.88f, 0.84f, 0.65f), C(0.8f, 0.4f, 0.55f)); }

        private static void Carton(Transform t, Color body, Color band)
        {
            Box(t, new Vector3(0f, 0.07f, 0f), new Vector3(0.09f, 0.14f, 0.09f), body);
            Box(t, new Vector3(0f, 0.07f, 0f), new Vector3(0.092f, 0.05f, 0.092f), band);
            var roofL = Box(t, new Vector3(-0.022f, 0.16f, 0f), new Vector3(0.05f, 0.005f, 0.09f), body);
            roofL.transform.localRotation = Quaternion.Euler(0f, 0f, 35f);
            var roofR = Box(t, new Vector3(0.022f, 0.16f, 0f), new Vector3(0.05f, 0.005f, 0.09f), body);
            roofR.transform.localRotation = Quaternion.Euler(0f, 0f, -35f);
            Cyl(t, new Vector3(0f, 0.185f, 0f), 0.012f, 0.012f, C(0.9f, 0.9f, 0.92f));
        }

        private static void Vanilla(Transform t)
        {
            Cyl(t, new Vector3(0f, 0.05f, 0f), 0.03f, 0.1f, C(0.3f, 0.15f, 0.05f), 0.4f);
            Cyl(t, new Vector3(0f, 0.11f, 0f), 0.015f, 0.03f, C(0.3f, 0.15f, 0.05f), 0.4f);
            Cyl(t, new Vector3(0f, 0.135f, 0f), 0.017f, 0.02f, C(0.88f, 0.88f, 0.9f));
            Box(t, new Vector3(0f, 0.05f, 0.031f), new Vector3(0.05f, 0.05f, 0.004f), C(0.85f, 0.8f, 0.6f));
        }

        private static void Cones(Transform t)
        {
            Box(t, new Vector3(0f, 0.015f, 0f), new Vector3(0.2f, 0.03f, 0.13f), C(0.65f, 0.65f, 0.68f));
            for (int i = 0; i < 3; i++)
                Cone(t, new Vector3(-0.06f + i * 0.06f, 0.11f, 0f), 0.028f, 0.16f, C(0.75f, 0.5f, 0.2f), true);
            for (int i = 0; i < 3; i++)
                Cyl(t, new Vector3(-0.06f + i * 0.06f, 0.2f, 0f), 0.03f, 0.012f, C(0.78f, 0.55f, 0.24f)); // rims
        }

        private static void HotDogPack(Transform t)
        {
            Box(t, new Vector3(0f, 0.02f, 0f), new Vector3(0.22f, 0.04f, 0.11f), C(0.85f, 0.85f, 0.88f));
            for (int i = 0; i < 3; i++)
                Caps(t, new Vector3(0f, 0.055f, -0.03f + i * 0.03f), 0.014f, 0.2f, new Vector3(0f, 0f, 90f), C(0.7f, 0.3f, 0.2f), 0.3f);
            Box(t, new Vector3(0f, 0.045f, 0.056f), new Vector3(0.22f, 0.03f, 0.004f), C(0.75f, 0.15f, 0.15f));
        }

        private static void Buns(Transform t)
        {
            for (int i = 0; i < 2; i++)
            {
                Caps(t, new Vector3(0f, 0.04f + i * 0.06f, 0f), 0.04f, 0.22f, new Vector3(0f, 0f, 90f), C(0.8f, 0.6f, 0.35f));
                Box(t, new Vector3(0f, 0.045f + i * 0.06f, 0.038f), new Vector3(0.18f, 0.006f, 0.008f), C(0.62f, 0.42f, 0.2f));
            }
        }

        private static void Squeeze(Transform t, Color c)
        {
            Cyl(t, new Vector3(0f, 0.07f, 0f), 0.03f, 0.14f, c, 0.4f);
            Cyl(t, new Vector3(0f, 0.155f, 0f), 0.02f, 0.03f, c, 0.4f);
            Cone(t, new Vector3(0f, 0.19f, 0f), 0.014f, 0.04f, c);
        }

        private static void Cheese(Transform t)
        {
            Box(t, new Vector3(0f, 0.045f, 0f), new Vector3(0.2f, 0.09f, 0.12f), C(0.9f, 0.7f, 0.15f));
            Box(t, new Vector3(0f, 0.091f, 0f), new Vector3(0.2f, 0.004f, 0.12f), C(0.95f, 0.78f, 0.25f));
            var rnd = new System.Random(4);
            for (int i = 0; i < 6; i++)
                Sph(t, new Vector3(((float)rnd.NextDouble() - 0.5f) * 0.16f, 0.03f + (float)rnd.NextDouble() * 0.05f, 0.058f), 0.02f + (float)rnd.NextDouble() * 0.015f, C(0.75f, 0.55f, 0.08f));
        }

        private static void NachoBag(Transform t)
        {
            Box(t, new Vector3(0f, 0.13f, 0f), new Vector3(0.2f, 0.26f, 0.07f), C(0.8f, 0.2f, 0.1f));
            Box(t, new Vector3(0f, 0.13f, 0.037f), new Vector3(0.12f, 0.12f, 0.004f), C(0.9f, 0.7f, 0.1f));
            var tri = Box(t, new Vector3(0f, 0.13f, 0.04f), new Vector3(0.06f, 0.06f, 0.004f), C(0.75f, 0.45f, 0.1f));
            tri.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var rnd = new System.Random(6);
            for (int i = 0; i < 5; i++)
            {
                var chip = Box(t, new Vector3(((float)rnd.NextDouble() - 0.5f) * 0.3f, 0.004f, 0.06f + (float)rnd.NextDouble() * 0.06f), new Vector3(0.05f, 0.006f, 0.05f), C(0.78f, 0.6f, 0.25f));
                chip.transform.localRotation = Quaternion.Euler(0f, 45f + (float)rnd.NextDouble() * 20f, 0f);
            }
        }

        private static void Jalapenos(Transform t)
        {
            for (int i = 0; i < 4; i++)
            {
                float x = -0.06f + i * 0.04f, z = (i % 2 == 0 ? 0.02f : -0.02f);
                Sph(t, new Vector3(x, 0.03f, z), new Vector3(0.03f, 0.03f, 0.09f), C(0.25f, 0.55f, 0.15f), 0.3f);
                Cyl(t, new Vector3(x, 0.03f, z - 0.05f), 0.006f, 0.02f, C(0.3f, 0.4f, 0.12f)).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
        }

        private static void AppleCrate(Transform t)
        {
            Box(t, new Vector3(0f, 0.02f, 0f), new Vector3(0.26f, 0.04f, 0.18f), Wood);
            foreach (float sx in new[] { -0.125f, 0.125f }) Box(t, new Vector3(sx, 0.06f, 0f), new Vector3(0.01f, 0.08f, 0.18f), Wood);
            foreach (float sz in new[] { -0.085f, 0.085f }) Box(t, new Vector3(0f, 0.06f, sz), new Vector3(0.26f, 0.08f, 0.01f), Wood);
            for (int i = 0; i < 3; i++)
            {
                float x = -0.08f + i * 0.08f;
                Sph(t, new Vector3(x, 0.09f, (i % 2 == 0) ? 0.02f : -0.02f), new Vector3(0.085f, 0.075f, 0.085f), C(0.7f, 0.1f, 0.1f), 0.4f);
                Cyl(t, new Vector3(x, 0.135f, (i % 2 == 0) ? 0.02f : -0.02f), 0.004f, 0.02f, C(0.35f, 0.2f, 0.1f));
            }
        }

        private static void StickBundle(Transform t)
        {
            for (int i = 0; i < 10; i++)
            {
                float a = i / 10f * Mathf.PI * 2f;
                Cyl(t, new Vector3(Mathf.Cos(a) * 0.02f, 0.1f, Mathf.Sin(a) * 0.02f), 0.006f, 0.2f, C(0.75f, 0.6f, 0.35f));
            }
            Cyl(t, new Vector3(0f, 0.1f, 0f), 0.034f, 0.03f, C(0.7f, 0.15f, 0.15f));
        }

        private static void Flour(Transform t)
        {
            Box(t, new Vector3(0f, 0.13f, 0f), new Vector3(0.16f, 0.26f, 0.08f), C(0.8f, 0.75f, 0.65f));
            Box(t, new Vector3(0f, 0.14f, 0.041f), new Vector3(0.1f, 0.1f, 0.004f), C(0.55f, 0.35f, 0.18f));
            Box(t, new Vector3(0f, 0.265f, 0f), new Vector3(0.16f, 0.02f, 0.08f), C(0.72f, 0.68f, 0.58f));
        }

        private static void EggCarton(Transform t)
        {
            Box(t, new Vector3(0f, 0.025f, 0f), new Vector3(0.25f, 0.05f, 0.1f), C(0.6f, 0.55f, 0.45f));
            for (int i = 0; i < 6; i++)
            {
                float x = -0.08f + (i % 3) * 0.08f, z = (i / 3 == 0) ? -0.024f : 0.024f;
                Sph(t, new Vector3(x, 0.075f, z), new Vector3(0.045f, 0.06f, 0.045f), C(0.86f, 0.8f, 0.7f), 0.3f);
            }
        }

        private static void Syrup(Transform t)
        {
            Cyl(t, new Vector3(0f, 0.07f, 0f), 0.04f, 0.14f, C(0.5f, 0.25f, 0.05f), 0.5f);
            Cyl(t, new Vector3(0f, 0.16f, 0f), 0.02f, 0.05f, C(0.5f, 0.25f, 0.05f), 0.5f);
            Cyl(t, new Vector3(0f, 0.195f, 0f), 0.024f, 0.03f, C(0.75f, 0.12f, 0.12f));
            Box(t, new Vector3(0f, 0.07f, 0.041f), new Vector3(0.05f, 0.06f, 0.004f), C(0.85f, 0.75f, 0.5f));
        }
    }
}
