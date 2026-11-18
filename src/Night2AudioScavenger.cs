using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// One-shot, main-menu-only diagnostic: additively loads Night 2's own
    /// level-specific scene purely to search it for the real washing-
    /// machine sound. Confirmed by direct player testimony to exist there
    /// ("I've heard it, it's on the second night") — and never found
    /// anywhere in Night 6/TheParty's own composition (house_night +
    /// 6_scene + jumpscare + Player) across many live scans, which is
    /// itself evidence this is genuinely Night-2-specific content rather
    /// than a timing problem within Night 6: Night 6 already loads the
    /// same shared 'house_night' base scene Night 2 would, so if the
    /// object lived there, our repeated Night 6 scans should have already
    /// found it.
    ///
    /// Scene name is inferred, not confirmed — GameManager.PlayLevel
    /// (confirmed via decompile) additively loads each level's
    /// LevelData.secondarySceneName, and Night 6's is observed to be
    /// "6_scene", so Night 2's is a well-grounded guess of "2_scene"
    /// following that same pattern, not a blind one. If wrong,
    /// LoadSceneAsync returns null and this logs a clear warning instead
    /// of silently failing.
    ///
    /// Only ever runs once per game process, only from the main menu —
    /// additively loading and unloading an unrelated level's scene mid-run
    /// is real, non-trivial risk this mod has no reason to take.
    /// </summary>
    public class Night2AudioScavenger : MonoBehaviour
    {
        public static Night2AudioScavenger Instance { get; private set; }
        private static bool _attempted;
        /// <summary>True while 2_scene is additively loaded — the arcade waits for this before loading its level.</summary>
        public static bool Busy;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            Busy = false;
            if (Instance == this) Instance = null;
        }

        public static void TryScavenge()
        {
            if (_attempted) return;
            _attempted = true;
            if (Instance == null)
                new GameObject("KE_Night2AudioScavenger").AddComponent<Night2AudioScavenger>();
            Busy = true;
            Instance.StartCoroutine(Instance.ScavengeThenFree());
        }

        private IEnumerator ScavengeThenFree()
        {
            yield return StartCoroutine(ScavengeRoutine());
            Busy = false;
        }

        private IEnumerator ScavengeRoutine()
        {
            const string sceneName = "2_scene";
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [N2-SCAVENGE] Additively loading '" + sceneName + "' to search for the real washing machine sound...");

            AsyncOperation loadOp = null;
            try { loadOp = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive); }
            catch (System.Exception e)
            {
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [N2-SCAVENGE] LoadSceneAsync('" + sceneName + "') threw: " + e.Message + " — scene name likely wrong, aborting.");
                yield break;
            }
            if (loadOp == null)
            {
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [N2-SCAVENGE] LoadSceneAsync('" + sceneName + "') returned null — scene name likely wrong, aborting.");
                yield break;
            }
            while (!loadOp.isDone) yield return null;

            yield return new WaitForSeconds(1.5f); // let the scene's own Awake/Start settle before scanning

            string[] keywords = { "washing", "dryer", "clank", "clunk", "wash" };
            AudioClip foundClip = null;
            string foundFrom = null;

            foreach (var src in Resources.FindObjectsOfTypeAll<AudioSource>())
            {
                if (src == null || src.clip == null) continue;
                string srcNameLower = src.gameObject.name.ToLowerInvariant();
                string clipNameLower = src.clip.name.ToLowerInvariant();
                foreach (string kw in keywords)
                {
                    if (srcNameLower.Contains(kw) || clipNameLower.Contains(kw))
                    {
                        KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [N2-SCAVENGE] Match: source='" + src.gameObject.name + "' clip='" + src.clip.name +
                            "' scene='" + src.gameObject.scene.name + "'");
                        if (foundClip == null) { foundClip = src.clip; foundFrom = src.gameObject.name; }
                        break;
                    }
                }
            }

            if (foundClip != null)
            {
                // Captured as a plain C# reference before the scene that
                // held it unloads — same principle WashingMachineObjective
                // already relies on for its own in-level clip capture
                // (the AudioClip asset itself stays valid regardless of
                // what happens to the AudioSource/GameObject that
                // originally carried it), just extended across a scene
                // unload instead of just an object destroy.
                WashingMachineObjective.SetScavengedClip(foundClip);
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [N2-SCAVENGE] Captured real clip '" + foundClip.name + "' (from '" + foundFrom +
                    "') — cached for WashingMachineObjective, will be used instead of the in-level 15s scan.");
            }
            else
            {
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [N2-SCAVENGE] No washing/dryer/clank/clunk/wash match found in '" + sceneName + "'.");
            }

            var unloadOp = SceneManager.UnloadSceneAsync(sceneName);
            if (unloadOp != null)
                while (!unloadOp.isDone) yield return null;

            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [N2-SCAVENGE] Done — '" + sceneName + "' unloaded.");
        }
    }
}
