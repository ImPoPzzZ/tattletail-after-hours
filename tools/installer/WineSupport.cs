using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace AfterHoursInstaller
{
    /// <summary>
    /// Linux (Steam Deck, Proton, Wine): the installer runs inside Wine, sees the Linux disk as Z:, finds Steam's libraries there and tells
    /// the game's prefix to load the mod loader (winhttp = native,builtin, what the launch option WINEDLLOVERRIDES="winhttp=n,b" does).
    /// Nothing here runs on a real Windows PC.
    /// </summary>
    public static class WineSupport
    {
        public const string LaunchOption = "WINEDLLOVERRIDES=\"winhttp=n,b\" %command%";
        private const string AppId = "568090";

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)] private static extern IntPtr GetModuleHandle(string name);
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)] private static extern IntPtr GetProcAddress(IntPtr module, string proc);

        private static bool? _isWine;

        /// <summary>Are we running inside Wine / Proton? (Wine's ntdll has wine_get_version; Windows' does not.)</summary>
        public static bool IsWine
        {
            get
            {
                if (_isWine.HasValue) return _isWine.Value;
                bool wine = false;
                try { wine = GetProcAddress(GetModuleHandle("ntdll.dll"), "wine_get_version") != IntPtr.Zero; } catch { }
                if (!wine) { try { wine = Directory.Exists(@"Z:\home") && Directory.Exists(@"Z:\usr"); } catch { } }
                _isWine = wine;
                return wine;
            }
        }

        /// <summary>/home/me/x becomes Z:\home\me\x (Wine shows the Linux disk as Z:).</summary>
        public static string ToWin(string unixPath)
        {
            if (string.IsNullOrEmpty(unixPath)) return null;
            if (unixPath.Length > 1 && unixPath[1] == ':') return unixPath;   // (already a Windows path)
            return "Z:" + unixPath.Replace('/', '\\');
        }

        private static IEnumerable<string> Homes()
        {
            var homes = new List<string>();
            try { string h = Environment.GetEnvironmentVariable("HOME"); if (!string.IsNullOrEmpty(h)) homes.Add(ToWin(h)); } catch { }
            try { homes.Add(@"Z:\home\" + Environment.UserName); } catch { }
            try { if (Directory.Exists(@"Z:\home")) homes.AddRange(Directory.GetDirectories(@"Z:\home")); } catch { }   // (a Deck's "deck", or any other user)
            homes.Add(@"Z:\root");
            return homes.Where(h => !string.IsNullOrEmpty(h) && SafeExists(h)).Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private static bool SafeExists(string dir) { try { return Directory.Exists(dir); } catch { return false; } }

        // Where Steam keeps itself on Linux: the usual place, the old symlink, Debian's, Flatpak's and Snap's.
        private static readonly string[] SteamSubfolders =
        {
            @".local\share\Steam", @".steam\steam", @".steam\root", @".steam\debian-installation",
            @".var\app\com.valvesoftware.Steam\.local\share\Steam", @".var\app\com.valvesoftware.Steam\.steam\steam",
            @"snap\steam\common\.local\share\Steam", @"snap\steam\common\.steam\steam",
        };

        /// <summary>Every Steam library on this Linux system, as Z: paths (each has a steamapps folder).</summary>
        public static List<string> SteamLibraries()
        {
            var libs = new List<string>();
            var roots = new List<string>();
            foreach (var home in Homes())
                foreach (var sub in SteamSubfolders)
                {
                    string root = Path.Combine(home, sub);
                    if (SafeExists(Path.Combine(root, "steamapps"))) roots.Add(root);
                }
            libs.AddRange(roots);
            foreach (var root in roots)
            {
                try
                {
                    string vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
                    if (!File.Exists(vdf)) continue;
                    foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
                        libs.Add(ToWin(m.Groups[1].Value.Replace(@"\\", "/")));   // (Linux paths: SD cards, second drives)
                }
                catch { }
            }
            // A card or a second drive that is not listed (older Steam setups): the usual mount places, one level down.
            foreach (var mount in new[] { @"Z:\run\media", @"Z:\media", @"Z:\mnt" })
                foreach (var d1 in SafeDirs(mount))
                {
                    TryLibrary(libs, d1);
                    foreach (var d2 in SafeDirs(d1)) TryLibrary(libs, d2);
                }
            return libs.Where(SafeExists).Select(l => l.TrimEnd('\\')).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static void TryLibrary(List<string> libs, string dir)
        {
            if (SafeExists(Path.Combine(dir, "steamapps"))) libs.Add(dir);
            string sub = Path.Combine(dir, "SteamLibrary");
            if (SafeExists(Path.Combine(sub, "steamapps"))) libs.Add(sub);
        }

        private static IEnumerable<string> SafeDirs(string dir)
        {
            try { return SafeExists(dir) ? Directory.GetDirectories(dir) : new string[0]; }
            catch { return new string[0]; }
        }

        /// <summary>Steam's Proton folder for Tattletail ("compatdata\568090\pfx"), if this is a Steam install and the game has run once.</summary>
        public static string PrefixOf(GameInstall game)
        {
            try
            {
                string common = Path.GetDirectoryName(game.Folder.TrimEnd('\\'));     // ...\steamapps\common
                string steamapps = common == null ? null : Path.GetDirectoryName(common);
                if (steamapps == null || !steamapps.EndsWith("steamapps", StringComparison.OrdinalIgnoreCase)) return null;
                string pfx = Path.Combine(steamapps, "compatdata", AppId, "pfx");
                return Directory.Exists(pfx) ? pfx : null;
            }
            catch { return null; }
        }

        /// <summary>
        /// Makes the game's prefix load the mod loader. Returns what to tell the player (null if there is nothing to say).
        /// </summary>
        public static string Enable(GameInstall game)
        {
            if (!IsWine) return null;
            bool done = false;
            string how = null;
            // 1. The game's own Proton prefix: its user.reg (works on any Proton, new or old: the file format has not changed).
            string pfx = PrefixOf(game);
            if (pfx != null)
            {
                string reg = Path.Combine(pfx, "user.reg");
                try
                {
                    if (File.Exists(reg))
                    {
                        string text = File.ReadAllText(reg);
                        bool changed;
                        string fixedText = WineReg.Enable(text, out changed);
                        if (changed)
                        {
                            string bak = reg + ".afterhours.bak";
                            if (!File.Exists(bak)) File.Copy(reg, bak);
                            File.WriteAllText(reg, fixedText, new UTF8Encoding(false));
                        }
                        done = true;
                        how = "Set up Tattletail's Proton folder.";
                    }
                }
                catch (Exception e) { how = "Couldn't write to the Proton folder (" + e.Message + ")."; }
            }
            // 2. This Wine prefix too (if the game runs in the prefix the installer runs in: Lutris, Bottles, plain Wine).
            try
            {
                using (var k = Registry.CurrentUser.CreateSubKey(@"Software\Wine\AppDefaults\tattletailWindows.exe\DllOverrides"))
                    if (k != null) { k.SetValue("winhttp", "native,builtin"); if (!done) { done = true; how = "Set up this Wine prefix."; } }
            }
            catch { }

            if (done) return how + " If the mod doesn't load, add this Steam launch option:  " + LaunchOption;
            if (game.Source == "STEAM")
                return "Start Tattletail once from Steam (so Proton makes its folder), then run this installer again. Or add this Steam launch option:  " + LaunchOption;
            return "Add this launch option (or Wine DLL override):  winhttp = native,builtin";
        }

        /// <summary>Takes the override out again (uninstall).</summary>
        public static void Disable(GameInstall game)
        {
            if (!IsWine) return;
            string pfx = PrefixOf(game);
            if (pfx != null)
            {
                try
                {
                    string reg = Path.Combine(pfx, "user.reg");
                    if (File.Exists(reg))
                    {
                        bool changed;
                        string text = WineReg.Disable(File.ReadAllText(reg), out changed);
                        if (changed) File.WriteAllText(reg, text, new UTF8Encoding(false));
                    }
                }
                catch { }
            }
            try { using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Wine\AppDefaults\tattletailWindows.exe\DllOverrides", true)) if (k != null) k.DeleteValue("winhttp", false); }
            catch { }
        }
    }
}
