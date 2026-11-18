using System.Reflection;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// One-shot application of the three need-drain-rate Run Modifiers
    /// ("Batteries Not Included", "Always Hungry", "Brushing is Key").
    /// TattletailNeeds (confirmed via decompile) computes
    /// hungerDecreasePerSecond/batteryDecreasePerSecond/
    /// dirtinessDecreasePerSecond once in its own Awake() from
    /// maxX / xMaxTime, and never touches them again — no public API, no
    /// named multiplier to hook. Reflection is the only way in, same
    /// established pattern as MamaController's alertTimerMax/
    /// minimumNodePatrolTime access. Called once per run (StartRun()) —
    /// TattletailNeeds' Awake() reruns fresh on every level (re)load, so
    /// this can never compound across multiple runs.
    /// </summary>
    public static class RunModifiers
    {
        private const float FASTER_MULT = 1.5f; // "50% faster" — still literally true for battery, which has no baseline buff to interact with

        // Balance change: "+40% HP, don't let it fall that easily" on
        // hunger/dirtiness, battery deliberately excluded. maxHunger/
        // maxDirtiness are always a normalized 1.0 in the real vanilla
        // class (confirmed via decompile) — the need-bar UI isn't built
        // to render past that, so raising the max itself risks a broken-
        // looking overfilled bar. Expressed as a slower drain rate
        // instead: takes 40% longer to reach empty, same practical
        // effect, zero UI risk. Boredom isn't included — it's fully
        // locked for the whole run (see SceneBootstrap.StartEndless's
        // LockBoredom() call), so it never drains regardless of rate.
        //
        // When the matching "Always Hungry"/"Brushing is Key" modifier is
        // ALSO on, it doesn't stack as an independent 1.5x on top of the
        // already-slower buffed rate — doing that landed net FASTER than
        // unmodified vanilla (base * (1/1.4) * 1.5 ≈ base * 1.07),
        // completely erasing the whole point of the buff and then some.
        // Instead the modifier eats exactly half the buffer — "50% of
        // the 40% increase" — leaving a smaller but never-negative +20%
        // buffer instead of blowing past baseline vanilla.
        private const float NEED_BUFF_MULT = 1f / 1.4f;        // +40% buffer — modifier off
        private const float NEED_BUFF_MULT_MODDED = 1f / 1.2f; // +20% buffer — modifier on (half the buff, not a separate multiplier)

        private static readonly FieldInfo BatteryField =
            typeof(TattletailNeeds).GetField("batteryDecreasePerSecond", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo HungerField =
            typeof(TattletailNeeds).GetField("hungerDecreasePerSecond", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo DirtinessField =
            typeof(TattletailNeeds).GetField("dirtinessDecreasePerSecond", BindingFlags.NonPublic | BindingFlags.Instance);

        /// <summary>Baseline balance change, every run — hunger/dirtiness always get a buffer, and "Always Hungry"/"Brushing is Key" adjust its SIZE (half instead of none) rather than stacking a separate speed-up multiplier on top.</summary>
        public static void ApplyNeedBalanceBuffs(bool alwaysHungry, bool brushingIsKey)
        {
            var needs = RM.tattletail?.GetNeeds();
            if (needs == null)
            {
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: RunModifiers.ApplyNeedBalanceBuffs — RM.tattletail/GetNeeds() unavailable.");
                return;
            }

            Multiply(HungerField, needs, "hunger", alwaysHungry ? NEED_BUFF_MULT_MODDED : NEED_BUFF_MULT);
            Multiply(DirtinessField, needs, "dirtiness", brushingIsKey ? NEED_BUFF_MULT_MODDED : NEED_BUFF_MULT);
        }

        /// <summary>Shop "Steady Hands" power up — all three needs drain 20% slower for the run.</summary>
        public static void ApplySteadyHands()
        {
            var needs = RM.tattletail?.GetNeeds();
            if (needs == null) return;
            Multiply(HungerField, needs, "hunger (Steady Hands)", 0.8f);
            Multiply(DirtinessField, needs, "dirtiness (Steady Hands)", 0.8f);
            Multiply(BatteryField, needs, "battery (Steady Hands)", 0.8f);
        }

        /// <summary>Battery has no baseline buff to interact with, so its modifier stays a plain, unadjusted 1.5x.</summary>
        public static void ApplyBatteryModifier(bool batteriesNotIncluded)
        {
            if (!batteriesNotIncluded) return;

            var needs = RM.tattletail?.GetNeeds();
            if (needs == null)
            {
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: RunModifiers.ApplyBatteryModifier — RM.tattletail/GetNeeds() unavailable.");
                return;
            }

            Multiply(BatteryField, needs, "battery", FASTER_MULT);
        }

        /// <summary>3.7 traits: all three needs drain at this multiple (once, at run start).</summary>
        public static void ApplyTraitNeedsMult(float mult)
        {
            var needs = RM.tattletail?.GetNeeds();
            if (needs == null) return;
            Multiply(HungerField, needs, "hunger (traits)", mult);
            Multiply(DirtinessField, needs, "dirtiness (traits)", mult);
            Multiply(BatteryField, needs, "battery (traits)", mult);
        }

        private static void Multiply(FieldInfo field, TattletailNeeds needs, string label, float mult)
        {
            if ((object)field == null)
            {
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: RunModifiers — " + label + " decrease-rate field not found via reflection.");
                return;
            }
            float current = (float)field.GetValue(needs);
            field.SetValue(needs, current * mult);
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: Run modifier applied — " + label + " drain rate x" + mult + " (" + current + " -> " + (current * mult) + ").");
        }
    }
}
