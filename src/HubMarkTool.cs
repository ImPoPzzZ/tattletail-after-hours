using UnityEngine;
using UnityEngine.UI;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// Placement tool for laying out the arcade and the Summer Vacation beach (dev builds).
    /// Positions are logged relative to the place you're in ([HUB-MARK] or [BEACH-MARK]).
    /// (Originally hub-only: the placement tool for laying out the arcade.) E opens a naming
    /// panel (player frozen, cursor free): type a name, tick Wall or Prop,
    /// press Enter. Wall: a straight cyan line on the floor from where you
    /// stood to wherever you are, following you live — E sets it. Prop: a
    /// pink dot on the floor in front of you follows you — E sets it.
    /// Everything placed stays visible with its name and is logged as
    /// [HUB-MARK] with room-relative coordinates.
    /// </summary>
    public class HubMarkTool : MonoBehaviour
    {
        private enum State { Idle, Naming, PlacingWall, PlacingProp }

        private static readonly Color WallColor = new Color(0.2f, 0.9f, 1f);
        private static readonly Color PropColor = new Color(1f, 0.3f, 0.8f);
        private const float CURSOR_AHEAD = 1.2f;
        private const int MAX_NAME = 24;

        private State _state = State.Idle;
        private bool _wallMode = true;
        private string _name = "";
        private int _count;
        private bool _skipTypingThisFrame;

        private Vector3 _wallStart;
        private string _placingName;
        private GameObject _live;       // the line/dot currently following the player
        private Transform _placedRoot;

        // UI
        private GameObject _canvasGO;
        private GameObject _panel;
        private Text _nameField;
        private Text _wallBox, _propBox, _cancelBox;
        private Text _placingHint, _presetHelp;
        private Font _font;

        // -- Shared marks: every player in the lobby sees them (only dev builds can place) --
        private class Mark { public int Id; public bool Wall; public string Name; public Vector3 A, B; public Transform Holder; }
        private readonly System.Collections.Generic.List<Mark> _mine = new System.Collections.Generic.List<Mark>();
        private int _nextId;
        private Transform _remoteRoot;
        private bool _together;
        private static HubMarkTool _instance;
        private const int OP_ADD = 1, OP_REMOVE = 2, OP_CLEAR = 3;
        private static readonly Color FriendWallColor = new Color(0.4f, 1f, 0.45f), FriendPropColor = new Color(1f, 0.6f, 0.15f);

        private void Start()
        {
            _instance = this;
            _placedRoot = new GameObject("KE_HubMarks").transform;
            _placedRoot.SetParent(transform, false);
            _remoteRoot = new GameObject("KE_FriendMarks").transform;
            _remoteRoot.SetParent(transform, false);
            BuildUI();
        }

        private void Update()
        {
            var fps = RM.fpsController;
            if (fps == null || !PlaceReady) return;

            // The friend just got here (or came back): send them every mark placed so far.
            Vector3 lp; float ly; bool lpr;
            int ctx = VersusNet.LocalPose(out lp, out ly, out lpr);
            bool together = VersusNet.Online && VersusNet.RemoteFresh && VersusNet.RemoteCtx == ctx;
            if (together && !_together) SendAll();
            _together = together;

            if (!KaleidoscopeEndlessPlugin.DevTools) return; // everyone sees marks; only dev builds place them
            if (OverlayPanel.AnyOpen) return; // typing/clicking in Options or a machine's setup screen

            switch (_state)
            {
                case State.Idle:
                    if (Time.timeScale <= 0f) break;
                    if (Input.GetKeyDown(KeyCode.E)) OpenNaming(fps);
                    else if (Input.GetKeyDown(KeyCode.Z)) UndoLast();
                    break;
                case State.Naming:
                    UpdateNaming(fps);
                    break;
                case State.PlacingWall:
                case State.PlacingProp:
                    UpdatePlacing(fps);
                    break;
            }
        }

        // ── Facility Endless: quick names for every item that needs spawn spots (press the digit in the naming panel) ──
        private static readonly string[] PresetNames = { "Vase", "Candle", "Friend", "Tape", "WashingMachine", "Speaker", "FuseBox", "Coin", "Charger", "Phone" };
        private static readonly string[] PresetHelp =
        {
            "1 Vase (6+)", "2 Candle (6+)", "3 Friend (6+)", "4 Tape (6+)", "5 WashingMachine (3+)",
            "6 Speaker (3+)", "7 FuseBox (3+, on a wall)", "8 Coin (10+)", "9 Charger (2+)", "0 Phone (2+)",
        };

        private static string Preset(char digit)
        {
            int i = digit == '0' ? 9 : digit - '1';
            return i >= 0 && i < PresetNames.Length ? PresetNames[i] : null;
        }

        /// <summary>The marks of one item so far, so the next one is "Vase 4".</summary>
        private int CountOf(string baseName)
        {
            int n = 0;
            foreach (var m in _mine) if (m.Name == baseName || m.Name.StartsWith(baseName + " ")) n++;
            return n;
        }

        private static string MarksFile { get { return System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "facility_marks.txt"); } }

        /// <summary>Facility marks are also written to BepInEx\facility_marks.txt, so they can be sent as one file.</summary>
        private static void FileLog(string line)
        {
            if (!AtFactory) return;
            try { System.IO.File.AppendAllText(MarksFile, line + "\n"); } catch { }
        }

        // ── Naming panel ─────────────────────────────────────────────────────
        private void OpenNaming(FirstPersonController fps)
        {
            _state = State.Naming;
            _name = "";
            _skipTypingThisFrame = true; // this frame's input already contains the "e" that opened it

            fps.enabled = false;         // no moving or looking around
            GS.acceptInput = false;      // and no game interactions either
            _panel.SetActive(true);
            RefreshPanel();
        }

        private void CloseNaming(FirstPersonController fps)
        {
            _panel.SetActive(false);
            fps.enabled = true;
            GS.acceptInput = true;
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        private void UpdateNaming(FirstPersonController fps)
        {
            // Other game code may try to re-lock the cursor — keep it free while the panel is up.
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            if (Input.GetMouseButtonDown(0))
            {
                if (Hit(_wallBox)) _wallMode = true;
                else if (Hit(_propBox)) _wallMode = false;
                else if (Hit(_cancelBox)) { CloseNaming(fps); _state = State.Idle; return; }
            }

            if (_skipTypingThisFrame) { _skipTypingThisFrame = false; RefreshPanel(); return; }

            foreach (char c in Input.inputString)
            {
                if (c == '\b') { if (_name.Length > 0) _name = _name.Substring(0, _name.Length - 1); }
                else if (c == '\n' || c == '\r') { ConfirmName(fps); return; }
                else if (AtFactory && _name.Length == 0 && Preset(c) != null) _name = Preset(c);
                else if (!char.IsControl(c) && _name.Length < MAX_NAME) _name += c;
            }
            RefreshPanel();
        }

        private void ConfirmName(FirstPersonController fps)
        {
            _count++;
            _placingName = string.IsNullOrEmpty(_name.Trim()) ? "Mark " + _count : _name.Trim();
            if (AtFactory) foreach (string pn in PresetNames) if (_placingName == pn) { _placingName = pn + " " + (CountOf(pn) + 1); break; }
            CloseNaming(fps);

            _live = _wallMode ? MakeLine(WallColor) : MakeDot(PropColor);
            if (_wallMode) _wallStart = FloorCursor(fps);
            _state = _wallMode ? State.PlacingWall : State.PlacingProp;
            _placingHint.text = (_wallMode ? "Placing WALL" : "Placing PROP") + " '" + _placingName + "' — press E to set it down";
            _placingHint.gameObject.SetActive(true);
        }

        private void RefreshPanel()
        {
            bool caretOn = (Time.unscaledTime % 1f) < 0.5f;
            _nameField.text = _name + (caretOn ? "|" : " ");
            _presetHelp.gameObject.SetActive(AtFactory);
            _wallBox.text = (_wallMode ? "[X]" : "[  ]") + " Wall";
            _propBox.text = (_wallMode ? "[  ]" : "[X]") + " Prop";
            _wallBox.color = _wallMode ? WallColor : new Color(0.7f, 0.7f, 0.7f);
            _propBox.color = _wallMode ? new Color(0.7f, 0.7f, 0.7f) : PropColor;
        }

        private static bool Hit(Text t)
        {
            return RectTransformUtility.RectangleContainsScreenPoint(t.rectTransform, Input.mousePosition, null);
        }

        // ── Placing ──────────────────────────────────────────────────────────
        private void UpdatePlacing(FirstPersonController fps)
        {
            Vector3 cursor = FloorCursor(fps);
            if (_state == State.PlacingWall) SetLine(_live, _wallStart, cursor);
            else _live.transform.position = cursor;

            if (Time.timeScale <= 0f) return;
            if (Input.GetKeyDown(KeyCode.Z))
            {
                // Z mid-placement cancels the one you're dragging.
                Destroy(_live);
                _live = null;
                _state = State.Idle;
                _placingHint.gameObject.SetActive(false);
                HubMode.SetHudText("'" + _placingName + "' cancelled");
                return;
            }
            if (!Input.GetKeyDown(KeyCode.E)) return;

            // Each placed mark gets an unscaled holder, so its name label isn't squashed by the flat line/dot.
            var holder = new GameObject(_placingName).transform;
            holder.SetParent(_placedRoot, false);
            _live.transform.SetParent(holder, true);

            Vector3 origin = PlaceOrigin;
            var mark = new Mark { Id = ++_nextId, Wall = _state == State.PlacingWall, Name = _placingName, Holder = holder };
            mark.A = (mark.Wall ? _wallStart : cursor) - origin;
            mark.B = cursor - origin;
            _mine.Add(mark);
            Send(OP_ADD, mark);
            if (_state == State.PlacingWall)
            {
                Vector3 a = _wallStart - origin, b = cursor - origin;
                KaleidoscopeEndlessPlugin.Log.LogInfo(string.Format("KE: [" + Tag + "] WALL '{0}' from local ({1:F2}, {2:F2}) to ({3:F2}, {4:F2}) length {5:F2}m",
                    _placingName, a.x, a.z, b.x, b.z, Vector3.Distance(a, b)));
                FileLog(string.Format("WALL '{0}' from local ({1:F2}, {2:F2}) to ({3:F2}, {4:F2})", _placingName, a.x, a.z, b.x, b.z));
                AddLabel(holder, (_wallStart + cursor) * 0.5f, WallColor, _placingName);
            }
            else
            {
                Vector3 p = cursor - origin;
                // Whatever gets built here should face where you stood when you set it.
                Vector3 toPlayer = fps.transform.position - cursor; toPlayer.y = 0f;
                float yaw = toPlayer.sqrMagnitude > 0.001f ? Quaternion.LookRotation(toPlayer).eulerAngles.y : 0f;
                string propLine = string.Format("PROP '{0}' at local ({1:F2}, {2:F2}) height {4:F2} facing {3:F0}",
                    _placingName, p.x, p.z, yaw, p.y);
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [" + Tag + "] " + propLine);
                FileLog(propLine);
                AddLabel(holder, cursor, PropColor, _placingName);
            }

            _live = null;
            _state = State.Idle;
            _placingHint.gameObject.SetActive(false);
            HubMode.SetHudText("'" + _placingName + "' placed");
        }

        /// <summary>Z: removes the most recently placed wall or prop.</summary>
        private void UndoLast()
        {
            if (_mine.Count == 0) { HubMode.SetHudText("Nothing to undo"); return; }
            var mark = _mine[_mine.Count - 1];
            _mine.RemoveAt(_mine.Count - 1);
            Send(OP_REMOVE, mark);
            Transform last = mark.Holder;
            if (last == null) return;
            FileLog("REMOVED '" + last.name + "' (ignore its earlier line)");
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [" + Tag + "] REMOVED '" + last.name + "' (undo — ignore its earlier line)");
            HubMode.SetHudText("'" + last.name + "' removed");
            Destroy(last.gameObject);
            last.SetParent(null); // so a quick second Z sees the next one, not this one mid-destroy
        }

        // Where marks are being placed: the arcade, or the beach.
        private static bool AtBeach { get { return BeachMode.Active && BeachController.Instance != null && BeachController.Instance.Built; } }
        private static bool AtFactory { get { return FactoryMode.Active && FactoryController.Instance != null && FactoryController.Instance.Built; } }
        private static bool PlaceReady { get { return AtBeach || AtFactory || (HubController.Instance != null && HubController.Instance.Built); } }
        private static Vector3 PlaceOrigin { get { return AtBeach ? BeachController.Origin : AtFactory ? FactoryController.Origin : HubController.Origin; } }
        private static string Tag { get { return AtBeach ? "BEACH-MARK" : AtFactory ? "FACTORY-MARK" : "HUB-MARK"; } }

        /// <summary>A point on the floor (or the sand) just in front of the player, kept inside the room (or the cove).</summary>
        private static Vector3 FloorCursor(FirstPersonController fps)
        {
            Vector3 fwd = fps.transform.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
            fwd.Normalize();
            Vector3 p = fps.transform.position + fwd * CURSOR_AHEAD;
            Vector3 o = PlaceOrigin;
            if (AtBeach)
            {
                float lx = Mathf.Clamp(p.x - o.x, -BeachController.WALK_X, BeachController.WALK_X);
                float lz = Mathf.Clamp(p.z - o.z, BeachController.WALK_BACK, BeachController.WALL_Z);
                return new Vector3(o.x + lx, o.y + BeachController.Height(lx, lz) + 0.04f, o.z + lz); // on the sand
            }
            if (AtFactory) // inside the building (local x 0..60, z 0..40)
            {
                float cx = o.x + Mathf.Clamp(p.x - o.x, 0.1f, FactoryLayout.W - 0.1f), cz = o.z + Mathf.Clamp(p.z - o.z, 0.1f, FactoryLayout.D - 0.1f);
                float fy = fps.transform.position.y + 0.015f; // the floor you stand on (upstairs, the basement) unless a floor is found right below
                RaycastHit fh;
                if (Physics.Raycast(new Vector3(cx, fps.transform.position.y + 1.0f, cz), Vector3.down, out fh, 3f, ~0, QueryTriggerInteraction.Ignore)) fy = fh.point.y + 0.015f;
                return new Vector3(cx, fy, cz);
            }
            float hx = HubController.WIDTH / 2f - 0.1f, hz = HubController.DEPTH / 2f - 0.1f;
            return new Vector3(Mathf.Clamp(p.x, o.x - hx, o.x + hx), o.y + 0.015f, Mathf.Clamp(p.z, o.z - hz, o.z + hz));
        }

        // ── Visuals ──────────────────────────────────────────────────────────
        private GameObject MakeLine(Color c)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "HubWallMark";
            Destroy(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().material = new Material(Shader.Find("Unlit/Color")) { color = c };
            go.transform.SetParent(transform, false);
            go.SetActive(false);
            return go;
        }

        private static void SetLine(GameObject line, Vector3 a, Vector3 b)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 0.02f) { line.SetActive(false); return; }
            line.SetActive(true);
            line.transform.position = a + d * 0.5f;
            line.transform.rotation = Quaternion.LookRotation(d);
            line.transform.localScale = new Vector3(0.08f, 0.01f, len);
        }

        private GameObject MakeDot(Color c)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "HubPropMark";
            Destroy(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().material = new Material(Shader.Find("Unlit/Color")) { color = c };
            go.transform.SetParent(transform, false);
            go.transform.localScale = new Vector3(0.3f, 0.005f, 0.3f);
            return go;
        }

        private void AddLabel(Transform holder, Vector3 at, Color c, string name)
        {
            // World-space UI text: hidden behind walls like normal objects (TextMesh showed through them).
            var t = WorldText.Create(holder, name, c, 0.2f, TextAnchor.MiddleCenter, 3f);
            t.canvas.transform.position = at + Vector3.up * 0.6f;
            t.canvas.gameObject.AddComponent<PartyLabelBillboard>(); // keeps it turned toward you
        }

        // ── UI ───────────────────────────────────────────────────────────────
        private void BuildUI()
        {
            _font = MainMenuButtonInjector.RealFont != null ? MainMenuButtonInjector.RealFont : Resources.GetBuiltinResource<Font>("Arial.ttf");

            _canvasGO = new GameObject("KE_HubMarkCanvas");
            _canvasGO.transform.SetParent(transform, false);
            var canvas = _canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 2000;
            var scaler = _canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            _panel = new GameObject("NamePanel", typeof(RectTransform), typeof(Image));
            _panel.transform.SetParent(_canvasGO.transform, false);
            var prt = (RectTransform)_panel.transform;
            prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one; prt.offsetMin = prt.offsetMax = Vector2.zero;
            var dim = _panel.GetComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.65f);
            dim.raycastTarget = false;

            MakeLabel(_panel.transform, "What do you wanna name this Mark?", 40, new Vector2(0f, 150f), new Vector2(1200f, 60f), Color.white);

            var box = new GameObject("InputBox", typeof(RectTransform), typeof(Image));
            box.transform.SetParent(_panel.transform, false);
            var brt = (RectTransform)box.transform;
            brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0.5f, 0.5f);
            brt.anchoredPosition = new Vector2(0f, 60f);
            brt.sizeDelta = new Vector2(700f, 70f);
            var bimg = box.GetComponent<Image>();
            bimg.color = new Color(0.1f, 0.1f, 0.12f, 0.95f);
            bimg.raycastTarget = false;
            _nameField = MakeLabel(box.transform, "", 36, Vector2.zero, new Vector2(680f, 70f), new Color(1f, 0.95f, 0.75f));

            _wallBox = MakeLabel(_panel.transform, "", 34, new Vector2(-170f, -40f), new Vector2(260f, 60f), WallColor);
            _propBox = MakeLabel(_panel.transform, "", 34, new Vector2(170f, -40f), new Vector2(260f, 60f), PropColor);
            _cancelBox = MakeLabel(_panel.transform, "Cancel", 28, new Vector2(0f, -130f), new Vector2(200f, 50f), new Color(0.8f, 0.8f, 0.8f));
            MakeLabel(_panel.transform, "Type a name, click Wall or Prop, then press Enter", 24, new Vector2(0f, -200f), new Vector2(1200f, 40f), new Color(0.75f, 0.75f, 0.75f));
            _presetHelp = MakeLabel(_panel.transform, "In the Facility: press a digit on an empty name for the item (numbered for you)\n" +
                string.Join("     ", PresetHelp, 0, 5) + "\n" + string.Join("     ", PresetHelp, 5, 5), 22, new Vector2(0f, -290f), new Vector2(1500f, 110f), new Color(0.6f, 0.85f, 1f));
            _presetHelp.alignment = TextAnchor.UpperCenter;
            _panel.SetActive(false);

            _placingHint = MakeLabel(_canvasGO.transform, "", 30, new Vector2(0f, 420f), new Vector2(1400f, 50f), new Color(1f, 0.95f, 0.7f));
            _placingHint.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);
            _placingHint.gameObject.SetActive(false);
        }

        private Text MakeLabel(Transform parent, string text, int size, Vector2 pos, Vector2 sizeDelta, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = sizeDelta;
            var t = go.GetComponent<Text>();
            t.font = _font;
            t.fontSize = size;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.text = text;
            return t;
        }

        // -- Network --
        private static int PlaceCtx { get { return AtBeach ? VersusNet.CTX_BEACH : AtFactory ? VersusNet.CTX_NONE : VersusNet.CTX_ARCADE; } }

        private static void Send(int op, Mark m)
        {
            if (!VersusNet.Online || MpLobby.Instance == null) return;
            using (var p = new MpPacket(MpNetworking.PacketId.Marks))
            {
                p.Write(op); p.Write(PlaceCtx);
                p.Write(m != null ? m.Id : 0);
                p.Write(m != null ? m.Name : "");
                p.Write(m != null && m.Wall);
                p.Write(m != null ? m.A.x : 0f); p.Write(m != null ? m.A.z : 0f);
                p.Write(m != null ? m.B.x : 0f); p.Write(m != null ? m.B.z : 0f);
                MpNetworking.SendReliable(MpLobby.Instance.PeerSteamID, p);
            }
        }

        private void SendAll()
        {
            Send(OP_CLEAR, null);
            foreach (var m in _mine) Send(OP_ADD, m);
        }

        /// <summary>The friend placed, removed or re-sent their marks.</summary>
        public static void OnPacket(MpPacket p)
        {
            int op = p.ReadInt(), ctx = p.ReadInt(), id = p.ReadInt();
            string name = p.ReadString();
            bool wall = p.ReadBool();
            var a = new Vector3(p.ReadFloat(), 0f, p.ReadFloat());
            var b = new Vector3(p.ReadFloat(), 0f, p.ReadFloat());
            if (_instance == null || !PlaceReady || ctx != PlaceCtx) return; // not in the same place
            _instance.ApplyFriend(op, id, name, wall, a, b);
        }

        private void ApplyFriend(int op, int id, string name, bool wall, Vector3 a, Vector3 b)
        {
            if (op == OP_CLEAR)
            {
                for (int i = _remoteRoot.childCount - 1; i >= 0; i--) Destroy(_remoteRoot.GetChild(i).gameObject);
                return;
            }
            string key = "Friend " + id;
            var old = _remoteRoot.Find(key);
            if (old != null) { Destroy(old.gameObject); old.SetParent(null); }
            if (op != OP_ADD) return;
            // Logged here too, so one log has everyone's marks.
            KaleidoscopeEndlessPlugin.Log.LogInfo(string.Format(wall
                ? "KE: [" + Tag + "] FRIEND WALL '{0}' from local ({1:F2}, {2:F2}) to ({3:F2}, {4:F2})"
                : "KE: [" + Tag + "] FRIEND PROP '{0}' at local ({3:F2}, {4:F2})", name, a.x, a.z, b.x, b.z));

            var holder = new GameObject(key).transform;
            holder.SetParent(_remoteRoot, false);
            Vector3 wa = OnGround(a), wb = OnGround(b);
            if (wall)
            {
                var line = MakeLine(FriendWallColor);
                line.transform.SetParent(holder, true);
                SetLine(line, wa, wb);
                AddLabel(holder, (wa + wb) * 0.5f, FriendWallColor, name);
            }
            else
            {
                var dot = MakeDot(FriendPropColor);
                dot.transform.SetParent(holder, true);
                dot.transform.position = wb;
                AddLabel(holder, wb, FriendPropColor, name);
            }
        }

        /// <summary>A place-local (x, z) as a world point on the floor or the sand.</summary>
        private static Vector3 OnGround(Vector3 local)
        {
            Vector3 o = PlaceOrigin;
            float y = AtBeach ? BeachController.Height(local.x, local.z) + 0.04f : 0.015f;
            return new Vector3(o.x + local.x, o.y + y, o.z + local.z);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            // Leaving the hub mid-naming must never leave the player frozen.
            if (_state == State.Naming && RM.fpsController != null)
            {
                RM.fpsController.enabled = true;
                GS.acceptInput = true;
            }
        }
    }
}
