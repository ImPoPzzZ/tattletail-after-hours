using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// Builds a decorative, inert clone of the real Tattletail model in a random
    /// color — every color the mod actually has: the vanilla Yellow/Teal/Black/
    /// White, plus the mod's own Gold and Diamond, alongside the plain Original.
    /// Same "clone the real model, strip everything live off it" recipe as
    /// BirthdayDecor's Mama. Used for the party minigame tattletails — placement
    /// and the game itself land once the spots are marked.
    /// </summary>
    public static class PartyTattletailModels
    {
        private const float SCALE_MULT = 2.2f; // the real model reads far too small standing alone — bulk it up

        private static readonly RecolorType[] VanillaColors =
        {
            RecolorType.ORIGINAL, RecolorType.YELLOW, RecolorType.TEAL, RecolorType.BLACK, RecolorType.WHITE,
        };

        /// <summary>Clone of the in-hand Tattletail -> party size, standing upright (it inherits the tilt of your arms otherwise). Used by the Friend objective and the True Ending too.</summary>
        public static void StandLikePartyTattletail(GameObject clone)
        {
            clone.transform.rotation = Quaternion.identity;
            clone.transform.localScale *= SCALE_MULT;
        }

        /// <summary>Clones the real Tattletail, gives it a random color, and drops any live behaviour so it just stands there. Returns null if the real model isn't available (not mid-run).</summary>
        public static GameObject BuildRandomColored(Vector3 pos, Quaternion rotation, Transform parent)
        {
            string label;
            return BuildRandomColored(pos, rotation, parent, out label);
        }

        /// <summary>Same, and says which color it got ("Purple", "Yellow", "Teal", "Black", "White", "Gold", "Diamond").</summary>
        public static GameObject BuildRandomColored(Vector3 pos, Quaternion rotation, Transform parent, out string label)
        {
            return BuildColored(pos, rotation, parent, -1, out label);
        }

        /// <summary>How many colors there are to pick from (the vanilla five, Gold, Diamond).</summary>
        public static int ColorCount { get { return VanillaColors.Length + 2; } }

        /// <summary>A party Tattletail in color number 'pick' (0..ColorCount-1; -1 = random).</summary>
        public static GameObject BuildColored(Vector3 pos, Quaternion rotation, Transform parent, int pick, out string label)
        {
            label = null;
            GameObject source = PristineTattletail.Source; // untouched: its random color is its own, not the player's
            if (source == null)
            {
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Party] no real Tattletail model to clone for a party tattletail.");
                return null;
            }

            var clone = Object.Instantiate(source);
            clone.name = "KE_PartyTattletail";
            clone.transform.SetParent(parent, true);
            MakeInert(clone);
            clone.transform.position = pos;
            StandLikePartyTattletail(clone);
            clone.transform.rotation = rotation;
            clone.SetActive(true);

            label = ApplyColor(clone, pick);
            return clone;
        }

        internal static string ApplyRandomColor(GameObject clone) { return ApplyColor(clone, -1); }

        private static string ApplyColor(GameObject clone, int pick)
        {
            // 5 vanilla picks (incl. Original) + Gold + Diamond = every color the mod has, equal odds.
            if (pick < 0 || pick >= ColorCount) pick = Random.Range(0, ColorCount);
            string label;
            if (pick < VanillaColors.Length)
            {
                RecolorType type = VanillaColors[pick];
                if (type != RecolorType.ORIGINAL && RM.recolorManager != null)
                    RM.recolorManager.RecolorObject(clone, type);
                label = type.ToString();
            }
            else if (pick == VanillaColors.Length)
            {
                GoldRecolor.Apply(clone);
                label = "Gold";
            }
            else
            {
                DiamondRecolor.Apply(clone);
                label = "Diamond";
            }
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Party] Party tattletail colored: " + label + ".");
            switch (label)
            {
                case "ORIGINAL": return "Purple";
                case "YELLOW": return "Yellow";
                case "TEAL": return "Teal";
                case "BLACK": return "Black";
                case "WHITE": return "White";
                default: return label;
            }
        }

        // Strips anything that would make the clone act like a live, player-controlled or
        // AI Tattletail (scripts, colliders, physics, sound) — same approach BirthdayDecor
        // uses for its cloned Mama. The fur shader controller is kept so the coat still renders.
        private static void MakeInert(GameObject clone)
        {
            foreach (var mb in clone.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || mb is FurShaderControllerV1) continue;
                try { Object.DestroyImmediate(mb); } catch { }
            }
            foreach (var c in clone.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var rb in clone.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(rb);
            foreach (var a in clone.GetComponentsInChildren<AudioSource>(true)) Object.DestroyImmediate(a);
            foreach (var l in clone.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(l);
            foreach (var an in clone.GetComponentsInChildren<Animator>(true))
            {
                // Root motion stays off (it would drag the clone away from its marked spot,
                // and would fight the castle-bounce animation), but the animator itself is
                // left running — whatever its default/idle state is plays on its own, instead
                // of a completely frozen pose.
                an.applyRootMotion = false;
                an.speed = 1f;
            }
        }
    }
}
