using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// The daily objectives as three small papers pinned to the divider wall, just south of the mirror (so you do not have them behind you
    /// when you arrive). Each has its tier and reward written above it, always readable, and the goal with your progress on the paper. They are just notes: nothing to interact with.
    /// A paper you have finished is gone; when all three are gone a little note says to come back tomorrow.
    /// </summary>
    public static class DailyPapers
    {
        private class Paper { public GameObject Root; public Text Label, Face; public string Shown; }

        private static readonly Paper[] _papers = new Paper[3];
        private static GameObject _note, _reroll;

        // The divider wall (its west face is at x = 0.30), south of the mirror (z -5.6, 1.3 wide) and clear of the air hockey table.
        private const float WALL_X = 0.285f, FIRST_Z = -6.75f, STEP_Z = -0.62f, PAPER_Y = 1.45f;

        public static void Build(Transform room)
        {
            Material paper = Mat.Lit(new Color(0.93f, 0.9f, 0.8f), 0.05f), pin = Mat.Lit(new Color(0.8f, 0.15f, 0.15f), 0.6f);
            Color[] tierColor = { new Color(0.45f, 1f, 0.55f), new Color(1f, 0.85f, 0.3f), new Color(1f, 0.4f, 0.4f) };

            for (int tier = 0; tier < 3; tier++)
            {
                float z = FIRST_Z + tier * STEP_Z;
                var root = new GameObject("KE_DailyPaper" + tier);
                root.transform.SetParent(room, false);
                root.transform.localPosition = new Vector3(WALL_X, PAPER_Y + (tier == 1 ? 0.03f : 0f), z);
                root.transform.localRotation = Quaternion.Euler(0f, 270f, (tier - 1) * 2.5f);   // (faces west into the hall; a little crooked, like pinned notes)
                Transform t = root.transform;

                var sheet = Mat.Box(t, "Sheet", Vector3.zero, new Vector3(0.4f, 0.5f, 0.012f), paper);
                Object.Destroy(sheet.GetComponent<Collider>());
                var knob = Mat.Box(t, "Pin", new Vector3(0f, 0.2f, 0.012f), new Vector3(0.03f, 0.03f, 0.02f), pin);
                Object.Destroy(knob.GetComponent<Collider>());

                var p = new Paper { Root = root };
                // on the paper: the goal, small
                p.Face = WorldText.Create(t, "", new Color(0.12f, 0.1f, 0.08f), 0.034f, TextAnchor.MiddleCenter, 0.36f);
                p.Face.canvas.transform.localPosition = new Vector3(0f, -0.02f, 0.012f);
                p.Face.canvas.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                // above the paper: tier and reward, always readable
                p.Label = WorldText.Create(t, "", tierColor[tier], 0.07f, TextAnchor.MiddleCenter, 0.9f);
                p.Label.canvas.transform.localPosition = new Vector3(0f, 0.36f, 0.012f);
                p.Label.canvas.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

                _papers[tier] = p;
            }

            // the reroll paper: the one thing here you can use (once a day)
            _reroll = new GameObject("KE_DailyReroll");
            _reroll.transform.SetParent(room, false);
            _reroll.transform.localPosition = new Vector3(WALL_X, PAPER_Y - 0.1f, FIRST_Z + 3 * STEP_Z);
            _reroll.transform.localRotation = Quaternion.Euler(0f, 270f, 1.5f);
            Material rerollPaper = Mat.Lit(new Color(0.95f, 0.75f, 0.6f), 0.05f);
            var rs = Mat.Box(_reroll.transform, "Sheet", Vector3.zero, new Vector3(0.4f, 0.3f, 0.012f), rerollPaper);
            Object.Destroy(rs.GetComponent<Collider>());
            var rk = Mat.Box(_reroll.transform, "Pin", new Vector3(0f, 0.12f, 0.012f), new Vector3(0.03f, 0.03f, 0.02f), pin);
            Object.Destroy(rk.GetComponent<Collider>());
            var rt = WorldText.Create(_reroll.transform, "REROLL\nQUEST", new Color(0.3f, 0.08f, 0.05f), 0.05f, TextAnchor.MiddleCenter, 0.36f);
            rt.canvas.transform.localPosition = new Vector3(0f, -0.02f, 0.012f);
            rt.canvas.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var rcol = _reroll.AddComponent<BoxCollider>();
            rcol.size = new Vector3(0.6f, 0.5f, 0.4f);
            rcol.center = new Vector3(0f, 0f, 0.15f);
            rcol.isTrigger = true; // (never blocks you)
            Interactable rit = PartyGameRunner.MakeInteractable(_reroll, "Reroll Quest", 0.6f);
            rit.highlight = true;

            // "all done" note
            _note = new GameObject("KE_DailyNote");
            _note.transform.SetParent(room, false);
            _note.transform.localPosition = new Vector3(WALL_X, PAPER_Y, FIRST_Z + STEP_Z);
            _note.transform.localRotation = Quaternion.Euler(0f, 270f, 0f);
            var noteSheet = Mat.Box(_note.transform, "Sheet", Vector3.zero, new Vector3(0.5f, 0.3f, 0.012f), paper);
            Object.Destroy(noteSheet.GetComponent<Collider>());
            var nt = WorldText.Create(_note.transform, "All done.\nNew ones tomorrow.", new Color(0.12f, 0.1f, 0.08f), 0.04f, TextAnchor.MiddleCenter, 0.46f);
            nt.canvas.transform.localPosition = new Vector3(0f, 0f, 0.012f);
            nt.canvas.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            BuildHud(room);
            Refresh(true);
        }

        // ── The same three, always on the right side of the screen while you are in the Arcade ──
        private static Text _hudText;
        private static GameObject _hudRoot;

        private static void BuildHud(Transform room)
        {
            _hudRoot = new GameObject("KE_DailyHud");
            _hudRoot.transform.SetParent(room, false);
            var canvas = _hudRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;
            var scaler = _hudRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            var tgo = new GameObject("Text", typeof(RectTransform), typeof(Text), typeof(Shadow));
            tgo.transform.SetParent(_hudRoot.transform, false);
            var rt = (RectTransform)tgo.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);   // (the top right corner)
            rt.anchoredPosition = new Vector2(-36f, -28f);
            rt.sizeDelta = new Vector2(620f, 520f);
            _hudText = tgo.GetComponent<Text>();
            _hudText.font = MainMenuButtonInjector.RealFont != null ? MainMenuButtonInjector.RealFont : Resources.GetBuiltinResource<Font>("Arial.ttf");
            _hudText.fontSize = 24;
            _hudText.alignment = TextAnchor.UpperRight;
            _hudText.supportRichText = true;
            _hudText.raycastTarget = false;
            _hudText.color = new Color(0.92f, 0.9f, 0.84f);
            tgo.GetComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.9f);
        }

        private static void RefreshHud()
        {
            if (_hudText == null || _hudRoot == null) return;
            bool show = !OverlayPanel.AnyOpen && !PinballGame.Playing && !AirHockeyGame.Playing && !ArcadeAwakening.Running;
            if (_hudRoot.activeSelf != show) _hudRoot.SetActive(show);
            if (!show) return;
            var sb = new System.Text.StringBuilder("<size=26><color=#FFD633>DAILY OBJECTIVES</color></size>");
            if (Daily.AllDone) sb.Append("\nAll done. New ones tomorrow.");
            else
                for (int tier = 0; tier < 3; tier++)
                {
                    if (Daily.IsDone(tier)) continue;
                    Daily.Def d = Daily.Slot(tier);
                    string col = tier == 0 ? "#73FF8C" : tier == 1 ? "#FFD94D" : "#FF6666";
                    sb.Append("\n\n<color=").Append(col).Append(">").Append(Daily.TierNames[tier]).Append("  +").Append(Daily.Rewards[tier]).Append("</color>\n");
                    sb.Append(d.Text).Append("   ").Append(Mathf.Min(Daily.Progress(d), d.Target).ToString("N0")).Append(" / ").Append(d.Target.ToString("N0"));
                }
            _hudText.text = sb.ToString();
        }

        /// <summary>The reroll paper was used (once a day).</summary>
        public static void OnReroll(GameObject go)
        {
            if (go == null || _reroll == null || go != _reroll) return;
            HubMode.SetHudText(Daily.Reroll());
            Refresh(true);
        }

        /// <summary>Keeps the papers current while you are in the Arcade (spending shards counts too).</summary>
        public static void Refresh() { Refresh(false); }

        private static void Refresh(bool force)
        {
            if (_papers[0] == null || _papers[0].Root == null) return;
            bool all = Daily.AllDone;
            RefreshHud();
            if (_note != null) _note.SetActive(all);
            if (_reroll != null) _reroll.SetActive(!all && !Daily.RerollUsed); // (once a day; gone when used)
            for (int tier = 0; tier < 3; tier++)
            {
                Paper p = _papers[tier];
                if (p == null || p.Root == null) continue;
                bool done = Daily.IsDone(tier);
                if (p.Root.activeSelf == done) p.Root.SetActive(!done);
                if (done) continue;
                Daily.Def d = Daily.Slot(tier);
                int prog = Mathf.Min(Daily.Progress(d), d.Target);
                string shown = tier + "|" + d.Id + "|" + prog;
                if (!force && shown == p.Shown) continue;
                p.Shown = shown;
                p.Label.text = Daily.TierNames[tier] + "   +" + Daily.Rewards[tier];
                p.Face.text = d.Text + "\n" + prog.ToString("N0") + " / " + d.Target.ToString("N0");
            }
        }


}
}

namespace KaleidoscopeEndless
{
    // A finished hold on the reroll paper.
    [HarmonyLib.HarmonyPatch(typeof(Interactable), "OnInteractComplete")]
    public static class Patch_Interactable_OnInteractComplete_DailyReroll
    {
        static void Postfix(Interactable __instance, bool fromAutoComplete)
        {
            if (fromAutoComplete || !HubMode.Active || __instance == null || OverlayPanel.BlocksWorld) return;
            DailyPapers.OnReroll(__instance.gameObject);
        }
    }
}
