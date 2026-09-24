using System.Security.Cryptography;

namespace OrdenaPC.Core;

/// <summary>El PIN se guarda derivado con PBKDF2 y sal, nunca en texto plano.</summary>
public static class PinHasher
{
    private const int Iterations = 100_000;

    public static bool IsValidFormat(string pin) => pin.Length is >= 4 and <= 8 && pin.All(char.IsDigit);

    public static (string Hash, string Salt) Create(string pin)
    {
        var salt = new byte[16];
        using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(salt);
        return (Convert.ToBase64String(Derive(pin, salt)), Convert.ToBase64String(salt));
    }

    public static bool Verify(string pin, string hash, string salt)
    {
        try
        {
            var expected = Convert.FromBase64String(hash);
            var actual = Derive(pin, Convert.FromBase64String(salt));
            if (expected.Length != actual.Length) return false;
            int diff = 0;
            for (int i = 0; i < actual.Length; i++) diff |= expected[i] ^ actual[i];
            return diff == 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static byte[] Derive(string pin, byte[] salt)
    {
        using var kdf = new Rfc2898DeriveBytes(pin, salt, Iterations, HashAlgorithmName.SHA256);
        return kdf.GetBytes(32);
    }
}
