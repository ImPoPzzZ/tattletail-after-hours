using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// Version 2.0 "True Ending" easter egg — steps 1-6 of a hand-marked
    /// sequence (positions from DebugLocationMarker, same convention as
    /// every other objective in this project). Steps 7+ (the kaleidoscope
    /// portal beyond the gate) come later once this half is confirmed
    /// working in-game — see MamaController.DebugDisableMama, which stays
    /// true for the whole marking/testing phase.
    ///
    /// Fully self-contained: doesn't touch vanilla quest state, doesn't
    /// depend on the normal randomized objective rotation. Strictly
    /// sequential — only Tape 1 spawns at run start, and each following
    /// step (Tape 2, Friend 1, Friend 2, Diamond Tattletail) only spawns
    /// once the one before it is done. Step 6 (deleting the gate) fires
    /// automatically the instant step 5 is done.
    /// </summary>
    public class TrueEndingEasterEgg : MonoBehaviour
    {
        public static TrueEndingEasterEgg Instance { get; private set; }

        // Exact marks from DebugLocationMarker (E) — player positions, same
        // convention TapeObjective/TattletailFriendObjective use for their
        // own hand-marked spots.
        private static readonly Vector3 Tape1Pos = new Vector3(11.8f, 0.7f, -9.8f);
        private static readonly Vector3 Tape2Pos = new Vector3(2.5f, 2.2f, -0.9f);
        private static readonly Vector3 Friend1Pos = new Vector3(6.5f, -0.9f, -7.0f);
        private static readonly Vector3 Friend2Pos = new Vector3(7.9f, -1.0f, 8.6f);
        private static readonly Vector3 DiamondPos = new Vector3(5.7f, 2.2f, 6.5f);

        // Real vanilla gate mesh, not one of our own spawns — resolved by
        // exact name + nearest-to-mark, same lookup convention SceneBootstrap
        // uses for "BasementDoor". GateSearchAnchor is the raycast hit point
        // from mark 6 (looking at 'chainlink_single'), a better anchor than
        // the player's own foot position since it's the object's own surface.
        private const string GateObjectName = "chainlink_single";
        private static readonly Vector3 GateSearchAnchor = new Vector3(-1.7f, 0.7f, -11.1f);

        // Confirmed real, unused vanilla voice line — found directly in
        // resources.assets ("tt_go_away_mama"), same tt_-prefixed naming
        // convention as every other Tattletail voice line. No recording
        // needed for steps 3-5.
        private const string GoAwayMamaAudioID = "tt_go_away_mama";

        // Portal mark — outside the normal play bounds, past the gate.
        // Raw mark, unsnapped (same reasoning as the friend spots — trust
        // the exact position the player stood at rather than a raycast
        // that might drag it somewhere unintended out here). Y raised by
        // 1.5 per direct feedback ("put it further up").
        private static readonly Vector3 PortalPos = new Vector3(-1.4f, 2.2f, -15.3f);

        // Real vanilla asset names found directly in resources.assets
        // (same grep-the-.assets-files trick that found tt_go_away_mama) —
        // a whole family of "kaleidoscope portal" audio already exists in
        // this game's data. Tried in priority order; first one that
        // resolves as a valid AudioController ID wins, logged either way.
        private static readonly string[] PortalAudioCandidates =
        {
            "kaleidoscope_fracture",
            "ambience_kaleidoscope_drone_1",
            "ambience_kaleidoscope_interior",
            "kaleidoscope_stinger1",
            "kaleidoscope_stinger2",
        };
        private const string PortalAudioFallback = "vhs_glitch"; // confirmed valid/heard already, used only if none of the above resolve
        private string _portalAudioID;

        // Outer boundary marks, past the portal — the only 2 of the 4
        // originally-given marks that were actually open space ("looking at
        // nothing within 5m"). The other 2 pointed at real fence pieces
        // (chainlink_group-1) already blocking that side on their own — see
        // SpawnBoxWalls()'s comment.
        private static readonly Vector3 BoxCorner1 = new Vector3(0.0f, 0.6f, -17.3f);
        private static readonly Vector3 BoxCorner2 = new Vector3(-4.0f, 0.7f, -16.9f);

        private Material _tapeMaterial;
        private Transform _gateTransform;
        private bool _resolving;

        private GameObject _tape1GO, _tape2GO, _friend1GO, _friend2GO, _diamondGO, _portalGO;
        private bool _tape1Done, _tape2Done, _friend1Done, _friend2Done, _diamondDone, _gateDeleted, _portalEntered;

        private const float PortalEnterRadius = 1.5f; // roughly the ring's own radius (1.2) plus a little slack

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

        /// <summary>Call once, early (same timing as every other objective's resolve call) — finds the Friend clone template and the gate, then spawns steps 1/3/4/5.</summary>
        public void ResolveLocations()
        {
            if (_resolving) return;
            _resolving = true;
            StartCoroutine(ResolveWhenReady());
        }

        private System.Collections.IEnumerator ResolveWhenReady()
        {
            Transform[] all = null;
            float waited = 0f;
            const float maxWait = 20f;
            while (waited < maxWait)
            {
                all = Resources.FindObjectsOfTypeAll<Transform>();
                if (all.Length > 0) break;
                yield return new WaitForSeconds(0.25f);
                waited += 0.25f;
            }
            all = all ?? new Transform[0];

            Transform nearestGate = null;
            float nearestDist = float.MaxValue;
            foreach (Transform t in all)
            {
                if (t == null || !t.gameObject.scene.IsValid()) continue;
                if (t.gameObject.name != GateObjectName) continue;
                float d = Vector3.Distance(t.position, GateSearchAnchor);
                if (d < nearestDist) { nearestDist = d; nearestGate = t; }
            }
            _gateTransform = nearestGate;
            if (_gateTransform != null)
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [TrueEnding] Gate resolved to '" + GateObjectName + "' at " + _gateTransform.position + " (" + nearestDist + "m from mark).");
            else
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [TrueEnding] No '" + GateObjectName + "' found near the mark — step 6 will have nothing to delete.");

            // Same real, already-textured 'vhs_tape' material TapeObjective
            // resolves for its own placeholder — a flat near-black box was
            // basically invisible in a dark room, this reads as an actual
            // tape.
            foreach (var mat in Resources.FindObjectsOfTypeAll<Material>())
            {
                if (mat != null && mat.name == "vhs_tape")
                {
                    _tapeMaterial = mat;
                    KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [TrueEnding] Real 'vhs_tape' material resolved for tape placeholders.");
                    break;
                }
            }
            if (_tapeMaterial == null)
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [TrueEnding] 'vhs_tape' material not found — tapes will use a bright fallback color instead.");

            bool voiceValid = AudioController.IsValidAudioID(GoAwayMamaAudioID);
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [TrueEnding] Voice line '" + GoAwayMamaAudioID + "' valid AudioController ID=" + voiceValid);

            foreach (string candidate in PortalAudioCandidates)
            {
                bool valid = AudioController.IsValidAudioID(candidate);
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [TrueEnding] Portal audio candidate '" + candidate + "' valid=" + valid);
                if (valid && _portalAudioID == null) _portalAudioID = candidate;
            }
            if (_portalAudioID == null)
            {
                _portalAudioID = PortalAudioFallback;
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [TrueEnding] None of the portal audio candidates resolved — falling back to '" + PortalAudioFallback + "'.");
            }
            else
            {
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [TrueEnding] Portal will play '" + _portalAudioID + "'.");
            }

            _resolving = false;
            SpawnInitial();
        }

        // Marked positions were captured from the player camera, not exactly
        // floor height — same small correction every other hand-marked
        // objective in this project applies.
        private static Vector3 SnapToGround(Vector3 pos)
        {
            RaycastHit hit;
            if (Physics.Raycast(pos + Vector3.up * 0.5f, Vector3.down, out hit, 2f))
                return new Vector3(pos.x, hit.point.y, pos.z);
            return pos;
        }

        // Friend/Diamond marks were taken standing on top of a box, at eye
        // height. SnapToGround's wider 2f search punched straight through
        // the box onto the true floor below it (way too low). Raw,
        // unsnapped placed them floating at full eye height (too high — see
        // "put the boxes a bit down"). This is the middle ground: a small
        // window right below the mark, just enough to catch the box's own
        // top surface without reaching the floor beneath it.
        private static Vector3 SnapToNearbySurface(Vector3 pos)
        {
            RaycastHit hit;
            if (Physics.Raycast(pos + Vector3.up * 0.2f, Vector3.down, out hit, 0.6f))
                return new Vector3(pos.x, hit.point.y, pos.z);
            return pos;
        }

        // Strictly sequential — each step only spawns once the one before
        // it is complete: Tape 1 (spawns immediately) -> Tape 2 -> Friend 1
        // -> Friend 2 -> Diamond Tattletail -> (step 6) gate deleted.
        private void SpawnInitial()
        {
            _tape1GO = BuildTape(SnapToGround(Tape1Pos), "KE_TrueEnding_Tape1");
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [TrueEnding] Step 1 (Tape 1) spawned. Steps 2-5 unlock one at a time as each prior step completes.");
            SpawnBoxWalls();
        }

        // Only BoxCorner1/BoxCorner2 are actually open space ("looking at
        // nothing within 5m") — the true outer edge past the portal, where
        // there's nothing to stop the player from wandering into the void.
        // BoxCorner3/BoxCorner4 were pointing at real, already-solid
        // 'chainlink_group-1' fence pieces — not a request for an extra
        // wall there. A full 4-sided box (the first attempt) put a wall
        // right across the entrance path where the deleted gate used to be
        // — this is a single thin wall spanning just the two open-space
        // marks, nowhere near the way in.
        private void SpawnBoxWalls()
        {
            Vector3 a = BoxCorner1;
            Vector3 b = BoxCorner2;
            Vector3 dir = b - a;
            float length = dir.magnitude;

            const float thickness = 0.3f;
            const float wallHeight = 6f;
            const float wallBottomY = -1f; // a bit below the marked ground height so uneven terrain can't leave a gap underneath
            float centerY = wallBottomY + wallHeight / 2f;
            Vector3 mid = (a + b) / 2f;

            GameObject go = new GameObject("KE_TrueEnding_Wall_Outer");
            go.transform.position = new Vector3(mid.x, centerY, mid.z);
            go.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(thickness, wallHeight, length); // thin along local X (blocks crossing), full length along local Z (along the line between the marks)
            col.isTrigger = false; // solid — physically blocks the player, no renderer so it stays invisible

            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [TrueEnding] Outer boundary wall built between " + a + " and " + b + ".");
        }

        // Real, self-lit 'KaleidoscopePortal' prop, scavenged from the
        // actual 'house_kaleidoscope' scene — see KaleidoscopePortalScavenger.cs
        // (not currently auto-triggered, so CapturedTemplate stays null and
        // this always falls back for now). The fallback is our own portal:
        // a swirling vortex with a rainbow rim and sparks pulled in (PortalFx),
        // standing upright, facing along Z.
        private void SpawnPortal()
        {
            _portalGO = new GameObject("KE_TrueEnding_Portal");
            _portalGO.transform.position = PortalPos;

            var light = _portalGO.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.6f, 0.2f, 0.9f);
            light.range = 8f;
            light.intensity = 1.5f;

            GameObject realTemplate = KaleidoscopePortalScavenger.CapturedTemplate;
            if (realTemplate != null)
            {
                GameObject real = Instantiate(realTemplate, _portalGO.transform);
                real.transform.localPosition = Vector3.zero;
                real.transform.localRotation = Quaternion.identity;
                real.SetActive(true);
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [TrueEnding] Step 7 (portal) placed at " + PortalPos + " using the real scavenged 'KaleidoscopePortal' asset.");
            }
            else
            {
                PortalFx.Build(_portalGO.transform, light);
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [TrueEnding] Step 7 (portal) placed at " + PortalPos + ".");
            }

            AudioController.Play(_portalAudioID, PortalPos, _portalGO.transform, 1f);
        }

        // Simple proximity check instead of a Unity trigger collider —
        // avoids any CharacterController/trigger-detection quirks, cheap
        // enough to just poll every frame while the portal exists.
        private void Update()
        {
            if (_portalGO == null || _portalEntered) return;
            if (RM.fpsController == null) return;
            if (Vector3.Distance(RM.fpsController.transform.position, PortalPos) > PortalEnterRadius) return;

            _portalEntered = true;
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [TrueEnding] Step 7 — player entered the portal, returning to menu.");
            EndlessRunManager.Instance?.EndRun(false); // same "deliberate exit, not a death" path the exfil phone uses
        }

        private void SpawnTape2IfReady()
        {
            if (_tape2GO != null) return;
            _tape2GO = BuildTape(SnapToGround(Tape2Pos), "KE_TrueEnding_Tape2");
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [TrueEnding] Step 2 (Tape 2) spawned.");
        }

        // No SnapToGround here — these marks were taken standing right on
        // top of a specific box, and the raycast was dragging the clone
        // down past the box onto the true floor below it — see
        // SnapToNearbySurface's comment for the fix.
        private void SpawnFriend1IfReady()
        {
            if (_friend1GO != null) return;
            _friend1GO = BuildFriend(SnapToNearbySurface(Friend1Pos), "KE_TrueEnding_Friend1", false);
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [TrueEnding] Step 3 (Friend 1) spawned.");
        }

        private void SpawnFriend2IfReady()
        {
            if (_friend2GO != null) return;
            _friend2GO = BuildFriend(SnapToNearbySurface(Friend2Pos), "KE_TrueEnding_Friend2", false);
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [TrueEnding] Step 4 (Friend 2) spawned.");
        }

        private void SpawnDiamondIfReady()
        {
            if (_diamondGO != null) return;
            _diamondGO = BuildFriend(SnapToNearbySurface(DiamondPos), "KE_TrueEnding_Diamond", true);
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [TrueEnding] Step 5 (Diamond Tattletail) spawned.");
        }

        private GameObject BuildTape(Vector3 pos, string name)
        {
            const float thickness = 0.03f;
            GameObject go = new GameObject(name);
            go.transform.position = pos + Vector3.up * (thickness / 2f);

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.transform.SetParent(go.transform, false);
            visual.transform.localScale = new Vector3(0.19f, thickness, 0.11f);
            Destroy(visual.GetComponent<Collider>());
            var rend = visual.GetComponent<Renderer>();
            if (rend != null)
            {
                if (_tapeMaterial != null)
                    rend.material = _tapeMaterial;
                else
                    rend.material.color = new Color(0.8f, 0.75f, 0.1f); // bright fallback — a flat near-black box was invisible in a dark room
            }

            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(0.4f, 0.15f, 0.3f);
            col.isTrigger = true;

            Interactable[] existingInteractables = FindObjectsOfType<Interactable>();
            if (existingInteractables.Length > 0)
                go.layer = existingInteractables[0].gameObject.layer;

            var interactable = go.AddComponent<Interactable>();
            interactable.actionText = "Rewind Tape";
            interactable.holdTime = 2f;
            interactable.highlight = true;
            interactable.setInactiveOnInteract = false;
            interactable.honks = false;
            interactable.newStateOnInteract = Interactable.State.Enabled;
            interactable.currentState = Interactable.State.Enabled;
            interactable.holdAudioName = string.Empty;
            interactable.onInteractEvent = new UnityEngine.Events.UnityEvent();
            interactable.conditions = new InteractableConditions();
            interactable.actions = new InteractableActions { interactActions = new InteractableActions.Actions[0] };

            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [TrueEnding] '" + name + "' placed at " + pos);
            return go;
        }

        // Clones the player's own real Tattletail model (RM.tattletail.
        // tattletail) — always present and active for the whole run, unlike
        // the decorative 'tattletail3@Friend_N' props, which this same mod
        // deactivates at run start (see SceneBootstrap's FRIEND-REMOVE
        // pass), so a template search for those could lose the race and
        // fall back to a bare capsule. GoldRecolor/DiamondRecolor already
        // treat this exact object as a pure visual-only sub-object (no
        // TattletailController on it — that lives on FPSController), so
        // cloning it here is the same safe, established pattern.
        private GameObject BuildFriend(Vector3 pos, string name, bool applyDiamond)
        {
            GameObject template = RM.tattletail != null ? RM.tattletail.tattletail : null;
            GameObject go;
            if (template != null)
            {
                go = Instantiate(template, pos, template.transform.rotation);
                PartyTattletailModels.StandLikePartyTattletail(go);
                go.SetActive(true);
                foreach (Interactable existing in go.GetComponentsInChildren<Interactable>(true))
                    DestroyImmediate(existing);
                foreach (Trigger trigger in go.GetComponentsInChildren<Trigger>(true))
                    DestroyImmediate(trigger);
            }
            else
            {
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [TrueEnding] RM.tattletail.tattletail not available — '" + name + "' will use a placeholder capsule.");
                go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.transform.position = pos;
            }
            go.name = name;

            if (applyDiamond) DiamondRecolor.Apply(go);

            Collider existingCol = go.GetComponent<Collider>();
            if (existingCol != null) Destroy(existingCol);
            var box = go.AddComponent<BoxCollider>();
            box.size = Vector3.one * 1.0f;
            box.isTrigger = true;

            Interactable[] existingInteractables = FindObjectsOfType<Interactable>();
            if (existingInteractables.Length > 0)
                go.layer = existingInteractables[0].gameObject.layer;

            var interactable = go.AddComponent<Interactable>();
            interactable.actionText = "Say Hi";
            interactable.holdTime = 1.2f;
            interactable.highlight = true;
            interactable.setInactiveOnInteract = false;
            interactable.honks = false;
            interactable.newStateOnInteract = Interactable.State.Enabled;
            interactable.currentState = Interactable.State.Enabled;
            interactable.holdAudioName = string.Empty;
            interactable.onInteractEvent = new UnityEngine.Events.UnityEvent();
            interactable.conditions = new InteractableConditions();
            interactable.actions = new InteractableActions { interactActions = new InteractableActions.Actions[0] };

            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [TrueEnding] '" + name + "' placed at " + pos + (applyDiamond ? " (Diamond)" : ""));
            return go;
        }

        /// <summary>Called from Patch_Interactable_OnInteractComplete_TrueEndingEasterEgg — returns true if this interactable belonged to the easter egg (and was handled).</summary>
        public bool TryHandleInteract(Interactable interactable)
        {
            if (interactable == null) return false;
            GameObject go = interactable.gameObject;

            if (!_tape1Done && go == _tape1GO)
            {
                _tape1Done = true;
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [TrueEnding] Step 1 (Tape 1) complete.");
                SpawnTape2IfReady();
                CheckGate();
                Destroy(_tape1GO);
                _tape1GO = null;
                return true;
            }
            if (!_tape2Done && go == _tape2GO)
            {
                _tape2Done = true;
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [TrueEnding] Step 2 (Tape 2) complete.");
                SpawnFriend1IfReady();
                CheckGate();
                Destroy(_tape2GO);
                _tape2GO = null;
                return true;
            }
            if (!_friend1Done && go == _friend1GO)
            {
                _friend1Done = true;
                PlayGoAwayMama(interactable.transform.position, "Friend 1");
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [TrueEnding] Step 3 (Friend 1) complete.");
                SpawnFriend2IfReady();
                CheckGate();
                Destroy(_friend1GO);
                _friend1GO = null;
                return true;
            }
            if (!_friend2Done && go == _friend2GO)
            {
                _friend2Done = true;
                PlayGoAwayMama(interactable.transform.position, "Friend 2");
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [TrueEnding] Step 4 (Friend 2) complete.");
                SpawnDiamondIfReady();
                CheckGate();
                Destroy(_friend2GO);
                _friend2GO = null;
                return true;
            }
            if (!_diamondDone && go == _diamondGO)
            {
                _diamondDone = true;
                PlayGoAwayMama(interactable.transform.position, "Diamond Tattletail");
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [TrueEnding] Step 5 (Diamond Tattletail) complete.");
                CheckGate();
                Destroy(_diamondGO);
                _diamondGO = null;
                return true;
            }
            return false;
        }

        private static void PlayGoAwayMama(Vector3 pos, string label)
        {
            AudioController.Play(GoAwayMamaAudioID, pos, null, 1f);
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [TrueEnding] " + label + " said '" + GoAwayMamaAudioID + "'.");
        }

        private void CheckGate()
        {
            if (_gateDeleted) return;
            if (!(_tape1Done && _tape2Done && _friend1Done && _friend2Done && _diamondDone)) return;
            _gateDeleted = true;

            if (_gateTransform != null)
            {
                Destroy(_gateTransform.gameObject);
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [TrueEnding] Step 6 — all of steps 1-5 complete, gate '" + GateObjectName + "' deleted.");
            }
            else
            {
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [TrueEnding] All of steps 1-5 complete but the gate was never resolved — nothing to delete.");
            }

            SpawnPortal(); // only appears once the gate is actually gone — not present from run start
        }

        public void CleanUp()
        {
            if (_tape1GO != null) Destroy(_tape1GO);
            if (_tape2GO != null) Destroy(_tape2GO);
            if (_friend1GO != null) Destroy(_friend1GO);
            if (_friend2GO != null) Destroy(_friend2GO);
            if (_diamondGO != null) Destroy(_diamondGO);
            if (_portalGO != null) Destroy(_portalGO);
            _tape1GO = _tape2GO = _friend1GO = _friend2GO = _diamondGO = _portalGO = null;
            _tape1Done = _tape2Done = _friend1Done = _friend2Done = _diamondDone = _gateDeleted = _portalEntered = false;
            _gateTransform = null; // re-resolved next run via ResolveLocations()
            _portalAudioID = null; // re-resolved next run via ResolveLocations()
            _resolving = false;
        }
    }
}
