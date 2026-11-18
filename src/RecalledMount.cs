using System.Collections.Generic;
using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// What a mount looks like: a toy you ride (a wagon, a ride-on RC car, a rocking horse), built from plain shapes.
    /// The same view draws yours and your friends'. Wheels turn as it goes and the horse rocks.
    /// </summary>
    public class RcMountView
    {
        public const int WAGON = 0, CAR = 1, HORSE = 2, TYPES = 3;
        /// <summary>How high the rider sits above the ground, per mount.</summary>
        public static readonly float[] Seat = { 0.42f, 0.5f, 0.78f };

        public readonly int Type;
        public GameObject Root { get; private set; }
        private readonly List<Transform> _wheels = new List<Transform>();
        private Transform _rock;
        private Vector3 _last;
        private bool _has;
        private float _spin, _rockT, _speed;

        public RcMountView(int type)
        {
            Type = Mathf.Clamp(type, 0, TYPES - 1);
            Root = new GameObject("KE_RcMount" + Type);
            if (Type == WAGON) BuildWagon(); else if (Type == CAR) BuildCar(); else BuildHorse();
        }

        // ── Parts ────────────────────────────────────────────────────────────
        private GameObject Part(Transform parent, PrimitiveType shape, string name, Vector3 pos, Vector3 size, Material m)
        {
            var go = GameObject.CreatePrimitive(shape);
            Object.Destroy(go.GetComponent<Collider>());   // (a ride-on has no collider of its own: you're the one that bumps into things)
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = m;
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        private GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Material m) { return Part(parent, PrimitiveType.Cube, name, pos, size, m); }

        /// <summary>A wheel on its axle (x): the pivot turns, the tire is a flat cylinder lying on its side.</summary>
        private void Wheel(Vector3 pos, float radius, float width, Material tire, Material hub)
        {
            var pivot = new GameObject("Wheel").transform;
            pivot.SetParent(Root.transform, false);
            pivot.localPosition = pos;
            var t = Part(pivot, PrimitiveType.Cylinder, "Tire", Vector3.zero, new Vector3(radius * 2f, width * 0.5f, radius * 2f), tire);
            t.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            var h = Part(pivot, PrimitiveType.Cylinder, "Hub", Vector3.zero, new Vector3(radius * 0.9f, width * 0.55f, radius * 0.9f), hub);
            h.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Box(pivot, "Spoke", new Vector3(0f, radius * 0.5f, 0f), new Vector3(width * 1.15f, radius * 0.25f, radius * 0.18f), hub);   // (so you can see it turn)
            _wheels.Add(pivot);
        }

        private void BuildWagon()
        {
            Material red = Mat.Lit(new Color(0.78f, 0.12f, 0.1f), 0.15f), dark = Mat.Lit(new Color(0.08f, 0.08f, 0.09f), 0.05f), metal = Mat.Lit(new Color(0.7f, 0.7f, 0.72f), 0.4f), wood = Mat.Lit(new Color(0.5f, 0.34f, 0.18f), 0.1f);
            Transform r = Root.transform;
            Box(r, "Bed", new Vector3(0f, 0.3f, 0f), new Vector3(0.84f, 0.08f, 1.3f), red);
            Box(r, "SideL", new Vector3(-0.42f, 0.4f, 0f), new Vector3(0.07f, 0.24f, 1.3f), red);
            Box(r, "SideR", new Vector3(0.42f, 0.4f, 0f), new Vector3(0.07f, 0.24f, 1.3f), red);
            Box(r, "Back", new Vector3(0f, 0.4f, -0.65f), new Vector3(0.84f, 0.24f, 0.07f), red);
            Box(r, "Front", new Vector3(0f, 0.4f, 0.65f), new Vector3(0.84f, 0.24f, 0.07f), red);
            Box(r, "Slat", new Vector3(0f, 0.36f, 0f), new Vector3(0.7f, 0.04f, 1.1f), wood);
            Box(r, "AxleF", new Vector3(0f, 0.2f, 0.45f), new Vector3(0.9f, 0.05f, 0.05f), metal);
            Box(r, "AxleB", new Vector3(0f, 0.2f, -0.45f), new Vector3(0.9f, 0.05f, 0.05f), metal);
            var handle = Box(r, "Handle", new Vector3(0f, 0.55f, 1.0f), new Vector3(0.06f, 0.06f, 0.75f), metal);
            handle.transform.localRotation = Quaternion.Euler(-30f, 0f, 0f);
            Box(r, "Grip", new Vector3(0f, 0.74f, 1.3f), new Vector3(0.34f, 0.06f, 0.06f), dark);
            foreach (float z in new[] { -0.45f, 0.45f })
                foreach (float x in new[] { -0.48f, 0.48f }) Wheel(new Vector3(x, 0.17f, z), 0.17f, 0.1f, dark, metal);
        }

        private void BuildCar()
        {
            Material blue = Mat.Lit(new Color(0.1f, 0.3f, 0.85f), 0.45f), yellow = Mat.Lit(new Color(0.95f, 0.8f, 0.1f), 0.3f), dark = Mat.Lit(new Color(0.06f, 0.06f, 0.07f), 0.05f), glass = Mat.Lit(new Color(0.12f, 0.2f, 0.3f), 0.8f), metal = Mat.Lit(new Color(0.75f, 0.75f, 0.78f), 0.5f);
            Material lamp = Mat.Unlit(new Color(1f, 0.95f, 0.6f)), tail = Mat.Unlit(new Color(1f, 0.15f, 0.1f));
            Transform r = Root.transform;
            Box(r, "Chassis", new Vector3(0f, 0.3f, 0f), new Vector3(0.9f, 0.2f, 1.7f), blue);
            Box(r, "Hood", new Vector3(0f, 0.47f, 0.55f), new Vector3(0.78f, 0.16f, 0.6f), blue);
            Box(r, "Stripe", new Vector3(0f, 0.56f, 0.55f), new Vector3(0.2f, 0.02f, 0.62f), yellow);
            Box(r, "Back", new Vector3(0f, 0.5f, -0.6f), new Vector3(0.86f, 0.3f, 0.5f), blue);
            Box(r, "Screen", new Vector3(0f, 0.55f, 0.16f), new Vector3(0.7f, 0.22f, 0.05f), glass);
            Box(r, "Bumper", new Vector3(0f, 0.28f, 0.88f), new Vector3(0.94f, 0.08f, 0.08f), metal);
            Box(r, "LampL", new Vector3(-0.28f, 0.42f, 0.86f), new Vector3(0.16f, 0.1f, 0.05f), lamp);
            Box(r, "LampR", new Vector3(0.28f, 0.42f, 0.86f), new Vector3(0.16f, 0.1f, 0.05f), lamp);
            Box(r, "TailL", new Vector3(-0.3f, 0.5f, -0.86f), new Vector3(0.14f, 0.08f, 0.05f), tail);
            Box(r, "TailR", new Vector3(0.3f, 0.5f, -0.86f), new Vector3(0.14f, 0.08f, 0.05f), tail);
            var aerial = Box(r, "Aerial", new Vector3(0.34f, 0.95f, -0.6f), new Vector3(0.02f, 0.8f, 0.02f), metal);
            aerial.transform.localRotation = Quaternion.Euler(0f, 0f, -8f);
            Part(r, PrimitiveType.Sphere, "AerialTip", new Vector3(0.45f, 1.34f, -0.6f), new Vector3(0.07f, 0.07f, 0.07f), tail);
            foreach (float z in new[] { -0.6f, 0.62f })
                foreach (float x in new[] { -0.5f, 0.5f }) Wheel(new Vector3(x, 0.26f, z), 0.26f, 0.2f, dark, yellow);
        }

        private void BuildHorse()
        {
            Material wood = Mat.Lit(new Color(0.55f, 0.36f, 0.2f), 0.2f), cream = Mat.Lit(new Color(0.9f, 0.82f, 0.62f), 0.2f), dark = Mat.Lit(new Color(0.18f, 0.1f, 0.06f), 0.1f), red = Mat.Lit(new Color(0.75f, 0.1f, 0.1f), 0.2f), eye = Mat.Unlit(new Color(1f, 1f, 1f));
            Transform r = Root.transform;
            // The runners stay on the ground; everything above them rocks.
            Box(r, "RunnerL", new Vector3(-0.3f, 0.05f, 0f), new Vector3(0.07f, 0.1f, 1.7f), wood);
            Box(r, "RunnerR", new Vector3(0.3f, 0.05f, 0f), new Vector3(0.07f, 0.1f, 1.7f), wood);
            var up = Box(r, "RunnerTipFL", new Vector3(-0.3f, 0.13f, 0.85f), new Vector3(0.07f, 0.1f, 0.3f), wood); up.transform.localRotation = Quaternion.Euler(-25f, 0f, 0f);
            up = Box(r, "RunnerTipFR", new Vector3(0.3f, 0.13f, 0.85f), new Vector3(0.07f, 0.1f, 0.3f), wood); up.transform.localRotation = Quaternion.Euler(-25f, 0f, 0f);
            up = Box(r, "RunnerTipBL", new Vector3(-0.3f, 0.13f, -0.85f), new Vector3(0.07f, 0.1f, 0.3f), wood); up.transform.localRotation = Quaternion.Euler(25f, 0f, 0f);
            up = Box(r, "RunnerTipBR", new Vector3(0.3f, 0.13f, -0.85f), new Vector3(0.07f, 0.1f, 0.3f), wood); up.transform.localRotation = Quaternion.Euler(25f, 0f, 0f);
            _rock = new GameObject("Rock").transform;
            _rock.SetParent(r, false);
            _rock.localPosition = new Vector3(0f, 0.1f, 0f);
            foreach (float z in new[] { -0.4f, 0.4f })
                foreach (float x in new[] { -0.3f, 0.3f }) Box(_rock, "Leg", new Vector3(x, 0.3f, z), new Vector3(0.1f, 0.5f, 0.12f), wood);
            Box(_rock, "Body", new Vector3(0f, 0.68f, 0f), new Vector3(0.46f, 0.4f, 1.05f), cream);
            Box(_rock, "Saddle", new Vector3(0f, 0.9f, -0.05f), new Vector3(0.5f, 0.07f, 0.5f), red);
            var neck = Box(_rock, "Neck", new Vector3(0f, 1.0f, 0.55f), new Vector3(0.2f, 0.55f, 0.24f), cream); neck.transform.localRotation = Quaternion.Euler(-22f, 0f, 0f);
            Box(_rock, "Head", new Vector3(0f, 1.3f, 0.8f), new Vector3(0.22f, 0.24f, 0.46f), cream);
            Box(_rock, "Nose", new Vector3(0f, 1.24f, 1.04f), new Vector3(0.18f, 0.16f, 0.1f), dark);
            Box(_rock, "EyeL", new Vector3(-0.115f, 1.36f, 0.86f), new Vector3(0.03f, 0.05f, 0.05f), eye);
            Box(_rock, "EyeR", new Vector3(0.115f, 1.36f, 0.86f), new Vector3(0.03f, 0.05f, 0.05f), eye);
            Box(_rock, "EarL", new Vector3(-0.07f, 1.5f, 0.68f), new Vector3(0.05f, 0.14f, 0.07f), cream);
            Box(_rock, "EarR", new Vector3(0.07f, 1.5f, 0.68f), new Vector3(0.05f, 0.14f, 0.07f), cream);
            var mane = Box(_rock, "Mane", new Vector3(0f, 1.12f, 0.46f), new Vector3(0.06f, 0.6f, 0.2f), dark); mane.transform.localRotation = Quaternion.Euler(-22f, 0f, 0f);
            var tailBox = Box(_rock, "Tail", new Vector3(0f, 0.72f, -0.62f), new Vector3(0.08f, 0.4f, 0.1f), dark); tailBox.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);
            Box(_rock, "Handle", new Vector3(0f, 1.22f, 0.55f), new Vector3(0.5f, 0.04f, 0.04f), wood);
        }

        // ── Each frame ───────────────────────────────────────────────────────
        /// <summary>Puts the mount under its rider (feet: where they'd stand) and turns the wheels by how far it went.</summary>
        public void Place(Vector3 feet, float yaw)
        {
            if (Root == null) return;
            Root.transform.position = feet;
            Root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            float dt = Mathf.Max(0.0001f, Time.deltaTime);
            float moved = 0f;
            if (_has)
            {
                Vector3 d = feet - _last; d.y = 0f;
                moved = d.magnitude;
                if (moved > 3f) moved = 0f;   // (a teleport)
                if (Vector3.Dot(d, Root.transform.forward) < 0f) moved = -moved;
            }
            _last = feet; _has = true;
            _speed = Mathf.Lerp(_speed, Mathf.Abs(moved) / dt, Mathf.Clamp01(dt * 8f));
            if (_wheels.Count > 0)
            {
                _spin += moved / 0.22f * Mathf.Rad2Deg;
                for (int i = 0; i < _wheels.Count; i++) _wheels[i].localRotation = Quaternion.Euler(_spin, 0f, 0f);
            }
            if (_rock != null)
            {
                _rockT += dt * (3f + Mathf.Min(8f, _speed));
                _rock.localRotation = Quaternion.Euler(Mathf.Sin(_rockT) * (3f + Mathf.Min(10f, _speed * 1.4f)), 0f, 0f);
            }
        }

        public void Destroy()
        {
            if (Root != null) Object.Destroy(Root);
            Root = null;
        }
    }
}
