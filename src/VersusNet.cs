using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// Versus online sync (on top of the Steam lobby):
    ///  - Poses, ~10 a second, unreliable: where each player is and which
    ///    way they face, in the Arcade or in a match. VersusAvatars draws
    ///    the other player from them.
    ///  - Events, reliable: the survivor's noises (Mama's kill meter), Mama
    ///    away / back (the survivor's game decides: rests, the phone break,
    ///    Music Box, Flash Camera, Totem), the parrot's pull, Mama killing
    ///    the flashlight, catching, a Totem save, and the round ending.
    /// The survivor's game is the authority on when Mama may be around.
    /// </summary>
    public static class VersusNet
    {
        public const int CTX_NONE = -1, CTX_ARCADE = 0, CTX_MATCH = 1, CTX_BEACH = 2; // BEACH: Summer Vacation
        private const float POSE_INTERVAL = 0.1f;
        private const float POSE_STALE = 2f;
        private const float NOISE_SEND_GAP = 0.2f;

        private enum Ev { Noise = 1, MamaAway = 2, MamaBack = 3, FlashKill = 4, Caught = 5, Saved = 6, RoundOver = 7, ParrotLure = 8, Gaze = 9, Absence = 10, Voice = 11 }
        public enum Over { Caught = 1, Escaped = 2, Died = 3, Left = 4 }

        public static bool Online { get { return VersusLobby.HasFriend; } }

        // The other player's last pose.
        public static int RemoteCtx = CTX_NONE;
        public static Vector3 RemotePos;
        public static float RemoteYaw;
        public static bool RemotePresent;
        public static int RemoteSkin; // the other player's kid + outfit, or the hunter's body (VersusHunters codes)
        public static string RemoteSpec = ""; // their custom kid (Character Creator), if they play one

        /// <summary>Survivor side: Papa's Absence — he's invisible until then.</summary>
        public static float PapaHiddenUntil;
        public static bool PapaHidden { get { return Time.time < PapaHiddenUntil; } }
        private static float _remoteAt = -100f;
        public static bool RemoteFresh { get { return Time.time - _remoteAt < POSE_STALE; } }

        /// <summary>Survivor side: whether Mama is allowed to be around right now (this game decides).</summary>
        public static bool MamaPresentHere;

        /// <summary>Mama side: the last away/back the survivor sent (-1 none yet), for a Mama whose level loads late.</summary>
        public static int MamaPresence = -1;

        private static float _sendTimer, _nextNoiseAt, _tattleNoiseAt = -10f;

        /// <summary>The next Tattletail line doesn't count at all (his "Mama's coming!" warning as a hunt starts).</summary>
        public static void QuietTattletailLine() { _tattleNoiseAt = Time.time; }
        private static bool _roundOverSent;

        // ── Every frame (plugin Update) ──────────────────────────────────────
        // The flashlight flickers when the survivor looks at Mama / Papa (the Versus hunter and the idle bot are only bodies: the game's own Mama is not there to do it).
        private static float _nextGlance;
        public static void TickMamaGlance()
        {
            if (!VersusMode.IsSurvivor || Time.time < _nextGlance) return;
            _nextGlance = Time.time + 0.15f;
            var run = EndlessRunManager.Instance;
            if (run == null || !run.RunActive || RM.flashlight == null || MamaController.Instance == null) return;
            GameObject mama = MamaController.Instance.GetActiveMama();
            if (mama == null || !mama.activeInHierarchy) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 eye = cam.transform.position;
            Vector3 body = mama.transform.position + Vector3.up * 1.1f;
            Vector3 to = body - eye;
            float dist = to.magnitude;
            if (dist > 14f || dist < 0.3f) return;
            if (Vector3.Angle(cam.transform.forward, to) > 38f) return;   // not in front of the child
            RaycastHit hit;
            if (Physics.Raycast(eye, to / dist, out hit, dist - 0.2f, ~0, QueryTriggerInteraction.Ignore) && !hit.transform.IsChildOf(mama.transform)) return; // a wall in between
            RM.flashlight.RequestFlicker();
        }

        public static void Tick()
        {
            if (!Online) return;
            VersusAvatars.Ensure();

            _sendTimer -= Time.unscaledDeltaTime;
            if (_sendTimer > 0f) return;
            _sendTimer = POSE_INTERVAL;

            Vector3 pos; float yaw; bool present;
            int ctx = LocalPose(out pos, out yaw, out present);
            if (ctx == CTX_NONE) return;
            using (var p = new MpPacket(MpNetworking.PacketId.VersusPose))
            {
                p.Write(ctx);
                p.Write(pos.x); p.Write(pos.y); p.Write(pos.z);
                p.Write(yaw);
                p.Write(present);
                p.Write(ctx == CTX_MATCH && VersusMode.IsMama ? VersusHunters.LocalCode : KidSkins.Code);
                p.Write(KidSkins.Gender == KidGender.Custom ? CharacterSpec.LocalString : "");
                MpNetworking.SendUnreliable(MpLobby.Instance.PeerSteamID, p);
            }
        }

        /// <summary>Where this player is: in the Arcade, as the survivor in a match, or as Mama (her body).</summary>
        public static int LocalPose(out Vector3 pos, out float yaw, out bool present)
        {
            pos = Vector3.zero; yaw = 0f; present = true;
            var mama = VersusMamaController.Instance;
            if (VersusMode.IsMama && mama != null && mama.Ready)
            {
                pos = mama.BodyPosition;
                yaw = mama.BodyYaw;
                present = mama.Present;
                return CTX_MATCH;
            }
            var fps = RM.fpsController;
            if (fps == null) return CTX_NONE;
            pos = fps.transform.position;
            yaw = fps.transform.eulerAngles.y;
            if (VersusMode.IsSurvivor && EndlessRunManager.Instance != null && EndlessRunManager.Instance.RunActive) return CTX_MATCH;
            if (HubMode.Active && HubController.Instance != null && HubController.Instance.Built) return CTX_ARCADE;
            if (FactoryMode.Active || RecalledMode.Active) return CTX_NONE; // the factory, Recalled (for now): single-player
            if (BeachMode.Active) return CTX_NONE; // Summer Vacation is single-player for now (CTX_BEACH is kept for co-op later)
            return CTX_NONE;
        }

        public static void OnPose(MpPacket p)
        {
            RemoteCtx = p.ReadInt();
            RemotePos = new Vector3(p.ReadFloat(), p.ReadFloat(), p.ReadFloat());
            RemoteYaw = p.ReadFloat();
            RemotePresent = p.ReadBool();
            RemoteSkin = p.ReadInt();
            RemoteSpec = p.ReadString();
            _remoteAt = Time.time;
        }

        // ── Sending events ───────────────────────────────────────────────────
        private static void Send(Ev ev, Vector3 v, float f, int i)
        {
            if (!Online) return;
            using (var p = new MpPacket(MpNetworking.PacketId.VersusEvent))
            {
                p.Write((int)ev);
                p.Write(v.x); p.Write(v.y); p.Write(v.z);
                p.Write(f);
                p.Write(i);
                MpNetworking.SendReliable(MpLobby.Instance.PeerSteamID, p);
            }
        }

        private static bool SurvivorOnline { get { return Online && VersusMode.IsSurvivor && !VersusMode.OpponentIsBot; } }
        private static bool MamaOnline { get { return Online && VersusMode.IsMama && !VersusMode.OpponentIsBot; } }

        /// <summary>Survivor side: every noise the game reports (MamaManager.OnNoise postfix).</summary>
        public static void OnLocalNoise(Vector3 pos, bool big)
        {
            if (!SurvivorOnline || EndlessRunManager.Instance == null || !EndlessRunManager.Instance.RunActive) return;
            if (ToyParrot.Active != null && Vector3.Distance(pos, ToyParrot.Active.transform.position) <= 1f) return; // the parrot's crying isn't the child
            if (MamaTeleportGrace.IgnoresNoiseAt(pos)) return; // parrot down: Mama ignores the child
            // Tattletail talking reports a noise EVERY FRAME of a line: one line is one noise.
            // (It filled the kill meter in a few seconds: "instant death".)
            var tt = RM.tattletail;
            if (tt != null && tt.tattletail != null && (pos - tt.tattletail.transform.position).sqrMagnitude < 0.0001f)
            {
                bool sameLine = Time.time - _tattleNoiseAt < 0.5f;
                _tattleNoiseAt = Time.time;
                if (sameLine) return;
            }
            if (Time.time < _nextNoiseAt) return;
            _nextNoiseAt = Time.time + NOISE_SEND_GAP;
            Send(Ev.Noise, pos, 0f, big ? 1 : 0);
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Versus] Noise sent to Mama at " + pos + (big ? " (loud)" : "") + ".");
        }

        /// <summary>Survivor side: Mama leaves (rest, phone break, Music Box, Flash Camera, Totem).</summary>
        public static void SendMamaAway()
        {
            if (!SurvivorOnline || !MamaPresentHere) return;
            MamaPresentHere = false;
            Send(Ev.MamaAway, Vector3.zero, 0f, 0);
        }

        /// <summary>Survivor side: Mama may come back (an objective is on).</summary>
        public static void SendMamaBack()
        {
            if (!SurvivorOnline) return;
            MamaPresentHere = true;
            Send(Ev.MamaBack, Vector3.zero, 0f, 0);
        }

        public static void SendParrotLure(Vector3 pos)
        {
            if (!SurvivorOnline) return;
            Send(Ev.ParrotLure, pos, Gadgets.PARROT_DURATION, 0);
        }

        public static void SendFlashKill() { if (MamaOnline) Send(Ev.FlashKill, Vector3.zero, 0f, 0); }
        public static void SendGaze(float seconds) { if (MamaOnline) Send(Ev.Gaze, Vector3.zero, seconds, 0); }
        public static void SendAbsence(float seconds) { if (MamaOnline) Send(Ev.Absence, Vector3.zero, seconds, 0); }
        public static void SendCaught() { if (MamaOnline) Send(Ev.Caught, Vector3.zero, 0f, 0); }

        /// <summary>A kid's sound (KidVoice) for the other player: the kind and its random seed, so both hear the same one.</summary>
        public static void SendVoice(int kind, int seed, float seconds)
        {
            if (!Online || VersusMode.IsMama || (VersusMode.OpponentIsBot && VersusMode.Role != VersusRole.None)) return;
            Vector3 pos; float yaw; bool present;
            if (LocalPose(out pos, out yaw, out present) == CTX_NONE) return; // Endless: only you hear your kid (the other player doesn't see you there)
            Send(Ev.Voice, Vector3.zero, seconds, kind | (seed << 4));
        }

        /// <summary>The round ended on this side — tell the other one (once).</summary>
        public static void SendRoundOver(Over why)
        {
            if (_roundOverSent || !Online || VersusMode.Role == VersusRole.None || VersusMode.OpponentIsBot) return;
            _roundOverSent = true;
            Send(Ev.RoundOver, Vector3.zero, 0f, (int)why);
        }

        /// <summary>A new match is starting.</summary>
        public static void ResetMatch()
        {
            _roundOverSent = false;
            MamaPresentHere = false;
            MamaPresence = -1;
            PapaHiddenUntil = 0f;
            _matchTokens = 0;
        }

        // ── Receiving events ─────────────────────────────────────────────────
        public static void OnEvent(MpPacket p)
        {
            var ev = (Ev)p.ReadInt();
            Vector3 v = new Vector3(p.ReadFloat(), p.ReadFloat(), p.ReadFloat());
            float f = p.ReadFloat();
            int i = p.ReadInt();
            var mama = VersusMamaController.Instance;

            switch (ev)
            {
                // → Mama
                case Ev.Noise: VersusMamaController.OnSurvivorNoise(v, i == 1); break;
                case Ev.MamaAway: MamaPresence = 0; if (mama != null && mama.Ready) mama.SetPresent(false); break;
                case Ev.MamaBack: MamaPresence = 1; if (mama != null && mama.Ready) mama.SetPresent(true); break;
                case Ev.ParrotLure: if (mama != null) mama.PulledByParrot(v, f); break;
                case Ev.Saved: if (mama != null) mama.OnSaved(); break;
                case Ev.Voice: KidVoice.Hear(i & 15, i >> 4, f); break; // → Mama, or the other kid in the Arcade

                // → Survivor
                case Ev.FlashKill:
                    KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Versus] Kill Light received (survivor=" + VersusMode.IsSurvivor + ", flashlight=" + (RM.flashlight != null) + ", lit=" + (RM.flashlight != null && RM.flashlight.IsGlowing()) + ").");
                    if (VersusMode.IsSurvivor && FactoryMode.EndlessPreview && FactoryCamcorder.Instance != null)
                    {
                        FactoryCamcorder.Instance.Jam(10f); // the Facility: no flashlight, the camcorder is the light
                        HubMode.SetHudText("Mama jammed your camcorder! It comes back in a few seconds.");
                    }
                    else if (VersusMode.IsSurvivor && RM.flashlight != null)
                    {
                        RM.flashlight.KillLight(true);
                        HubMode.SetHudText("Mama killed your flashlight! Shake it back on.");
                    }
                    break;
                case Ev.Gaze:
                    KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Versus] Mama's Gaze received.");
                    if (VersusMode.IsSurvivor) KaleidoscopePluginRunner.Run(GazeRoutine(f));
                    break;
                case Ev.Absence:
                    KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Versus] Papa's Absence received.");
                    if (VersusMode.IsSurvivor)
                    {
                        PapaHiddenUntil = Time.time + f;
                        HubMode.SetHudText("Papa vanished... he could be anywhere.");
                    }
                    break;
                case Ev.Caught:
                    KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Versus] Caught received — kill meter full.");
                    SurvivorCaught();
                    break;

                // → either
                case Ev.RoundOver: OnRoundOver((Over)i); break;
            }
        }

        // Survivor: Mama's kill meter filled. A Totem saves you; otherwise the jumpscare ends the run.
        private static void SurvivorCaught()
        {
            var run = EndlessRunManager.Instance;
            if (!VersusMode.IsSurvivor || run == null || !run.RunActive) return;
            if (PowerUpObjective.Instance != null && PowerUpObjective.Instance.TryConsumeTotem(run.CurrentMamaDifficulty))
            {
                Send(Ev.Saved, Vector3.zero, 0f, 0);
                return;
            }
            SendRoundOver(Over.Caught);
            KaleidoscopePluginRunner.Run(CaughtRoutine());
        }

        // Survivor: Mama's Gaze. Frozen in place, shaking, and the trembling makes noise (her kill meter hears it).
        private static System.Collections.IEnumerator GazeRoutine(float seconds)
        {
            var run = EndlessRunManager.Instance;
            var fps = RM.fpsController;
            Camera cam = Camera.main;
            if (run == null || !run.RunActive || fps == null || cam == null) yield break;

            HubMode.SetHudText("Mama's Gaze! You're frozen in fear...");
            AudioController.Play("sting_scary", cam.transform);
            KidVoice.Say(KidVoice.Kind.Terrified, seconds); // trembling whimpers (Mama hears them too)
            fps.enabled = false;
            Transform ct = cam.transform;
            Vector3 home = ct.localPosition;
            float end = Time.time + seconds, nextNoise = 0f;
            while (Time.time < end && fps != null)
            {
                ct.localPosition = home + Random.insideUnitSphere * 0.035f;
                if (Time.time >= nextNoise)
                {
                    nextNoise = Time.time + 0.8f;
                    if (RM.mamaManager != null) RM.mamaManager.OnNoise(fps.transform.position, false); // counted by the Versus noise hook
                }
                yield return null;
            }
            if (ct != null) ct.localPosition = home;
            if (fps != null && !OverlayPanel.AnyOpen) fps.enabled = true;
        }

        private static System.Collections.IEnumerator CaughtRoutine()
        {
            KidVoice.Mine(KidVoice.Kind.Caught);
            // The game's own jumpscare, called by reflection: its callback is a System.Action, which this
            // mod must never reference directly (old Mono). It handles a null callback itself.
            try
            {
                var m = typeof(JumpscareManager).GetMethod("DoJumpscare");
                if (RM.jumpscare != null && (object)m != null) m.Invoke(RM.jumpscare, new object[] { null });
            }
            catch (System.Exception e) { KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Versus] Jumpscare failed: " + e.Message); }
            yield return new WaitForSeconds(3f);
            var run = EndlessRunManager.Instance;
            if (run != null && run.RunActive) run.EndRun(true);
        }

        // Versus Tokens (spent at the VERSUS SHOP): the hunter gets TOKENS_HUNTER_WIN for a catch,
        // the child gets TOKENS_PER_OBJECTIVE for every objective done (instead of shards).
        public const int TOKENS_HUNTER_WIN = 150, TOKENS_PER_OBJECTIVE = 30;   // shards (the old tokens were 1000 and 200: shards are scarcer)
        private static int _matchTokens;

        /// <summary>Hunter side: the child was caught (or died). Nothing against a bot.</summary>
        public static void AwardHunterWin()
        {
            if (!Online || VersusMode.OpponentIsBot) return;
            Shop.AddVersusTokens(TOKENS_HUNTER_WIN);
            VersusLobby.PendingArcadeMessage = "+" + TOKENS_HUNTER_WIN + " shards - you won! Spend them in the shops.";
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Versus] Hunter won: +" + TOKENS_HUNTER_WIN + " shards.");
        }

        /// <summary>Child side: an objective done. Returns false outside an online match (then it's normal shards).</summary>
        public static bool AwardObjectiveTokens()
        {
            if (!VersusMode.IsSurvivor || !Online || VersusMode.OpponentIsBot) return false;
            Shop.AddVersusTokens(TOKENS_PER_OBJECTIVE);
            _matchTokens += TOKENS_PER_OBJECTIVE;
            HubMode.SetHudText("+" + TOKENS_PER_OBJECTIVE + " shards");
            VersusLobby.PendingArcadeMessage = "+" + _matchTokens + " shards this match.";
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Versus] Objective: +" + TOKENS_PER_OBJECTIVE + " shards (" + _matchTokens + " this match).");
            return true;
        }

        private static void OnRoundOver(Over why)
        {
            _roundOverSent = true; // don't echo it back
            if (VersusMode.IsMama && VersusMamaController.Instance != null && (why == Over.Caught || why == Over.Died))
                AwardHunterWin();
            if (VersusMode.IsMama && VersusMamaController.Instance != null)
            {
                string msg = why == Over.Caught ? "CAUGHT! The child is yours."
                    : why == Over.Escaped ? "The child escaped on the phone!"
                    : why == Over.Died ? "The child didn't make it."
                    : "The child left the match.";
                if (why == Over.Caught) VersusMamaController.Instance.PlayKill(msg);
                else VersusMamaController.Instance.EndRound(msg);
            }
            else if (VersusMode.IsSurvivor && why == Over.Left)
            {
                var run = EndlessRunManager.Instance;
                if (run != null && run.RunActive)
                {
                    HubMode.SetHudText("Mama left the match.");
                    run.EndRun(false);
                }
            }
        }
    }

    /// <summary>Runs a coroutine on the plugin (for static code).</summary>
    internal static class KaleidoscopePluginRunner
    {
        public static void Run(System.Collections.IEnumerator routine)
        {
            if (KaleidoscopeEndlessPlugin.Instance != null) KaleidoscopeEndlessPlugin.Instance.StartCoroutine(routine);
        }
    }

    /// <summary>
    /// Draws the other player from their poses: a stand-in body with their
    /// Steam name in the Arcade and (for Mama) the survivor in a match; the
    /// Mama body on the survivor's side while she's around. Real player
    /// models and skins come later.
    /// </summary>
    public class VersusAvatars : MonoBehaviour
    {
        private static VersusAvatars _instance;
        private GameObject _person, _mama;
        private int _personGender = -1, _mamaCode = -1;
        private string _personSpec = "";
        private Text3D _name;
        private Vector3 _shown;

        public static void Ensure()
        {
            if (_instance != null) return;
            var go = new GameObject("KE_VersusAvatars");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<VersusAvatars>();
        }

        /// <summary>The other player's kid, if it's showing.</summary>
        public static GameObject VisiblePerson { get { return _instance != null && _instance._person != null && _instance._person.activeSelf ? _instance._person : null; } }

        /// <summary>Mama's kill: keep the kid right where it is (no pose updates) for a while, then reset it.</summary>
        public static void HoldPerson(float seconds)
        {
            if (_instance == null) return;
            _instance._holdUntil = Time.time + seconds;
            _instance._holding = true;
        }
        private float _holdUntil;
        private bool _holding;

        /// <summary>The Mama body on the survivor's side while she's around (Flash Camera aims at it).</summary>
        public static GameObject VisibleMama { get { return _instance != null && _instance._mama != null && _instance._mama.activeSelf ? _instance._mama : null; } }

        private void Update()
        {
            if (_holding)
            {
                if (Time.time < _holdUntil) return;
                _holding = false;
                if (_person != null) _person.SetActive(false); // shows again fresh (standing) next time
            }
            Vector3 dummyPos; float dummyYaw; bool dummyPresent;
            int localCtx = VersusNet.LocalPose(out dummyPos, out dummyYaw, out dummyPresent);
            bool same = VersusNet.Online && VersusNet.RemoteFresh && VersusNet.RemoteCtx == localCtx && localCtx != VersusNet.CTX_NONE;

            bool showPerson = same && (localCtx == VersusNet.CTX_ARCADE || localCtx == VersusNet.CTX_BEACH || VersusMode.IsMama);
            bool showMama = same && localCtx == VersusNet.CTX_MATCH && VersusMode.IsSurvivor && VersusNet.MamaPresentHere && VersusNet.RemotePresent && !VersusNet.PapaHidden;

            if (showPerson) ShowPerson(); else if (_person != null) _person.SetActive(false);
            if (showMama) { ShowMama(); FlickerIfLookingAtMama(); ReactToMama(); } else if (_mama != null) _mama.SetActive(false);
        }

        // Like the game's own Mama (Mama.IsFlashlightNearMama): a lit flashlight (not being shaken) with
        // Mama within 12 m, on screen and not behind a wall flickers with the danger sound.
        private const float FLICKER_DIST = 12f; // PatrolNode.maxVisMamaDist
        // The child sees Mama (the same reactions as in Endless: KidVoice.WatchFor).
        private void ReactToMama() { KidVoice.WatchFor(_mama); }

        private void FlickerIfLookingAtMama()
        {
            var light = RM.flashlight;
            Camera cam = Camera.main;
            if (light == null || cam == null || _mama == null || !light.IsGlowing() || light.IsShaking()) return;
            Vector3 chest = _mama.transform.position + Vector3.up * 1.4f;
            if (Vector3.Distance(cam.transform.position, chest) > FLICKER_DIST) return;
            Vector3 vp = cam.WorldToViewportPoint(chest);
            if (vp.z <= 0f || vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f) return;
            if (Physics.Linecast(cam.transform.position, chest, ~0, QueryTriggerInteraction.Ignore)) return; // a wall in the way
            light.RequestFlicker();
        }

        private void ShowPerson()
        {
            KidGender gender = KidSkins.GenderOf(VersusNet.RemoteSkin);
            string spec = gender == KidGender.Custom ? VersusNet.RemoteSpec : "";
            if (_person != null && (_personGender != (int)gender || _personSpec != spec)) { Destroy(_person); _person = null; } // they switched characters, or edited their kid
            if (_person == null) BuildPerson(gender);
            if (_person == null) return;
            // The other game sends its player's controller position; stand the body on the floor under it.
            Vector3 target = WorldRays.FeetOf(VersusNet.RemotePos); // their feet: on the floor, or in the air mid-jump
            if (!_person.activeSelf)
            {
                _person.SetActive(true);
                _shown = target;
                var anim = _person.GetComponent<KidAnimator>();
                if (anim != null) anim.ResetFlinch();
            }
            _shown = Vector3.Lerp(_shown, target, Mathf.Clamp01(Time.deltaTime * 12f));
            _person.transform.position = _shown;
            _person.transform.rotation = Quaternion.Slerp(_person.transform.rotation, Quaternion.Euler(0f, VersusNet.RemoteYaw, 0f), Mathf.Clamp01(Time.deltaTime * 12f));
            if (_name != null) _name.Set(VersusLobby.FriendName);
            var look = _person.GetComponent<KidLook>();
            if (look != null) look.Set(KidSkins.SkinOf(VersusNet.RemoteSkin));
        }

        private void ShowMama()
        {
            int code = VersusHunters.IsHunterCode(VersusNet.RemoteSkin) ? VersusNet.RemoteSkin : VersusHunters.CODE_BASE;
            if (_mama != null && _mamaCode != code) { Destroy(_mama); _mama = null; } // a different hunter
            if (_mama == null) { _mama = VersusHunters.BuildBody("KE_VersusRemoteMama", code); _mamaCode = code; }
            if (_mama == null) return;
            _mama.SetActive(true);
            _mama.transform.position = VersusNet.RemotePos; // she snaps between spawn spots, no smoothing
            _mama.transform.rotation = Quaternion.Slerp(_mama.transform.rotation, Quaternion.Euler(0f, VersusNet.RemoteYaw, 0f), Mathf.Clamp01(Time.deltaTime * 10f));
        }

        // The other player as a kid, with their name above.
        private void BuildPerson(KidGender gender)
        {
            _personSpec = gender == KidGender.Custom ? VersusNet.RemoteSpec : "";
            _person = gender == KidGender.Custom
                ? CharacterKit.Build("KE_VersusRemotePlayer", CharacterSpec.FromString(_personSpec), KidSkins.SkinOf(VersusNet.RemoteSkin))
                : ChildModel.Build("KE_VersusRemotePlayer", gender, KidSkins.SkinOf(VersusNet.RemoteSkin));
            _person.SetActive(false); // ShowPerson places it fresh
            _personGender = (int)gender;
            DontDestroyOnLoad(_person); // lives across the Arcade and the match
            _name = new Text3D(_person.transform, new Vector3(0f, ChildModel.HeightOf(gender) + 0.15f, 0f));
        }

        private void OnDestroy()
        {
            if (_person != null) Destroy(_person);
            if (_mama != null) Destroy(_mama);
        }

        /// <summary>A name label that always faces the camera.</summary>
        private class Text3D
        {
            private readonly UnityEngine.UI.Text _t;
            public Text3D(Transform parent, Vector3 pos)
            {
                _t = WorldText.Create(parent, "", Color.white, 0.12f, TextAnchor.MiddleCenter, 2f);
                _t.canvas.transform.localPosition = pos;
                _t.canvas.gameObject.AddComponent<PartyLabelBillboard>();
            }
            public void Set(string s) { if (_t.text != s) _t.text = s; }
        }
    }
}
