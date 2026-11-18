using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// A real, hold-to-interact charging station, built entirely fresh in
    /// code and placed at the basement spot the level's own story already
    /// points to ('TattletailPlanningProp' position). The vanilla object
    /// itself turned out to have too many independent hidden side effects
    /// (HUD text, actionText, a Trigger component, a UnityEvent — all
    /// neutralized in GamePatches.cs, but still worth avoiding entirely) to
    /// trust for a real interaction, so instead of reusing it this adds a
    /// brand-new Interactable component with nothing pre-wired: no Trigger
    /// sibling, no onInteractEvent listeners, nothing left over from the
    /// scene's original configuration.
    /// </summary>
    public class ChargingStation : MonoBehaviour
    {
        public static ChargingStation Instance { get; private set; }

        private GameObject _stationGO;
        private BoxCollider _stationCol;
        private Interactable _stationInteractable;
        private bool _building;
        private float _watchdogTimer;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Setup()
        {
            if (_stationGO != null || _building) return;
            StartCoroutine(SetupWhenReady());
        }

        // The station used to be built exactly once per run and never
        // checked again, and its only visible landmark was the vanilla
        // 'TattletailPlanningProp' mesh — so if that prop wasn't active
        // (or the one-shot build got missed/destroyed) the station was
        // simply missing. Now it's rebuilt if it ever disappears mid-run,
        // and keeps its collider/state enabled.
        private void Update()
        {
            if (EndlessRunManager.Instance == null || !EndlessRunManager.Instance.RunActive) return;

            if (FactoryMode.EndlessPreview) return; // (the Facility has its own care stations)
            _watchdogTimer -= Time.deltaTime;
            if (_watchdogTimer > 0f) return;
            _watchdogTimer = 1f;

            if (_stationGO == null)
            {
                if (!_building)
                {
                    KaleidoscopeEndlessPlugin.Log.LogWarning("KE: Charging station missing mid-run — rebuilding.");
                    Setup();
                }
                return;
            }

            if (_stationCol != null && !_stationCol.enabled) _stationCol.enabled = true;
            if (_stationInteractable != null && _stationInteractable.currentState != Interactable.State.Enabled)
                _stationInteractable.currentState = Interactable.State.Enabled;
        }

        /// <summary>
        /// SetupMonoBehaviours() runs right after level load, before the
        /// level's own Interactables (BasementDoor included) are active —
        /// a one-shot FindObjectsOfType at that point finds nothing at all,
        /// same problem TattletailPlanningProp's lookup hit earlier. Retry
        /// for a few seconds instead of giving up on the first frame.
        /// </summary>
        private System.Collections.IEnumerator SetupWhenReady()
        {
            _building = true;
            Interactable layerSource = null;
            float waited = 0f;
            const float maxWait = 20f;

            while (layerSource == null && waited < maxWait)
            {
                Interactable[] existing = FindObjectsOfType<Interactable>();
                foreach (var it in existing)
                {
                    if (it != null && it.gameObject.name == "BasementDoor") { layerSource = it; break; }
                }
                if (layerSource == null && existing.Length > 0) layerSource = existing[0]; // fallback: any Interactable

                if (layerSource == null)
                {
                    yield return new WaitForSeconds(0.25f);
                    waited += 0.25f;
                }
            }

            try { BuildStation(layerSource); }
            finally { _building = false; }
        }

        private void BuildStation(Interactable layerSource)
        {
            Vector3 pos = new Vector3(0f, -2.5f, -0.7f); // confirmed via earlier hover log

            _stationGO = new GameObject("KE_ChargingStation");
            _stationGO.transform.position = pos;

            // Its own visible dock, so the station never depends on the
            // vanilla prop's mesh being active (the vanilla mesh, when it
            // is there, just sits under/around this).
            BuildVisual(_stationGO.transform);

            var col = _stationGO.AddComponent<BoxCollider>();
            _stationCol = col;
            col.size = Vector3.one * 1.3f; // 2.0f reached too far — tightened
            col.isTrigger = true; // was a solid collider, physically blocking the player from walking through it — same fix applied to vases/candles/friend

            // PlayerActionController's interaction raycast uses a LayerMask
            // that we can't see from decompiled code — if our object stays
            // on the Default layer and that mask excludes it, it'll be
            // visible but never actually raycast-hittable. Copy the layer
            // from an existing, confirmed-clickable Interactable instead of
            // guessing.
            if (layerSource != null)
            {
                _stationGO.layer = layerSource.gameObject.layer;
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Charging station using layer " + _stationGO.layer + " (copied from '" + layerSource.gameObject.name + "')");
            }
            else
            {
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: Timed out waiting for any Interactable to appear — charging station may not be raycast-hittable.");
            }

            var interactable = _stationGO.AddComponent<Interactable>();
            interactable.actionText = "Charge Tattletail";
            interactable.holdTime = 5f; // was 1.2f, then 10f, now 5f per explicit request
            interactable.highlight = true;
            interactable.setInactiveOnInteract = false;
            interactable.honks = false;
            // newStateOnInteract has no inline default and so is 0 (Hidden)
            // by default — OnInteractComplete() unconditionally applies it
            // after every interaction ("if (newStateOnInteract !=
            // currentState) currentState = newStateOnInteract;") and then
            // disables the collider outright once currentState == Hidden.
            // That's why it only worked once: after the first use it went
            // Hidden and its collider got turned off permanently — our
            // ConditionUpdate patch forcing currentState back to Enabled
            // every frame doesn't touch the (separately) disabled collider.
            interactable.newStateOnInteract = Interactable.State.Enabled;
            interactable.currentState = Interactable.State.Enabled;
            // These have no inline default in the vanilla class and are
            // only ever populated by Unity's Inspector serialization — a
            // component AddComponent()'d at runtime skips that, so leaving
            // them null risks a NullReferenceException the first time
            // vanilla code touches them.
            interactable.holdAudioName = string.Empty;
            interactable.onInteractEvent = new UnityEngine.Events.UnityEvent();
            interactable.conditions = new InteractableConditions();
            interactable.actions = new InteractableActions
            {
                interactActions = new InteractableActions.Actions[] { InteractableActions.Actions.ChargeTattletail },
            };

            _stationInteractable = interactable;
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Custom charging station Interactable created at " + pos);
        }

        // A small charging dock built from primitives: dark base, glowing
        // green pad, and a battery block — flat Unlit colors like the rest
        // of this mod's generated props (no lighting dependency).
        private static void BuildVisual(Transform parent)
        {
            AddPart(parent, PrimitiveType.Cylinder, new Vector3(0f, 0.03f, 0f),  new Vector3(0.7f, 0.03f, 0.7f),   new Color(0.16f, 0.16f, 0.18f));
            AddPart(parent, PrimitiveType.Cylinder, new Vector3(0f, 0.065f, 0f), new Vector3(0.55f, 0.006f, 0.55f), new Color(0.15f, 0.65f, 0.3f));
            AddPart(parent, PrimitiveType.Cube,     new Vector3(0f, 0.22f, 0f),  new Vector3(0.14f, 0.3f, 0.09f),   new Color(0.3f, 0.3f, 0.34f));
            AddPart(parent, PrimitiveType.Cube,     new Vector3(0f, 0.38f, 0f),  new Vector3(0.06f, 0.03f, 0.05f),  new Color(0.15f, 0.65f, 0.3f));
        }

        private static void AddPart(Transform parent, PrimitiveType type, Vector3 localPos, Vector3 localScale, Color color)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = "KE_ChargingStationPart";
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPos;
            part.transform.localScale = localScale;
            Collider c = part.GetComponent<Collider>();
            if (c != null) Destroy(c);
            Renderer r = part.GetComponent<Renderer>();
            if (r != null)
            {
                Shader unlit = Shader.Find("Unlit/Color");
                if (unlit != null)
                {
                    var mat = new Material(unlit);
                    mat.color = color;
                    r.material = mat;
                }
            }
        }
    }
}
