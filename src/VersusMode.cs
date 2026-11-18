using System.Collections.Generic;
using UnityEngine;

namespace KaleidoscopeEndless
{
    public enum VersusRole { None, Mama, Survivor }

    /// <summary>
    /// Versus (step 1: solo playtest). The VERSUS machine's screen picks a
    /// side; PLAYTEST fills the other side with a bot that does nothing.
    ///  - Survivor: a normal Endless run, but Mama's AI is replaced by an
    ///    idle Mama bot that just stands at a spawn spot (moved each objective).
    ///  - Mama: the level loads like the Arcade does; the player's body is
    ///    switched off and you look at Mama in third person from her spot,
    ///    with an idle "gray blob" survivor bot standing at the level start.
    /// Online play, Mama's abilities and the 10-objective rule come next.
    /// Hold Esc (the game's own) to leave; you land back in the Arcade.
    /// </summary>
    public static class VersusMode
    {
        public static VersusRole Role;
        public static bool OpponentIsBot;
        /// <summary>The map of this match: the Waygetter Facility instead of the house (picked on the Versus screen, the same for both players).</summary>
        public static bool Facility;
        /// <summary>Armed for the Mama side's level load, consumed by SceneMonitor.</summary>
        public static bool PendingMamaLoad;

        public static bool IsMama { get { return Role == VersusRole.Mama; } }
        public static bool IsSurvivor { get { return Role == VersusRole.Survivor; } }

        public static void Reset()
        {
            VersusNet.SendRoundOver(VersusNet.Over.Left); // no-op unless a match with a friend was still on
            Role = VersusRole.None;
            OpponentIsBot = false;
            Facility = false;
            PendingMamaLoad = false;
            VersusBots.Clear();
            VersusMamaController.DestroyInstance();
        }

        public static void StartSurvivor(bool botMama)
        {
            SceneMonitor.ResetForNewRun(); // leaves the hub (also clears any old Versus state)
            VersusNet.ResetMatch();
            Role = VersusRole.Survivor;
            OpponentIsBot = botMama;
            Facility = VersusLobby.FacilityMap; // (after the reset above, which clears it)
            HubMode.ReturnToHub = true;
            EndlessSubmenu.LaunchEndless(Facility); // takes the gadget picked on the Versus screen
            EndlessTitle.PendingLucky = false;
            EndlessTitle.PendingTitle = "Versus";
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Versus] Starting as the survivor" + (Facility ? " in the Waygetter Facility" : "") + (botMama ? " vs an idle Mama bot." : "."));
        }

        public static void StartMama(bool botSurvivor)
        {
            SceneMonitor.ResetForNewRun();
            VersusNet.ResetMatch();
            Role = VersusRole.Mama;
            OpponentIsBot = botSurvivor;
            Facility = VersusLobby.FacilityMap;
            PendingMamaLoad = true;
            HubMode.ReturnToHub = true;
            EndlessTitle.PendingLucky = false;
            EndlessTitle.PendingTitle = "Versus";
            EndlessRunManager.PendingEndlessRun = true; // "the mod owns this level load" (no story save, SceneMonitor routes it)
            if (Camera.main != null) AudioController.Play("vhs_cam_change", Camera.main.transform);
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Versus] Starting as Mama" + (Facility ? " in the Waygetter Facility" : "") + (botSurvivor ? " vs an idle survivor bot." : "."));
            if (Facility) { EndlessRunManager.PendingBirthdayMode = false; LoadingCover.Show(null); FactoryMode.Pending = true; FactoryMode.EndlessPreview = true; } // (the Facility is built like the Endless map; the hunter's screen is set up there)
            GameManager.PlayLevel(LevelEnum._6_TheParty);
        }
    }

    /// <summary>The PLAYTEST stand-ins, and the Mama body both sides use.</summary>
    /// <summary>
    /// Who hunts on the Mama side: Mama or Papa (both free), each in a skin from the VERSUS SHOP.
    /// The hunter's body goes to the child's game as one number (Code) in the pose packets:
    /// 1000 + (Papa ? 100 : 0) + skin, so the child sees exactly who's coming.
    /// </summary>
    public static class VersusHunters
    {
        public struct Skin
        {
            public string Name, Description;
            public Color Fur;
            public bool Tint, Kaleidoscope, Rainbow;
            public int Price;
        }

        private static Skin S(string name, int price, string desc, Color fur, bool tint = true, bool kal = false, bool rainbow = false)
        { return new Skin { Name = name, Price = price, Description = desc, Fur = fur, Tint = tint, Kaleidoscope = kal, Rainbow = rainbow }; }

        private static readonly Skin[] MamaSkins =
        {
            S("Classic", 0, "Mama as you know her.", Color.white, false),
            S("Kaleidoscope", 400, "VERSUS: Mama as her Kaleidoscope self.", Color.white, false, true),
            S("Golden", 600, "VERSUS: Mama with golden fur.", new Color(0.95f, 0.72f, 0.22f)),
            S("Shadow", 400, "VERSUS: Mama with pitch-black fur. Good luck spotting her.", new Color(0.09f, 0.09f, 0.11f)),
            S("Crimson", 400, "VERSUS: Mama with blood-red fur.", new Color(0.62f, 0.07f, 0.1f)),
        };

        private static readonly Skin[] PapaSkins =
        {
            S("Classic", 0, "Papa as you know him: white fur, top hat, monocle.", new Color(0.85f, 0.85f, 0.83f)),
            S("Golden", 600, "VERSUS: Papa with golden fur.", new Color(0.95f, 0.72f, 0.22f)),
            S("Midnight", 400, "VERSUS: Papa in deep midnight blue.", new Color(0.1f, 0.12f, 0.26f)),
            S("Crimson", 400, "VERSUS: Papa with blood-red fur.", new Color(0.62f, 0.07f, 0.1f)),
            S("Rainbow", 800, "VERSUS: Papa's fur cycles through every color.", Color.white, true, false, true),
        };

        public static Skin[] Skins(bool papa) { return papa ? PapaSkins : MamaSkins; }
        public static string Key(bool papa, int i) { return "Versus Skin " + (papa ? "Papa " : "Mama ") + Skins(papa)[i].Name; }
        public static bool Owned(bool papa, int i) { return i == 0 || Shop.IsOwned(Key(papa, i)); }

        private const string PREF_PAPA = "KE_VersusPlayPapa", PREF_SKIN = "KE_VersusSkin_";

        /// <summary>This player hunts as Papa (picked on the Versus screen).</summary>
        public static bool PlayAsPapa { get { return PlayerPrefs.GetInt(PREF_PAPA, 0) == 1; } }
        public static void SetPapa(bool papa) { PlayerPrefs.SetInt(PREF_PAPA, papa ? 1 : 0); PlayerPrefs.Save(); }

        public static int SkinFor(bool papa)
        {
            int i = PlayerPrefs.GetInt(PREF_SKIN + (papa ? "Papa" : "Mama"), 0);
            return i >= 0 && i < Skins(papa).Length && Owned(papa, i) ? i : 0;
        }

        /// <summary>Steps the current hunter's skin through the owned ones.</summary>
        public static void StepSkin(int dir)
        {
            bool papa = PlayAsPapa;
            int n = Skins(papa).Length, cur = SkinFor(papa);
            for (int step = 1; step <= n; step++)
            {
                int next = ((cur + dir * step) % n + n) % n;
                if (Owned(papa, next)) { PlayerPrefs.SetInt(PREF_SKIN + (papa ? "Papa" : "Mama"), next); PlayerPrefs.Save(); return; }
            }
        }

        public static string SkinName { get { return Skins(PlayAsPapa)[SkinFor(PlayAsPapa)].Name; } }

        public const int CODE_BASE = 1000;
        public static int LocalCode { get { return CODE_BASE + (PlayAsPapa ? 100 : 0) + SkinFor(PlayAsPapa); } }
        public static bool IsHunterCode(int code) { return code >= CODE_BASE; }
        public static bool IsPapaCode(int code) { return code - CODE_BASE >= 100; }

        /// <summary>The body code for a VERSUS SHOP skin item (for its preview).</summary>
        public static int CodeForKey(string key)
        {
            for (int papa = 0; papa < 2; papa++)
                for (int i = 0; i < Skins(papa == 1).Length; i++)
                    if (Key(papa == 1, i) == key) return CODE_BASE + papa * 100 + i;
            return CODE_BASE;
        }

        public static GameObject BuildBody(string name, int code)
        {
            bool papa = IsPapaCode(code);
            var skins = Skins(papa);
            int i = (code - CODE_BASE) % 100;
            Skin s = skins[i >= 0 && i < skins.Length ? i : 0];
            var body = VersusBots.BuildMamaBody(name, s.Kaleidoscope);
            if (body == null) return null;
            if (papa) BirthdayDecor.DressAsPapa(body, s.Fur, s.Rainbow);
            else if (s.Tint) BirthdayDecor.TintFur(body, s.Fur, s.Rainbow);
            return body;
        }
    }

    /// <summary>Where the hunter can stand and how the map is split in floors: the house's patrol nodes (house, basement) or the Waygetter Facility's own (ground, upstairs, basement).</summary>
    public static class VersusMap
    {
        private static bool Facility { get { return VersusMode.Facility; } }

        public static bool Ready
        {
            get
            {
                if (Facility) return FactoryController.Instance != null && FactoryController.Instance.Built && FacilityMama.Nodes != null && FacilityMama.Nodes.patrolNodes != null && FacilityMama.Nodes.patrolNodes.Count > 0;
                return RM.patrolNodeSet != null && RM.patrolNodeSet.patrolNodes != null;
            }
        }

        public static List<PatrolNode> Nodes()
        {
            if (Facility) return FacilityMama.Nodes != null ? FacilityMama.Nodes.patrolNodes : new List<PatrolNode>();
            return RM.patrolNodeSet != null ? RM.patrolNodeSet.patrolNodes : new List<PatrolNode>();
        }

        public static PatrolNode RandomNode()
        {
            var list = Nodes();
            if (list == null || list.Count == 0) return null;
            for (int tries = 0; tries < 8; tries++)
            {
                PatrolNode n = list[Random.Range(0, list.Count)];
                if (n != null) return n;
            }
            return null;
        }

        public static int FloorCount { get { return Facility ? 3 : 2; } }

        public static string FloorName(int i)
        {
            if (Facility) return i == 0 ? "GROUND FLOOR" : i == 1 ? "UPSTAIRS" : "BASEMENT";
            return i == 0 ? "HOUSE" : "BASEMENT";
        }

        public static int FloorOf(Vector3 p)
        {
            if (Facility)
            {
                float ly = p.y - FactoryController.Origin.y;
                return ly < -2f ? 2 : ly > 2f ? 1 : 0;
            }
            return p.y <= -0.2f ? 1 : 0;
        }

        public static List<PatrolNode> FloorNodes(int floor)
        {
            var list = new List<PatrolNode>();
            foreach (var n in Nodes()) if (n != null && FloorOf(n.transform.position) == floor) list.Add(n);
            return list;
        }
    }

    public static class VersusBots
    {
        private static GameObject _idleMama, _survivorBlob;

        /// <summary>A lifeless copy of the real Mama model (from the game's model library — her AI never wakes).</summary>
        public static GameObject BuildMamaBody(string name, bool kaleidoscope = false)
        {
            GameObject src = RM.modelManager == null ? null : (kaleidoscope && RM.modelManager.mamaModel_k != null ? RM.modelManager.mamaModel_k : RM.modelManager.mamaModel);
            if (src == null) { KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Versus] No Mama model in this level."); return null; }
            var go = Object.Instantiate(src);
            go.name = name;
            BirthdayDecor.MakeInert(go);
            go.SetActive(true);
            return go;
        }

        public static PatrolNode RandomNode() { return VersusMap.RandomNode(); }

        /// <summary>Survivor side's PLAYTEST Mama: stands at a random spawn spot, does nothing. Moved each objective.</summary>
        public static void PlaceIdleMama()
        {
            PatrolNode node = RandomNode();
            if (node == null) return;
            if (_idleMama == null) _idleMama = BuildMamaBody("KE_VersusIdleMama");
            if (_idleMama == null) return;
            _idleMama.transform.position = node.transform.position;
            _idleMama.transform.rotation = node.transform.rotation;
            _idleMama.SetActive(true);
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Versus] Idle Mama bot at " + node.transform.position + ".");
        }

        /// <summary>The idle Mama while she's out (for the Flash Camera), else null.</summary>
        public static GameObject ActiveIdleMama { get { return _idleMama != null && _idleMama.activeSelf ? _idleMama : null; } }

        /// <summary>Rest, phone break or Music Box: she's gone until the next activation.</summary>
        public static void HideIdleMama()
        {
            if (_idleMama != null) _idleMama.SetActive(false);
        }

        /// <summary>Toy Parrot: the (visible) idle Mama moves to the spawn spot nearest the parrot, same floor. False if she's away.</summary>
        public static bool MoveIdleMamaNear(Vector3 pos)
        {
            if (_idleMama == null || !_idleMama.activeSelf || !VersusMap.Ready) return false;
            int floor = VersusMap.FloorOf(pos);
            PatrolNode best = null;
            float bestDist = float.MaxValue;
            foreach (var n in VersusMap.Nodes())
            {
                if (n == null || VersusMap.FloorOf(n.transform.position) != floor) continue;
                float d = Vector3.Distance(n.transform.position, pos);
                if (d < bestDist) { bestDist = d; best = n; }
            }
            if (best == null) return false;
            _idleMama.transform.position = best.transform.position;
            _idleMama.transform.rotation = best.transform.rotation;
            return true;
        }

        /// <summary>Mama side's PLAYTEST survivor: a kid standing still.</summary>
        public static void PlaceSurvivorBlob(Vector3 pos)
        {
            if (_survivorBlob == null) _survivorBlob = ChildModel.Build("KE_VersusSurvivorBot");
            RaycastHit hit;
            float floorY = Physics.Raycast(pos + Vector3.up, Vector3.down, out hit, 4f, ~0, QueryTriggerInteraction.Ignore) ? hit.point.y : pos.y - 1f;
            _survivorBlob.transform.position = new Vector3(pos.x, floorY, pos.z);
        }

        /// <summary>The PLAYTEST survivor's body (null if there isn't one).</summary>
        public static GameObject SurvivorBot { get { return _survivorBlob; } }

        /// <summary>Where the PLAYTEST survivor stands (null if there isn't one).</summary>
        public static Vector3? SurvivorBotPosition { get { return _survivorBlob != null ? (Vector3?)_survivorBlob.transform.position : null; } }

        public static void Clear()
        {
            if (_idleMama != null) Object.Destroy(_idleMama);
            if (_survivorBlob != null) Object.Destroy(_survivorBlob);
            _idleMama = _survivorBlob = null;
        }
    }
}
