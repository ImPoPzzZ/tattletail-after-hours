using System;
using System.IO;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// Where the mod's save files live: Documents\Tattletail After Hours\, so
    /// progress survives mod updates and even reinstalling the game. Saves from
    /// the older locations (Documents\Kaleidoscope Endless\, from before the
    /// rename, and the game's install folder) are moved over automatically,
    /// never overwriting a save that's already in the new folder.
    /// </summary>
    public static class SaveFiles
    {
        private static readonly string[] Known = { "Shop.txt", "Shop.txt.bak", "Stats.txt", "Scrapbook.txt" };
        public const string FOLDER = "Tattletail After Hours";   // no ':' allowed in Windows folder names
        private const string OLD_FOLDER = "Kaleidoscope Endless"; // the mod's name before 3.1's rebrand

        private static string _dir;
        public static string Dir
        {
            get
            {
                if (_dir == null) Init();
                return _dir;
            }
        }

        public static string PathFor(string fileName) { return Path.Combine(Dir, fileName); }

        public static void Init()
        {
            if (_dir != null) return;
            try
            {
                string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                _dir = Path.Combine(docs, FOLDER);
                Directory.CreateDirectory(_dir);
                foreach (string name in Known) MoveInto(Path.Combine(Path.Combine(docs, OLD_FOLDER), name), name);
                TryRemoveIfEmpty(Path.Combine(docs, OLD_FOLDER));
            }
            catch (Exception e)
            {
                // No usable Documents folder — keep saving next to the game like before.
                _dir = BepInEx.Paths.GameRootPath;
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Saves] Couldn't use the Documents folder (" + e.Message + ") — saving in the game folder instead.");
                return;
            }

            foreach (string name in Known) MoveInto(Path.Combine(BepInEx.Paths.GameRootPath, name), name);
            KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Saves] Save folder: " + _dir);
        }

        /// <summary>Moves an old save into the save folder, unless the save folder already has that file.</summary>
        private static void MoveInto(string oldPath, string name)
        {
            string newPath = Path.Combine(_dir, name);
            if (!File.Exists(oldPath)) return;
            if (File.Exists(newPath))
            {
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Saves] '" + name + "' exists in both places — keeping the one in " + _dir + ", the old copy at " + oldPath + " stays untouched.");
                return;
            }
            try
            {
                File.Move(oldPath, newPath);
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Saves] Moved '" + name + "' to " + _dir);
            }
            catch (Exception e)
            {
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Saves] Couldn't move '" + name + "': " + e.Message);
            }
        }

        private static void TryRemoveIfEmpty(string dir)
        {
            try { if (Directory.Exists(dir) && Directory.GetFileSystemEntries(dir).Length == 0) Directory.Delete(dir); }
            catch (Exception) { } // harmless if it stays
        }
    }
}
