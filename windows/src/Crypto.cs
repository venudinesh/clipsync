// At rest encryption for everything the app keeps.
//
// The phone build leans on Hive's AES boxes with the key held in the Android
// Keystore. The Windows equivalent is DPAPI: a 64 byte master secret is sealed
// to the current Windows account, so the key file is inert if it is copied to
// another machine or read by another user, and no passphrase has to be invented
// for the person using the app.
//
// Records are encrypted then authenticated. A store that fails its tag is
// refused rather than decrypted, which is what stops a silently corrupted or
// edited history file from being loaded as if it were ours.

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ClipSyncAI
{
    internal static class Crypto
    {
        private const int KeyBytes = 32;   // AES-256
        private const int MacBytes = 32;   // HMAC-SHA256
        private const int IvBytes = 16;
        private static readonly byte[] Magic = new byte[] { (byte)'C', (byte)'S', (byte)'A', (byte)'1' };

        /// Extra entropy mixed into DPAPI. Not a secret, and not pretending to
        /// be one: it scopes the sealed blob to this application so another
        /// program running as the same user cannot unseal it by accident.
        private static readonly byte[] Entropy =
            Encoding.UTF8.GetBytes("ClipSyncAI/v1/local-store");

        private static byte[] _master;
        private static readonly object Gate = new object();

        public static byte[] Random(int n)
        {
            byte[] b = new byte[n];
            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider()) rng.GetBytes(b);
            return b;
        }

        /// Drops the cached master secret so the next call reloads it. Used by
        /// the test suite after it repoints the store directory.
        public static void Reset()
        {
            lock (Gate) { _master = null; }
        }

        /// Loads the master secret, creating it on first run. The key file is
        /// written with a restrictive ACL by virtue of living under the user's
        /// roaming profile; DPAPI is what actually protects it.
        public static byte[] Master()
        {
            lock (Gate)
            {
                if (_master != null) return _master;
                string path = Paths.KeyFile;
                if (File.Exists(path))
                {
                    try
                    {
                        byte[] sealedKey = File.ReadAllBytes(path);
                        byte[] raw = ProtectedData.Unprotect(
                            sealedKey, Entropy, DataProtectionScope.CurrentUser);
                        if (raw != null && raw.Length == KeyBytes + MacBytes)
                        {
                            _master = raw;
                            return _master;
                        }
                    }
                    catch (Exception)
                    {
                        // An unreadable key means the existing stores cannot be
                        // opened either. Rotating is the only way forward; the
                        // store layer keeps the old file aside rather than
                        // deleting it, so nothing is destroyed here.
                        try { File.Move(path, path + ".unreadable-" + Clock.NowMs()); }
                        catch (Exception) { }
                    }
                }
                _master = Random(KeyBytes + MacBytes);
                byte[] blob = ProtectedData.Protect(_master, Entropy, DataProtectionScope.CurrentUser);
                Paths.EnsureRoot();
                File.WriteAllBytes(path, blob);
                return _master;
            }
        }

        private static byte[] AesKey(byte[] master)
        {
            byte[] k = new byte[KeyBytes];
            Buffer.BlockCopy(master, 0, k, 0, KeyBytes);
            return k;
        }

        private static byte[] MacKey(byte[] master)
        {
            byte[] k = new byte[MacBytes];
            Buffer.BlockCopy(master, KeyBytes, k, 0, MacBytes);
            return k;
        }

        public static byte[] Seal(string text)
        {
            byte[] master = Master();
            byte[] plain = Encoding.UTF8.GetBytes(text ?? "");
            byte[] iv = Random(IvBytes);
            byte[] cipher;
            using (AesCryptoServiceProvider aes = new AesCryptoServiceProvider())
            {
                aes.KeySize = 256;
                aes.BlockSize = 128;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = AesKey(master);
                aes.IV = iv;
                using (ICryptoTransform enc = aes.CreateEncryptor())
                {
                    cipher = enc.TransformFinalBlock(plain, 0, plain.Length);
                }
            }
            byte[] body = new byte[Magic.Length + IvBytes + cipher.Length];
            Buffer.BlockCopy(Magic, 0, body, 0, Magic.Length);
            Buffer.BlockCopy(iv, 0, body, Magic.Length, IvBytes);
            Buffer.BlockCopy(cipher, 0, body, Magic.Length + IvBytes, cipher.Length);
            byte[] tag;
            using (HMACSHA256 h = new HMACSHA256(MacKey(master))) tag = h.ComputeHash(body);
            byte[] all = new byte[body.Length + MacBytes];
            Buffer.BlockCopy(body, 0, all, 0, body.Length);
            Buffer.BlockCopy(tag, 0, all, body.Length, MacBytes);
            return all;
        }

        /// Returns null when the blob is not ours, was truncated, or fails its
        /// tag. Every caller treats null as "no data".
        public static string Open(byte[] blob)
        {
            if (blob == null || blob.Length < Magic.Length + IvBytes + MacBytes + 16) return null;
            for (int i = 0; i < Magic.Length; i++) if (blob[i] != Magic[i]) return null;
            byte[] master = Master();
            int bodyLen = blob.Length - MacBytes;
            byte[] tag = new byte[MacBytes];
            using (HMACSHA256 h = new HMACSHA256(MacKey(master)))
            {
                tag = h.ComputeHash(blob, 0, bodyLen);
            }
            if (!FixedTimeEquals(tag, 0, blob, bodyLen, MacBytes)) return null;
            byte[] iv = new byte[IvBytes];
            Buffer.BlockCopy(blob, Magic.Length, iv, 0, IvBytes);
            int cipherLen = bodyLen - Magic.Length - IvBytes;
            try
            {
                using (AesCryptoServiceProvider aes = new AesCryptoServiceProvider())
                {
                    aes.KeySize = 256;
                    aes.BlockSize = 128;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    aes.Key = AesKey(master);
                    aes.IV = iv;
                    using (ICryptoTransform dec = aes.CreateDecryptor())
                    {
                        byte[] plain = dec.TransformFinalBlock(blob, Magic.Length + IvBytes, cipherLen);
                        return Encoding.UTF8.GetString(plain);
                    }
                }
            }
            catch (CryptographicException)
            {
                return null;
            }
        }

        /// Comparison that does not return early, so a wrong tag cannot be
        /// narrowed down by timing it.
        private static bool FixedTimeEquals(byte[] a, int ao, byte[] b, int bo, int len)
        {
            int diff = 0;
            for (int i = 0; i < len; i++) diff |= a[ao + i] ^ b[bo + i];
            return diff == 0;
        }
    }
}
