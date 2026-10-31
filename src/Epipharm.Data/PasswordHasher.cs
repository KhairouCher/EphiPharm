using System;
using System.Security.Cryptography;

namespace Epipharm.Data
{
    /// <summary>
    /// PBKDF2-HMAC-SHA256, 210,000 iterations, 32-byte output. Must stay in
    /// lockstep with scripts/generate_accounts.py's hash_password(), which
    /// seeded the accounts table directly from Python's hashlib.pbkdf2_hmac.
    /// If you change ITERATIONS or the hash algorithm here, you must also
    /// change it there, or every seeded account stops matching its password.
    /// </summary>
    public static class PasswordHasher
    {
        public const int Iterations = 210_000;
        public const int HashLengthBytes = 32;
        public const int SaltLengthBytes = 16;

        public static byte[] Hash(string password, byte[] salt)
        {
            if (password == null) throw new ArgumentNullException(nameof(password));
            if (salt == null || salt.Length == 0) throw new ArgumentException("salt required", nameof(salt));

            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256))
            {
                return pbkdf2.GetBytes(HashLengthBytes);
            }
        }

        public static byte[] NewSalt()
        {
            var salt = new byte[SaltLengthBytes];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }
            return salt;
        }

        /// <summary>Constant-time comparison so a login check doesn't leak timing info.</summary>
        public static bool Matches(byte[] expectedHash, string candidatePassword, byte[] salt)
        {
            var candidateHash = Hash(candidatePassword, salt);
            return FixedTimeEquals(expectedHash, candidateHash);
        }

        private static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++)
            {
                diff |= a[i] ^ b[i];
            }
            return diff == 0;
        }
    }
}
