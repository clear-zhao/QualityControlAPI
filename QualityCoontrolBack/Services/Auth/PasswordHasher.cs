using System.Security.Cryptography;

namespace QualityControlAPI.Services.Auth
{
    public static class PasswordHasher
    {
        private const string Prefix = "pbkdf2";
        private const string Algorithm = "sha256";
        private const int SaltSize = 16;   // 128-bit
        private const int KeySize = 32;    // 256-bit
        private const int Iterations = 100_000;

        public static bool IsHashed(string? storedPassword)
            => !string.IsNullOrWhiteSpace(storedPassword)
               && storedPassword.StartsWith($"{Prefix}$", StringComparison.OrdinalIgnoreCase);

        public static string HashPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("密码不能为空", nameof(password));

            var salt = RandomNumberGenerator.GetBytes(SaltSize);
            var hash = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                Iterations,
                HashAlgorithmName.SHA256,
                KeySize);

            return $"{Prefix}${Algorithm}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
        }

        public static bool VerifyPassword(string inputPassword, string? storedPassword)
        {
            if (string.IsNullOrWhiteSpace(inputPassword) || string.IsNullOrWhiteSpace(storedPassword))
                return false;

            // 新格式：PBKDF2 哈希；旧格式：明文（兼容历史数据）
            if (!TryVerifyHashedPassword(inputPassword, storedPassword, out var verified))
                return string.Equals(inputPassword, storedPassword, StringComparison.Ordinal);

            return verified;
        }

        private static bool TryVerifyHashedPassword(string inputPassword, string storedPassword, out bool verified)
        {
            verified = false;

            var parts = storedPassword.Split('$');
            if (parts.Length != 5)
                return false;

            if (!parts[0].Equals(Prefix, StringComparison.OrdinalIgnoreCase) ||
                !parts[1].Equals(Algorithm, StringComparison.OrdinalIgnoreCase))
                return false;

            if (!int.TryParse(parts[2], out var iterations) || iterations <= 0)
                return false;

            byte[] salt;
            byte[] expectedHash;
            try
            {
                salt = Convert.FromBase64String(parts[3]);
                expectedHash = Convert.FromBase64String(parts[4]);
            }
            catch (FormatException)
            {
                return false;
            }

            var actualHash = Rfc2898DeriveBytes.Pbkdf2(
                inputPassword,
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                expectedHash.Length);

            verified = CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
            return true;
        }
    }
}
