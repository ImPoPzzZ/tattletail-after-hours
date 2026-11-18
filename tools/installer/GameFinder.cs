using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace AfterHoursInstaller
{
    /// <summary>A Tattletail folder on this PC.</summary>
    public class GameInstall
    {
        public string Folder;
        public bool Is64;         // itch.io's build is 64-bit, Steam's is 32-bit: they need different loaders
        public bool Modded;       // After Hours is already in it
        public bool SameVersion;  // ...and it's exactly this installer's version
        public string Source;     // STEAM, ITCH.IO or GAME
    }

    /// <summary>Finds Tattletail: every Steam library, the itch.io app's folder, and the usual places people unzip games.</summary>
    public static class GameFinder
    {
        public const string ExeName = "tattletailWindows.exe";
        private const int MAX_DIRS = 6000; // keep the scan quick even on a huge Documents folder

        public static List<GameInstall> FindAll()
        {
            var folders = new List<string>();
            foreach (var lib in SteamLibraries())
                folders.Add(Path.Combine(lib, "steamapps", "common", "Tattletail"));

            if (WineSupport.IsWine) // Linux: Steam's libraries are on the Z: drive
                foreach (var lib in WineSupport.SteamLibraries())
                    folders.Add(Path.Combine(lib, "steamapps", "common", "Tattletail"));

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string itchApps = Path.Combine(appData, "itch", "apps");
            int budget = MAX_DIRS;
            foreach (var d in SafeDirs(itchApps))
                if (Path.GetFileName(d).IndexOf("tattletail", StringComparison.OrdinalIgnoreCase) >= 0) Scan(folders, d, 2, ref budget);

            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var places = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                Path.Combine(home, "Downloads"),
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Path.Combine(home, "Desktop"), Path.Combine(home, "OneDrive", "Desktop"),
                @"C:\Games", @"D:\Games", @"E:\Games",
            };
            foreach (var p in places) Scan(folders, p, 3, ref budget);

            var found = new List<GameInstall>();
            foreach (var f in folders)
            {
                var g = Inspect(f);
                if (g != null && !found.Any(x => string.Equals(x.Folder, g.Folder, StringComparison.OrdinalIgnoreCase))) found.Add(g);
            }
            // Steam first, then itch.io, then the rest.
            return found.OrderBy(g => g.Source == "STEAM" ? 0 : g.Source == "ITCH.IO" ? 1 : 2).ToList();
        }

        public static GameInstall Inspect(string folder)
        {
            if (string.IsNullOrEmpty(folder)) return null;
            string exe = Path.Combine(folder, ExeName);
            if (!File.Exists(exe)) return null;
            var g = new GameInstall { Folder = Path.GetFullPath(folder), Is64 = IsExe64(exe) };
            string dll = Path.Combine(folder, "BepInEx", "plugins", "KaleidoscopeEndless", "KaleidoscopeEndless.dll");
            g.Modded = File.Exists(dll);
            if (g.Modded) g.SameVersion = ModInstaller.MatchesPayload(dll);
            if (folder.IndexOf("steamapps", StringComparison.OrdinalIgnoreCase) >= 0) g.Source = "STEAM";
            else if (folder.IndexOf(@"\itch\", StringComparison.OrdinalIgnoreCase) >= 0 || g.Is64) g.Source = "ITCH.IO";
            else g.Source = "GAME";
            return g;
        }

        public static bool IsRunning()
        {
            try { return Process.GetProcessesByName("tattletailWindows").Length > 0; }
            catch { return false; }
        }

        /// <summary>The exe's PE header says which CPU it's built for.</summary>
        public static bool IsExe64(string exe)
        {
            try
            {
                using (var f = File.OpenRead(exe))
                using (var r = new BinaryReader(f))
                {
                    f.Seek(0x3C, SeekOrigin.Begin);
                    int pe = r.ReadInt32();
                    f.Seek(pe + 4, SeekOrigin.Begin);
                    return r.ReadUInt16() == 0x8664;
                }
            }
            catch { return false; }
        }

        private static IEnumerable<string> SteamLibraries()
        {
            var roots = new List<string>();
            TryRead(Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath", roots);
            TryRead(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", roots);
            TryRead(Registry.LocalMachine, @"SOFTWARE\Valve\Steam", "InstallPath", roots);
            roots.Add(@"C:\Program Files (x86)\Steam");
            var libs = new List<string>(roots);
            foreach (var root in roots)
            {
                try
                {
                    string vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
                    if (!File.Exists(vdf)) continue;
                    foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
                        libs.Add(m.Groups[1].Value.Replace(@"\\", @"\"));
                }
                catch { }
            }
            return libs.Select(l => l.Replace('/', '\\').TrimEnd('\\')).Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private static void TryRead(RegistryKey hive, string key, string value, List<string> into)
        {
            try
            {
                using (var k = hive.OpenSubKey(key))
                    if (k?.GetValue(value) is string v && v.Length > 0) into.Add(v);
            }
            catch { }
        }

        private static void Scan(List<string> found, string dir, int depth, ref int budget)
        {
            if (budget-- <= 0 || string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
            if (File.Exists(Path.Combine(dir, ExeName))) { found.Add(dir); return; }
            if (depth == 0) return;
            foreach (var d in SafeDirs(dir))
            {
                string name = Path.GetFileName(d);
                if (name.StartsWith(".") || name.Equals("node_modules", StringComparison.OrdinalIgnoreCase) || name.Equals("AppData", StringComparison.OrdinalIgnoreCase)) continue;
                Scan(found, d, depth - 1, ref budget);
            }
        }

        private static IEnumerable<string> SafeDirs(string dir)
        {
            try { return Directory.Exists(dir) ? Directory.GetDirectories(dir) : new string[0]; }
            catch { return new string[0]; }
        }
    }
}
