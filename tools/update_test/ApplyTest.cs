using System;
using System.IO;
using System.Reflection;
using System.Text;
using KaleidoscopeEndless;

/// <summary>Runs the real updater DLL against a temporary BepInEx folder: a good staged update, a damaged file, a tampered manifest.</summary>
static class ApplyTest
{
    static int fail;
    static void Check(bool ok, string what) { Console.WriteLine((ok ? "ok   " : "FAIL ") + what); if (!ok) fail++; }

    public static int Run(string relDir, string patcherDll)
    {
        string manifestText = File.ReadAllText(Path.Combine(relDir, "latest.txt"));
        var m = UpdateCore.Parse(manifestText);
        // 1. a good staged update replaces the old files
        string root = NewTree(relDir, m, patcherDll, out string bep, out string plug);
        string oldDll = File.ReadAllText(Path.Combine(plug, "KaleidoscopeEndless.dll"));
        RunPatcher(Path.Combine(bep, "patchers", "AfterHoursUpdater.dll"));
        string newHash = UpdateCore.Sha256HexOfFile(Path.Combine(plug, "KaleidoscopeEndless.dll"));
        Check(newHash == m.Files[0].Sha256, "the mod's DLL was replaced by the staged one");
        Check(File.Exists(Path.Combine(bep, "afterhours_updated.txt")) && File.ReadAllText(Path.Combine(bep, "afterhours_updated.txt")) == m.Version, "the 'updated' note was left for the mod");
        Check(!Directory.Exists(Path.Combine(bep, "AfterHoursUpdate")), "the staging folder was removed");
        Check(!File.Exists(Path.Combine(plug, "KaleidoscopeEndless.dll.new")), "no temporary file is left");
        Check(File.Exists(Path.Combine(plug, "Sounds", "rc_bg.wav")), "a sound file was put in its folder");

        // 2. a damaged staged file: nothing changes
        root = NewTree(relDir, m, patcherDll, out bep, out plug);
        string staged = Path.Combine(bep, "AfterHoursUpdate", m.Version, "KaleidoscopeEndless.dll");
        File.AppendAllText(staged, "x");
        string before = UpdateCore.Sha256HexOfFile(Path.Combine(plug, "KaleidoscopeEndless.dll"));
        RunPatcher(Path.Combine(bep, "patchers", "AfterHoursUpdater.dll"));
        Check(UpdateCore.Sha256HexOfFile(Path.Combine(plug, "KaleidoscopeEndless.dll")) == before, "a damaged staged file leaves the installed mod untouched");
        Check(!Directory.Exists(Path.Combine(bep, "AfterHoursUpdate")), "and the staged update is dropped");

        // 3. a tampered manifest: nothing changes
        root = NewTree(relDir, m, patcherDll, out bep, out plug);
        string mf = Path.Combine(bep, "AfterHoursUpdate", "manifest.txt");
        File.WriteAllText(mf, File.ReadAllText(mf).Replace("version=3.9.0", "version=3.9.1"));
        File.WriteAllText(Path.Combine(bep, "AfterHoursUpdate", "ready.txt"), "3.9.1");
        before = UpdateCore.Sha256HexOfFile(Path.Combine(plug, "KaleidoscopeEndless.dll"));
        RunPatcher(Path.Combine(bep, "patchers", "AfterHoursUpdater.dll"));
        Check(UpdateCore.Sha256HexOfFile(Path.Combine(plug, "KaleidoscopeEndless.dll")) == before, "a manifest not signed by the owner changes nothing");

        // 4. nothing staged: nothing happens
        root = NewTree(relDir, m, patcherDll, out bep, out plug);
        Directory.Delete(Path.Combine(bep, "AfterHoursUpdate"), true);
        before = UpdateCore.Sha256HexOfFile(Path.Combine(plug, "KaleidoscopeEndless.dll"));
        RunPatcher(Path.Combine(bep, "patchers", "AfterHoursUpdater.dll"));
        Check(UpdateCore.Sha256HexOfFile(Path.Combine(plug, "KaleidoscopeEndless.dll")) == before && !File.Exists(Path.Combine(bep, "afterhours_updated.txt")), "with nothing staged the updater does nothing");

        Console.WriteLine(fail == 0 ? "APPLY: ALL PASS" : "APPLY: " + fail + " FAILED");
        return fail;
    }

    // A fake game: BepInEx\patchers (a copy of the updater), the mod's old files, and a staged update made from the release folder.
    static string NewTree(string relDir, UpdateCore.Manifest m, string patcherDll, out string bep, out string plug)
    {
        string root = Path.Combine(Path.GetTempPath(), "ke_apply_" + Guid.NewGuid().ToString("N").Substring(0, 8));
        bep = Path.Combine(root, "BepInEx");
        plug = Path.Combine(bep, "plugins", "KaleidoscopeEndless");
        Directory.CreateDirectory(Path.Combine(bep, "patchers"));
        Directory.CreateDirectory(Path.Combine(plug, "Sounds"));
        File.Copy(patcherDll, Path.Combine(bep, "patchers", "AfterHoursUpdater.dll"));
        File.WriteAllText(Path.Combine(plug, "KaleidoscopeEndless.dll"), "OLD MOD");
        string stg = Path.Combine(bep, "AfterHoursUpdate");
        string dir = Path.Combine(stg, m.Version);
        foreach (var e in m.Files)
        {
            string dest = Path.Combine(dir, e.Path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(dest));
            File.Copy(Path.Combine(relDir, "assets", e.Asset), dest);
        }
        File.Copy(Path.Combine(relDir, "latest.txt"), Path.Combine(stg, "manifest.txt"));
        File.WriteAllText(Path.Combine(stg, "manifest.sig"), File.ReadAllText(Path.Combine(relDir, "latest.txt.sig")).Trim());
        File.WriteAllText(Path.Combine(stg, "ready.txt"), m.Version);
        return root;
    }

    static void RunPatcher(string dll)
    {
        var alc = new System.Runtime.Loader.AssemblyLoadContext(Guid.NewGuid().ToString(), true);   // (a fresh copy of the updater each time: it keeps a static "already ran" flag and finds its folder from where it was loaded)
        Assembly a = alc.LoadFromAssemblyPath(dll);
        Type t = a.GetType("AfterHoursUpdater.Patcher");
        t.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
    }
}
