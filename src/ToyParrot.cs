using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// The Toy Parrot gadget's placed toy: squawks a pitched-up imitation of
    /// Tattletail crying, loudly and nonstop, for Gadgets.PARROT_DURATION
    /// seconds. Mama is lured to the patrol spot nearest it (again every 2 s)
    /// and ignores the player the whole time: no catches, no flashlight
    /// kills, and only the parrot's noise reaches her (MamaTeleportGrace).
    /// Then it pops and disappears.
    /// </summary>
    public class ToyParrot : MonoBehaviour
    {
        public static ToyParrot Active { get; private set; }

        private AudioSource _voice;
        private Transform _head;
        private float _life;
        private float _noiseTimer;

        /// <summary>The parrot model (also used, unanimated, as the placement preview).</summary>
        public static GameObject BuildModel(string name)
        {
            var root = new GameObject(name);
            Material red = Mat.Lit(new Color(0.9f, 0.12f, 0.12f), 0.6f);
            Material green = Mat.Lit(new Color(0.15f, 0.75f, 0.25f), 0.6f);
            Material blue = Mat.Lit(new Color(0.15f, 0.35f, 0.95f), 0.6f);
            Material yellow = Mat.Lit(new Color(1f, 0.8f, 0.1f), 0.6f);
            Material black = Mat.Lit(new Color(0.02f, 0.02f, 0.02f), 0.9f);
            Material wood = Mat.Lit(new Color(0.45f, 0.28f, 0.12f), 0.3f);

            Piece(root.transform, PrimitiveType.Cylinder, "Base", new Vector3(0f, 0.015f, 0f), new Vector3(0.16f, 0.015f, 0.16f), wood);
            Piece(root.transform, PrimitiveType.Cylinder, "Perch", new Vector3(0f, 0.06f, 0f), new Vector3(0.02f, 0.04f, 0.02f), wood);
            Piece(root.transform, PrimitiveType.Sphere, "Body", new Vector3(0f, 0.17f, 0f), new Vector3(0.12f, 0.16f, 0.11f), red);
            Piece(root.transform, PrimitiveType.Cube, "WingL", new Vector3(-0.058f, 0.16f, -0.01f), new Vector3(0.02f, 0.11f, 0.08f), blue);
            Piece(root.transform, PrimitiveType.Cube, "WingR", new Vector3(0.058f, 0.16f, -0.01f), new Vector3(0.02f, 0.11f, 0.08f), blue);
            var tail = Piece(root.transform, PrimitiveType.Cube, "Tail", new Vector3(0f, 0.09f, -0.07f), new Vector3(0.05f, 0.12f, 0.02f), green);
            tail.transform.localRotation = Quaternion.Euler(-30f, 0f, 0f);

            var head = new GameObject("Head").transform;
            head.SetParent(root.transform, false);
            head.localPosition = new Vector3(0f, 0.27f, 0.01f);
            Piece(head, PrimitiveType.Sphere, "Skull", Vector3.zero, Vector3.one * 0.09f, red);
            Piece(head, PrimitiveType.Sphere, "Crest", new Vector3(0f, 0.045f, -0.01f), new Vector3(0.03f, 0.05f, 0.04f), yellow);
            var beak = Piece(head, PrimitiveType.Cube, "Beak", new Vector3(0f, -0.005f, 0.05f), new Vector3(0.03f, 0.035f, 0.04f), yellow);
            beak.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);
            Piece(head, PrimitiveType.Sphere, "EyeL", new Vector3(-0.03f, 0.012f, 0.03f), Vector3.one * 0.016f, black);
            Piece(head, PrimitiveType.Sphere, "EyeR", new Vector3(0.03f, 0.012f, 0.03f), Vector3.one * 0.016f, black);
            return root;
        }

        private static GameObject Piece(Transform parent, PrimitiveType type, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().material = mat;
            Object.Destroy(go.GetComponent<Collider>());
            return go;
        }

        public static void Place(Vector3 floorPoint, float yaw)
        {
            if (Active != null) Active.Pop();
            var go = BuildModel("KE_ToyParrot");
            go.transform.position = floorPoint;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.AddComponent<ToyParrot>();
        }

        private void Awake()
        {
            Active = this;
            _head = transform.Find("Head");
            _life = Gadgets.PARROT_DURATION;

            _voice = gameObject.AddComponent<AudioSource>();
            _voice.clip = GadgetAudio.ParrotCry;
            _voice.loop = true;
            _voice.pitch = 1.3f;          // a parrot's squawky imitation of the real cry
            _voice.volume = 1f;
            _voice.spatialBlend = 1f;     // you hear where it is
            _voice.rolloffMode = AudioRolloffMode.Linear;
            _voice.minDistance = 4f;
            _voice.maxDistance = 45f;     // HUGE: most of the house hears it
            _voice.Play();

            _lureTimer = 0f; // lure right away (Update)
        }

        private float _lureTimer;
        private const float LURE_INTERVAL = 2f; // keeps pulling her back if her patrol moves her, or if she comes out after it's placed

        private void Update()
        {
            _life -= Time.deltaTime;

            // Head bobs and the body shakes while it wails.
            float t = Time.time;
            if (_head != null)
            {
                _head.localRotation = Quaternion.Euler(Mathf.Sin(t * 14f) * 18f, Mathf.Sin(t * 5f) * 25f, 0f);
                _head.localPosition = new Vector3(0f, 0.27f + Mathf.Abs(Mathf.Sin(t * 14f)) * 0.012f, 0.01f);
            }

            _lureTimer -= Time.deltaTime;
            if (_lureTimer <= 0f)
            {
                _lureTimer = LURE_INTERVAL;
                MamaController.Instance?.LureTo(transform.position);
            }

            // Keep Mama hearing it (and alerting if she's close), every second.
            _noiseTimer -= Time.deltaTime;
            if (_noiseTimer <= 0f)
            {
                _noiseTimer = 1f;
                RM.mamaManager?.OnNoise(transform.position, true);
            }

            // Last half second: shrink away.
            if (_life < 0.5f) transform.localScale = Vector3.one * Mathf.Max(0.01f, _life / 0.5f);
            if (_life <= 0f) Pop();
        }

        public void Pop()
        {
            if (Active == this) Active = null;
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (Active == this) Active = null;
        }
    }

    /// <summary>Cycles a renderer's or UI graphic's color through the rainbow — the DLC gumballs.</summary>
    public class RainbowCycler : MonoBehaviour
    {
        public float speed = 0.35f;
        public float offset;
        private Renderer _renderer;
        private UnityEngine.UI.Graphic _graphic;

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _graphic = GetComponent<UnityEngine.UI.Graphic>();
            offset = Random.value;
        }

        private void Update()
        {
            Color c = Gadgets.Rainbow(Time.unscaledTime * speed + offset);
            if (_renderer != null) _renderer.material.color = c;
            if (_graphic != null) _graphic.color = new Color(c.r, c.g, c.b, _graphic.color.a);
        }
    }
}
