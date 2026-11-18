using System;
using System.IO;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// All-time stats shown in the new persistent Stats submenu (replaces
    /// the old auto-popup StatsUI death screen). Saved next to Shop.txt in
    /// the Documents save folder (see SaveFiles). Survival times are
    /// stored as whole seconds (int), not raw floats — nobody needs
    /// fractional-second precision, and it keeps Stats.txt readable.
    /// </summary>
    public static class EndlessStats
    {
        private const string FILE_NAME = "Stats.txt";
        private static string FilePath => SaveFiles.PathFor(FILE_NAME);

        private static bool _loaded;
        private static int _lastSurvivalTime;
        private static int _longestSurvivalTime;
        private static int _totalObjectivesCompleted;
        private static int _totalDeaths;
        private static int _totalSurvived;

        public static int LastSurvivalTime { get { EnsureLoaded(); return _lastSurvivalTime; } }
        public static int LongestSurvivalTime { get { EnsureLoaded(); return _longestSurvivalTime; } }
        public static int TotalObjectivesCompleted { get { EnsureLoaded(); return _totalObjectivesCompleted; } }
        public static int TotalDeaths { get { EnsureLoaded(); return _totalDeaths; } }
        // Successful exfils — answered the phone before a death caught up with them.
        public static int TotalSurvived { get { EnsureLoaded(); return _totalSurvived; } }

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            try
            {
                if (!File.Exists(FilePath)) return;
                foreach (string line in File.ReadAllLines(FilePath))
                {
                    int eq = line.IndexOf('=');
                    if (eq < 0) continue;
                    string key = line.Substring(0, eq).Trim();
                    string val = line.Substring(eq + 1).Trim();
                    switch (key)
                    {
                        case "LastSurvivalTime": int.TryParse(val, out _lastSurvivalTime); break;
                        case "LongestSurvivalTime": int.TryParse(val, out _longestSurvivalTime); break;
                        case "TotalObjectivesCompleted": int.TryParse(val, out _totalObjectivesCompleted); break;
                        case "TotalDeaths": int.TryParse(val, out _totalDeaths); break;
                        case "TotalSurvived": int.TryParse(val, out _totalSurvived); break;
                    }
                }
            }
            catch (Exception e)
            {
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: EndlessStats read failed: " + e.Message);
            }
        }

        private static void Save()
        {
            try
            {
                File.WriteAllLines(FilePath, new[]
                {
                    "LastSurvivalTime=" + _lastSurvivalTime,
                    "LongestSurvivalTime=" + _longestSurvivalTime,
                    "TotalObjectivesCompleted=" + _totalObjectivesCompleted,
                    "TotalDeaths=" + _totalDeaths,
                    "TotalSurvived=" + _totalSurvived,
                });
            }
            catch (Exception e)
            {
                KaleidoscopeEndlessPlugin.Log.LogError("KE: EndlessStats write failed: " + e.Message);
            }
        }

        /// <summary>Call once per objective completion, any run.</summary>
        public static void RecordObjectiveCompleted()
        {
            Daily.Add("endless_obj", 1);
            EnsureLoaded();
            _totalObjectivesCompleted++;
            Save();
        }

        /// <summary>Call once when a run ends, whatever the cause — isDeath=false for a voluntary exfil.</summary>
        public static void RecordRunEnd(float runTimeSeconds, bool isDeath)
        {
            Daily.Best("run_time", (int)runTimeSeconds);
            if (!isDeath) Daily.Add("exfil", 1);
            EnsureLoaded();
            int seconds = (int)runTimeSeconds;
            _lastSurvivalTime = seconds;
            if (seconds > _longestSurvivalTime) _longestSurvivalTime = seconds;
            if (isDeath) _totalDeaths++;
            else _totalSurvived++;
            Save();
        }
    }
}
