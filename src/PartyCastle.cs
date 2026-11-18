using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// The party's inflatable bouncy castle — built big, at one hand-marked
    /// spot. Just the structure for now (walkable floor, open front, four
    /// corner towers, a back wall, flags): the "carry a tattletail onto it,
    /// it hops around, come get it before Entertainment drops" mechanic isn't
    /// wired up yet — still need the Entertainment-loss amount and which
    /// tattletails ever ask for it.
    /// </summary>
    public static class PartyCastle
    {
        private static Shader _lit;
        private static Shader Lit { get { return _lit != null ? _lit : (_lit = Shader.Find("Standard") ?? Shader.Find("Diffuse")); } }

        private static readonly Color Red = new Color(0.78f, 0.15f, 0.15f);
        private static readonly Color Yellow = new Color(0.9f, 0.78f, 0.15f);
        private static readonly Color Blue = new Color(0.15f, 0.4f, 0.8f);
        private static readonly Color Floor = new Color(0.8f, 0.25f, 0.3f);

        private static Material Mat(Color c)
        {
            var m = new Material(Lit);
            m.color = c;
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.4f); // inflatable vinyl has a bit of sheen
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            return m;
        }

        private static GameObject Prim(PrimitiveType type, Transform p, Vector3 pos, Vector3 scale, Color c)
        {
            var go = GameObject.CreatePrimitive(type);
            go.transform.SetParent(p, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            go.GetComponent<Renderer>().sharedMaterial = Mat(c);
            return go;
        }

        private static GameObject Cyl(Transform p, Vector3 pos, float radius, float height, Color c)
        { return Prim(PrimitiveType.Cylinder, p, pos, new Vector3(radius * 2f, height * 0.5f, radius * 2f), c); }

        private static GameObject Cone(Transform p, Vector3 pos, float radius, float height, Color c)
        {
            var go = new GameObject("Cone");
            go.transform.SetParent(p, false);
            go.transform.localPosition = pos;
            go.AddComponent<MeshFilter>().sharedMesh = ConeMesh(radius, height, 16);
            go.AddComponent<MeshRenderer>().sharedMaterial = Mat(c);
            return go;
        }

        private static Mesh ConeMesh(float radius, float height, int sides)
        {
            var verts = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();
            verts.Add(new Vector3(0f, height, 0f));
            verts.Add(new Vector3(0f, 0f, 0f));
            for (int i = 0; i < sides; i++)
            {
                float a = (float)i / sides * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
            }
            for (int i = 0; i < sides; i++)
            {
                int a = 2 + i, b = 2 + (i + 1) % sides;
                tris.Add(0); tris.Add(b); tris.Add(a);
                tris.Add(1); tris.Add(a); tris.Add(b);
            }
            var m = new Mesh();
            m.vertices = verts.ToArray();
            m.triangles = tris.ToArray();
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        // The mark itself — E-key playerPos, on the floor.
        private static readonly Vector3 Mark = new Vector3(-9.0f, 0.7f, 4.3f);

        public const float Width = 4.2f;
        public const float Depth = 4.2f;
        public const float WallHeight = 2.2f;

        /// <summary>The bouncy floor's own transform, set once Spawn() has built it — used to find a spot inside for a bouncing tattletail.</summary>
        public static Transform FloorPad { get; private set; }

        /// <summary>A random point on the floor, inset from the walls, for a tattletail to bounce around near.</summary>
        public static Vector3 RandomPointInside()
        {
            if (FloorPad == null) return Vector3.zero;
            float half = Width * 0.5f - 0.9f;
            Vector3 c = FloorPad.position + Vector3.up * 0.3f;
            return c + new Vector3(Random.Range(-half, half), 0f, Random.Range(-half, half));
        }

        public static GameObject Spawn(Transform parent)
        {
            Vector3 floor = PartyMaterialSpawner.FloorUnder(Mark);
            var root = new GameObject("KE_BouncyCastle");
            root.transform.SetParent(parent, false);
            root.transform.position = floor;
            Transform t = root.transform;

            float half = Width * 0.5f;
            float towerR = 0.55f;

            // Bouncy floor — raised a little off the ground, and solid (walkable).
            var floorPad = Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.15f, 0f), new Vector3(Width, 0.3f, Depth), Floor);
            var floorCol = floorPad.AddComponent<BoxCollider>();
            floorCol.size = Vector3.one; // matches the scaled cube exactly
            FloorPad = floorPad.transform;

            // Back and side walls (open front, at +Z, so you walk straight in).
            BuildWall(t, new Vector3(0f, WallHeight * 0.5f + 0.3f, -half + 0.15f), new Vector3(Width - towerR * 2f, WallHeight, 0.3f));
            BuildWall(t, new Vector3(-half + 0.15f, WallHeight * 0.5f + 0.3f, 0f), new Vector3(0.3f, WallHeight, Depth - towerR * 2f));
            BuildWall(t, new Vector3(half - 0.15f, WallHeight * 0.5f + 0.3f, 0f), new Vector3(0.3f, WallHeight, Depth - towerR * 2f));

            // A short lip across the open front so it still reads as an enclosure.
            Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.6f, half - 0.15f), new Vector3(Width - towerR * 2f, 0.6f, 0.3f), Red);

            // Four corner towers with striped tops and flags.
            Vector3[] corners =
            {
                new Vector3(-half + towerR, 0f, -half + towerR), new Vector3(half - towerR, 0f, -half + towerR),
                new Vector3(-half + towerR, 0f, half - towerR),  new Vector3(half - towerR, 0f, half - towerR),
            };
            for (int i = 0; i < corners.Length; i++) BuildTower(t, corners[i], towerR, WallHeight + 1.6f, i % 2 == 0);

            // Solid walls block movement — a physical box collider matching the whole footprint minus the open front gap is
            // simplest as three separate wall colliders, already added by BuildWall below.

            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Party] Bouncy castle placed at " + floor + ".");
            return root;
        }

        private static void BuildWall(Transform parent, Vector3 pos, Vector3 size)
        {
            var wall = Prim(PrimitiveType.Cube, parent, pos, size, Blue);
            var col = wall.AddComponent<BoxCollider>();
            col.size = Vector3.one;
            // Horizontal stripes.
            for (int i = 0; i < 3; i++)
            {
                float f = (i + 0.5f) / 3f;
                var stripe = new GameObject("Stripe");
                stripe.transform.SetParent(wall.transform, false);
                stripe.transform.localPosition = new Vector3(0f, (f - 0.5f) * 0.9f, 0f);
                stripe.transform.localScale = new Vector3(1.01f, 0.12f, 1.02f);
                var mf = stripe.AddComponent<MeshFilter>();
                mf.sharedMesh = CubeMeshUnit();
                stripe.AddComponent<MeshRenderer>().sharedMaterial = Mat(i % 2 == 0 ? Yellow : Red);
            }
        }

        private static Mesh _unitCube;
        private static Mesh CubeMeshUnit()
        {
            if (_unitCube != null) return _unitCube;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _unitCube = go.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(go);
            return _unitCube;
        }

        private static void BuildTower(Transform parent, Vector3 basePos, float radius, float height, bool redFirst)
        {
            var tower = new GameObject("Tower");
            tower.transform.SetParent(parent, false);
            tower.transform.localPosition = basePos;

            Cyl(tower.transform, new Vector3(0f, height * 0.5f, 0f), radius, height, redFirst ? Red : Blue);
            for (int i = 0; i < 3; i++)
            {
                float y = height * (0.2f + i * 0.28f);
                Cyl(tower.transform, new Vector3(0f, y, 0f), radius + 0.005f, 0.22f, i % 2 == 0 ? Yellow : (redFirst ? Blue : Red));
            }
            Cone(tower.transform, new Vector3(0f, height, 0f), radius * 1.15f, radius * 1.4f, Yellow);

            // A small flag on top.
            var pole = Cyl(tower.transform, new Vector3(0f, height + radius * 1.4f + 0.3f, 0f), 0.02f, 0.6f, new Color(0.8f, 0.8f, 0.8f));
            var flag = Prim(PrimitiveType.Cube, tower.transform, new Vector3(0.12f, height + radius * 1.4f + 0.55f, 0f), new Vector3(0.24f, 0.16f, 0.01f), redFirst ? Blue : Red);

            // A solid tower trunk so it blocks movement like a real post.
            var col = tower.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, height * 0.5f, 0f);
            col.height = height;
            col.radius = radius;
        }
    }
}
