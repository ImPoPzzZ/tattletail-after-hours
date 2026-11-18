using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// The guard: a ball of blue light round a kid while they hold a block. It pops open when it's raised, gleams
    /// brighter for the moment when a hit would be a perfect block, flashes when something lands on it, and bursts
    /// white on a perfect block. Drawn for you and for the friends you see.
    /// </summary>
    public class RcBubble
    {
        private const float SIZE = 1.95f;                     // across: a bit taller than a kid
        private GameObject _root;
        private Transform _outer, _inner;
        private Material _shell, _fill;
        private float _on, _flash, _perfect;
        private bool _wasOn;
        private static Texture2D _lattice;
        private static readonly Color Blue = new Color(0.22f, 0.55f, 1f), Bright = new Color(0.78f, 0.96f, 1f);

        public RcBubble()
        {
            Shader sh = Shader.Find("Particles/Alpha Blended");
            if (sh == null) return; // (no such shader in this build: no bubble, the block still works)
            if (_lattice == null) _lattice = MakeLattice();
            _root = new GameObject("KE_RcBubble");
            _shell = new Material(sh) { mainTexture = _lattice };
            _fill = new Material(sh);
            _outer = Sphere("Shell", _shell);
            _inner = Sphere("Fill", _fill);
            _root.SetActive(false);
        }

        private Transform Sphere(string name, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(_root.transform, false);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go.transform;
        }

        // A soft diamond lattice, like the cells of a bubble. White: the tint colors it.
        private static Texture2D MakeLattice()
        {
            const int N = 128;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, true);
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = x / (float)N, v = y / (float)N;
                    float l1 = Line(u * 8f + v * 4f), l2 = Line(u * 8f - v * 4f + 16f);
                    float line = Mathf.Max(l1, l2), cross = l1 * l2;
                    float a = Mathf.Clamp01(0.16f + 0.7f * line + 0.5f * cross);
                    px[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            t.SetPixels32(px);
            t.wrapMode = TextureWrapMode.Repeat;
            t.filterMode = FilterMode.Bilinear;
            t.Apply(true);
            return t;
        }

        private static float Line(float f)
        {
            f -= Mathf.Floor(f);
            float d = Mathf.Min(f, 1f - f) / 0.09f; // 0 on the line, 1 a little way off it
            d = Mathf.Clamp01(d);
            return 1f - d * d * (3f - 2f * d);
        }

        /// <summary>Something landed on the guard (perfect: right on time).</summary>
        public void Hit(bool perfect)
        {
            _flash = 1f;
            if (perfect) _perfect = 1f;
        }

        /// <summary>Every frame. 'window': a hit right now would be a perfect block.</summary>
        public void Tick(bool blocking, Vector3 feet, bool window)
        {
            if (_root == null) return;
            float dt = Time.deltaTime;
            _wasOn = blocking;
            _on = Mathf.MoveTowards(_on, blocking ? 1f : 0f, dt * (blocking ? 9f : 6f));
            _flash = Mathf.MoveTowards(_flash, 0f, dt * 5f);
            _perfect = Mathf.MoveTowards(_perfect, 0f, dt * 2.2f);
            if (_on <= 0.001f && _perfect <= 0.001f) { if (_root.activeSelf) _root.SetActive(false); return; }
            if (!_root.activeSelf) _root.SetActive(true);

            float t = Time.time;
            float ease = _on * _on * (3f - 2f * _on);
            float open = 0.5f + 0.5f * ease + (blocking && _on < 1f ? Mathf.Sin(_on * Mathf.PI) * 0.14f : 0f); // pops open with a little overshoot
            float size = SIZE * open * (1f + 0.025f * Mathf.Sin(t * 7f)) * (1f + 0.08f * _flash + 0.28f * _perfect);
            _root.transform.position = feet + Vector3.up * 0.58f;
            _outer.localScale = Vector3.one * size;
            _inner.localScale = Vector3.one * size * 0.93f;
            _outer.localRotation = Quaternion.Euler(0f, t * 35f, 0f);
            _shell.mainTextureOffset = new Vector2(t * 0.04f, t * 0.09f);

            float glow = Mathf.Clamp01((window ? 0.4f : 0f) + _flash * 0.55f + _perfect * 0.9f);
            Color c = Color.Lerp(Blue, Bright, glow);
            float alive = Mathf.Max(_on, _perfect);
            float flicker = 0.93f + 0.07f * Mathf.Sin(t * 13f);
            _shell.SetColor("_TintColor", new Color(c.r, c.g, c.b, Mathf.Clamp01((0.3f + glow * 0.45f) * alive * flicker)));
            _fill.SetColor("_TintColor", new Color(c.r, c.g, c.b, (0.09f + glow * 0.22f) * alive));
        }

        public void Destroy()
        {
            if (_root != null) Object.Destroy(_root);
            _root = null;
        }
    }

    /// <summary>The dodge roll: a forward somersault along the way you're going, tucked, with a brief squat at each end.</summary>
    public static class RcRoll
    {
        public const float TIME = 0.5f;

        /// <summary>
        /// Puts a kid partway through a roll (k: 0 at the start, 1 at the end): whole body turned about its middle,
        /// so the head goes down and round and the feet come up. Returns how tucked the limbs should be (0..1).
        /// </summary>
        public static float Apply(Transform kid, Vector3 feet, Vector3 dir, float k)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) dir = Vector3.forward;
            dir.Normalize();
            k = Mathf.Clamp01(k);
            float ease = k < 0.5f ? 2f * k * k : 1f - 2f * (1f - k) * (1f - k); // slow, fast, slow
            Quaternion spin = Quaternion.AngleAxis(360f * ease, Vector3.Cross(Vector3.up, dir));
            const float MIDDLE = 0.42f;
            float sink = Mathf.Sin(k * Mathf.PI) * 0.07f; // ducks under it
            Vector3 centre = feet + Vector3.up * (MIDDLE - sink);
            kid.position = centre + spin * (Vector3.down * MIDDLE);
            kid.rotation = spin * Quaternion.LookRotation(dir);
            return Mathf.Clamp01(Mathf.Min(k, 1f - k) * 7f);
        }
    }

    /// <summary>A street light that isn't well: it gutters, stutters, and now and then goes out for a few seconds.</summary>
    public class RcFlicker : MonoBehaviour
    {
        private Light _l;
        private float _base, _seed, _next, _until, _deadUntil;

        private void Start()
        {
            _l = GetComponent<Light>();
            if (_l != null) _base = _l.intensity;
            _seed = Random.value * 100f;
            _next = Time.time + Random.Range(1f, 8f);
        }

        private void Update()
        {
            if (_l == null) return;
            float t = Time.time;
            if (t < _deadUntil) { _l.intensity = 0f; return; }
            if (t < _until) { _l.intensity = Mathf.PerlinNoise(_seed, t * 45f) > 0.45f ? _base * 0.1f : _base; return; }
            _l.intensity = _base * (0.9f + 0.1f * Mathf.PerlinNoise(_seed, t * 2.5f));
            if (t < _next) return;
            _until = t + Random.Range(0.3f, 1.1f);                          // a stutter...
            if (Random.value < 0.35f) _deadUntil = _until + Random.Range(1.5f, 4.5f); // ...and sometimes it stays dark a while
            _next = _until + Random.Range(5f, 16f);
        }
    }
}
