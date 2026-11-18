using System.Collections.Generic;
using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// Care stations all over the factory, so Tattletail can be looked after wherever you are:
    /// TATTLE-TREATS boxes (feed), grooming kits (brush) and charging docks (charge). Each one calls
    /// Tattletail's own TattletailNeeds.Feed / Groom / Charge (set dressing while there's no story to use them).
    /// </summary>
    public static partial class FactoryLayout
    {
        public enum StationKind { Feed, Groom, Charge }

        public class Station
        {
            public GameObject Go;
            public StationKind Kind;
        }

        public static readonly List<Station> Stations = new List<Station>();

        /// <summary>Spare camcorder batteries lying around the building (FactoryCamcorder makes them pick-up-able).</summary>
        public static readonly List<GameObject> CamBatteries = new List<GameObject>();

        private static void CamBatteryPickups()
        {
            CamBatteries.Clear();
            CamBattery(11.1f, 1.1f, 5.75f, 20f);          // lobby, the security desk
            CamBattery(5.9f, 0.77f, 33.3f, -30f);         // break room table
            CamBattery(24f, 0.96f, 3.3f, 60f);            // testing lab bench
            CamBattery(35.2f, 1.1f, 39.3f, 10f);          // assembly, the tool chest
            CamBattery(54.5f, 1.35f, 19.35f, 80f);        // warehouse, the forklift seat
            CamBattery(4f, UP + 0.77f, 36.7f, -15f);      // upstairs, the foreman's desk
            CamBattery(8.2f, UP + 1.4f, 39.5f, 40f);      // records, on a cabinet
            CamBattery(5f, UP + 0.77f, 12.8f, 0f);        // the manager's desk
            CamBattery(12.6f, UP + 0.9f, 16.3f, 25f);     // PA console
            CamBattery(4.5f, DOWN + 1.9f, 12.6f, 70f);    // basement, on the generator
            CamBattery(27.8f, DOWN + 0.96f, 4.4f, -40f);  // R&D workbench
        }

        // A camcorder battery pack: dark grey, a silver label, the contacts.
        private static void CamBattery(float x, float y, float z, float yaw)
        {
            var go = new GameObject("Camcorder battery");
            go.transform.SetParent(_root, false);
            go.transform.localPosition = new Vector3(x, y, z);
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Transform keep = _root;
            _root = go.transform;
            Box("Battery", -0.03f, 0f, -0.02f, 0.03f, 0.022f, 0.02f, FTex.Plain(new Color(0.16f, 0.16f, 0.17f), 0.4f), false);
            Box("Battery label", -0.022f, 0.022f, -0.012f, 0.022f, 0.0225f, 0.012f, _metalGrey, false);
            Box("Battery contacts", 0.03f, 0.006f, -0.008f, 0.031f, 0.014f, 0.008f, FTex.Plain(new Color(0.8f, 0.65f, 0.25f), 0.8f), false);
            _root = keep;
            var hit = go.AddComponent<BoxCollider>(); // generous, easy to aim at
            hit.center = new Vector3(0f, 0.04f, 0f);
            hit.size = new Vector3(0.22f, 0.12f, 0.2f);
            AddGlow(go.transform, new Vector3(0f, 0.3f, 0f), GlowBattery);
            CamBatteries.Add(go);
        }

        // ── Endless in the Facility: the care stations are where the owner marked them (docs/facility_marks.txt) ──
        // 'fridge' feeds, 'brush' and 'groom tattletail' groom, 'charger tattletail' charges.
        private static void MarkedCareStations()
        {
            Fridge(10.09f, 0.01f, 38.92f, 223f); Fridge(33.11f, 0.01f, 29.53f, 275f); Fridge(47.72f, 0.01f, 22.91f, 280f); Fridge(0.58f, UP + 0.01f, 39.13f, 156f);
            Stand(33.54f, 0.01f, 23.31f); Brush(33.54f, 0.91f, 23.31f, 260f);
            Stand(6.73f, 0.01f, 13.06f); Brush(6.73f, 0.91f, 13.06f, 70f);
            Brush(3.48f, 4.78f, 36.54f, 43f);                                   // upstairs, on the desk
            Stand(30.89f, DOWN + 0.01f, 8.76f); Dock(30.89f, DOWN + 0.91f, 8.76f, 178f);
            Stand(8.21f, UP + 0.01f, 24.88f); Dock(8.21f, UP + 0.91f, 24.88f, 142f);
        }

        // A fridge: the Feed station (white, a handle, a freezer line).
        private static void Fridge(float x, float y, float z, float yaw)
        {
            var go = StationRoot("Fridge", x, y, z, yaw, StationKind.Feed, new Vector3(0.8f, 1.8f, 0.8f));
            Transform keep = _root;
            _root = go.transform;
            Box("Fridge body", -0.36f, 0f, -0.34f, 0.36f, 1.75f, 0.34f, FTex.Plain(new Color(0.88f, 0.89f, 0.9f), 0.5f), true); // (solid: you cannot walk through it; the use box around it is a trigger)
            Box("Fridge seam", -0.361f, 1.12f, -0.341f, 0.361f, 1.14f, 0.341f, _metalGrey, false);
            Box("Fridge handle", 0.24f, 1.2f, 0.34f, 0.27f, 1.65f, 0.38f, _metalGrey, false);
            Box("Fridge handle", 0.24f, 0.5f, 0.34f, 0.27f, 1.05f, 0.38f, _metalGrey, false);
            _root = keep;
        }

        /// <summary>Endless in the Facility: gives every marked station its Interactable (Feed, Groom or Charge, as the house's care stations do).</summary>
        public static void MakeStationsInteractive()
        {
            foreach (var s in Stations)
            {
                if (s.Go == null) continue;
                var col = s.Go.GetComponent<BoxCollider>();
                if (col != null) col.isTrigger = true; // (never blocks you)
                string text = s.Kind == StationKind.Feed ? "Feed Tattletail" : s.Kind == StationKind.Groom ? "Groom Tattletail" : "Charge Tattletail";
                float hold = s.Kind == StationKind.Charge ? 5f : 3f;   // (charging is 5 s, as the house's)
                Interactable it = PartyGameRunner.MakeInteractable(s.Go, text, hold);
                InteractableActions.Actions act = s.Kind == StationKind.Feed ? InteractableActions.Actions.FeedTattletail
                    : s.Kind == StationKind.Groom ? InteractableActions.Actions.GroomTattletail : InteractableActions.Actions.ChargeTattletail;
                it.actions = new InteractableActions { interactActions = new InteractableActions.Actions[] { act } };
            }
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Factory] " + Stations.Count + " care stations made usable.");
        }

        private static void CareStations()
        {
            Stations.Clear();
            if (FactoryMode.EndlessPreview) { MarkedCareStations(); return; }
            // Feed: TATTLE-TREATS.
            Treats(12.6f, 1.1f, 8.3f, 0f);           // lobby, on the security desk
            Treats(5.6f, 0.77f, 32.6f, 20f);         // break room table
            Treats(32.7f, 0.96f, 17.9f, 0f);         // inspection table
            Treats(47.9f, 1.6f, 1.5f, -15f);         // warehouse, on a crate
            Treats(7f, UP + 0.78f, 4f, 10f);         // upstairs, conference table
            Treats(9.35f, DOWN + 0.9f, 9.35f, 0f);   // basement, on the fuel drum
            // Groom: brushes.
            Brush(20.2f, 0.96f, 3.6f, 30f);          // testing lab bench
            Brush(13.5f, 0.93f, 31.3f, 90f);         // break room counter
            Brush(7.5f, 0.85f, 14.8f, -20f);         // lobby, the display case
            Brush(35.7f, 1.1f, 39.4f, 0f);           // assembly, the tool chest
            Brush(3f, UP + 0.77f, 36.3f, 45f);       // upstairs, the foreman's desk
            Stand(21f, DOWN, 9.6f); Brush(21f, DOWN + 0.9f, 9.6f, 0f); // basement, boiler room
            // Charge: docks.
            Stand(5.2f, 0f, 15f); Dock(5.2f, 0.9f, 15f, 90f);           // lobby, by the display case
            Stand(41.5f, 0f, 30.8f); Dock(41.5f, 0.9f, 30.8f, -90f);   // assembly, by the warehouse wall
            Stand(59.4f, 0f, 26.6f); Dock(59.4f, 0.9f, 26.6f, -90f);   // warehouse, by the docks
            Dock(13.2f, UP + 0.9f, 16.5f, 180f);                       // upstairs, PA console
            Dock(13.5f, 0.93f, 32.3f, 90f);                            // break room counter
            Dock(23.5f, 0.96f, 3.6f, 0f);                              // testing lab bench
            Stand(11.3f, DOWN, 17.3f); Dock(11.3f, DOWN + 0.9f, 17.3f, 90f); // basement, generator room
        }

        // ── Glow: the player has to see where batteries and care stations are ──
        // A soft light and a small bright orb over each, pulsing slowly. Colors: batteries yellow, charge green, groom pink, feed orange.
        private static readonly Color GlowBattery = new Color(1f, 0.92f, 0.25f), GlowCharge = new Color(0.25f, 1f, 0.45f),
            GlowGroom = new Color(1f, 0.45f, 0.75f), GlowFeed = new Color(1f, 0.6f, 0.2f);

        private static void AddGlow(Transform parent, Vector3 localPos, Color c)
        {
            var orb = new GameObject("Glow");
            orb.transform.SetParent(parent, false);
            orb.transform.localPosition = localPos;
            var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(core.GetComponent<Collider>());
            core.transform.SetParent(orb.transform, false);
            core.transform.localScale = Vector3.one * 0.12f;
            core.GetComponent<Renderer>().sharedMaterial = FTex.Glow(c).Mat;
            core.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var l = orb.AddComponent<Light>();
            l.type = LightType.Point; l.range = 5f; l.intensity = 1.3f; l.color = c; l.shadows = LightShadows.None;
            orb.AddComponent<GlowPulse>();
        }

        private static GameObject StationRoot(string name, float x, float y, float z, float yaw, StationKind kind, Vector3 hitSize)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.transform.localPosition = new Vector3(x, y, z);
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var hit = go.AddComponent<BoxCollider>(); // generous, easy to aim at
            hit.center = new Vector3(0f, hitSize.y * 0.5f, 0f);
            hit.size = hitSize;
            Stations.Add(new Station { Go = go, Kind = kind });
            AddGlow(go.transform, new Vector3(0f, hitSize.y + 0.25f, 0f), kind == StationKind.Feed ? GlowFeed : kind == StationKind.Groom ? GlowGroom : GlowCharge);
            return go;
        }

        // A box of TATTLE-TREATS (purple, like the toy's box).
        private static void Treats(float x, float y, float z, float yaw)
        {
            var go = StationRoot("Tattle-Treats", x, y, z, yaw, StationKind.Feed, new Vector3(0.45f, 0.35f, 0.35f));
            Transform keep = _root;
            _root = go.transform;
            Box("Treats box", -0.14f, 0f, -0.09f, 0.14f, 0.22f, 0.09f, FTex.Plain(new Color(0.42f, 0.16f, 0.52f), 0.35f), false);
            Box("Treats lid (open)", -0.14f, 0.22f, 0.09f, 0.14f, 0.23f, 0.24f, FTex.Plain(new Color(0.42f, 0.16f, 0.52f), 0.35f), false);
            Box("Treats band", -0.141f, 0.08f, -0.091f, 0.141f, 0.13f, 0.091f, FTex.Plain(new Color(0.95f, 0.8f, 0.15f), 0.3f), false);
            Sign("TATTLE-TREATS", 0f, 0.105f, -0.093f, 0f, 0.035f, 0.28f, new Color(0.25f, 0.08f, 0.3f));
            for (int i = 0; i < 3; i++) Ball("Treat", new Vector3(-0.07f + i * 0.07f, 0.23f, 0f), new Vector3(0.05f, 0.03f, 0.05f), FTex.Plain(new Color(0.75f, 0.35f, 0.2f), 0.2f));
            _root = keep;
        }

        // A grooming brush on a little tray.
        private static void Brush(float x, float y, float z, float yaw)
        {
            var go = StationRoot("Grooming kit", x, y, z, yaw, StationKind.Groom, new Vector3(0.4f, 0.25f, 0.3f));
            Transform keep = _root;
            _root = go.transform;
            Box("Tray", -0.15f, 0f, -0.1f, 0.15f, 0.02f, 0.1f, _metalGrey, false);
            Box("Brush handle", -0.13f, 0.02f, -0.02f, 0.05f, 0.045f, 0.02f, FTex.Plain(new Color(0.85f, 0.35f, 0.55f), 0.4f), false);
            Box("Brush head", 0.05f, 0.02f, -0.04f, 0.13f, 0.05f, 0.04f, FTex.Plain(new Color(0.85f, 0.35f, 0.55f), 0.4f), false);
            Box("Bristles", 0.055f, 0.05f, -0.035f, 0.125f, 0.075f, 0.035f, FTex.Plain(new Color(0.9f, 0.88f, 0.8f), 0.05f), false);
            _root = keep;
        }

        // A Tattletail charging dock (the little green light is its standby LED).
        private static void Dock(float x, float y, float z, float yaw)
        {
            var go = StationRoot("Charging dock", x, y, z, yaw, StationKind.Charge, new Vector3(0.4f, 0.3f, 0.35f));
            Transform keep = _root;
            _root = go.transform;
            Box("Dock base", -0.16f, 0f, -0.13f, 0.16f, 0.05f, 0.13f, _beige, false);
            Box("Dock cradle", -0.12f, 0.05f, 0.06f, 0.12f, 0.2f, 0.12f, _beige, false);
            Box("Dock contacts", -0.05f, 0.05f, -0.02f, 0.05f, 0.06f, 0.04f, _metalGrey, false);
            Box("Dock LED", 0.08f, 0.12f, 0.055f, 0.1f, 0.14f, 0.06f, FTex.Glow(new Color(0.2f, 0.9f, 0.3f)), false);
            Sign("CHARGE", 0f, 0.16f, 0.055f, 0f, 0.03f, 0.2f, new Color(0.2f, 0.2f, 0.2f));
            _root = keep;
        }

        // A little metal stand for a station that has no table to sit on.
        private static void Stand(float x, float y, float z)
        {
            Box("Station stand", x - 0.2f, y, z - 0.2f, x + 0.2f, y + 0.05f, z + 0.2f, _dark, true);
            Box("Station stand", x - 0.05f, y + 0.05f, z - 0.05f, x + 0.05f, y + 0.85f, z + 0.05f, _metalGrey, true);
            Box("Station stand", x - 0.2f, y + 0.85f, z - 0.2f, x + 0.2f, y + 0.9f, z + 0.2f, _dark, true);
        }
    }
}

namespace KaleidoscopeEndless
{
    /// <summary>A slow pulse on a Facility glow (batteries and care stations).</summary>
    public class GlowPulse : MonoBehaviour
    {
        private Light _l;
        private float _base, _seed;
        private void Start() { _l = GetComponent<Light>(); _base = _l != null ? _l.intensity : 1f; _seed = Random.value * 10f; }
        private void Update()
        {
            if (_l == null) return;
            _l.intensity = _base * (0.75f + 0.25f * Mathf.Sin(Time.time * 2.2f + _seed));
            transform.localScale = Vector3.one * (1f + 0.12f * Mathf.Sin(Time.time * 2.2f + _seed));
        }
    }
}
