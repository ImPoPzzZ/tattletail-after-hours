using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// Two ways to get the real 'KaleidoscopePortal' prop (Transform +
    /// Animator, confirmed via a real scene load — see the removed
    /// [PORTAL-SCAVENGE] logs) instead of TrueEndingEasterEgg's placeholder
    /// rings:
    ///
    /// 1. TryResourcesLoad() — free. Resources.Load works regardless of
    ///    which scene is currently loaded, no scene load/unload at all, if
    ///    (and only if) the original developer actually placed this prop
    ///    under a Resources folder. Safe to call unconditionally — a no-op
    ///    if it isn't. Called automatically at every main-menu visit.
    ///
    /// 2. ScavengeViaSceneLoad() — the fallback if #1 comes back empty.
    ///    Additively loads the real 'house_kaleidoscope' scene (confirmed
    ///    build index 15) purely to clone the prop before unloading again —
    ///    same trick Night2AudioScavenger uses for dryer_clunk, extended
    ///    across a whole GameObject instead of a plain asset. That scene is
    ///    a real ~8MB level, and doing this at every menu visit caused a
    ///    visible disruption — NOT auto-invoked; call explicitly if/when
    ///    the trade-off (a hitch, once) is worth it for the real asset.
    /// </summary>
    public class KaleidoscopePortalScavenger : MonoBehaviour
    {
        public static GameObject CapturedTemplate { get; private set; }
        private static bool _resourcesLoadAttempted;
        private static bool _sceneLoadAttempted;

        public static KaleidoscopePortalScavenger Instance { get; private set; }

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

        /// <summary>Free, instant, no scene load — call this unconditionally. No-op if the prop isn't Resources-loadable.</summary>
        public static void TryResourcesLoad()
        {
            if (_resourcesLoadAttempted || CapturedTemplate != null) return;
            _resourcesLoadAttempted = true;

            GameObject viaResources = Resources.Load<GameObject>("KaleidoscopePortal");
            if (viaResources == null)
            {
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [PORTAL-SCAVENGE] 'KaleidoscopePortal' isn't a Resources-loadable asset — would need ScavengeViaSceneLoad() (not auto-triggered, real scene load, real hitch).");
                return;
            }

            GameObject clone = UnityEngine.Object.Instantiate(viaResources);
            clone.name = "KE_KaleidoscopePortalTemplate";
            UnityEngine.Object.DontDestroyOnLoad(clone);
            clone.SetActive(false);
            CapturedTemplate = clone;
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [PORTAL-SCAVENGE] Found 'KaleidoscopePortal' directly via Resources.Load — no scene load needed at all.");
        }

        /// <summary>Real scene load — additively loads 'house_kaleidoscope', clones the prop, unloads. Causes a real hitch; call only when that trade-off is wanted.</summary>
        public static void ScavengeViaSceneLoad()
        {
            if (_sceneLoadAttempted || CapturedTemplate != null) return;
            _sceneLoadAttempted = true;
            if (Instance == null)
                new GameObject("KE_KaleidoscopePortalScavenger").AddComponent<KaleidoscopePortalScavenger>();
            Instance.StartCoroutine(Instance.ScavengeRoutine());
        }

        private IEnumerator ScavengeRoutine()
        {
            const string sceneName = "house_kaleidoscope";
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [PORTAL-SCAVENGE] Additively loading '" + sceneName + "' to search for the real KaleidoscopePortal prop...");

            AsyncOperation loadOp = null;
            try { loadOp = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive); }
            catch (System.Exception e)
            {
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [PORTAL-SCAVENGE] LoadSceneAsync('" + sceneName + "') threw: " + e.Message + " — aborting, portal keeps its placeholder look.");
                yield break;
            }
            if (loadOp == null)
            {
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [PORTAL-SCAVENGE] LoadSceneAsync('" + sceneName + "') returned null — aborting, portal keeps its placeholder look.");
                yield break;
            }
            while (!loadOp.isDone) yield return null;

            yield return new WaitForSeconds(1f); // let the scene's own Awake/Start settle before scanning

            Scene loadedScene = SceneManager.GetSceneByName(sceneName);
            Transform found = null;

            foreach (Transform t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (t == null || !t.gameObject.scene.IsValid()) continue;
                if (t.gameObject.scene != loadedScene) continue; // only this freshly-loaded scene, not our own house
                string nameLower = t.gameObject.name.ToLowerInvariant();
                if (!nameLower.Contains("kaleidoscope")) continue;
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [PORTAL-SCAVENGE] Candidate '" + t.gameObject.name + "' active=" + t.gameObject.activeInHierarchy + " pos=" + t.position);
                if (found == null && nameLower == "kaleidoscopeportal")
                    found = t;
            }

            if (found != null)
            {
                GameObject clone = Instantiate(found.gameObject, Vector3.zero, Quaternion.identity);
                clone.name = "KE_KaleidoscopePortalTemplate";
                DontDestroyOnLoad(clone);
                clone.SetActive(false);
                CapturedTemplate = clone;

                var componentNames = new System.Collections.Generic.List<string>();
                foreach (var c in found.GetComponents<Component>())
                    if (c != null) componentNames.Add(c.GetType().Name);
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [PORTAL-SCAVENGE] Captured real 'KaleidoscopePortal' — components: " + string.Join(", ", componentNames.ToArray()));
            }
            else
            {
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [PORTAL-SCAVENGE] No exact 'KaleidoscopePortal' match found in '" + sceneName + "' — portal keeps its placeholder look.");
            }

            var unloadOp = SceneManager.UnloadSceneAsync(sceneName);
            if (unloadOp != null)
                while (!unloadOp.isDone) yield return null;

            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [PORTAL-SCAVENGE] Done — '" + sceneName + "' unloaded.");
        }
    }
}
