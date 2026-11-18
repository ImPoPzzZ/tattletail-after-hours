using System.Collections.Generic;
using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// 3.7, The Profile Update: your character's psychological profile in Endless. You choose two traits on the setup
    /// screen (PROFILE). Each has a bonus and a price, so nothing is free; none of them pays shards (a few cost some).
    /// This file is the data and the rules (what each trait changes, as numbers the rest of the game asks for);
    /// EndlessTraitsRuntime makes the visible and timed ones happen; TraitPickerMenu is the screen.
    /// With no traits chosen, every answer here is "no change", so the base game is untouched.
    /// Fictional, in-universe labels (Waygetter's profile of the child), not diagnoses.
    /// </summary>
    public enum Trait
    {
        Paranoid, ViolentTendencies, LoneWolf, Professional, Fearless, Reliable, Tormented, AggressiveBehaviour, Survivor,
        CalmUnderPressure, Methodical, Relentless, Dependable, Impatient, PersistentCuriosity, UnhealthyAttachment,
        CompulsiveObservation, FearOfAbandonment, SeparationAnxiety, HypervigilantBehaviour, EnvironmentalFixation,
        RepetitiveBehaviour, UnwarrantedSuspicion, CompulsiveChecking, AnomalousAttachment, UnresolvedFear, RealityAvoidance,
        DistortedPerception, IntrusiveThoughts, ParanoidFixation, CompulsiveSecrecy, UnstableAttachment, PersistentDread,
        UnexplainedBehaviour,
    }

    public static class EndlessTraits
    {
        public const int Count = 34;
        private const string KEY_A = "KE_TRAIT_A", KEY_B = "KE_TRAIT_B";

        // ── The traits ───────────────────────────────────────────────────────
        private static readonly string[] Names =
        {
            "Paranoid", "Violent Tendencies", "Lone Wolf", "Professional", "Fearless", "Reliable", "Tormented", "Aggressive Behaviour",
            "Survivor", "Calm Under Pressure", "Methodical", "Relentless", "Dependable", "Impatient", "Persistent Curiosity",
            "Unhealthy Attachment", "Compulsive Observation", "Fear of Abandonment", "Separation Anxiety", "Hypervigilant Behaviour",
            "Environmental Fixation", "Repetitive Behaviour", "Unwarranted Suspicion", "Compulsive Checking", "Anomalous Attachment",
            "Unresolved Fear", "Reality Avoidance", "Distorted Perception", "Intrusive Thoughts", "Paranoid Fixation",
            "Compulsive Secrecy", "Unstable Attachment", "Persistent Dread", "Unexplained Behaviour",
        };

        // true = steady, false = condition
        private static readonly bool[] Steady =
        {
            false, false, true, true, true, true, false, false, true,
            true, true, true, true, false, true, false,
            true, false, false, false, false,
            true, false, false, false, false, false,
            false, false, false, true, false, false,
            false,
        };

        private static readonly string[] Bonus =
        {
            "A heartbeat and a red pulse when Mama is within about 12 m.",
            "Vases break 50% faster, and 30% of breaks make no noise.",
            "Gadget cooldowns 20% shorter, and you move 5% faster.",
            "Mama's first alert in each objective comes 10 s later.",
            "You can take 4 flashlight kills instead of 3.",
            "Care stations (charging) take 30% less time.",
            "Adrenaline: you move 12% faster while Mama is within 8 m.",
            "You move 8% faster, and breaking vases and lighting candles takes 35% less time.",
            "You start with a Totem of Undying.",
            "While Mama is within 8 m, holds are 30% faster and your noise is 20% less likely to reach her.",
            "Every hold takes 35% less time.",
            "You move 10% faster during objectives.",
            "Answering the phone pays a 40% exfil bonus instead of 25%.",
            "Holds take 25% less time, and you move 5% faster.",
            "Power-up coins appear twice as often and show what they are from farther away.",
            "Tattletail's needs drain 25% slower.",
            "Objectives near you (within 6 m) are pointed out.",
            "Each flashlight shake charges 20% more.",
            "Care-station holds take 40% less time.",
            "When Mama teleports, the way she went is flagged on screen.",
            "After 20 s in an objective, the exact spot is pointed out.",
            "Each repeat of an objective type is 25% faster, up to 50% faster.",
            "Gadget reach is 30% longer (Plunge range, Flash Camera range, Toy Parrot reach).",
            "Once per run you can skip an objective (press X) and take the next one.",
            "Each objective Tattletail changes color and gives you one small random buff.",
            "You move 15% faster while Mama is hunting you.",
            "Mama is 25% less likely to hear you.",
            "Holds take half as long while Mama is hunting you.",
            "Whispers now and then point toward the nearest objective.",
            "A pointer shows where Mama is when she is within 15 m.",
            "Mama loses interest 20% sooner.",
            "A random gadget replaces yours each objective, each with a 25% shorter cooldown.",
            "Tattletail's needs stop draining while Mama is hunting.",
            "The effect is a random pair of the other traits each objective: one steady, one condition.",
        };

        private static readonly string[] Price =
        {
            "False alarms now and then.",
            "Everything else you touch is a little louder.",
            "Tattletail's needs drain 15% faster.",
            "Rests are 20 s instead of 30.",
            "Mama's alert time is 15% longer.",
            "Gadget cooldowns are 15% longer.",
            "Bad-memory moments: the lights dip, and a whisper hides the warnings for a few seconds.",
            "Your running is louder.",
            "You earn 15% fewer shards.",
            "Tattletail's needs drain 10% faster.",
            "You move 5% slower.",
            "Rests are cut to 15 s.",
            "The phone rings every 4th objective instead of every 3rd.",
            "Flashlight Survival lasts 20% longer.",
            "Picking up a coin makes a small noise.",
            "Distracted: while any need is below 25% you move 10% slower and holds take 20% longer.",
            "Your flashlight drains 10% faster.",
            "With the flashlight off for 10 s, you are louder.",
            "Objective holds take 20% longer.",
            "Your running is louder.",
            "You earn 10% fewer shards.",
            "The first time you meet an objective type, it takes 25% longer.",
            "10% of objectives add a harmless decoy prompt.",
            "Every hold takes 25% longer.",
            "And one small random debuff.",
            "Mama starts every objective already alerted.",
            "The HUD is stripped: no timer, no quest text.",
            "Harmless phantom Mamas appear, and the picture warps now and then.",
            "Other whispers give a random debuff for 8 s.",
            "Mama's patrol speed is 10% faster.",
            "The phone's exfil bonus is halved.",
            "You never choose which gadget you get.",
            "Mama's patrol speed is 15% faster and her alert time 10% longer.",
            "No other price. Pure chaos.",
        };

        public static string NameOf(int id) { return id >= 0 && id < Count ? Names[id] : "None"; }
        public static bool IsSteady(int id) { return id >= 0 && id < Count && Steady[id]; }
        public static string BonusOf(int id) { return id >= 0 && id < Count ? Bonus[id] : ""; }
        public static string PriceOf(int id) { return id >= 0 && id < Count ? Price[id] : ""; }

        // ── Your two picks (remembered between sessions) ─────────────────────
        public static int Pick(int slot) { return PlayerPrefs.GetInt(slot == 0 ? KEY_A : KEY_B, -1); }

        private static void SetPick(int slot, int id)
        {
            PlayerPrefs.SetInt(slot == 0 ? KEY_A : KEY_B, id);
            PlayerPrefs.Save();
        }

        /// <summary>Traits that can't be taken together (the same hook pushed opposite ways, or opposite attachments).</summary>
        public static bool Conflicts(int a, int b)
        {
            if (a < 0 || b < 0 || a == b) return true;
            return ConflictOne(a, b) || ConflictOne(b, a);
        }

        private static bool ConflictOne(int a, int b)
        {
            if (a == (int)Trait.LoneWolf)
                return b == (int)Trait.FearOfAbandonment || b == (int)Trait.SeparationAnxiety || b == (int)Trait.UnhealthyAttachment
                    || b == (int)Trait.UnstableAttachment || b == (int)Trait.AnomalousAttachment;
            if (a == (int)Trait.Impatient) return b == (int)Trait.Methodical;
            return false;
        }

        /// <summary>Picks or drops a trait. Returns a note for the screen ("" when nothing needs saying).</summary>
        public static string Toggle(int id)
        {
            if (id < 0 || id >= Count) return "";
            int a = Pick(0), b = Pick(1);
            if (a == id) { SetPick(0, b); SetPick(1, -1); return ""; }
            if (b == id) { SetPick(1, -1); return ""; }
            int other = a >= 0 ? a : b;
            if (a >= 0 && b >= 0)
            {
                // Two already: the newest pick replaces the older one (if they can pair).
                if (Conflicts(id, b)) return Names[id] + " can't be paired with " + Names[b] + ".";
                SetPick(0, b); SetPick(1, id);
                return "";
            }
            if (other >= 0 && Conflicts(id, other)) return Names[id] + " can't be paired with " + Names[other] + ".";
            if (a < 0) SetPick(0, id); else SetPick(1, id);
            return "";
        }

        public static void Clear() { SetPick(0, -1); SetPick(1, -1); }

        // ── Names, titles and combos ─────────────────────────────────────────
        private static readonly string[] Word =
        {
            "Paranoid", "Violent", "Lone Wolf", "Professional", "Fearless", "Reliable", "Tormented", "Aggressive", "Survivor",
            "Calm", "Methodical", "Relentless", "Dependable", "Impatient", "Curious", "Attached", "Observant", "Clinging",
            "Anxious", "Hypervigilant", "Fixated", "Repetitive", "Suspicious", "Compulsive", "Anomalous", "Haunted", "Avoidant",
            "Distorted", "Intrusive", "Fixated", "Secretive", "Unstable", "Dreading", "Unexplained",
        };

        private static bool IsNoun(int id) { return id == (int)Trait.LoneWolf || id == (int)Trait.Professional || id == (int)Trait.Survivor; }

        public enum Combo { None, NothingGetsPastYou, TheContractor, BackFromIt, ByTheBook, ColdBlood, Inseparable }

        public static Combo ComboOf(int a, int b)
        {
            return ComboHas(a, b, Trait.Paranoid, Trait.HypervigilantBehaviour) ? Combo.NothingGetsPastYou
                 : ComboHas(a, b, Trait.LoneWolf, Trait.Professional) ? Combo.TheContractor
                 : ComboHas(a, b, Trait.Survivor, Trait.Tormented) ? Combo.BackFromIt
                 : ComboHas(a, b, Trait.Methodical, Trait.CompulsiveChecking) ? Combo.ByTheBook
                 : ComboHas(a, b, Trait.Fearless, Trait.CalmUnderPressure) ? Combo.ColdBlood
                 : ComboHas(a, b, Trait.SeparationAnxiety, Trait.UnhealthyAttachment) ? Combo.Inseparable
                 : Combo.None;
        }

        private static bool ComboHas(int a, int b, Trait x, Trait y)
        {
            return (a == (int)x && b == (int)y) || (a == (int)y && b == (int)x);
        }

        public static string ComboName(Combo c)
        {
            switch (c)
            {
                case Combo.NothingGetsPastYou: return "Nothing Gets Past You";
                case Combo.TheContractor: return "The Contractor";
                case Combo.BackFromIt: return "Back From It";
                case Combo.ByTheBook: return "By The Book";
                case Combo.ColdBlood: return "Cold Blood";
                case Combo.Inseparable: return "Inseparable";
                default: return "";
            }
        }

        public static string ComboNote(Combo c)
        {
            switch (c)
            {
                case Combo.NothingGetsPastYou: return "Both warnings reach 25% further and last longer.";
                case Combo.TheContractor: return "+5% shards.";
                case Combo.BackFromIt: return "After a catch, the Totem recharges once per run.";
                case Combo.ByTheBook: return "A skipped objective pays +10 shards.";
                case Combo.ColdBlood: return "The first flashlight kill in each objective doesn't count.";
                case Combo.Inseparable: return "Needs drain 35% slower, and care stations are 20% faster.";
                default: return "";
            }
        }

        public static string TitleOf(int a, int b)
        {
            if (a < 0 && b < 0) return "No profile";
            if (a < 0 || b < 0)
            {
                int one = a >= 0 ? a : b;
                return "The " + Word[one] + (IsNoun(one) ? "" : " Child");
            }
            Combo c = ComboOf(a, b);
            if (c != Combo.None) return ComboName(c);
            bool na = IsNoun(a), nb = IsNoun(b);
            if (na && nb) return "The " + Word[a] + " " + Word[b];
            if (na) return "The " + Word[b] + " " + Word[a];
            if (nb) return "The " + Word[a] + " " + Word[b];
            return "The " + Word[a] + " " + Word[b] + " Child";
        }

        public static string TitleNow() { return TitleOf(Pick(0), Pick(1)); }

        /// <summary>What the setup screen shows for your profile: the title, then each trait with its bonus and price.</summary>
        public static string Describe()
        {
            int a = Pick(0), b = Pick(1);
            if (a < 0 && b < 0) return "No traits chosen. Press Profile to pick two.";
            string s = TitleOf(a, b);
            Combo c = ComboOf(a, b);
            if (c != Combo.None) s += "  (" + ComboNote(c) + ")";
            if (a >= 0) s += "\n" + Names[a] + ": " + Bonus[a] + " But: " + Price[a];
            if (b >= 0) s += "\n" + Names[b] + ": " + Bonus[b] + " But: " + Price[b];
            return s;
        }

        public static string ShortSummary()
        {
            int a = Pick(0), b = Pick(1);
            return ((a >= 0 ? 1 : 0) + (b >= 0 ? 1 : 0)) + " of 2";
        }

        // ── A run's own copy of the profile ──────────────────────────────────
        /// <summary>True from the moment a run starts until it ends, in Endless only (not Versus, not the party).</summary>
        public static bool Running { get; private set; }

        private static readonly bool[] Active = new bool[Count];
        private static readonly int[] Rolled = { -1, -1 };   // Unexplained Behaviour's random pair for this objective
        private static int _skipsLeft;
        private static bool _delayPending, _coldBloodUsed, _backFromItUsed;
        private static readonly Dictionary<ObjectiveType, int> Seen = new Dictionary<ObjectiveType, int>();
        private static float _repeatFactor = 1f;
        private static int _objectivesStarted;

        // Things the runtime keeps up to date, and the rules below read.
        public static float MamaDist = 999f;
        public static bool MamaHunting, NeedLow, MemoryHidesWarnings;
        public static float FlashlightOffFor;
        public static float AnomalySpeed = 1f, AnomalyHold = 1f, AnomalyLoud;
        public static float WhisperSpeed = 1f, WhisperHold = 1f, WhisperLoud;

        public static int SkipsLeft { get { return _skipsLeft; } }

        public static bool Has(Trait t) { return Running && Active[(int)t]; }
        private static bool Chose(Trait t) { return Running && (Pick(0) == (int)t || Pick(1) == (int)t); }

        public static void Reset()
        {
            Running = false;
            for (int i = 0; i < Count; i++) Active[i] = false;
            Rolled[0] = Rolled[1] = -1;
            MamaDist = 999f; MamaHunting = false; NeedLow = false; MemoryHidesWarnings = false; FlashlightOffFor = 0f;
            AnomalySpeed = AnomalyHold = WhisperSpeed = WhisperHold = 1f;
            AnomalyLoud = WhisperLoud = 0f;
            Seen.Clear();
            _repeatFactor = 1f;
            _objectivesStarted = 0;
        }

        /// <summary>The run has started (after the shop's power ups): copy your picks and apply what applies once.</summary>
        public static void BeginRun()
        {
            Reset();
            if (VersusMode.IsSurvivor) return;
            int a = Pick(0), b = Pick(1);
            if (a >= 0 && b >= 0 && Conflicts(a, b)) b = -1; // (an old pick that can't pair: only the first counts)
            if (a < 0 && b < 0) return;
            Running = true;
            if (a >= 0) Active[a] = true;
            if (b >= 0) Active[b] = true;
            _skipsLeft = Has(Trait.CompulsiveChecking) ? 1 : 0;
            _backFromItUsed = false;
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Traits] Profile: " + TitleOf(a, b) + " (" + NameOf(a) + ", " + NameOf(b) + ").");
            EndlessTraitsRuntime.Instance?.Say("PROFILE: " + TitleOf(a, b), 5f);

            try
            {
                float needs = NeedsStaticMult();
                if (Mathf.Abs(needs - 1f) > 0.001f) RunModifiers.ApplyTraitNeedsMult(needs);
                if (Has(Trait.Survivor)) PowerUpObjective.Instance?.GrantTotem();
                if (Has(Trait.FearOfAbandonment) && RM.flashlight != null) RM.flashlight.chargePerShake *= 1.2f;
                if (Has(Trait.UnstableAttachment) && Gadgets.RunGadget == GadgetType.None) Gadgets.RunGadget = RandomGadget();
            }
            catch (System.Exception e) { KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Traits] BeginRun: " + e.Message); }
        }

        public static void EndRun() { Running = false; }

        public static GadgetType RandomGadget()
        {
            var all = new List<GadgetType>();
            foreach (GadgetType g in Gadgets.All) if (g != GadgetType.None) all.Add(g);
            return all.Count == 0 ? GadgetType.None : all[Random.Range(0, all.Count)];
        }

        /// <summary>A new objective is starting.</summary>
        public static void OnObjectiveStart(ObjectiveType type)
        {
            if (!Running) return;
            _objectivesStarted++;
            _delayPending = Has(Trait.Professional);
            _coldBloodUsed = false;
            AnomalySpeed = AnomalyHold = 1f; AnomalyLoud = 0f;

            int seen; Seen.TryGetValue(type, out seen);
            _repeatFactor = seen == 0 ? 1.25f : (seen == 1 ? 0.75f : 0.5f);
            Seen[type] = seen + 1;

            if (Chose(Trait.UnexplainedBehaviour)) RollUnexplained();
            EndlessTraitsRuntime.Instance?.OnObjectiveStart(type, _objectivesStarted);
        }

        private static void RollUnexplained()
        {
            if (Rolled[0] >= 0) Active[Rolled[0]] = false;
            if (Rolled[1] >= 0) Active[Rolled[1]] = false;
            Rolled[0] = Rolled[1] = -1;
            int other = Pick(0) == (int)Trait.UnexplainedBehaviour ? Pick(1) : Pick(0);
            for (int tries = 0; tries < 60 && (Rolled[0] < 0 || Rolled[1] < 0); tries++)
            {
                int id = Random.Range(0, Count);
                if (id == (int)Trait.UnexplainedBehaviour || id == other || Active[id]) continue;
                if (other >= 0 && Conflicts(id, other)) continue;
                if (Steady[id] && Rolled[0] < 0) Rolled[0] = id;
                else if (!Steady[id] && Rolled[1] < 0) Rolled[1] = id;
            }
            if (Rolled[0] >= 0) Active[Rolled[0]] = true;
            if (Rolled[1] >= 0) Active[Rolled[1]] = true;
            EndlessTraitsRuntime.Instance?.Say("Unexplained: " + NameOf(Rolled[0]) + " and " + NameOf(Rolled[1]), 4f);
        }

        // ── The numbers the game asks for ────────────────────────────────────
        private static bool ComboNow(Combo c) { return Running && ComboOf(Pick(0), Pick(1)) == c; }

        /// <summary>How long a hold takes now, as a multiple of its normal time, for one of the mod's interactables (by its object name).</summary>
        public static float HoldMult(string objectName)
        {
            if (!Running || objectName == null) return 1f;
            bool care = objectName.StartsWith("KE_ChargingStation");
            bool vase = objectName.StartsWith("KE_Vase"), candle = objectName.StartsWith("KE_Candle");
            float m = 1f;
            if (Has(Trait.Methodical)) m *= 0.65f;
            if (Has(Trait.Impatient)) m *= 0.75f;
            if (Has(Trait.CalmUnderPressure) && MamaDist <= 8f) m *= 0.7f;
            if (Has(Trait.DistortedPerception) && MamaHunting) m *= 0.5f;
            if (Has(Trait.CompulsiveChecking)) m *= 1.25f;
            if (Has(Trait.UnhealthyAttachment) && NeedLow) m *= 1.2f;
            if (Has(Trait.RepetitiveBehaviour) && !care) m *= _repeatFactor;
            if (care)
            {
                if (Has(Trait.Reliable)) m *= 0.7f;
                if (Has(Trait.SeparationAnxiety)) m *= 0.6f;
                if (ComboNow(Combo.Inseparable)) m *= 0.8f;
            }
            else
            {
                if (Has(Trait.SeparationAnxiety)) m *= 1.2f;
                if (vase && Has(Trait.ViolentTendencies)) m *= 0.5f;
                if ((vase || candle) && Has(Trait.AggressiveBehaviour)) m *= 0.65f;
            }
            return m * AnomalyHold * WhisperHold;
        }

        /// <summary>Your movement speed as a multiple of normal (on top of the shop's walking-speed passive).</summary>
        public static float SpeedMult()
        {
            if (!Running) return 1f;
            var run = EndlessRunManager.Instance;
            bool objective = run != null && run.RunActive && !run.InRestPhase && !run.PhoneIsRinging;
            float m = 1f;
            if (Has(Trait.LoneWolf)) m *= 1.05f;
            if (Has(Trait.Methodical)) m *= 0.95f;
            if (Has(Trait.Relentless) && objective) m *= 1.10f;
            if (Has(Trait.AggressiveBehaviour)) m *= 1.08f;
            if (Has(Trait.Impatient)) m *= 1.05f;
            if (Has(Trait.Tormented) && MamaDist <= 8f) m *= 1.12f;
            if (Has(Trait.UnresolvedFear) && MamaHunting) m *= 1.15f;
            if (Has(Trait.UnhealthyAttachment) && NeedLow) m *= 0.9f;
            return m * AnomalySpeed * WhisperSpeed;
        }

        /// <summary>The chance that a noise you make never reaches Mama.</summary>
        public static float NoiseSkipChance()
        {
            if (!Running) return 0f;
            float keep = 1f;
            if (Has(Trait.CalmUnderPressure) && MamaDist <= 8f) keep *= 0.8f;
            if (Has(Trait.RealityAvoidance)) keep *= 0.75f;
            return 1f - keep;
        }

        /// <summary>The chance that a noise you make counts as a big one.</summary>
        public static float NoiseLoudChance()
        {
            if (!Running) return 0f;
            float keep = 1f;
            if (Has(Trait.ViolentTendencies)) keep *= 0.8f;
            if (Has(Trait.AggressiveBehaviour)) keep *= 0.75f;
            if (Has(Trait.HypervigilantBehaviour)) keep *= 0.8f;
            if (Has(Trait.FearOfAbandonment) && FlashlightOffFor >= 10f) keep *= 0.9f;
            keep *= (1f - AnomalyLoud) * (1f - WhisperLoud);
            return 1f - keep;
        }

        /// <summary>Vases: some breaks make no noise (Violent Tendencies).</summary>
        public static bool VaseBreakIsSilent() { return Has(Trait.ViolentTendencies) && Random.value < 0.3f; }

        public static float NeedsStaticMult()
        {
            if (!Running) return 1f;
            if (ComboNow(Combo.Inseparable)) return 0.65f;
            float m = 1f;
            if (Has(Trait.LoneWolf)) m *= 1.15f;
            if (Has(Trait.CalmUnderPressure)) m *= 1.10f;
            if (Has(Trait.UnhealthyAttachment)) m *= 0.75f;
            return m;
        }

        public static bool NeedsFrozen { get { return Has(Trait.PersistentDread) && MamaHunting; } }

        public static float AlertMult()
        {
            if (!Running) return 1f;
            float m = 1f;
            if (Has(Trait.Fearless)) m *= 1.15f;
            if (Has(Trait.PersistentDread)) m *= 1.10f;
            if (Has(Trait.CompulsiveSecrecy)) m *= 0.8f;
            return m;
        }

        /// <summary>How much faster Mama patrols (her time on each spot is divided by this).</summary>
        public static float PatrolMult()
        {
            if (!Running) return 1f;
            float m = 1f;
            if (Has(Trait.ParanoidFixation)) m *= 1.10f;
            if (Has(Trait.PersistentDread)) m *= 1.15f;
            return m;
        }

        /// <summary>Seconds to hold Mama back at the start of this objective (once per objective).</summary>
        public static float TakeMamaDelay()
        {
            if (!Running || !_delayPending) return 0f;
            _delayPending = false;
            return 10f;
        }

        public static bool MamaStartsAlerted() { return Has(Trait.UnresolvedFear); }

        public static int CloseCallLimit() { return Has(Trait.Fearless) ? 4 : 3; }

        /// <summary>Cold Blood: the first flashlight kill in each objective doesn't count.</summary>
        public static bool ColdBloodForgives()
        {
            if (!ComboNow(Combo.ColdBlood) || _coldBloodUsed) return false;
            _coldBloodUsed = true;
            return true;
        }

        public static float RestSeconds()
        {
            if (!Running) return 30f;
            if (Has(Trait.Relentless)) return 15f;
            if (Has(Trait.Professional)) return 20f;
            return 30f;
        }

        public static int LoopLength() { return Has(Trait.Dependable) ? 4 : 3; }

        public static float ExfilFraction()
        {
            float f = Has(Trait.Dependable) ? 0.40f : 0.25f;
            if (Has(Trait.CompulsiveSecrecy)) f *= 0.5f;
            return f;
        }

        public static float ShardMult()
        {
            if (!Running) return 1f;
            float m = 1f;
            if (Has(Trait.Survivor)) m *= 0.85f;
            if (Has(Trait.EnvironmentalFixation)) m *= 0.9f;
            if (ComboNow(Combo.TheContractor)) m *= 1.05f;
            return m;
        }

        public static float GadgetCooldownMult()
        {
            if (!Running) return 1f;
            float m = 1f;
            if (Has(Trait.LoneWolf)) m *= 0.8f;
            if (Has(Trait.Reliable)) m *= 1.15f;
            if (Has(Trait.UnstableAttachment)) m *= 0.75f;
            return m;
        }

        public static float GadgetReachMult() { return Has(Trait.UnwarrantedSuspicion) ? 1.3f : 1f; }

        public static float FlashlightSurvivalMult() { return Has(Trait.Impatient) ? 1.2f : 1f; }

        public static float ExtraFlashlightDrain() { return Has(Trait.CompulsiveObservation) ? 0.1f : 0f; }

        public static bool HideHud { get { return Has(Trait.RealityAvoidance); } }

        public static float CoinChanceMult() { return Has(Trait.PersistentCuriosity) ? 2f : 1f; }
        public static float CoinRevealMult() { return Has(Trait.PersistentCuriosity) ? 1.6f : 1f; }
        public static bool CoinsWithoutModifier() { return Has(Trait.PersistentCuriosity); }

        public static float WarningRange(float basis)
        {
            return ComboNow(Combo.NothingGetsPastYou) ? basis * 1.25f : basis;
        }

        public static bool NothingGetsPastYou { get { return ComboNow(Combo.NothingGetsPastYou); } }

        /// <summary>A catch was just forgiven by a Totem: Back From It recharges one, once.</summary>
        public static void OnTotemUsed()
        {
            if (!ComboNow(Combo.BackFromIt) || _backFromItUsed) return;
            _backFromItUsed = true;
            PowerUpObjective.Instance?.GrantTotem();
            EndlessTraitsRuntime.Instance?.Say("Back From It: the Totem recharged.", 3f);
        }

        /// <summary>Compulsive Checking: the skip. By The Book pays 10 shards for it.</summary>
        public static bool TrySkip()
        {
            if (!Has(Trait.CompulsiveChecking) || _skipsLeft <= 0) return false;
            var run = EndlessRunManager.Instance;
            if (run == null || !run.TraitSkipObjective()) return false;
            _skipsLeft--;
            if (ComboNow(Combo.ByTheBook)) run.TraitPay(10);
            return true;
        }

        public static void OnCoinGrabbed()
        {
            if (!Has(Trait.PersistentCuriosity) || RM.fpsController == null) return;
            RM.mamaManager?.OnNoise(RM.fpsController.transform.position, false);
        }

        /// <summary>The picks that are active right now, for the screen's trait icons.</summary>
        public static string ActiveLine()
        {
            if (!Running) return "";
            string s = "";
            for (int i = 0; i < Count; i++)
                if (Active[i] && i != (int)Trait.UnexplainedBehaviour) s += (s.Length > 0 ? ", " : "") + Names[i];
            return s;
        }
    }
}
