namespace KaleidoscopeEndless
{
    /// <summary>
    /// Recalled has two worlds, one at a time: 0 is the town (part 1), 1 is Egypt (part 2). A session is in one of them; everything
    /// that differs (the plan, the toys, the names, the jobs, the gear) looks here to know which. Your character is one character
    /// in both: level, gear and stash come with you; the jobs are one chain (the town's 25, then Egypt's).
    /// </summary>
    public static class RcWorlds
    {
        /// <summary>The world this session is in (set before the town is built).</summary>
        private static int _now;
        public static int Now { get { return TOWN; } set { _now = TOWN; } }   // (Egypt is removed: there is only the town)

        public const int TOWN = 0, EGYPT = 1;
        public const int TOWN_JOBS = 25;      // the first world's jobs: the chain's indexes 0 to 24; Egypt's start at 25
        public const int BOSSES_PER_WORLD = 5;

        public static bool Egypt { get { return Now == EGYPT; } }

        /// <summary>Is Last Stop (Egypt) open to players? Not yet: it's built but under development. The config setting Developer/OpenLastStop opens it for testing.</summary>
        public static bool EgyptOpen { get { return false; } set { } }

        /// <summary>A job's number inside its own world (1 to 25 in the town, 1 to 23 in Egypt), and how many that world has.</summary>
        public static int JobNo(int job) { return job - (OfJob(job) == EGYPT ? TOWN_JOBS : 0) + 1; }
        public static int JobsIn(int job) { return OfJob(job) == EGYPT ? RcData.Quests.Count - TOWN_JOBS : TOWN_JOBS; }

        public static string Name(int world) { return world == EGYPT ? "Egypt" : "The Town"; }

        /// <summary>Which world a job (an index into the chain) belongs to.</summary>
        public static int OfJob(int job) { return job >= TOWN_JOBS ? EGYPT : TOWN; }
    }
}
