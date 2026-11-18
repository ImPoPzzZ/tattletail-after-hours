using UnityEngine;
using UnityEngine.UI;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// The VERSUS machine's screen: a MAMA seat and a SURVIVOR seat, shared
    /// with the friend in your Steam lobby (VersusLobby). JOIN takes a seat
    /// (LEAVE gives it back); the empty seat then offers INVITE PLAYER (Steam invite). Each side has its own
    /// look (and the survivor's gadget) — the same shop items as Endless.
    /// START when both seats are filled starts both games.
    /// </summary>
    public class VersusMenu : OverlayPanel
    {
        private static VersusMenu _instance;

        private Text _mamaStatus, _survivorStatus, _mismatch, _hint;
        private Text _mamaJoin, _survivorJoin, _mamaLeave, _survivorLeave, _mamaInvite, _survivorInvite, _start;
        private Row _gadget, _color, _beam, _voice, _mamaSkin, _hunter, _map;

        protected override string Title { get { return "VERSUS"; } }
        protected override Vector2 PanelSize { get { return new Vector2(1500f, 860f); } }

        public static void Open() { GetOrCreate(ref _instance, "KE_VersusMenu").Show(); }

        public static void CloseIfOpen() { if (_instance != null && _instance.Visible) _instance.Hide(); }

        protected override void OnShown()
        {
            VersusLobby.ResetIfSolo();
            CustomizationSubmenu.SetVersusMode(); // Versus picks — the VERSUS SHOP's items
            CustomizationSubmenu.Refresh();
        }

        protected override void BuildContent(Transform p)
        {
            const float L = -360f, R = 360f;
            MakeText(p, "MAMA", 44, TextAnchor.MiddleCenter, new Vector2(L, 300f), new Vector2(520f, 60f), new Color(1f, 0.35f, 0.5f));
            MakeText(p, "SURVIVOR", 44, TextAnchor.MiddleCenter, new Vector2(R, 300f), new Vector2(520f, 60f), new Color(0.4f, 0.8f, 1f));
            _mamaStatus = Fit(MakeText(p, "", 28, TextAnchor.MiddleCenter, new Vector2(L, 240f), new Vector2(520f, 40f), Dim));
            _survivorStatus = Fit(MakeText(p, "", 28, TextAnchor.MiddleCenter, new Vector2(R, 240f), new Vector2(520f, 40f), Dim));

            // JOIN / LEAVE share a spot, INVITE sits under it.
            _mamaJoin = AddButton(p, new Vector2(L, 170f), "MAMA JOIN", () => VersusLobby.Claim(VersusRole.Mama, VersusLobby.Me));
            _mamaLeave = AddButton(p, new Vector2(L, 170f), "LEAVE", () => VersusLobby.Claim(VersusRole.Mama, VersusSeat.Empty));
            _survivorJoin = AddButton(p, new Vector2(R, 170f), "PLAYER JOIN", () => VersusLobby.Claim(VersusRole.Survivor, VersusLobby.Me));
            _survivorLeave = AddButton(p, new Vector2(R, 170f), "LEAVE", () => VersusLobby.Claim(VersusRole.Survivor, VersusSeat.Empty));
            _mamaInvite = AddButton(p, new Vector2(L, 105f), "INVITE PLAYER", Invite, 380f);
            _survivorInvite = AddButton(p, new Vector2(R, 105f), "INVITE PLAYER", Invite, 380f);

            // Your side's look — the same shop items as Endless.
            _hunter = AddStepper(p, L, -30f, "Hunter", StepHunter, () => VersusHunters.PlayAsPapa ? "Papa" : "Mama");
            _mamaSkin = AddStepper(p, L, -85f, "Skin", VersusHunters.StepSkin, () => VersusHunters.SkinName);
            _gadget = AddStepper(p, R, -30f, "Gadget", EndlessSetupMenu.StepGadget,
                () => Gadgets.Name(Gadgets.SelectedIfOwned) + (Gadgets.IsDlc(Gadgets.SelectedIfOwned) ? " (DLC)" : ""));
            _color = AddStepper(p, R, -85f, "Tattletail Color", CustomizationSubmenu.StepColor, () => CustomizationSubmenu.ColorName);
            _beam = AddStepper(p, R, -140f, "Flashlight Beam", CustomizationSubmenu.StepBeam, () => CustomizationSubmenu.BeamName);
            _voice = AddToggle(p, R, -195f, "Kaleidoscope Voice", CustomizationSubmenu.ToggleVoicePublic,
                () => !CustomizationSubmenu.VoiceOwned ? "LOCKED" : (CustomizationSubmenu.KaleidoscopeVoice ? "ON" : "OFF"));

            // The map: both players play the same one (the host's choice).
            _map = AddStepper(p, 0f, -245f, "Map", StepMap, () => VersusLobby.FacilityMap ? "Waygetter Facility" : "The House");

            // (A friend on another version: this line takes the hint's place.)
            _mismatch = Fit(MakeText(p, "", 24, TextAnchor.MiddleCenter, new Vector2(0f, -290f), new Vector2(1300f, 60f), new Color(1f, 0.45f, 0.4f)));

            _hint = Fit(MakeText(p, "INVITE PLAYER sends a Steam invite: your friend lands in their Arcade and presses PLAYER JOIN here.\nSkins, colors and beams come from the red VERSUS SHOP.",
                22, TextAnchor.MiddleCenter, new Vector2(0f, -290f), new Vector2(1300f, 40f), Dim));

            AddButton(p, new Vector2(-220f, -360f), "BACK", () =>
            {
                Hide();
                if (Camera.main != null) AudioController.Play("vhs_cam_change", Camera.main.transform);
            });
            _start = AddButton(p, new Vector2(220f, -360f), "START", VersusLobby.Start);
        }

        private static void StepMap(int dir)
        {
            VersusLobby.SetMap(!VersusLobby.FacilityMap);
        }

        private static void StepHunter(int dir)
        {
            VersusHunters.SetPapa(!VersusHunters.PlayAsPapa); // both are free
        }

        private static void Invite()
        {
            if (MpLobby.Instance != null) MpLobby.Instance.InviteOrCreate();
        }

        protected override void RefreshAll()
        {
            base.RefreshAll();
            if (_start == null) return;
            VersusSeat me = VersusLobby.Me, mama = VersusLobby.Mama, surv = VersusLobby.Survivor;
            bool friendHere = VersusLobby.HasFriend;

            _mamaStatus.text = Status(mama);
            _survivorStatus.text = Status(surv);
            var lobby = MpLobby.Instance;
            _mismatch.text = lobby != null && lobby.PeerIncompatible ? lobby.MismatchText : "";
            _hint.gameObject.SetActive(_mismatch.text.Length == 0);

            _mamaJoin.gameObject.SetActive(mama == VersusSeat.Empty || mama == VersusSeat.Bot);
            _survivorJoin.gameObject.SetActive(surv == VersusSeat.Empty || surv == VersusSeat.Bot);
            _mamaLeave.gameObject.SetActive(mama == me);
            _survivorLeave.gameObject.SetActive(surv == me);
            _mamaInvite.gameObject.SetActive(mama == VersusSeat.Empty && surv == me && !friendHere);
            _survivorInvite.gameObject.SetActive(surv == VersusSeat.Empty && mama == me && !friendHere);
            _start.gameObject.SetActive(VersusLobby.Ready && (mama == me || surv == me));

            _hunter.SetVisible(mama == me);
            _mamaSkin.SetVisible(mama == me);
            bool survivorMe = surv == me;
            _gadget.SetVisible(survivorMe);
            _color.SetVisible(survivorMe);
            _beam.SetVisible(survivorMe);
            _voice.SetVisible(survivorMe);
        }

        private static string Status(VersusSeat s)
        {
            if (s == VersusSeat.Empty) return "Empty";
            if (s == VersusSeat.Bot) return "BOT (playtest)";
            return s == VersusLobby.Me ? "YOU" : VersusLobby.FriendName;
        }
    }
}
