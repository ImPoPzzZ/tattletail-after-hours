using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace KaleidoscopeEndless
{
    public enum HubMachine { NewGame, Continue, Endless, Papas, Options, Quit, EndlessShop, PapaShop, Kaleidoscope, LevelSelect, Gumball, Versus, Wardrobe, VersusShop, SummerVacation, NightShift, Pinball, AirHockey, Mirror, Recalled }

    /// <summary>
    /// Everything inside the arcade HUB, placed from the user's own marks
    /// (HubMarkTool, room-relative coordinates): black partition walls, the
    /// big full-height divider, hanging doorway signs, the arcade machines
    /// (each with a lit marquee saying what it does) and the stats board.
    /// </summary>
    public static class HubLayout
    {
        public const float PARTITION_HEIGHT = 2.6f;
        public static readonly Vector3 SpawnLocal = new Vector3(-11.2f, 0.05f, -0.16f);
        public const float SPAWN_YAW = 90f; // facing into the hall

        private struct Seg { public float x1, z1, x2, z2; public bool fullHeight; }

        // Cleaned-up versions of the marked wall lines (snapped straight, ends pulled to the room edges).
        private static readonly Seg[] Walls =
        {
            new Seg { x1 = -5.54f, z1 = 3.40f,  x2 = -5.54f, z2 = 9f },      // arcade | options divider
            new Seg { x1 = -12f,   z1 = 3.45f,  x2 = -8.25f, z2 = 3.45f },   // arcade room front (left of its doorway)
            new Seg { x1 = -5.74f, z1 = 3.45f,  x2 = -4.18f, z2 = 3.45f },   // between the two doorways
            new Seg { x1 = -2.79f, z1 = 3.45f,  x2 = 0.40f,  z2 = 3.45f },   // options room front (right of its doorway)
            new Seg { x1 = 0.40f,  z1 = -9f,    x2 = 0.40f,  z2 = 9f, fullHeight = true }, // splits the whole room in half
            new Seg { x1 = -12f,   z1 = -3.47f, x2 = -5.64f, z2 = -3.47f },  // shop room top
            new Seg { x1 = -5.64f, z1 = -9f,    x2 = -5.64f, z2 = -5.86f },  // shop room side (below its doorway)
        };

        private struct MachineDef
        {
            public HubMachine id; public string title, blurb; public Vector3 pos; public float yaw; public Color color;
            public float hold; // 0 = the normal 0.4s hold
            public Color body; // cabinet color; unset (clear) = the usual near-black
        }

        private static readonly MachineDef[] Machines =
        {
            new MachineDef { id = HubMachine.NewGame,  title = "NEW GAME",   blurb = "Start the story\nfrom Night 1",       pos = new Vector3(-11.56f, 0f, 8.58f), yaw = 180f, color = new Color(0.25f, 0.55f, 1f) },
            new MachineDef { id = HubMachine.Continue, title = "CONTINUE",   blurb = "Back to the story\nwhere you left off", pos = new Vector3(-9.66f, 0f, 8.58f),  yaw = 180f, color = new Color(0.25f, 1f, 0.45f) },
            new MachineDef { id = HubMachine.Endless,  title = "ENDLESS",    blurb = "Survive Mama\nas long as you can",    pos = new Vector3(-7.57f, 0f, 8.58f),  yaw = 180f, color = new Color(0.8f, 0.35f, 1f) },
            // Next to Level Select on the arcade room's left wall (where the first Level Select mark was).
            new MachineDef { id = HubMachine.Versus, title = "VERSUS", blurb = "Mama vs the child\n(1v1)", pos = new Vector3(-11.61f, 0f, 6.39f), yaw = 90f, color = new Color(0.9f, 0.15f, 0.35f) },
            new MachineDef { id = HubMachine.LevelSelect, title = "LEVEL\nSELECT", blurb = "Replay any night\nof the story",  pos = new Vector3(-11.61f, 0f, 5.43f), yaw = 90f,  color = new Color(0.55f, 0.85f, 0.3f) },
            new MachineDef { id = HubMachine.Papas,    title = "PAPA'S 10TH\nBIRTHDAY!", blurb = "Feed Papa & keep\nthe party going", pos = new Vector3(-11.58f, 0f, 4.47f), yaw = 90f, color = new Color(1f, 0.6f, 0.15f) },
            new MachineDef { id = HubMachine.Options,  title = "OPTIONS",    blurb = "Settings &\nvolume",                      pos = new Vector3(-4.55f, 0f, 8.58f),  yaw = 180f, color = new Color(0.2f, 0.9f, 1f) },
            // Against the big divider wall, facing the hall. Longer hold so nobody quits by accident.
            new MachineDef { id = HubMachine.Quit,     title = "QUIT",       blurb = "Leave the game",                      pos = new Vector3(-0.12f, 0f, 0.32f),  yaw = 270f, color = new Color(1f, 0.25f, 0.25f), hold = 1.5f },
            // The Kaleidoscope DLC (the game's "Memories"), next to QUIT on the same wall.
            new MachineDef { id = HubMachine.Kaleidoscope, title = "KALEIDOSCOPE", blurb = "Relive Tattletail's\nmemories (DLC)", pos = new Vector3(-0.12f, 0f, -1.6f), yaw = 270f, color = new Color(1f, 0.35f, 0.75f) },
            // Versus outfits (boy or girl), next to the Kaleidoscope machine.
            new MachineDef { id = HubMachine.Wardrobe, title = "OUTFITS", blurb = "Boy or girl\n& your Versus look", pos = new Vector3(-0.12f, 0f, -3.5f), yaw = 270f, color = new Color(0.3f, 1f, 0.6f) },
            // Shop room: against its back (south) wall, facing the entrance, inside the marked shop area.
            new MachineDef { id = HubMachine.EndlessShop, title = "ENDLESS\nSHOP", blurb = "Spend your shards",     pos = new Vector3(-10.9f, 0f, -8.58f), yaw = 0f,   color = new Color(0.8f, 0.35f, 1f) },
            // Next to the shop machines. Its own model (a gumball machine, not a cabinet).
            new MachineDef { id = HubMachine.Gumball, title = "GUMBALLS", blurb = "", pos = new Vector3(-7.51f, 0f, -8.51f), yaw = 0f, color = new Color(1f, 0.3f, 0.35f) },
            new MachineDef { id = HubMachine.PapaShop,    title = "PAPA\nSHOP",    blurb = "Spend shards",    pos = new Vector3(-9.1f, 0f, -8.58f),  yaw = 0f,   color = new Color(1f, 0.6f, 0.15f) },
            // Arcade room, right wall (facing VERSUS): sand cabinet, yellow bars.
            new MachineDef { id = HubMachine.SummerVacation, title = "SUMMER\nVACATION", blurb = "Work Mama's\nsmoothie bar", pos = new Vector3(-6.03f, 0f, 6.39f), yaw = 270f, color = new Color(1f, 0.85f, 0.15f), body = new Color(0.86f, 0.74f, 0.52f) },
            // Next to SUMMER VACATION: Night Shift (3.4, in progress: dev builds only until it ships). Concrete-grey
            // cabinet, night-vision green marquee.
            new MachineDef { id = HubMachine.NightShift, title = "WAYGETTER\nFACILITY", blurb = "Waygetter Electronics\n2026", pos = new Vector3(-6.03f, 0f, 5.43f), yaw = 270f, color = new Color(0.35f, 1f, 0.42f), body = new Color(0.2f, 0.21f, 0.22f) },
            new MachineDef { id = HubMachine.VersusShop,  title ="VERSUS\nSHOP",  blurb = "Spend shards\nMama & Papa skins", pos = new Vector3(-6.4f, 0f, -8.58f), yaw = 0f, color = new Color(0.95f, 0.12f, 0.18f) },
        };

        private static readonly Dictionary<GameObject, HubMachine> _machineByObject = new Dictionary<GameObject, HubMachine>();

        public static bool TryGetMachine(GameObject go, out HubMachine m) { return _machineByObject.TryGetValue(go, out m); }
        public static void Register(GameObject go, HubMachine m) { _machineByObject[go] = m; }

        public static void Build(Transform room, float roomHeight)
        {
            _machineByObject.Clear();
            Material black = Mat.Lit(new Color(0.02f, 0.02f, 0.025f), 0.1f);

            foreach (var w in Walls) Wall(room, w, w.fullHeight ? roomHeight : PARTITION_HEIGHT, black);

            // Signs hanging from the ceiling over each doorway, facing the hall.
            HangingSign(room, new Vector3(-7.0f, 3.2f, 3.45f), 180f, "ARCADE", new Color(1f, 0.3f, 0.8f), roomHeight);
            HangingSign(room, new Vector3(-3.49f, 3.2f, 3.45f), 180f, "OPTIONS", new Color(0.2f, 0.9f, 1f), roomHeight);
            HangingSign(room, new Vector3(-5.64f, 3.2f, -4.67f), 90f, "SHOP", new Color(1f, 0.85f, 0.2f), roomHeight);

            foreach (var m in Machines)
            {
                if (m.id == HubMachine.NightShift) continue; // the Waygetter Facility is shelved (kept for later): no machine
                if (m.id == HubMachine.Gumball) GumballMachine(room, m);
                else Machine(room, m);
            }

            // Tattletail Pinball, at the user's mark: in the hall, its back against the shop room's wall, facing the hall.
            try { PinballGame.Build(room, new Vector3(-6.95f, 0f, -2.63f), 0f); }
            catch (System.Exception e) { KaleidoscopeEndlessPlugin.Log.LogError("KE: [Pinball] Couldn't build the machine (the Arcade loads without it): " + e); }

            // Tattletail Air Hockey, by the user's mark (0.8 m east of it, so the blue end has room to stand and leave):
            // in the hall by the south wall, running east-west (an end each).
            try { AirHockeyGame.Build(room, new Vector3(-3.1f, 0f, -8.11f), 0f); }
            catch (System.Exception e) { KaleidoscopeEndlessPlugin.Log.LogError("KE: [AirHockey] Couldn't build the table (the Arcade loads without it): " + e); }

            // The door outside (Recalled): on the wall right behind where you arrive.
            try { OutsideDoor(room, new Vector3(-11.98f, 0f, SpawnLocal.z), 90f); }
            catch (System.Exception e) { KaleidoscopeEndlessPlugin.Log.LogError("KE: [Recalled] Couldn't build the door (the Arcade loads without it): " + e); }

            StatsBoard(room);
            HowToPlayBoard(room);
            try { DailyPapers.Build(room); }
            catch (System.Exception e) { KaleidoscopeEndlessPlugin.Log.LogError("KE: [Daily] Couldn't build the board (the Arcade loads without it): " + e); }
            // A full-length mirror on the divider wall, just past the OUTFITS machine: pick an outfit, turn, look.
            try { HubMirror.Build(room, new Vector3(0.29f, 1.22f, -5.6f), 270f, 1.3f, 2.0f); }
            catch (System.Exception e) { KaleidoscopeEndlessPlugin.Log.LogError("KE: [Mirror] Couldn't build the mirror (the Arcade loads without it): " + e); }
        }

        // ── Walls ────────────────────────────────────────────────────────────
        private static void Wall(Transform room, Seg s, float height, Material mat)
        {
            const float T = 0.2f;
            bool alongX = Mathf.Abs(s.x2 - s.x1) >= Mathf.Abs(s.z2 - s.z1);
            float len = alongX ? Mathf.Abs(s.x2 - s.x1) : Mathf.Abs(s.z2 - s.z1);
            var go = Mat.Box(room, "HubWall", new Vector3((s.x1 + s.x2) / 2f, height / 2f, (s.z1 + s.z2) / 2f),
                alongX ? new Vector3(len + T, height, T) : new Vector3(T, height, len + T), mat);
            go.GetComponent<Renderer>().material = mat;
        }

        // ── Signs ────────────────────────────────────────────────────────────
        private static void HangingSign(Transform room, Vector3 pos, float yaw, string text, Color color, float roomHeight)
        {
            var sign = SignBoard(room, pos, yaw, 1.8f, 0.5f, color);
            AddText(sign, text, color * 0.85f, 0.3f, TextAnchor.MiddleCenter, Vector3.zero, 1.7f);

            // Two thin wires up to the ceiling.
            Material wire = Mat.Lit(new Color(0.15f, 0.15f, 0.15f), 0.4f);
            float wireLen = roomHeight - (pos.y + 0.25f);
            foreach (float x in new[] { -0.7f, 0.7f })
            {
                var w = Mat.Box(sign, "Wire", new Vector3(x, 0.25f + wireLen / 2f, 0f), new Vector3(0.015f, wireLen, 0.015f), wire);
                Object.Destroy(w.GetComponent<Collider>());
            }
        }

        /// <summary>A dark board with a neon frame, its readable side facing local +z. Returns the board's holder.</summary>
        private static Transform SignBoard(Transform parent, Vector3 localPos, float yaw, float width, float height, Color color)
        {
            var holder = new GameObject("Sign").transform;
            holder.SetParent(parent, false);
            holder.localPosition = localPos;
            holder.localRotation = Quaternion.Euler(0f, yaw, 0f);

            var board = Mat.Box(holder, "Board", Vector3.zero, new Vector3(width, height, 0.06f), Mat.Lit(new Color(0.03f, 0.03f, 0.035f), 0.2f));
            Object.Destroy(board.GetComponent<Collider>());

            Material neon = Mat.Unlit(color);
            const float e = 0.03f;
            Vector3[] pos = { new Vector3(0f, height / 2f, 0.035f), new Vector3(0f, -height / 2f, 0.035f), new Vector3(-width / 2f, 0f, 0.035f), new Vector3(width / 2f, 0f, 0.035f) };
            Vector3[] size = { new Vector3(width + e, e, e), new Vector3(width + e, e, e), new Vector3(e, height, e), new Vector3(e, height, e) };
            for (int i = 0; i < 4; i++)
                Object.Destroy(Mat.Box(holder, "Neon", pos[i], size[i], neon).GetComponent<Collider>());
            return holder;
        }

        private static Text AddText(Transform holder, string text, Color color, float lineHeight, TextAnchor anchor, Vector3 offset, float width)
        {
            var t = WorldText.Create(holder, text, color, lineHeight, anchor, width);
            t.canvas.transform.localPosition = offset + new Vector3(0f, 0f, 0.045f);
            t.canvas.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // world UI reads from behind its forward — flip it to face +z
            return t;
        }

        /// <summary>
        /// The way out of the Arcade: a plain door with night in its window and OUTSIDE over it. Hold to use it: it
        /// leads to the town (Recalled). Its front faces local +z.
        /// </summary>
        private static void OutsideDoor(Transform room, Vector3 pos, float yaw)
        {
            var root = new GameObject("KE_OutsideDoor");
            root.transform.SetParent(room, false);
            root.transform.localPosition = pos;
            root.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Transform t = root.transform;
            Material frame = Mat.Lit(new Color(0.16f, 0.1f, 0.07f), 0.25f), slab = Mat.Lit(new Color(0.3f, 0.19f, 0.12f), 0.3f);
            Part(t, new Vector3(-0.62f, 1.15f, 0.07f), new Vector3(0.12f, 2.3f, 0.14f), frame);
            Part(t, new Vector3(0.62f, 1.15f, 0.07f), new Vector3(0.12f, 2.3f, 0.14f), frame);
            Part(t, new Vector3(0f, 2.26f, 0.07f), new Vector3(1.36f, 0.12f, 0.14f), frame);
            Part(t, new Vector3(0f, 1.075f, 0.05f), new Vector3(1.12f, 2.15f, 0.06f), slab);
            // The window: night outside, a street lamp a long way off.
            Part(t, new Vector3(0f, 1.55f, 0.085f), new Vector3(0.56f, 0.62f, 0.012f), Mat.Unlit(new Color(0.03f, 0.05f, 0.12f)));
            Part(t, new Vector3(0.12f, 1.62f, 0.093f), new Vector3(0.035f, 0.035f, 0.004f), Mat.Unlit(new Color(1f, 0.82f, 0.45f)));
            Part(t, new Vector3(0f, 1.55f, 0.09f), new Vector3(0.02f, 0.62f, 0.014f), frame);
            Part(t, new Vector3(0f, 1.55f, 0.09f), new Vector3(0.56f, 0.02f, 0.014f), frame);
            var knob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(knob.GetComponent<Collider>());
            knob.transform.SetParent(t, false);
            knob.transform.localPosition = new Vector3(0.44f, 1.02f, 0.11f);
            knob.transform.localScale = Vector3.one * 0.08f;
            knob.GetComponent<Renderer>().sharedMaterial = Mat.Lit(new Color(0.75f, 0.6f, 0.25f), 0.8f);
            // Recall tape across the bottom panel, and the sign.
            Part(t, new Vector3(0f, 0.55f, 0.083f), new Vector3(1.12f, 0.09f, 0.006f), Mat.Lit(new Color(0.85f, 0.62f, 0.05f), 0.2f));
            Color amber = new Color(1f, 0.72f, 0.18f);
            var sign = SignBoard(t, new Vector3(0f, 2.72f, 0.06f), 0f, 1.5f, 0.42f, amber);
            AddText(sign, "OUTSIDE", amber * 0.9f, 0.26f, TextAnchor.MiddleCenter, Vector3.zero, 1.4f);
            var lightGO = new GameObject("DoorLight");
            lightGO.transform.SetParent(t, false);
            lightGO.transform.localPosition = new Vector3(0f, 2.3f, 0.6f);
            var l = lightGO.AddComponent<Light>();
            l.type = LightType.Point; l.color = amber; l.range = 3.5f; l.intensity = 0.9f;

            var hit = root.AddComponent<BoxCollider>();
            hit.center = new Vector3(0f, 1.1f, 0.12f);
            hit.size = new Vector3(1.3f, 2.2f, 0.3f);
            PartyGameRunner.MakeInteractable(root, "Go outside", 0.6f);
            Register(root, HubMachine.Recalled);
        }

        // ── Arcade machines ──────────────────────────────────────────────────
        /// <summary>Level Select and Kaleidoscope unlock by beating the story, like the game's own menu.</summary>
        private static bool IsLocked(HubMachine m)
        {
            if (m == HubMachine.LevelSelect) return !GD.HasCompletedGame();
            if (m == HubMachine.Kaleidoscope) return !GD.WaydriveContentUnlocked();
            return false;
        }

        private static void Machine(Transform room, MachineDef d)
        {
            var root = new GameObject("ArcadeMachine_" + d.id);
            root.transform.SetParent(room, false);
            root.transform.localPosition = d.pos;
            root.transform.localRotation = Quaternion.Euler(0f, d.yaw, 0f);
            Transform t = root.transform;

            Material body = Mat.Lit(d.body.a > 0f ? d.body : new Color(0.06f, 0.06f, 0.07f), 0.35f);
            Material stripe = Mat.Unlit(d.color);

            Part(t, new Vector3(0f, 0.525f, 0f), new Vector3(0.75f, 1.05f, 0.8f), body);            // lower cabinet
            Part(t, new Vector3(0f, 1.45f, -0.15f), new Vector3(0.75f, 0.8f, 0.5f), body);           // monitor housing
            var deck = Part(t, new Vector3(0f, 1.08f, 0.2f), new Vector3(0.75f, 0.08f, 0.36f), Mat.Lit(new Color(0.1f, 0.1f, 0.12f), 0.3f));
            deck.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);                             // tilted toward the player

            // Screen, leaning back a little, glowing in the machine's color with what it does.
            var screen = new GameObject("Screen").transform;
            screen.SetParent(t, false);
            screen.localPosition = new Vector3(0f, 1.48f, 0.145f); // clear of the housing's front face even when tilted (was flickering)
            screen.localRotation = Quaternion.Euler(-8f, 0f, 0f);
            Part(screen, Vector3.zero, new Vector3(0.6f, 0.45f, 0.02f), Mat.Unlit(d.color * 0.18f));
            bool locked = IsLocked(d.id);
            AddText(screen, locked ? "LOCKED\nBeat the story" : d.blurb, locked ? new Color(1f, 0.3f, 0.3f) : d.color * 0.8f, 0.06f, TextAnchor.MiddleCenter, new Vector3(0f, 0f, -0.03f), 0.56f);

            // Lit marquee on top — the sign saying what this machine is.
            Part(t, new Vector3(0f, 2.0f, -0.05f), new Vector3(0.75f, 0.32f, 0.34f), body);
            var marquee = new GameObject("Marquee").transform;
            marquee.SetParent(t, false);
            marquee.localPosition = new Vector3(0f, 2.0f, 0.13f); // in front of the box's face, not flush with it (was flickering)
            Part(marquee, Vector3.zero, new Vector3(0.7f, 0.26f, 0.01f), Mat.Unlit(d.color));
            AddText(marquee, d.title, new Color(0.04f, 0.04f, 0.05f), d.title.Contains("\n") ? 0.085f : 0.12f, TextAnchor.MiddleCenter, new Vector3(0f, 0f, -0.035f), 0.66f);

            // Side neon stripes.
            Part(t, new Vector3(0.38f, 1.05f, 0.33f), new Vector3(0.012f, 1.8f, 0.06f), stripe);
            Part(t, new Vector3(-0.38f, 1.05f, 0.33f), new Vector3(0.012f, 1.8f, 0.06f), stripe);

            // Joystick + buttons.
            var stickMat = Mat.Lit(new Color(0.15f, 0.15f, 0.15f), 0.5f);
            var stick = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stick.transform.SetParent(t, false);
            stick.transform.localPosition = new Vector3(-0.17f, 1.16f, 0.22f);
            stick.transform.localScale = new Vector3(0.025f, 0.05f, 0.025f);
            stick.GetComponent<Renderer>().material = stickMat;
            Object.Destroy(stick.GetComponent<Collider>());
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.transform.SetParent(t, false);
            ball.transform.localPosition = new Vector3(-0.17f, 1.22f, 0.22f);
            ball.transform.localScale = Vector3.one * 0.06f;
            ball.GetComponent<Renderer>().material = Mat.Lit(new Color(0.85f, 0.1f, 0.1f), 0.7f);
            Object.Destroy(ball.GetComponent<Collider>());
            Color[] btn = { new Color(1f, 0.2f, 0.2f), new Color(1f, 0.85f, 0.2f), new Color(0.2f, 0.6f, 1f) };
            for (int i = 0; i < 3; i++)
            {
                var b = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                b.transform.SetParent(t, false);
                b.transform.localPosition = new Vector3(0.02f + i * 0.09f, 1.135f, 0.21f);
                b.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);
                b.transform.localScale = new Vector3(0.05f, 0.01f, 0.05f);
                b.GetComponent<Renderer>().material = Mat.Unlit(btn[i]);
                Object.Destroy(b.GetComponent<Collider>());
            }

            // Coin door with a glowing slot.
            Part(t, new Vector3(0f, 0.5f, 0.405f), new Vector3(0.22f, 0.26f, 0.01f), Mat.Lit(new Color(0.12f, 0.12f, 0.13f), 0.5f));
            Part(t, new Vector3(0f, 0.55f, 0.412f), new Vector3(0.025f, 0.08f, 0.005f), Mat.Unlit(new Color(1f, 0.55f, 0.1f)));

            // A soft colored glow in front of the machine.
            var glowGO = new GameObject("Glow");
            glowGO.transform.SetParent(t, false);
            glowGO.transform.localPosition = new Vector3(0f, 1.5f, 0.7f);
            var glow = glowGO.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = d.color;
            glow.range = 2.2f;
            glow.intensity = 0.7f;
            glow.shadows = LightShadows.None;
            glow.renderMode = LightRenderMode.ForcePixel; // otherwise it pops between pixel and vertex lighting as you walk

            // One solid collider for walking into it and for the interact prompt.
            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 1.08f, 0f);
            col.size = new Vector3(0.78f, 2.16f, 0.82f);
            PartyGameRunner.MakeInteractable(root, (d.id == HubMachine.Quit ? "" : "Play: ") + d.title.Replace("\n", " "), d.hold > 0f ? d.hold : 0.4f);
            _machineByObject[root] = d.id;
        }

        // ── Gumball machine: a random gadget for Gadgets.GUMBALL_PRICE shards ──
        public static Transform GumballChute; // where the gumball pops out

        private static void GumballMachine(Transform room, MachineDef d)
        {
            var root = new GameObject("GumballMachine");
            root.transform.SetParent(room, false);
            root.transform.localPosition = d.pos;
            root.transform.localRotation = Quaternion.Euler(0f, d.yaw, 0f);
            Transform t = root.transform;

            Material red = Mat.Lit(new Color(0.8f, 0.08f, 0.1f), 0.75f);
            Material chrome = Mat.Lit(new Color(0.75f, 0.75f, 0.8f), 0.9f);

            Shape(t, PrimitiveType.Cylinder, new Vector3(0f, 0.04f, 0f), new Vector3(0.5f, 0.04f, 0.5f), red);     // foot
            Shape(t, PrimitiveType.Cylinder, new Vector3(0f, 0.5f, 0f), new Vector3(0.12f, 0.45f, 0.12f), red);     // pole
            Part(t, new Vector3(0f, 1.05f, 0f), new Vector3(0.42f, 0.34f, 0.42f), red);                             // coin box
            Shape(t, PrimitiveType.Cylinder, new Vector3(0f, 1.24f, 0f), new Vector3(0.46f, 0.02f, 0.46f), chrome); // bowl rim

            // The coin knob and the chute on the front.
            var knob = Shape(t, PrimitiveType.Cylinder, new Vector3(0f, 1.1f, 0.22f), new Vector3(0.14f, 0.02f, 0.14f), chrome);
            knob.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Part(t, new Vector3(0f, 1.1f, 0.235f), new Vector3(0.03f, 0.1f, 0.02f), Mat.Lit(new Color(0.3f, 0.3f, 0.32f), 0.8f));
            Part(t, new Vector3(0f, 0.93f, 0.225f), new Vector3(0.12f, 0.06f, 0.05f), chrome);
            var chute = new GameObject("Chute").transform;
            chute.SetParent(t, false);
            chute.localPosition = new Vector3(0f, 0.97f, 0.3f);
            GumballChute = chute;

            // The globe: a pile of gumballs, under a glass dome if the game has a see-through shader.
            Color[] colors = { new Color(1f, 0.25f, 0.25f), new Color(0.4f, 0.6f, 1f), new Color(1f, 0.8f, 0.25f), new Color(0.4f, 1f, 0.5f), new Color(1f, 0.45f, 0.9f), Color.white };
            var rng = new System.Random(7); // same pile every time
            int placed = 0;
            while (placed < 70)
            {
                Vector3 p = new Vector3((float)rng.NextDouble() * 2f - 1f, (float)rng.NextDouble(), (float)rng.NextDouble() * 2f - 1f);
                if (p.magnitude > 1f) continue;
                p *= 0.19f;
                Shape(t, PrimitiveType.Sphere, new Vector3(p.x, 1.28f + p.y * 1.4f, p.z), Vector3.one * 0.055f, Mat.Lit(colors[placed % colors.Length], 0.8f));
                placed++;
            }
            // A few rainbow DLC gumballs sitting on top of the pile.
            for (int i = 0; i < 5; i++)
            {
                float a = i * 72f * Mathf.Deg2Rad;
                var dlc = Shape(t, PrimitiveType.Sphere, new Vector3(Mathf.Cos(a) * 0.09f, 1.5f + (i % 2) * 0.03f, Mathf.Sin(a) * 0.09f), Vector3.one * 0.058f, Mat.Lit(Color.white, 0.85f));
                dlc.AddComponent<RainbowCycler>();
            }
            GlassBubble(t, new Vector3(0f, 1.44f, 0f), 0.24f);
            Shape(t, PrimitiveType.Cylinder, new Vector3(0f, 1.67f, 0f), new Vector3(0.2f, 0.03f, 0.2f), red);      // lid
            Shape(t, PrimitiveType.Sphere, new Vector3(0f, 1.72f, 0f), Vector3.one * 0.07f, red);                   // lid knob

            // Price sign above it.
            var sign = SignBoard(t, new Vector3(0f, 2.15f, -0.1f), 0f, 0.95f, 0.42f, d.color);
            AddText(sign, "GUMBALLS", d.color * 0.9f, 0.13f, TextAnchor.MiddleCenter, new Vector3(0f, 0.07f, 0f), 0.9f);
            AddText(sign, "Random gadget  -  " + Gadgets.GUMBALL_PRICE + " shards", new Color(0.8f, 0.8f, 0.82f), 0.055f, TextAnchor.MiddleCenter, new Vector3(0f, -0.1f, 0f), 0.9f);
            Part(t, new Vector3(0f, 1.87f, -0.1f), new Vector3(0.03f, 0.1f, 0.03f), chrome);

            var glowGO = new GameObject("Glow");
            glowGO.transform.SetParent(t, false);
            glowGO.transform.localPosition = new Vector3(0f, 1.5f, 0.6f);
            var glow = glowGO.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = d.color;
            glow.range = 2f;
            glow.intensity = 0.6f;
            glow.shadows = LightShadows.None;
            glow.renderMode = LightRenderMode.ForcePixel;

            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.9f, 0f);
            col.size = new Vector3(0.55f, 1.8f, 0.55f);
            PartyGameRunner.MakeInteractable(root, "Buy a gumball (" + Gadgets.GUMBALL_PRICE + " shards)", 1f);
            _machineByObject[root] = d.id;
        }

        /// <summary>
        /// A glass globe drawn as a see-through disc that always faces the
        /// camera (a sphere looks the same from every side). Built from world-
        /// space UI like the signs' text: see-through 3D materials either
        /// aren't in Tattletail's build or render solid, but UI draws with
        /// transparency and is still hidden behind walls. Gumballs in front
        /// of the disc stay crisp; the ones behind it get a glassy tint.
        /// </summary>
        private static void GlassBubble(Transform parent, Vector3 localCenter, float radius)
        {
            const float SCALE = 0.002f;
            var go = new GameObject("GlassBubble", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localCenter;
            go.transform.localScale = Vector3.one * SCALE;
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            ((RectTransform)go.transform).sizeDelta = Vector2.one * (radius * 2f / SCALE);

            var img = new GameObject("Glass", typeof(RectTransform), typeof(Image));
            img.transform.SetParent(go.transform, false);
            var rt = (RectTransform)img.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            var image = img.GetComponent<Image>();
            image.sprite = GlassSprite();
            image.raycastTarget = false;

            go.AddComponent<PartyLabelBillboard>();
        }

        private static Sprite _glassSprite;

        /// <summary>Clear in the middle, a bit cloudier toward the edge, a bright rim and a shine up-left.</summary>
        private static Sprite GlassSprite()
        {
            if (_glassSprite != null) return _glassSprite;
            const int N = 256;
            var tex = new Texture2D(N, N, TextureFormat.ARGB32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            float r = N / 2f;
            var shine = new Vector2(-0.38f, 0.42f); // up-left, in -1..1 disc coordinates
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f - r) / r, dy = (y + 0.5f - r) / r;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > 1f) { tex.SetPixel(x, y, Color.clear); continue; }

                    float a = 0.05f + Mathf.Pow(d, 4f) * 0.35f;                   // edges look thicker, like real glass
                    float rim = Mathf.Clamp01(1f - Mathf.Abs(d - 0.97f) / 0.03f); // thin bright outline
                    a = Mathf.Max(a, rim * 0.6f);

                    float sd = Vector2.Distance(new Vector2(dx, dy), shine);
                    float spot = Mathf.Clamp01(1f - sd / 0.22f);                   // soft shine
                    a = Mathf.Max(a, spot * spot * 0.7f);

                    float edge = Mathf.Clamp01((1f - d) * r);                      // 1px soft outer edge
                    float white = Mathf.Max(rim, spot);
                    tex.SetPixel(x, y, new Color(Mathf.Lerp(0.8f, 1f, white), Mathf.Lerp(0.9f, 1f, white), 1f, a * edge));
                }
            tex.Apply();
            _glassSprite = Sprite.Create(tex, new Rect(0f, 0f, N, N), new Vector2(0.5f, 0.5f));
            return _glassSprite;
        }

        private static GameObject Shape(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().material = mat;
            Object.Destroy(go.GetComponent<Collider>());
            return go;
        }

        private static GameObject Part(Transform parent, Vector3 pos, Vector3 size, Material mat)
        {
            var go = Mat.Box(parent, "Part", pos, size, mat);
            Object.Destroy(go.GetComponent<Collider>());
            return go;
        }

        // ── Stats board (the options room's back wall) ───────────────────────
        private static void StatsBoard(Transform room)
        {
            Color cyan = new Color(0.2f, 0.9f, 1f);
            var board = SignBoard(room, new Vector3(-2.02f, 1.65f, 8.96f), 180f, 3.44f, 1.7f, cyan);
            AddText(board, "STATS", cyan * 0.85f, 0.22f, TextAnchor.MiddleCenter, new Vector3(0f, 0.6f, 0f), 3.2f);

            string lines =
                "Longest survival:   " + Clock(EndlessStats.LongestSurvivalTime) + "\n" +
                "Last survival:      " + Clock(EndlessStats.LastSurvivalTime) + "\n" +
                "Objectives done:    " + EndlessStats.TotalObjectivesCompleted + "\n" +
                "Deaths:             " + EndlessStats.TotalDeaths + "\n" +
                "Times escaped:      " + EndlessStats.TotalSurvived + "\n" +
                "Shards:  " + Shards.Balance;
            AddText(board, lines, new Color(0.72f, 0.72f, 0.75f), 0.13f, TextAnchor.MiddleLeft, new Vector3(0f, -0.12f, 0f), 3.0f); // a left-aligned block centered on the board
        }

        // ── How to play (on the divider wall, next to QUIT) ──────────────────
        private static void HowToPlayBoard(Transform room)
        {
            Color yellow = new Color(1f, 0.85f, 0.2f);
            var board = SignBoard(room, new Vector3(0.265f, 1.55f, 2.1f), 270f, 1.8f, 1.2f, yellow);
            AddText(board, "HOW TO PLAY", yellow * 0.9f, 0.14f, TextAnchor.MiddleCenter, new Vector3(0f, 0.42f, 0f), 1.7f);
            string lines =
                "Hold LEFT CLICK on a machine to use it.\n" +
                "ENDLESS: finish objectives, survive Mama, earn shards.\n" +
                "When the PHONE rings, pick it up to escape with a bonus.\n" +
                "Press 1 to use your gadget. Get them at the GUMBALL machine.\n" +
                "PAPA'S PARTY: craft food, feed Papa, keep the party fun.\n" +
                "Spend shards in the SHOPS.";
            AddText(board, lines, new Color(0.8f, 0.8f, 0.82f), 0.068f, TextAnchor.MiddleLeft, new Vector3(0f, -0.1f, 0f), 1.66f);
        }

        private static string Clock(int seconds) { return (seconds / 60).ToString("D2") + ":" + (seconds % 60).ToString("D2"); }
    }

    /// <summary>
    /// Text placed in the 3D world. Uses a tiny world-space UI canvas instead
    /// of TextMesh: TextMesh's built-in font shader draws on top of
    /// everything (you could read signs through walls), while world-space UI
    /// text is hidden behind walls like any other object.
    /// </summary>
    public static class WorldText
    {
        private const float SCALE = 0.002f; // meters per canvas unit — small units keep the font crisp

        /// <summary>Readable from the canvas's -Z side (flip it 180° to face +Z). Returns the Text; its canvas is text.canvas.</summary>
        public static Text Create(Transform parent, string text, Color color, float lineHeight, TextAnchor alignment, float width)
        {
            var go = new GameObject("WorldText", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * SCALE;
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(width / SCALE, lineHeight * 10f / SCALE);

            var tgo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            tgo.transform.SetParent(go.transform, false);
            var trt = (RectTransform)tgo.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
            var t = tgo.GetComponent<Text>();
            t.font = MainMenuButtonInjector.RealFont != null ? MainMenuButtonInjector.RealFont : Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.fontSize = Mathf.RoundToInt(lineHeight / SCALE * 0.8f);
            t.alignment = alignment;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.text = text;
            return t;
        }
    }

    /// <summary>Shared primitive/material helpers for the hub.</summary>
    public static class Mat
    {
        private static Shader _lit;
        private static Shader LitShader { get { return _lit != null ? _lit : (_lit = Shader.Find("Standard") ?? Shader.Find("Diffuse")); } }

        public static Material Lit(Color c, float gloss)
        {
            var m = new Material(LitShader) { color = c };
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", gloss);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            return m;
        }

        public static Material Unlit(Color c) { return new Material(Shader.Find("Unlit/Color")) { color = c }; }

        public static GameObject Box(Transform parent, string name, Vector3 localPos, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().material = mat;
            return go;
        }
    }

    /// <summary>What each machine does when you use it.</summary>
    public static class HubLaunch
    {
        public static void Use(HubMachine m)
        {
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Hub] Machine used: " + m);
            switch (m)
            {
                case HubMachine.NewGame:
                    LeaveHub();
                    GameManager.PlayLevel((LevelEnum)Utils.GetLevelFlow().levels[0].name.GetHashCode()); // same as the menu's New Game
                    break;

                case HubMachine.Continue:
                    if (!StorySave.Known || StorySave.ContinueLevel == LevelEnum.None)
                    {
                        HubMode.SetHudText("No story save yet. Try New Game!");
                        return;
                    }
                    LevelEnum level = StorySave.ContinueLevel;
                    LeaveHub();
                    GameManager.PlayLevel(level); // same as the menu's Continue, but always the real story level
                    break;

                case HubMachine.Endless:
                    EndlessSetupMenu.Open(); // pick settings first; its PLAY calls StartEndless()
                    break;

                case HubMachine.Papas:
                    PapaSetupMenu.Open(); // pick the party look first; its PLAY calls StartPapas()
                    break;

                case HubMachine.EndlessShop:
                    ShopPanel.OpenEndless();
                    break;

                case HubMachine.PapaShop:
                    ShopPanel.OpenParty();
                    break;

                case HubMachine.VersusShop:
                    ShopPanel.OpenVersus();
                    break;

                case HubMachine.Options:
                    OptionsMenu.Open();
                    break;

                case HubMachine.Kaleidoscope:
                    // Same as the old menu's Memories button, which only showed up after beating the story.
                    if (!GD.WaydriveContentUnlocked())
                    {
                        HubMode.SetHudText("Beat the story to unlock the Kaleidoscope!");
                        return;
                    }
                    LeaveHub();
                    GameManager.PlayLevel(LevelEnum._Memory_01_Christmas);
                    break;

                case HubMachine.Versus:
                    VersusMenu.Open();
                    break;

                case HubMachine.LevelSelect:
                    if (!GD.HasCompletedGame())
                    {
                        HubMode.SetHudText("Beat the story to unlock Level Select!");
                        return;
                    }
                    LevelSelectMenu.Open();
                    break;

                case HubMachine.Gumball:
                    Gumball();
                    break;

                case HubMachine.Wardrobe:
                    OutfitMenu.Open();
                    break;

                case HubMachine.SummerVacation:
                    LeaveHub();
                    BeachMode.Launch();
                    break;

                case HubMachine.NightShift:
                    LeaveHub();
                    FactoryMode.Launch();
                    break;

                case HubMachine.Pinball:
                    if (PinballGame.Instance != null) PinballGame.Instance.Enter();
                    break;

                case HubMachine.Recalled:
                    RcDoorMenu.Open(); // alone, with friends, or with whoever's out there
                    break;

                case HubMachine.Mirror:
                    OutfitMenu.Open(); // "Remember who you are"
                    break;

                case HubMachine.AirHockey:
                    if (AirHockeyGame.Instance != null) AirHockeyGame.Instance.Enter();
                    break;

                case HubMachine.Quit:
                    PlayerPrefs.Save();
                    Application.Quit();
                    break;
            }
        }

        private static void Gumball()
        {
            GadgetType got;
            switch (Gadgets.BuyGumball(out got))
            {
                case Gadgets.GumballResult.HaveThemAll:
                    HubMode.SetHudText("You already have every gadget! More are coming soon.");
                    return;
                case Gadgets.GumballResult.NotEnoughShards:
                    HubMode.SetHudText("A gumball costs " + Gadgets.GUMBALL_PRICE + " shards. You have " + Shards.Balance + ".");
                    return;
            }
            DropGumball(Gadgets.GumballColor(got), Gadgets.IsDlc(got));
            HubMode.SetHudText("You got the " + Gadgets.Name(got).ToUpper() + "!");
            GumballReveal.ShowAfterDrop(got);
        }

        /// <summary>A gumball in the gadget's color pops out of the chute and rolls away.</summary>
        private static void DropGumball(Color color, bool rainbow)
        {
            if (Camera.main != null) AudioController.Play("vhs_glitch", Camera.main.transform);
            Transform chute = HubLayout.GumballChute;
            if (chute == null) return;
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.transform.position = chute.position;
            ball.transform.localScale = Vector3.one * 0.07f;
            ball.GetComponent<Renderer>().material = Mat.Lit(color, 0.85f);
            if (rainbow) ball.AddComponent<RainbowCycler>();
            var body = ball.AddComponent<Rigidbody>();
            body.mass = 0.05f;
            body.velocity = chute.forward * 1.2f + Vector3.up * 0.8f;
            Object.Destroy(ball, 6f);
        }

        /// <summary>A Level Select button: plays that story night.</summary>
        public static void PlayStoryLevel(LevelEnum level)
        {
            LeaveHub();
            GameManager.PlayLevel(level);
        }

        /// <summary>The Endless setup screen's PLAY button.</summary>
        public static void StartEndless()
        {
            LeaveHub();
            HubMode.ReturnToHub = true;
            EndlessSubmenu.LaunchEndless();
        }

        /// <summary>The Endless setup screen's PLAY button with the Waygetter Facility map (dev builds): no Mama, the marking tool on.</summary>
        public static void StartFacilityEndless()
        {
            LeaveHub();
            HubMode.ReturnToHub = true;
            EndlessSubmenu.LaunchEndless(true);
        }
        /// <summary>The Papa's setup screen's PLAY button.</summary>

        public static void StartPapas()
        {
            LeaveHub();
            HubMode.ReturnToHub = true;
            EndlessSubmenu.StartBirthdayMode(null);
        }

        /// <summary>Wipes the hub (it must never float above the house during a mode) and clears all mod launch state.</summary>
        private static void LeaveHub()
        {
            SceneMonitor.ResetForNewRun();
        }
    }

    // A menu is open: the machine behind it can't be used again (no hold, no reopening it on top of itself).
    [HarmonyPatch(typeof(Interactable), "OnInteract")]
    public static class Patch_Interactable_OnInteract_MenuOpen
    {
        static bool Prefix() { return !OverlayPanel.BlocksWorld && !PinballGame.BlocksWorld && !AirHockeyGame.BlocksWorld && !ArcadeAwakening.Running; } // (at the pinball machine, clicks are flippers)
    }

    [HarmonyPatch(typeof(Interactable), "OnInteractComplete")]
    public static class Patch_Interactable_OnInteractComplete_Hub
    {
        static void Postfix(Interactable __instance, bool fromAutoComplete)
        {
            if (fromAutoComplete || !HubMode.Active || __instance == null || OverlayPanel.BlocksWorld || PinballGame.BlocksWorld || AirHockeyGame.BlocksWorld) return;
            HubMachine m;
            if (HubLayout.TryGetMachine(__instance.gameObject, out m)) HubLaunch.Use(m);
        }
    }
}
