using System.Reflection;
using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// Thin wrapper around the game's MamaManager that adds difficulty scaling
    /// and hooks for our objective system.
    /// </summary>
    public class MamaController : MonoBehaviour
    {
        public static MamaController Instance { get; private set; }

        // Cached references
        private MamaManager _mamaManager;
        private FieldInfo _alertTimerField;
        private FieldInfo _alertTimerMaxField;
        private FieldInfo _minimumNodePatrolTimeField; // on Mama component

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Cache reflection fields for private values we need to tweak
            _alertTimerField      = typeof(MamaManager).GetField("currentAlertTimer",      System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            _alertTimerMaxField   = typeof(MamaManager).GetField("alertTimerMax",           System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            _minimumNodePatrolTimeField = typeof(Mama).GetField("minimumNodePatrolTime",   System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── Public API ───────────────────────────────────────────────────────

        // Power Up spot marking is done (positions confirmed and hardcoded
        // in PowerUpObjective.cs). Mama's back on.
        public static bool DebugDisableMama = false;

        /// <summary>Music Box gadget: Mama can't (re)activate before this Time.time. 0 = no suppression.</summary>
        public static float SuppressedUntil;

        public void ActivateMamaForObjective(float difficultyMult)
        {
            if (DebugDisableMama)
            {
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Mama activation skipped (DebugDisableMama is true).");
                return;
            }

            if (EndlessRunManager.Instance != null && EndlessRunManager.Instance.InRestPhase)
            {
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Mama activation skipped — rest period.");
                return;
            }

            // 3.7 (Professional): her first alert in an objective comes later.
            float delay = EndlessTraits.TakeMamaDelay();
            if (delay > 0f && !VersusMode.IsSurvivor)
            {
                StartCoroutine(ActivateLater(difficultyMult, delay));
                return;
            }

            if (Time.time < SuppressedUntil)
            {
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Mama activation skipped — the Music Box is playing.");
                return;
            }

            // Never let her (re)activate mid-call — EndlessRunManager.
            // ResumeMamaAfterMissedCall() retries this the moment the call
            // resolves (answered ends the run anyway; missed retries here).
            if (EndlessRunManager.Instance != null && EndlessRunManager.Instance.PhoneIsRinging)
            {
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Mama activation skipped — phone is ringing.");
                return;
            }

            // Versus survivor playtest: the "Mama" is an idle bot that just stands somewhere
            // (after the Music Box / phone checks above, so both still keep her away).
            if (VersusMode.IsSurvivor)
            {
                PlayHuntMusic(); // she's hunting — the chase track, same as Endless
                // Lights out for the hunt, same as Endless: Tattletail only chatters with the lights
                // on, and every word of it is a noise on the hunter's kill meter. (Same guard as below.)
                if (RM.lightsController != null && RM.lightsController.AreLightsOn())
                    RM.lightsController.RequestLightsOut(null);
                if (FactoryMode.EndlessPreview) FactoryController.SetHunt(true); // the Facility lights go out (and come back in DeactivateMama)
                if (VersusMode.OpponentIsBot) VersusBots.PlaceIdleMama();
                else VersusNet.SendMamaBack(); // the Mama player comes back at a random spot
                return; // vs a real Mama player the game's AI Mama never wakes
            }

            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [M1] ActivateMamaForObjective begin");
            EnsureMamaManager();
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [M2] EnsureMamaManager returned");
            if (_mamaManager == null)
            {
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: MamaManager not found — Mama cannot activate!");
                return;
            }

            if ((object)_alertTimerMaxField != null)
            {
                // alertTimerMax (confirmed via decompile) is how long she
                // stays in her fast "alert" state (QuickMama, MaxPatrolTime)
                // once triggered by SetAlert() — raised from 15 so she's
                // noticeably more active/aggressive even at base difficulty.
                float baseAlert = 22f;
                float scaled = baseAlert * difficultyMult * EndlessTraits.AlertMult();
                _alertTimerMaxField.SetValue(_mamaManager, scaled);
            }
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [M3] alert timer set");

            // Lights out whenever Mama is actively haunting, regardless of
            // which objective triggered her. Vanilla RequestLightsOut(),
            // when lights are already off, calls the callback directly
            // without a null-check ("if (!AreLightsOn()) { callback(); }")
            // — since lights now stay off between objectives instead of
            // toggling back on, that path fires on every subsequent call
            // and NREs on our null callback. Only call it while lights are
            // actually still on.
            if (RM.lightsController != null && RM.lightsController.AreLightsOn())
                RM.lightsController.RequestLightsOut(null);

            ScalePatrolSpeed(difficultyMult);
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [M4] ScalePatrolSpeed returned");

            try
            {
                // Mama's model/eye-cycling/recolor/bark-audio all read the
                // same per-level "kaleidoscope" flag (confirmed via
                // decompile) — flip it to match the player's menu choice
                // before she (re)builds herself below.
                var levelData = Utils.GetLevelFlow().GetLevelByEnum(GD.currentLevel);
                if (levelData != null)
                    levelData.kaleidoscope = EndlessRunManager.PendingKaleidoscopeMama;

                // Don't use CreateMamaSpawner/StartActivation — MamaSpawner
                // calls RequestLightsOut internally (conflicting with ours)
                // and calls RM.questOrder every frame (which we disabled).
                // Instead: call InitializeNewMama to build her model/script,
                // then ActivateMama directly to put her in PATROL state.
                // Reset patrol node set so InitializeNewMama can re-register it
                // (MamaManager.SetPatrolNodes errors if called twice without reset).
                var nodeField = typeof(MamaManager).GetField("currentPatrolNodeSet",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if ((object)nodeField != null)
                    nodeField.SetValue(_mamaManager, null);

                if (FactoryMode.EndlessPreview) FacilityMama.Unfreeze(_mamaManager);
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [M5] calling InitializeNewMama");
                _mamaManager.InitializeNewMama();
                if (FactoryMode.EndlessPreview) FacilityMama.UseNodes(_mamaManager); // (her nodes are the Facility's, set after she is built, as the Night Shift story did)
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [M6] InitializeNewMama returned, calling ActivateMama");
                _mamaManager.ActivateMama();
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [M7] ActivateMama returned — Mama patrolling (diff=" + difficultyMult + ")");
                if (EndlessTraits.MamaStartsAlerted()) StartAlerted(); // 3.7 (Unresolved Fear)

                // Play suspense music — normally MamaSpawner.StartActivation()
                // does this, but we bypassed it.
                PlayHuntMusic();
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [M7b] mama_music playing.");
                if (FactoryMode.EndlessPreview) FactoryController.SetHunt(true); // the Facility's lights go out

                // Mama's name is set to "MamaEvil" in InitializeNewMama.
                // Enable all renderers on her hierarchy.
                GameObject mamaGO = GameObject.Find("MamaEvil");
                if (mamaGO != null)
                {
                    foreach (Renderer r in mamaGO.GetComponentsInChildren<Renderer>(true))
                        r.enabled = true;
                    KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [M8] Mama found and renderers enabled.");
                }
                else
                {
                    KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [M8] MamaEvil GameObject not found.");
                }
            }
            catch (System.Exception e)
            {
                KaleidoscopeEndlessPlugin.Log.LogError("KE: Mama activation failed: " + e.Message + "\n" + e.StackTrace);
            }
        }

        private System.Collections.IEnumerator ActivateLater(float difficultyMult, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            var run = EndlessRunManager.Instance;
            if (run == null || !run.RunActive || run.InRestPhase) yield break;
            ActivateMamaForObjective(difficultyMult);
        }

        private void StartAlerted()
        {
            try
            {
                var set = typeof(MamaManager).GetMethod("SetAlert", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if ((object)set != null && _mamaManager != null) set.Invoke(_mamaManager, null);
            }
            catch (System.Exception e) { KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Traits] start alerted: " + e.Message); }
        }

        /// <summary>Flash Camera: Mama's model while she's out (patrolling, travelling or attacking), else null.</summary>
        public GameObject GetActiveMama()
        {
            if (VersusMode.IsSurvivor && VersusMode.OpponentIsBot) return VersusBots.ActiveIdleMama;
            if (VersusMode.IsSurvivor) return VersusAvatars.VisibleMama; // the Mama player's body (Flash Camera)
            EnsureMamaManager();
            if (_mamaManager == null || (object)_stateField == null || (object)_mamaField == null) return null;
            if ((MamaState)_stateField.GetValue(_mamaManager) == MamaState.INACTIVE) return null;
            var mama = _mamaField.GetValue(_mamaManager) as GameObject;
            return mama != null && mama.activeInHierarchy ? mama : null;
        }

        /// <summary>Toy Parrot: moves an active Mama to the patrol spot nearest pos (same floor). False if she isn't out.</summary>
        public bool LureTo(Vector3 pos)
        {
            if (VersusMode.IsSurvivor && VersusMode.OpponentIsBot) return VersusBots.MoveIdleMamaNear(pos); // Toy Parrot in Versus
            if (VersusMode.IsSurvivor) { VersusNet.SendParrotLure(pos); return true; }
            try
            {
                if (GetActiveMama() == null) return false;
                var script = (object)_mamaScriptField != null ? /* (object): FieldInfo != null is .NET 4 only */ _mamaScriptField.GetValue(_mamaManager) as Mama : null;
                var set = (object)_nodeSetField != null ? _nodeSetField.GetValue(_mamaManager) as PatrolNodeSet : null;
                if (script == null || set == null || set.patrolNodes == null) return false;

                bool above = _mamaManager.IsAboveGround(pos);
                PatrolNode best = null;
                float bestDist = float.MaxValue;
                foreach (var node in set.patrolNodes)
                {
                    if (node == null || _mamaManager.IsAboveGround(node.transform.position) != above) continue;
                    float d = Vector3.Distance(node.transform.position, pos);
                    if (d < bestDist) { bestDist = d; best = node; }
                }
                if (best == null) return false;
                if (script.currentPatrolNode == best) return true; // already there
                script.MoveToNode(best);
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Parrot] Mama lured to the spot " + bestDist.ToString("F1") + "m from the parrot.");
                return true;
            }
            catch (System.Exception e)
            {
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Parrot] Couldn't lure Mama: " + e.Message);
                return false;
            }
        }

        private static readonly FieldInfo _mamaScriptField = typeof(MamaManager).GetField("mamaScript", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo _nodeSetField = typeof(MamaManager).GetField("currentPatrolNodeSet", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo _stateField = typeof(MamaManager).GetField("currentState", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo _mamaField = typeof(MamaManager).GetField("mama", BindingFlags.NonPublic | BindingFlags.Instance);

        /// <summary>The game's own chase track (normally MamaSpawner starts it), heard everywhere.</summary>
        public static void PlayHuntMusic()
        {
            if (AudioController.IsPlaying("mama_music")) return;
            AudioItem musicItem = AudioController.GetAudioItem("mama_music");
            if (musicItem != null)
            {
                musicItem.overrideAudioSourceSettings = true;
                musicItem.audioSource_MaxDistance = 500f;
                musicItem.spatialBlend = 0f;
            }
            AudioController.Play("mama_music");
        }

        public static void StopHuntMusic() { AudioController.Stop("mama_music"); }

        public void DeactivateMama()
        {
            // Versus: rest periods, the phone break and the Music Box really are Mama-free.
            if (VersusMode.IsSurvivor && VersusMode.OpponentIsBot) VersusBots.HideIdleMama();
            if (VersusMode.IsSurvivor && !VersusMode.OpponentIsBot) VersusNet.SendMamaAway();
            EnsureMamaManager();
            _mamaManager?.DeactivateMama();
            if (FactoryMode.EndlessPreview) FactoryController.SetHunt(false); // the Facility's lights come back
            AudioController.Stop("mama_music");
            // Lights stay off for the whole run — don't turn them back on
            // between objectives.
        }

        public void TriggerAlert()
        {
            EnsureMamaManager();
            if (_mamaManager == null) return;
            // Force an alert by setting the alert timer directly
            if ((object)_alertTimerField != null)
            {
                float alertVal = (object)_alertTimerMaxField != null
                    ? (float)_alertTimerMaxField.GetValue(_mamaManager)
                    : 15f;
                _alertTimerField.SetValue(_mamaManager, alertVal);
            }
            _mamaManager.MakeSpawnerManual(); // no-op if spawnerRef is null
        }

        public void TriggerJumpscare()
        {
            EnsureMamaManager();
            _mamaManager?.RequestJumpscareDeath(false);
        }

        /// <summary>Totem of Undying save: she leaves your current position and reappears elsewhere — just a deactivate followed by a delayed re-activation (InitializeNewMama picks a fresh spawn/patrol start each time, same as any other objective transition), no new mechanics needed.</summary>
        public void RelocateAway(float difficultyMult, float delaySeconds = 4f)
        {
            DeactivateMama();
            StartCoroutine(ReactivateAfterDelay(difficultyMult, delaySeconds));
        }

        private System.Collections.IEnumerator ReactivateAfterDelay(float difficultyMult, float delaySeconds)
        {
            yield return new WaitForSeconds(delaySeconds);
            ActivateMamaForObjective(difficultyMult);
        }

        // ── Private helpers ──────────────────────────────────────────────────

        /// <summary>
        /// The real Mama model, for cloning as set dressing (Papa's 10th
        /// Birthday!). Uses her existing model if the level already has one,
        /// otherwise builds it the same way an objective does — without
        /// activating her.
        /// </summary>
        public GameObject GetMamaModelSource()
        {
            EnsureMamaManager();
            if (_mamaManager == null) return null;
            GameObject existing = ReadMamaObject();
            if (existing != null) return existing;

            try
            {
                var levelData = Utils.GetLevelFlow().GetLevelByEnum(GD.currentLevel);
                if (levelData != null) levelData.kaleidoscope = false; // the regular Mama, not the Kaleidoscope one
                var nodeField = typeof(MamaManager).GetField("currentPatrolNodeSet",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if ((object)nodeField != null) nodeField.SetValue(_mamaManager, null);
                _mamaManager.InitializeNewMama();
            }
            catch (System.Exception e)
            {
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Birthday] GetMamaModelSource — InitializeNewMama failed: " + e.Message);
            }
            return ReadMamaObject();
        }

        // MamaManager.mama isn't accessible from here — read it by reflection.
        private static readonly System.Reflection.FieldInfo MamaObjectField =
            typeof(MamaManager).GetField("mama", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        private GameObject ReadMamaObject()
        {
            if (_mamaManager == null || (object)MamaObjectField == null) return null;
            return MamaObjectField.GetValue(_mamaManager) as GameObject;
        }

        private void EnsureMamaManager()
        {
            if (_mamaManager == null)
            {
                _mamaManager = Object.FindObjectOfType<MamaManager>();
                if (_mamaManager == null)
                    KaleidoscopeEndlessPlugin.Log.LogWarning("KE: FindObjectOfType<MamaManager>() found nothing in this scene.");
                else
                    KaleidoscopeEndlessPlugin.Log.LogInfo("KE: MamaManager found on GameObject '" + _mamaManager.gameObject.name + "'.");
            }

            if (RM.patrolNodeSet == null)
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: RM.patrolNodeSet is null — Mama.InitializeNewMama() will silently refuse to create Mama.");
        }

        private void ScalePatrolSpeed(float difficultyMult)
        {
            // Each Mama component has a minimumNodePatrolTime that controls how
            // long she stays on a node. Lower = moves faster. Lowered from
            // 10 so she's already briskly patrolling at base difficulty,
            // not just faster as loops progress.
            Mama mama = Object.FindObjectOfType<Mama>();
            if (mama == null || (object)_minimumNodePatrolTimeField == null) return;

            float baseTime = 6f;
            float scaled = Mathf.Max(2f, baseTime / difficultyMult / EndlessTraits.PatrolMult());
            _minimumNodePatrolTimeField.SetValue(mama, scaled);
        }
    }
}
