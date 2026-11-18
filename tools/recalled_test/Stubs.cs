using System;
namespace UnityEngine
{
    public static class Mathf
    {
        public static float Clamp(float v, float a, float b) { return v < a ? a : v > b ? b : v; }
        public static int Clamp(int v, int a, int b) { return v < a ? a : v > b ? b : v; }
        public static float Max(float a, float b) { return Math.Max(a, b); }
        public static int Max(int a, int b) { return Math.Max(a, b); }
        public static float Min(float a, float b) { return Math.Min(a, b); }
        public static int Min(int a, int b) { return Math.Min(a, b); }
    }
}

namespace UnityEngine { public static class SystemInfo { public static string deviceUniqueIdentifier = "test-pc"; } }
namespace Steamworks
{
    public struct CSteamID { public ulong m_SteamID; }
    public static class SteamUser { public static ulong Id = 76561190000000001UL; public static bool Fail; public static CSteamID GetSteamID() { if (Fail) throw new System.Exception("no steam"); return new CSteamID { m_SteamID = Id }; } }
}
namespace KaleidoscopeEndless
{
    public static class SaveFiles { public static string Dir = "."; public static string PathFor(string f) { return System.IO.Path.Combine(Dir, f); } }
    public class FakeLog { public void LogInfo(object o) { } public void LogWarning(object o) { System.Console.WriteLine("    (log) " + o); } public void LogError(object o) { System.Console.WriteLine("    (log error) " + o); } }
    public static class KaleidoscopeEndlessPlugin { public static FakeLog Log = new FakeLog(); }
}
