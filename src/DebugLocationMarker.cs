using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// Temporary debug tool: press E anytime during a run to log the
    /// player's exact position (plus whatever's directly in front, if
    /// anything) — lets the user walk to wherever they actually want an
    /// object placed and mark it directly, instead of guessing coordinates
    /// or being stuck with wherever an existing prop happens to sit.
    /// </summary>
    public class DebugLocationMarker : MonoBehaviour
    {
        public static DebugLocationMarker Instance { get; private set; }

        private int _markCount;

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

        private void Update()
        {
            // The arcade hub has its own E tool (HubMarkTool).
            if (EndlessRunManager.Instance == null || !EndlessRunManager.Instance.RunActive) return;
            if (RM.fpsController == null) return;
            if (!Input.GetKeyDown(KeyCode.E)) return;

            _markCount++;
            Transform cam = RM.fpsController.transform;

            RaycastHit hit;
            string hitInfo = Physics.Raycast(cam.position, cam.forward, out hit, 5f)
                ? "'" + hit.collider.gameObject.name + "' at " + hit.point
                : "nothing within 5m";

            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [MARK " + _markCount + "] playerPos=" + cam.position + " looking at " + hitInfo);
        }
    }
}
