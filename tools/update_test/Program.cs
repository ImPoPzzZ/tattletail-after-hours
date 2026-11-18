using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using KaleidoscopeEndless;

class P
{
    static int fail;
    static void Check(bool ok, string what) { Console.WriteLine((ok ? "ok   " : "FAIL ") + what); if (!ok) fail++; }

    static int Main(string[] a)
    {
        if (a.Length > 2 && a[0] == "--apply") return ApplyTest.Run(a[1], a[2]);
        // 1. a release made by make_release.ps1, checked with the real public key
        if (a.Length > 0)
        {
            string dir = a[0];
            byte[] mb = File.ReadAllBytes(Path.Combine(dir, "latest.txt"));
            string sig = File.ReadAllText(Path.Combine(dir, "latest.txt.sig"));
            Check(UpdateCore.VerifyOwner(mb, sig), "the real release's signature verifies with the owner's key");
            byte[] bad = (byte[])mb.Clone(); bad[bad.Length / 2] ^= 1;
            Check(!UpdateCore.VerifyOwner(bad, sig), "a manifest changed by one bit is refused");
            Check(!UpdateCore.VerifyOwner(mb, "AAAA"), "a wrong signature is refused");
            var m = UpdateCore.Parse(Encoding.UTF8.GetString(mb));
            Check(m != null && m.Files.Count > 0, "the manifest parses (" + (m == null ? 0 : m.Files.Count) + " files)");
            bool allHash = m != null;
            if (m != null) foreach (var e in m.Files)
            {
                string f = Path.Combine(dir, "assets", e.Asset);
                if (!File.Exists(f) || UpdateCore.Sha256HexOfFile(f) != e.Sha256 || new FileInfo(f).Length != e.Size) allHash = false;
            }
            Check(allHash, "every asset matches its hash and size in the manifest");
        }

        // 2. a fresh key pair: sign and verify (the same algorithm the release script uses)
        using (var rsa = new RSACryptoServiceProvider(2048))
        {
            byte[] data = Encoding.UTF8.GetBytes("KEAU1\nversion=1.0.0\n");
            byte[] sg = rsa.SignData(data, SHA256.Create());
            Check(UpdateCore.Verify(data, Convert.ToBase64String(sg), rsa.ToXmlString(false)), "sign and verify with a new key");
            using (var other = new RSACryptoServiceProvider(2048))
                Check(!UpdateCore.Verify(data, Convert.ToBase64String(sg), other.ToXmlString(false)), "another key does not verify it");
        }

        // 2b. the hand-made check (for old Mono) agrees with the framework on real and fake signatures
        using (var rsa = new RSACryptoServiceProvider(2048))
        using (var other = new RSACryptoServiceProvider(2048))
        {
            byte[] data = Encoding.UTF8.GetBytes("KEAU1\nversion=3.9.0\nnotes=x\n");
            byte[] hash = SHA256.Create().ComputeHash(data);
            byte[] sg = rsa.SignData(data, SHA256.Create());
            Check(UpdateCore.VerifyManual(hash, sg, rsa.ToXmlString(false)), "manual check: a good signature passes");
            byte[] badSig = (byte[])sg.Clone(); badSig[10] ^= 1;
            Check(!UpdateCore.VerifyManual(hash, badSig, rsa.ToXmlString(false)), "manual check: a changed signature fails");
            byte[] badHash = (byte[])hash.Clone(); badHash[0] ^= 1;
            Check(!UpdateCore.VerifyManual(badHash, sg, rsa.ToXmlString(false)), "manual check: a changed manifest fails");
            Check(!UpdateCore.VerifyManual(hash, sg, other.ToXmlString(false)), "manual check: another key fails");
        }
        if (a.Length > 0)
        {
            byte[] mb2 = File.ReadAllBytes(Path.Combine(a[0], "latest.txt"));
            string sig2 = File.ReadAllText(Path.Combine(a[0], "latest.txt.sig")).Trim();
            Check(UpdateCore.VerifyManual(SHA256.Create().ComputeHash(mb2), Convert.FromBase64String(sig2), UpdateKey.PublicXml), "manual check: the real release verifies with the owner's key");
        }

        // 3. versions
        Check(UpdateCore.CompareVersions("3.9.0", "3.8.0") > 0, "3.9.0 is newer than 3.8.0");
        Check(UpdateCore.CompareVersions("3.8.0", "3.8.0") == 0, "3.8.0 equals 3.8.0");
        Check(UpdateCore.CompareVersions("3.9.0", "4.0") < 0, "the public 3.9.0 is OLDER than a private 4.0: never installed over it");
        Check(UpdateCore.CompareVersions("4.0", "3.9.0") > 0, "4.0 is newer than 3.9.0");
        Check(UpdateCore.CompareVersions("3.10.0", "3.9.0") > 0, "3.10.0 is newer than 3.9.0 (numbers, not text)");
        Check(UpdateCore.CompareVersions("3.9", "3.9.0") == 0, "3.9 equals 3.9.0");
        Check(UpdateCore.CompareVersions("junk", "3.8.0") < 0, "junk counts as the oldest");

        // 4. path safety
        string[] good = { "KaleidoscopeEndless.dll", "Sounds/rc_bg.wav", "Sounds/children yay.wav" };
        string[] evil = { "../x.dll", "..\\x.dll", "/etc/passwd", "C:/Windows/x.dll", "a/../../b", "", "a//b", "Sounds/", "x:y", "a/b\0c" };
        foreach (var g in good) Check(UpdateCore.IsSafePath(g), "safe path: " + g);
        foreach (var e in evil) Check(!UpdateCore.IsSafePath(e), "unsafe path refused: " + e.Replace("\0", "\0"));
        Check(UpdateCore.IsSafeAsset("Sounds__rc_bg.wav") && !UpdateCore.IsSafeAsset("a/b.dll") && !UpdateCore.IsSafeAsset("..x"), "asset names");

        // 5. parsing refuses bad manifests
        string h = new string('a', 64);
        string ok1 = "KEAU1\nversion=3.9.0\nbase=https://x.y/z/\nfile=" + h + "|10|a.dll|a.dll\n";
        Check(UpdateCore.Parse(ok1) != null, "a valid manifest parses");
        Check(UpdateCore.Parse(ok1.Replace("KEAU1", "NOPE")) == null, "wrong header refused");
        Check(UpdateCore.Parse(ok1.Replace("https://", "http://")) == null, "a base that is not https is refused");
        Check(UpdateCore.Parse(ok1.Replace("a.dll|a.dll", "../a.dll|a.dll")) == null, "a path that escapes the folder is refused");
        Check(UpdateCore.Parse(ok1.Replace(h, "zz" + h.Substring(2))) == null, "a bad hash is refused");
        Check(UpdateCore.Parse(ok1.Replace("|10|", "|ten|")) == null, "a bad size is refused");
        Check(UpdateCore.Parse("KEAU1\nversion=3.9.0\nbase=https://x/\n") == null, "a manifest with no files is refused");
        Check(UpdateCore.Parse(ok1 + "futurekey=1\n") != null, "an unknown key is ignored (newer manifests may add lines)");

        Console.WriteLine(fail == 0 ? "ALL PASS" : fail + " FAILED");
        return fail;
    }
}
