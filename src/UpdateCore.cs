using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// The auto-updater's pure logic (no Unity, no BepInEx): the release manifest, its signature, hashes and path safety.
    /// Shared by the mod (it downloads and stages an update) and by the updater (it applies a staged update before the mod loads), and
    /// tested headless. Old-Mono safe: no LINQ, no Func/Action, no Type ==.
    ///
    /// The manifest is plain text (LF lines), signed as exact bytes (RSA, SHA-256, PKCS#1) by tools/release/make_release.ps1:
    ///   KEAU1
    ///   version=3.9.0
    ///   notes=one line
    ///   base=https://.../download/v3.9.0/
    ///   file=SHA256HEX|SIZE|path/inside/the/mod/folder|asset-name-on-the-server
    /// </summary>
    public static class UpdateCore
    {
        public const string Header = "KEAU1";

        public class Entry { public string Sha256, Path, Asset; public long Size; }

        public class Manifest
        {
            public string Version = "", Notes = "", Base = "";
            public List<Entry> Files = new List<Entry>();
        }

        /// <summary>Reads a manifest. Null if it is not a valid one (wrong header, a bad line, an unsafe path).</summary>
        public static Manifest Parse(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            if (lines.Length == 0 || lines[0].Trim() != Header) return null;
            var m = new Manifest();
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0) continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) return null;
                string key = line.Substring(0, eq), val = line.Substring(eq + 1);
                if (key == "version") m.Version = val.Trim();
                else if (key == "notes") m.Notes = val.Trim();
                else if (key == "base") m.Base = val.Trim();
                else if (key == "file")
                {
                    string[] f = val.Split('|');
                    if (f.Length != 4) return null;
                    long size;
                    if (!long.TryParse(f[1], out size) || size < 0) return null;
                    var e = new Entry { Sha256 = f[0].Trim().ToLowerInvariant(), Size = size, Path = f[2].Trim().Replace('\\', '/'), Asset = f[3].Trim() };
                    if (e.Sha256.Length != 64 || !IsHex(e.Sha256) || !IsSafePath(e.Path) || !IsSafeAsset(e.Asset)) return null;
                    m.Files.Add(e);
                }
                // (unknown keys are ignored, so a newer manifest can add lines)
            }
            if (CompareVersions(m.Version, "0.0.0") <= 0 || m.Files.Count == 0) return null;
            if (!m.Base.StartsWith("https://", StringComparison.Ordinal)) return null;
            return m;
        }

        private static bool IsHex(string s)
        {
            foreach (char c in s) if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
            return true;
        }

        /// <summary>A path inside the mod's folder: relative, forward slashes, no "..", no drive, no odd characters.</summary>
        public static bool IsSafePath(string p)
        {
            if (string.IsNullOrEmpty(p) || p.Length > 200) return false;
            if (p[0] == '/' || p[0] == '\\' || p.IndexOf(':') >= 0 || p.IndexOf("..", StringComparison.Ordinal) >= 0 || p.IndexOf("//", StringComparison.Ordinal) >= 0) return false;
            foreach (char c in p)
                if (!(char.IsLetterOrDigit(c) || c == '/' || c == '.' || c == '_' || c == '-' || c == ' ')) return false;
            return !p.EndsWith("/", StringComparison.Ordinal);
        }

        /// <summary>A file name on the server (no folders).</summary>
        public static bool IsSafeAsset(string a)
        {
            if (string.IsNullOrEmpty(a) || a.Length > 200) return false;
            foreach (char c in a)
                if (!(char.IsLetterOrDigit(c) || c == '.' || c == '_' || c == '-')) return false;
            return a.IndexOf("..", StringComparison.Ordinal) < 0;
        }

        /// <summary>Compares "3.9.0" style versions: negative if a is older, 0 if the same, positive if a is newer. Anything unreadable counts as 0.0.0.</summary>
        public static int CompareVersions(string a, string b)
        {
            Version va = Read(a), vb = Read(b);
            return va.CompareTo(vb);
        }

        private static Version Read(string s)
        {
            try
            {
                if (string.IsNullOrEmpty(s)) return new Version(0, 0, 0);
                string[] p = s.Trim().Split('.');
                int major = p.Length > 0 ? int.Parse(p[0]) : 0, minor = p.Length > 1 ? int.Parse(p[1]) : 0, build = p.Length > 2 ? int.Parse(p[2]) : 0;
                return new Version(major, minor, build);
            }
            catch { return new Version(0, 0, 0); }
        }

        public static string Sha256Hex(byte[] data)
        {
            using (var sha = SHA256.Create()) return Hex(sha.ComputeHash(data));
        }

        public static string Sha256HexOfFile(string path)
        {
            using (var f = File.OpenRead(path))
            using (var sha = SHA256.Create()) return Hex(sha.ComputeHash(f));
        }

        private static string Hex(byte[] h)
        {
            var sb = new StringBuilder(h.Length * 2);
            foreach (byte b in h) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        /// <summary>Why the last Verify said no (for the log): an exception text, "bad signature", ...</summary>
        public static string LastError = "";

        /// <summary>Is this signature (base64) a valid RSA / SHA-256 signature of exactly these bytes, by the key?</summary>
        public static bool Verify(byte[] manifestBytes, string signatureBase64, string publicKeyXml)
        {
            LastError = "";
            byte[] sig;
            byte[] hash;
            try
            {
                sig = Convert.FromBase64String((signatureBase64 ?? "").Trim());
                using (var sha = SHA256.Create()) hash = sha.ComputeHash(manifestBytes);
            }
            catch (Exception e) { LastError = "bad input: " + e.Message; return false; }

            // 1. The framework's own check (not every old Mono does RSA / SHA-256 this way).
            try
            {
                using (var rsa = new RSACryptoServiceProvider())
                {
                    rsa.FromXmlString(publicKeyXml);
                    if (rsa.VerifyHash(hash, CryptoConfig.MapNameToOID("SHA256"), sig)) return true;
                }
            }
            catch (Exception e) { LastError = "framework check: " + e.Message; }

            // 2. Our own PKCS#1 v1.5 check with plain arithmetic (works wherever the framework's does not).
            try
            {
                if (VerifyManual(hash, sig, publicKeyXml)) { LastError = ""; return true; }
                if (LastError.Length == 0) LastError = "the signature does not match";
            }
            catch (Exception e) { LastError = "manual check: " + e.Message; }
            return false;
        }

        private static byte[] XmlPart(string xml, string tag)
        {
            int a = xml.IndexOf("<" + tag + ">", StringComparison.Ordinal);
            int b = xml.IndexOf("</" + tag + ">", StringComparison.Ordinal);
            if (a < 0 || b < 0) throw new Exception("no " + tag + " in the key");
            a += tag.Length + 2;
            return Convert.FromBase64String(xml.Substring(a, b - a).Trim());
        }

        private static readonly byte[] Sha256Prefix = { 0x30, 0x31, 0x30, 0x0d, 0x06, 0x09, 0x60, 0x86, 0x48, 0x01, 0x65, 0x03, 0x04, 0x02, 0x01, 0x05, 0x00, 0x04, 0x20 };

        /// <summary>RSASSA-PKCS1-v1_5 with SHA-256: signature^e mod n must be 00 01 FF..FF 00 + DigestInfo + hash.</summary>
        public static bool VerifyManual(byte[] hash, byte[] sig, string publicKeyXml)
        {
            byte[] n = XmlPart(publicKeyXml, "Modulus");
            byte[] e = XmlPart(publicKeyXml, "Exponent");
            int k = n.Length;
            if (sig.Length != k) return false;
            byte[] m = ModPow(sig, e, n, k);
            if (m == null) return false;

            int tLen = Sha256Prefix.Length + hash.Length;
            if (k < tLen + 11) return false;
            byte[] expect = new byte[k];
            expect[1] = 1;
            for (int i = 2; i < k - tLen - 1; i++) expect[i] = 0xff;
            expect[k - tLen - 1] = 0;
            Array.Copy(Sha256Prefix, 0, expect, k - tLen, Sha256Prefix.Length);
            Array.Copy(hash, 0, expect, k - hash.Length, hash.Length);
            int diff = 0;
            for (int i = 0; i < k; i++) diff |= m[i] ^ expect[i];
            return diff == 0;
        }

        // ── Tiny big-number arithmetic (little-endian uint limbs): only what an RSA public operation needs ──
        private static uint[] ToLimbs(byte[] be, int limbs)
        {
            var r = new uint[limbs];
            for (int i = 0; i < be.Length; i++)
            {
                int pos = be.Length - 1 - i;   // byte position from the least significant end
                r[pos / 4] |= (uint)be[i] << (8 * (pos % 4));
            }
            return r;
        }

        private static int Cmp(uint[] a, uint[] b)
        {
            for (int i = a.Length - 1; i >= 0; i--) if (a[i] != b[i]) return a[i] > b[i] ? 1 : -1;
            return 0;
        }

        private static void Add(uint[] a, uint[] b)
        {
            ulong carry = 0;
            for (int i = 0; i < a.Length; i++) { ulong s = (ulong)a[i] + b[i] + carry; a[i] = (uint)s; carry = s >> 32; }
        }

        private static void Sub(uint[] a, uint[] b)
        {
            long borrow = 0;
            for (int i = 0; i < a.Length; i++) { long d = (long)a[i] - b[i] - borrow; if (d < 0) { d += 4294967296L; borrow = 1; } else borrow = 0; a[i] = (uint)d; }
        }

        private static uint[] MulMod(uint[] a, uint[] b, uint[] n)
        {
            var r = new uint[n.Length];
            for (int bit = b.Length * 32 - 1; bit >= 0; bit--)
            {
                Add(r, r); if (Cmp(r, n) >= 0) Sub(r, n);
                if (((b[bit / 32] >> (bit % 32)) & 1) != 0) { Add(r, a); if (Cmp(r, n) >= 0) Sub(r, n); }
            }
            return r;
        }

        private static byte[] ModPow(byte[] baseBe, byte[] expBe, byte[] modBe, int k)
        {
            int limbs = k / 4 + 2;   // (room above n so 2n never wraps)
            uint[] n = ToLimbs(modBe, limbs);
            uint[] b = ToLimbs(baseBe, limbs);
            if (Cmp(b, n) >= 0) return null;   // (a signature must be smaller than the modulus)
            uint[] result = new uint[limbs]; result[0] = 1;
            uint[] e = ToLimbs(expBe, (expBe.Length + 3) / 4);
            for (int bit = e.Length * 32 - 1; bit >= 0; bit--)
            {
                result = MulMod(result, result, n);
                if (((e[bit / 32] >> (bit % 32)) & 1) != 0) result = MulMod(result, b, n);
            }
            var outBe = new byte[k];
            for (int pos = 0; pos < k; pos++) outBe[k - 1 - pos] = (byte)(result[pos / 4] >> (8 * (pos % 4)));
            return outBe;
        }

        public static bool VerifyOwner(byte[] manifestBytes, string signatureBase64) { return Verify(manifestBytes, signatureBase64, UpdateKey.PublicXml); }
    }
}
