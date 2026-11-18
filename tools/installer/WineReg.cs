using System;
using System.Collections.Generic;
using System.Text;

namespace AfterHoursInstaller
{
    /// <summary>
    /// Edits a Wine registry file (user.reg): the one setting the mod loader needs on Linux, winhttp = native,builtin for tattletailWindows.exe
    /// (the same thing as the launch option WINEDLLOVERRIDES="winhttp=n,b", but kept for good). Pure text, no Windows or Wine calls, so it can be tested anywhere.
    /// </summary>
    public static class WineReg
    {
        public const string Section = @"[Software\\Wine\\AppDefaults\\tattletailWindows.exe\\DllOverrides]";
        public const string Line = "\"winhttp\"=\"native,builtin\"";

        private static bool IsHeader(string s) { return s.StartsWith("[", StringComparison.Ordinal); }

        /// <summary>The registry text with the override in it (added, or corrected if the section already had a different winhttp value).</summary>
        public static string Enable(string text, out bool changed)
        {
            changed = false;
            string nl = text.Contains("\r\n") ? "\r\n" : "\n";
            var lines = new List<string>(text.Replace("\r\n", "\n").Split('\n'));
            int at = FindSection(lines);
            if (at < 0)
            {
                while (lines.Count > 0 && lines[lines.Count - 1].Length == 0) lines.RemoveAt(lines.Count - 1);
                lines.Add("");
                lines.Add(Section + " " + DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                lines.Add("#time=" + DateTime.UtcNow.ToFileTimeUtc().ToString("x"));
                lines.Add(Line);
                lines.Add("");
                changed = true;
                return string.Join(nl, lines.ToArray());
            }
            int end = at + 1;
            int lastMeta = at;
            for (; end < lines.Count && !IsHeader(lines[end]) && lines[end].Length > 0; end++)
            {
                if (lines[end].StartsWith("#", StringComparison.Ordinal) || lines[end].StartsWith(";", StringComparison.Ordinal)) lastMeta = end;
                if (lines[end].StartsWith("\"winhttp\"=", StringComparison.OrdinalIgnoreCase))
                {
                    if (lines[end] != Line) { lines[end] = Line; changed = true; }
                    return string.Join(nl, lines.ToArray());
                }
            }
            lines.Insert(lastMeta + 1, Line);
            changed = true;
            return string.Join(nl, lines.ToArray());
        }

        /// <summary>The registry text without the mod's override (nothing else is touched).</summary>
        public static string Disable(string text, out bool changed)
        {
            changed = false;
            string nl = text.Contains("\r\n") ? "\r\n" : "\n";
            var lines = new List<string>(text.Replace("\r\n", "\n").Split('\n'));
            int at = FindSection(lines);
            if (at < 0) return text;
            for (int i = at + 1; i < lines.Count && !IsHeader(lines[i]) && lines[i].Length > 0; i++)
                if (lines[i].StartsWith("\"winhttp\"=", StringComparison.OrdinalIgnoreCase)) { lines.RemoveAt(i); changed = true; break; }
            return changed ? string.Join(nl, lines.ToArray()) : text;
        }

        public static bool HasOverride(string text)
        {
            var lines = new List<string>(text.Replace("\r\n", "\n").Split('\n'));
            int at = FindSection(lines);
            if (at < 0) return false;
            for (int i = at + 1; i < lines.Count && !IsHeader(lines[i]) && lines[i].Length > 0; i++)
                if (lines[i] == Line) return true;
            return false;
        }

        private static int FindSection(List<string> lines)
        {
            for (int i = 0; i < lines.Count; i++)
                if (lines[i].StartsWith(Section, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }
    }
}
