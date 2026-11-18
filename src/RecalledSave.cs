using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

namespace KaleidoscopeEndless
{
    /// <summary>
    /// Recalled's save: one file, Recalled.sav, next to the mod's other saves, but not a text file. It's encrypted
    /// (AES), signed (HMAC-SHA256) with a key made from a secret inside the mod and your Steam account, and checked
    /// for being a character the game could have produced (RcCharacter.Problem). A file that fails any of that isn't
    /// loaded: the last good backup is tried, then a new character.
    ///
    /// It stops casual cheating (editing the file, copying someone else's). It can't stop someone who digs the secret
    /// out of the DLL: with no server, nothing can. The secret lives in RecalledSecret.cs, which is NOT in the
    /// repository (see RecalledSecret.cs.example).
    ///
    /// Who the save belongs to has to be the same every time it's written. Steam is shut down before the last save
    /// on the way out of the game, so the account is remembered from the first time Steam answers, and a save signed
    /// for this PC (what the last version wrote at that moment) still opens and is signed again for the account.
    /// </summary>
    public static class RecalledSave
    {
        private const string FILE = "Recalled.sav", BACKUP = "Recalled.sav.bak";
        private static readonly byte[] Magic = { (byte)'R', (byte)'C', (byte)'L', (byte)'1' };

        private static string FilePath { get { return SaveFiles.PathFor(FILE); } }
        private static string BackupPath { get { return SaveFiles.PathFor(BACKUP); } }

        // The most progress ever saved. Experience and bosses only ever go up in Recalled, so a character that's behind this wasn't
        // played that way: something took the progress (a load that failed, a bug), and the best one is brought back.
        private const string BEST = "Recalled.best.sav";
        private static string BestPath { get { return SaveFiles.PathFor(BEST); } }
        private static int _bestXp = -1, _bestBosses = -1;

        private static string _lastSteam;

        /// <summary>Who saves are signed for: your Steam account (remembered once Steam has said it), else this PC.</summary>
        private static string PrimaryOwner()
        {
            try
            {
                ulong id = Steamworks.SteamUser.GetSteamID().m_SteamID;
                if (id != 0) _lastSteam = id.ToString();
            }
            catch (System.Exception) { }
            return _lastSteam != null ? _lastSteam : DeviceOwner();
        }

        private static string DeviceOwner()
        {
            try { return "pc:" + SystemInfo.deviceUniqueIdentifier; }
            catch (System.Exception) { return "pc"; }
        }

        /// <summary>Everyone a save of yours could have been signed for, most likely first.</summary>
        private static List<string> Owners()
        {
            var list = new List<string> { PrimaryOwner() };
            string device = DeviceOwner();
            if (!list.Contains(device)) list.Add(device);
            if (!list.Contains("pc")) list.Add("pc");
            return list;
        }

        private static void Keys(string owner, out byte[] aes, out byte[] mac)
        {
            using (var sha = SHA256.Create())
            {
                byte[] baseKey = System.Text.Encoding.UTF8.GetBytes(RecalledSecret.Value + "|" + owner);
                aes = sha.ComputeHash(Join(baseKey, new byte[] { 1 }));
                mac = sha.ComputeHash(Join(baseKey, new byte[] { 2 }));
            }
        }

        private static byte[] Join(byte[] a, byte[] b)
        {
            var r = new byte[a.Length + b.Length];
            System.Buffer.BlockCopy(a, 0, r, 0, a.Length);
            System.Buffer.BlockCopy(b, 0, r, a.Length, b.Length);
            return r;
        }

        /// <summary>Character bytes -> the protected file's bytes (magic, IV, ciphertext, signature).</summary>
        public static byte[] Seal(byte[] plain)
        {
            byte[] aesKey, macKey;
            Keys(PrimaryOwner(), out aesKey, out macKey);
            using (var aes = new RijndaelManaged { Key = aesKey, Mode = CipherMode.CBC, Padding = PaddingMode.PKCS7 })
            {
                aes.GenerateIV();
                byte[] cipher;
                using (var enc = aes.CreateEncryptor()) cipher = enc.TransformFinalBlock(plain, 0, plain.Length);
                byte[] body = Join(Join(Magic, aes.IV), cipher);
                using (var h = new HMACSHA256(macKey)) return Join(body, h.ComputeHash(body));
            }
        }

        /// <summary>The protected file's bytes -> character bytes, or null if it was changed or isn't yours.</summary>
        public static byte[] Open(byte[] file)
        {
            bool primary;
            return Open(file, out primary);
        }

        /// <summary>As Open; 'primary' says whether it was signed for your account (false: for this PC, and worth signing again).</summary>
        public static byte[] Open(byte[] file, out bool primary)
        {
            primary = true;
            if (file == null || file.Length < Magic.Length + 16 + 16 + 32) return null;
            for (int i = 0; i < Magic.Length; i++) if (file[i] != Magic[i]) return null;
            List<string> owners = Owners();
            for (int o = 0; o < owners.Count; o++)
            {
                byte[] plain = OpenFor(file, owners[o]);
                if (plain == null) continue;
                primary = o == 0;
                return plain;
            }
            return null;
        }

        private static byte[] OpenFor(byte[] file, string owner)
        {
            byte[] aesKey, macKey;
            Keys(owner, out aesKey, out macKey);
            int bodyLen = file.Length - 32;
            using (var h = new HMACSHA256(macKey))
            {
                byte[] want = h.ComputeHash(file, 0, bodyLen);
                int diff = 0;
                for (int i = 0; i < 32; i++) diff |= want[i] ^ file[bodyLen + i];
                if (diff != 0) return null;
            }
            var iv = new byte[16];
            System.Buffer.BlockCopy(file, Magic.Length, iv, 0, 16);
            try
            {
                using (var aes = new RijndaelManaged { Key = aesKey, IV = iv, Mode = CipherMode.CBC, Padding = PaddingMode.PKCS7 })
                using (var dec = aes.CreateDecryptor())
                    return dec.TransformFinalBlock(file, Magic.Length + 16, bodyLen - Magic.Length - 16);
            }
            catch (System.Exception) { return null; }
        }

        private static RcCharacter TryLoad(string path, out string why, out bool resign)
        {
            why = null;
            resign = false;
            if (!File.Exists(path)) { why = "no file"; return null; }
            byte[] plain;
            bool primary;
            try { plain = Open(File.ReadAllBytes(path), out primary); }
            catch (System.Exception e) { why = "couldn't read it: " + e.Message; return null; }
            if (plain == null) { why = "it was changed, or it belongs to another account"; return null; }
            RcCharacter c;
            using (var r = new BinaryReader(new MemoryStream(plain))) c = RcCharacter.Read(r);
            if (c == null) { why = "it isn't a character"; return null; }
            string problem = c.Problem();
            if (problem != null) { why = "it isn't a possible character (" + problem + ")"; return null; }
            resign = !primary;
            return c;
        }

        /// <summary>Your character: the save, else its backup, else a new one. 'note' says so when the save was rejected.</summary>
        public static RcCharacter Load(out string note)
        {
            note = null;
            string why, whyBackup;
            bool resign;
            RcCharacter c = TryLoad(FilePath, out why, out resign);
            if (c != null)
            {
                if (resign) { KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Recalled] The save was signed for this PC: signing it again for your account."); Save(c); }
                return Best(c, ref note, "KE: [Recalled] Loaded the save");
            }
            if (why != "no file") KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Recalled] Save rejected: " + why + ".");
            bool unreadable = why != "no file";
            c = TryLoad(BackupPath, out whyBackup, out resign);
            if (c != null)
            {
                if (unreadable) note = "Your save couldn't be loaded. Your last good backup was loaded instead.";
                KaleidoscopeEndlessPlugin.Log.LogInfo("KE: [Recalled] Loaded the backup save.");
                if (resign) Save(c);
                return Best(c, ref note, "KE: [Recalled] Loaded the backup");
            }
            if (unreadable)
            {
                note = "Your save couldn't be loaded, so this is a new character.";
                KeepUnreadable(); // (the file is about to be saved over: keep a copy of it)
            }
            return Best(new RcCharacter(), ref note, "KE: [Recalled] Started a new character");
        }

        /// <summary>The character, unless a save of more progress exists: then that one.</summary>
        private static RcCharacter Best(RcCharacter c, ref string note, string what)
        {
            string why;
            bool resign;
            RcCharacter best = TryLoad(BestPath, out why, out resign);
            if (best != null && (best.Xp > c.Xp || best.BossesBeaten > c.BossesBeaten))
            {
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Recalled] The save had less progress (xp " + c.Xp + ", " + c.BossesBeaten + " bosses) than the best one ever saved (xp " + best.Xp + ", " + best.BossesBeaten + " bosses): restored the best.");
                note = "Your progress was recovered from a backup.";
                c = best;
                Save(c);
            }
            _bestXp = System.Math.Max(_bestXp, c.Xp); _bestBosses = System.Math.Max(_bestBosses, c.BossesBeaten);
            KaleidoscopeEndlessPlugin.Log.LogInfo(what + ": level " + c.Level + ", xp " + c.Xp + ", " + c.BossesBeaten + " boss(es), job " + c.Quest + ".");
            return c;
        }

        private static void KeepUnreadable()
        {
            try
            {
                if (!File.Exists(FilePath)) return;
                string keep = SaveFiles.PathFor("Recalled.unreadable-" + System.DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".sav");
                File.Copy(FilePath, keep, true);
                KaleidoscopeEndlessPlugin.Log.LogWarning("KE: [Recalled] Kept a copy of the save that wouldn't load: " + keep);
            }
            catch (System.Exception) { }
        }

        public static void Save(RcCharacter c)
        {
            if (c == null) return;
            try
            {
                byte[] plain;
                using (var ms = new MemoryStream())
                {
                    using (var w = new BinaryWriter(ms)) c.Write(w);
                    plain = ms.ToArray();
                }
                byte[] sealedBytes = Seal(plain);
                string tmp = FilePath + ".tmp";
                File.WriteAllBytes(tmp, sealedBytes);
                if (File.Exists(FilePath)) File.Replace(tmp, FilePath, BackupPath); // the old one becomes the backup
                else File.Move(tmp, FilePath);
                // The best progress ever saved is kept apart, and only ever replaced by as much or more.
                // (never over one that isn't ours to read: another account's, or one this run couldn't open)
                if (c.Xp >= _bestXp && c.BossesBeaten >= _bestBosses && (!File.Exists(BestPath) || Open(File.ReadAllBytes(BestPath)) != null))
                {
                    File.WriteAllBytes(BestPath, sealedBytes);
                    _bestXp = c.Xp; _bestBosses = c.BossesBeaten;
                }
            }
            catch (System.Exception e)
            {
                KaleidoscopeEndlessPlugin.Log.LogError("KE: [Recalled] Couldn't save: " + e.Message);
            }
        }
    }
}
