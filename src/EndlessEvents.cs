using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KaleidoscopeEndless
{
    public enum EndlessEvent { ChaosRound, GiftFromMama }

    /// <summary>
    /// Random events. Chaos Round has a 15% chance each time an objective
    /// starts; every 20 seconds of a run there's a separate 10% roll for one
    /// of the timed events (currently Gift from Mama). See
    /// EndlessRunManager.DebugForceEventOnFirstObjective for the testing
    /// override. "Lucky Night" and "Kaleidoscope Night" are run-start
    /// events, not mid-run ones — see EndlessTitle.cs.
    /// </summary>
    public static class EndlessEvents
    {
        private const float CHAOS_CHANCE = 0.15f;      // per objective start
        public const float TIMED_INTERVAL = 20f;        // seconds between rolls
        private const float TIMED_CHANCE = 0.10f;      // per roll

        public static void OnObjectiveStart(EndlessRunManager mgr, ObjectiveType type, bool firstObjectiveOfRun)
        {
            bool chaosAllowed = type != ObjectiveType.FlashlightSurvival && type != ObjectiveType.None;

            if (EndlessRunManager.DebugForceEventOnFirstObjective && firstObjectiveOfRun)
            {
                // Testing: one random event of any kind on the first objective.
                var pool = new List<EndlessEvent> { EndlessEvent.GiftFromMama };
                if (chaosAllowed) pool.Add(EndlessEvent.ChaosRound);
                Trigger(mgr, pool[Random.Range(0, pool.Count)], true);
                return;
            }

            if (chaosAllowed && Random.value < CHAOS_CHANCE)
                Trigger(mgr, EndlessEvent.ChaosRound, false);
        }

        /// <summary>Called every 20 seconds of a run — 10% chance of one of the timed events.</summary>
        public static void RollTimed(EndlessRunManager mgr)
        {
            if (!mgr.GiftAllowed) return; // 0% right after a gift — see EndlessRunManager.BlockGiftsForNextTask
            if (Random.value >= TIMED_CHANCE) return;
            Trigger(mgr, EndlessEvent.GiftFromMama, false);
        }

        private static void Trigger(EndlessRunManager mgr, EndlessEvent pick, bool debug)
        {
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Event] Triggering '" + pick + "'" + (debug ? " (debug: first objective)" : "") + ".");
            switch (pick)
            {
                case EndlessEvent.ChaosRound:
                    if (!mgr.TryStartChaosRound())
                        KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Event] Chaos Round couldn't pair an objective — skipped.");
                    break;
                case EndlessEvent.GiftFromMama:
                    GiftFromMama(mgr);
                    break;
            }
        }

        // ── Gift from Mama ────────────────────────────────────────────────────
        // A Power Up coin (random spot, random ability — same odds as any
        // coin: Totem of Undying 5%, Max Stats and Strong Battery split the
        // rest) appears in the house.
        // Walk near it to see what it is, same as any coin.
        private static void GiftFromMama(EndlessRunManager mgr)
        {
            var pu = PowerUpObjective.Instance;
            if (pu == null) return;
            mgr.BlockGiftsForNextTask();
            PowerUpAbility gift = pu.SpawnGiftCoin();
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Event] Gift from Mama — a '" + gift + "' coin is hiding in the house.");
            mgr.StartCoroutine(GiftNotice(mgr));
        }

        private static IEnumerator GiftNotice(EndlessRunManager mgr)
        {
            if (RM.hud != null && !mgr.PhoneIsRinging)
                RM.hud.SetNewQuest("A gift from Mama...\nSomething is hiding in the house.");
            yield return new WaitForSeconds(5f);
            if (RM.hud != null && !mgr.PhoneIsRinging && mgr.RunActive)
                RM.hud.SetNewQuest(mgr.CurrentObjectiveHudText);
        }
    }
}
