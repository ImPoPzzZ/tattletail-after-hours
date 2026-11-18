using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;

namespace AfterHoursInstaller
{
    /// <summary>
    /// Copies the mod into a Tattletail folder from the embedded payload.zip: the release files at
    /// its root, plus x64/ copies of the files that differ for the 64-bit (itch.io) game (the loader).
    /// </summary>
    public static class ModInstaller
    {
        private const string DllEntry = "BepInEx/plugins/KaleidoscopeEndless/KaleidoscopeEndless.dll";
        private static string _payloadDllHash;

        // What the uninstaller removes (saves live in Documents\Tattletail After Hours and stay).
        private static readonly string[] ModFolders = { "BepInEx", "ShopPreviews" };
        private static readonly string[] ModFiles = { "winhttp.dll", "doorstop_config.ini", ".doorstop_version", "CHANGELOG.txt", "README - HOW TO INSTALL.txt" };

        private static Stream Payload() { return Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip"); }

        public static void Install(GameInstall game, Action<float, string> progress)
        {
            using (var s = Payload())
            using (var zip = new ZipArchive(s, ZipArchiveMode.Read))
            {
                var files = zip.Entries.Where(e => e.Name.Length > 0 && !e.FullName.StartsWith("x64/")).ToList();
                for (int i = 0; i < files.Count; i++)
                {
                    var entry = files[i];
                    var source = entry;
                    if (game.Is64) source = zip.GetEntry("x64/" + entry.FullName) ?? entry; // the 64-bit loader
                    string dest = Path.Combine(game.Folder, entry.FullName.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(dest));
                    source.ExtractToFile(dest, true);
                    progress((i + 1f) / files.Count, Describe(entry.FullName));
                    Thread.Sleep(Math.Max(15, 2200 / files.Count)); // a moment per file, so the bar is something to watch
                }
            }
        }

        public static void Uninstall(GameInstall game)
        {
            foreach (var d in ModFolders)
            {
                string p = Path.Combine(game.Folder, d);
                if (Directory.Exists(p)) Directory.Delete(p, true);
            }
            foreach (var f in ModFiles)
            {
                string p = Path.Combine(game.Folder, f);
                if (File.Exists(p)) File.Delete(p);
            }
        }

        /// <summary>Is the installed mod exactly the one in this installer?</summary>
        public static bool MatchesPayload(string installedDll)
        {
            try
            {
                if (_payloadDllHash == null)
                    using (var s = Payload())
                    using (var zip = new ZipArchive(s, ZipArchiveMode.Read))
                    using (var d = zip.GetEntry(DllEntry).Open())
                        _payloadDllHash = Hash(d);
                using (var f = File.OpenRead(installedDll)) return Hash(f) == _payloadDllHash;
            }
            catch { return false; }
        }

        private static string Hash(Stream s)
        {
            using (var sha = SHA1.Create()) return Convert.ToBase64String(sha.ComputeHash(s));
        }

        private static string Describe(string path)
        {
            if (path.StartsWith("BepInEx/core")) return "Setting up BepInEx (the mod loader)...";
            if (path.StartsWith("BepInEx/plugins")) return "Installing After Hours...";
            if (path.StartsWith("BepInEx/")) return "Setting up BepInEx...";
            if (path.StartsWith("ShopPreviews")) return "Adding the shop pictures...";
            if (path.Contains("winhttp") || path.Contains("doorstop")) return "Hooking the mod into the game...";
            return "Copying " + Path.GetFileName(path) + "...";
        }
    }
}
