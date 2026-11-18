using UnityEngine;
using UnityEngine.UI;

namespace KaleidoscopeEndless
{
    /// <summary>The scenes at the end of the first world, as the IDs of the lines in the order they're said (RcVoiceData has the words).</summary>
    public static class RcScenes
    {
        /// <summary>Moss plays the Tape: Waygetter's message, then a voice breaking in, then the kids.</summary>
        public static readonly string[] Call =
        {
            "TAPE-WG-1", "TAPE-WG-2", "TAPE-WG-3", "TAPE-WG-4", "TAPE-WG-5", "TAPE-WG-6",
            "CALL-NN-1", "CALL-NN-2", "CALL-NN-3", "CALL-NN-4", "CALL-SFX",
            "CALL-THE-1", "CALL-JUN-1", "CALL-MOS-1", "CALL-THE-2", "CALL-MOS-2", "CALL-JUN-2", "CALL-THE-3", "CALL-JUN-3",
        };

        /// <summary>The kids talk over each other until you get a word in.</summary>
        public static readonly string[] Argue = { "ARG-THE-1" };

        /// <summary>...and what they say once you've asked.</summary>
        public static readonly string[] ArgueAfter =
        {
            "ARG-ASK", "ARG-JUN-1", "ARG-MOS-1", "ARG-THE-2", "ARG-JUN-2", "ARG-THE-3", "ARG-JUN-3", "ARG-JUN-4", "ARG-MOS-2", "ARG-THE-4",
        };

        /// <summary>At the station.</summary>
        public static readonly string[] Goodbyes = { "JUN-STN-1", "THE-STN-1", "MOS-STN-1", "JUN-STN-2", "THE-STN-2", "MOS-STN-2" };
    }

    /// <summary>One of Waygetter's notices, on a paper card; the list on the left is every one in the town, found or not.</summary>
    public class RcNoticeMenu : RcMenu
    {
        private static RcNoticeMenu _instance;
        private readonly Text[] _rows = new Text[RcNotices.COUNT];
        private Text _zone, _title, _body, _note, _count;
        private int _sel, _want = -1;

        private static readonly Color Paper = new Color(0.93f, 0.89f, 0.76f), Ink2 = new Color(0.14f, 0.11f, 0.08f), Brown = new Color(0.42f, 0.28f, 0.16f), Stamp = new Color(0.55f, 0.12f, 0.1f);

        protected override string Title { get { return "RECALL NOTICES"; } }
        protected override Vector2 PanelSize { get { return new Vector2(1560f, 880f); } }

        /// <summary>Opens the list; with a number, on that notice (the one you've just read).</summary>
        public static void Open(int index)
        {
            RcNoticeMenu m = GetOrCreate(ref _instance, "KE_RcNoticeMenu");
            m._want = index;
            m.Show();
        }

        protected override void OnShown()
        {
            base.OnShown();
            _sel = -1;
            if (Rc.Char != null)
            {
                if (_want >= 0 && Rc.Char.HasNotice(_want)) _sel = _want;
                else for (int i = 0; i < RcNotices.COUNT && _sel < 0; i++) if (Rc.Char.HasNotice(i)) _sel = i;
            }
            _want = -1;
        }

        protected override void BuildContent(Transform p)
        {
            for (int i = 0; i < RcNotices.COUNT; i++)
            {
                int index = i;
                _rows[i] = AddButton(p, new Vector2(-500f, 300f - i * 46f), "", () => { _sel = index; }, 560f);
                _rows[i].alignment = TextAnchor.MiddleLeft;
                _rows[i].fontSize = 22; _rows[i].resizeTextMaxSize = 22;
            }
            _count = MakeText(p, "", 24, TextAnchor.MiddleLeft, new Vector2(-500f, -395f), new Vector2(560f, 40f), Dim);
            Vector2 c = new Vector2(300f, -10f);
            MakeImage(p, c, new Vector2(900f, 720f), new Color(0f, 0f, 0f, 0.5f));
            MakeImage(p, c, new Vector2(880f, 700f), Paper);
            Rule(p, c, new Vector2(850f, 670f), 2f, new Color(Brown.r, Brown.g, Brown.b, 0.5f));
            _zone = MakeText(p, "", 22, TextAnchor.MiddleCenter, c + new Vector2(0f, 285f), new Vector2(800f, 34f), Brown);
            _title = Fit(MakeText(p, "", 38, TextAnchor.MiddleCenter, c + new Vector2(0f, 215f), new Vector2(800f, 100f), Stamp));
            _body = Fit(MakeText(p, "", 32, TextAnchor.UpperLeft, c + new Vector2(0f, 20f), new Vector2(780f, 290f), Ink2));
            _body.lineSpacing = 1.15f;
            _note = MakeText(p, "", 26, TextAnchor.MiddleCenter, c + new Vector2(0f, -200f), new Vector2(780f, 40f), Brown);
            AddButton(p, new Vector2(300f, -395f), "CLOSE", Hide, 240f);
        }

        protected override void RefreshAll()
        {
            base.RefreshAll();
            if (_title == null || Rc.Char == null) return;
            RcCharacter c = Rc.Char;
            for (int i = 0; i < RcNotices.COUNT; i++)
            {
                bool has = c.HasNotice(i);
                _rows[i].text = has ? RcNotices.All[i].Title : "???   (" + RcData.ZoneNames[RcNotices.All[i].Zone] + ")";
                _rows[i].color = i == _sel ? Accent : has ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            }
            _count.text = "Found " + c.NoticeCount + " of " + RcNotices.COUNT + ". They're papers lying on top of scrap piles.";
            if (_sel >= 0 && _sel < RcNotices.COUNT && c.HasNotice(_sel))
            {
                RcNotice n = RcNotices.All[_sel];
                _zone.text = RcData.ZoneNames[n.Zone].ToUpper();
                _title.text = n.Title;
                _body.text = n.Text;
                _note.text = n.Note;
            }
            else
            {
                _zone.text = "";
                _title.text = "";
                _body.text = c.NoticeCount == 0 ? "Nothing yet.\n\nWaygetter left its papers all over town: on desks, in folders, on top of scrap piles. When you see a sheet of paper lying on one, press E to read it." : "Pick one on the left.";
                _note.text = "";
            }
        }
    }

    /// <summary>The departure board at the junkyard station: where the Recall Express goes, and the way on to the next world.</summary>
    public class RcBoardMenu : RcMenu
    {
        private static RcBoardMenu _instance;
        private const int ROWS = 7;
        private readonly Text[] _name = new Text[ROWS], _where = new Text[ROWS], _status = new Text[ROWS];
        private Text _foot, _board;
        private bool _boarding, _boarded;
        private Text _ride;

        private static readonly string[] Towns = { "Harbor", "Lodge", "Fairground", "Hollow", "???" };

        protected override string Title { get { return "THE RECALL EXPRESS"; } }
        protected override Vector2 PanelSize { get { return new Vector2(1400f, 680f); } }
        protected override Vector2 PanelOffset { get { return new Vector2(0f, 90f); } }

        public static void Open() { GetOrCreate(ref _instance, "KE_RcBoardMenu").Show(); }

        protected override void OnShown()
        {
            base.OnShown();
            _boarding = _boarded = _closed = false;
            Talk.Say("", "A freight train stands on the rails, engine ticking. The board over the platform lists the stations on the Line.");
            if (RcLobby.Exists && Rc.Char != null) Talk.Add("", RcLobby.IsHost ? "Whoever is with you rides with you." : "Only the one who runs the town can call the train: ask them. The whole party rides together.");
        }

        protected override void OnHidden() { Rc.Save(); }

        protected override void BuildContent(Transform p)
        {
            AddTalk();
            Inset(p, new Vector2(0f, -5f), new Vector2(1290f, 400f));
            MakeText(p, "STATION", 22, TextAnchor.MiddleLeft, new Vector2(-520f, 225f), new Vector2(420f, 30f), Dim);
            MakeText(p, "TOWN", 22, TextAnchor.MiddleLeft, new Vector2(-70f, 225f), new Vector2(420f, 30f), Dim);
            MakeText(p, "STATUS", 22, TextAnchor.MiddleLeft, new Vector2(300f, 225f), new Vector2(300f, 30f), Dim);
            for (int i = 0; i < ROWS; i++)
            {
                float y = 165f - i * 52f;
                _name[i] = MakeText(p, "", 32, TextAnchor.MiddleLeft, new Vector2(-300f, y), new Vector2(520f, 46f), Color.white);
                _where[i] = MakeText(p, "", 30, TextAnchor.MiddleLeft, new Vector2(110f, y), new Vector2(470f, 46f), Color.white);
                _status[i] = MakeText(p, "", 28, TextAnchor.MiddleLeft, new Vector2(500f, y), new Vector2(340f, 46f), Color.white);
            }
            _foot = Fit(MakeText(p, "", 24, TextAnchor.MiddleCenter, new Vector2(0f, -240f), new Vector2(1250f, 36f), Dim));
            _board = AddButton(p, new Vector2(-330f, -295f), "BOARD THE TRAIN", Board, 460f);
            _ride = AddButton(p, new Vector2(110f, -295f), "RIDE ON", RideOn, 340f);
            AddButton(p, new Vector2(470f, -295f), "CLOSE", Hide, 200f);
        }

        protected override void Update()
        {
            base.Update();
            if (_boarding && !_boarded && Talk != null && Talk.Done)
            {
                _boarded = true;
                if (Rc.Char == null) return;
                if (_ridingOn || _closed) { Rc.Char.OnBoarded(); Rc.Save(); return; }
                if (!RcWorlds.Egypt && Rc.Char.ActiveQuest == TownTrain)
                {
                    // The job "The Recall Express" is done by boarding: paid here, so the next job (Egypt's first) is the one you carry over.
                    Rc.Char.OnBoarded();
                    int xp = Rc.Char.TurnIn();
                    if (xp > 0) Rc.Player.AddXp(xp);
                }
                int to = RcWorlds.Egypt ? RcWorlds.TOWN : RcWorlds.EGYPT;
                Hide();
                if (RcNet.IsHost) RcNet.SendTravel(to);
                RecalledMode.Travel(to);
            }
        }

        private const int TownTrain = 24;   // (the town's last job)
        private bool _ridingOn, _closed;

        // In the town you can board once the job "The Recall Express" is the one you're on (or done); from Egypt you can always ride home.
        private bool Ready { get { return Rc.Char != null && (RcWorlds.Egypt || Rc.Char.ActiveQuest >= TownTrain); } }
        private bool Allowed { get { return !RcLobby.Exists || RcLobby.IsHost; } }

        private void Board()
        {
            if (!Ready || _boarding) return;
            if (!Allowed) { Talk.Say("Tip", "Only the one who runs the town can call the train: the whole party rides together."); return; }
            if (!RcWorlds.Egypt && Rc.Char.ActiveQuest == TownTrain)
            {
                RcQuest q = Rc.Char.CurrentQuest;
                if (Rc.Char.Backpack.Count + q.Reward.Length > Rc.Char.BackpackSlots) { Talk.Say("Tip", "Make room in your backpack first: there's a reward to carry."); return; }
            }
            _boarding = true; _ridingOn = false;
            Talk.Clear();
            if (!RcWorlds.Egypt && !RcWorlds.EgyptOpen)
            {
                // Last Stop isn't open yet: the job is still done by standing at the train, and that's as far as it goes.
                _closed = true;
                if (Rc.Char.ActiveQuest == TownTrain)
                    for (int i = 0; i < RcScenes.Goodbyes.Length; i++) { RcSay s = RcVoiceData.Line(RcScenes.Goodbyes[i]); Talk.Add(s.Who, s.Text); }
                Talk.Add("", "The engine's warm, but Last Stop is under development. The line opens in a later update.");
                return;
            }
            if (!RcWorlds.Egypt && Rc.Char.ActiveQuest == TownTrain)
            {
                for (int i = 0; i < RcScenes.Goodbyes.Length; i++) { RcSay s = RcVoiceData.Line(RcScenes.Goodbyes[i]); Talk.Add(s.Who, s.Text); }
                Talk.Add("", "The engine's warm. The track goes on out of sight, east, into the desert. All aboard.");
            }
            else Talk.Say("", RcWorlds.Egypt ? "The engine's warm. The track goes back the way it came. All aboard." : "The engine's warm. The track goes on out of sight, east, into the desert. All aboard.");
        }

        // The last job in Egypt for now: the next station's line isn't open yet.
        private void RideOn()
        {
            if (_boarding || Rc.Char == null) return;
            _boarding = true; _ridingOn = true;
            Talk.Clear();
            Talk.Say("", "Past the platform the track goes on, toward the next station. The signal from there is still just static. (That line opens in a later update.)");
        }

        protected override void RefreshAll()
        {
            base.RefreshAll();
            if (_foot == null || Rc.Char == null) return;
            RcCharacter c = Rc.Char;
            bool manual = c.HasNotice(RcNotices.MANUAL), file = c.HasNotice(RcNotices.FILE), tape = c.HasNotice(RcNotices.CASSETTE);
            bool egypt = RcWorlds.Egypt;
            for (int i = 0; i < ROWS; i++)
            {
                _name[i].text = (i + 1) + "   " + (i == 0 ? "HOME" : i == 1 ? "LAST STOP" : "STATION " + (i + 1));
                _name[i].color = (egypt ? i == 0 : i == 1 && Ready && RcWorlds.EgyptOpen) ? Accent : i < 2 ? Color.white : Dim;
                if (i == 0) { _where[i].text = manual ? "Town 1: the pilot" : "the town"; _status[i].text = egypt ? "ALL ABOARD" : c.BossesBeaten >= 5 ? "TAPE OFFLINE" : "TAPE PLAYING"; }
                else if (i == 1) { _where[i].text = "Egypt"; _status[i].text = egypt ? "YOU ARE HERE" : !RcWorlds.EgyptOpen ? "UNDER DEVELOPMENT" : Ready ? "ALL ABOARD" : "ENGINE COLD"; }
                else { _where[i].text = file ? Towns[i - 2] : "???"; _status[i].text = "NO SIGNAL"; }
                _where[i].color = i < 2 ? Color.white : Dim;
                _status[i].color = (egypt ? i == 0 : i == 1 && Ready && RcWorlds.EgyptOpen) ? Accent : Dim;
            }
            _foot.text = tape ? "Pencilled under the board: \"Don't play it in the dark.\"" : "";
            _board.text = egypt ? "RIDE HOME" : "BOARD THE TRAIN";
            _board.gameObject.SetActive(Ready && !_boarding);
            RcQuest cq = c.CurrentQuest;
            _ride.gameObject.SetActive(egypt && !_boarding && cq != null && cq.Kind == RcQuestKind.Station && cq.World == RcWorlds.EGYPT && !c.QuestComplete);
        }
    }
}
