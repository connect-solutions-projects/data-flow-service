using System.Security.Cryptography;

namespace DataFlow.Infrastructure.Security;

public static class ClientSecretHasher
{
    private const int SaltSize = 32;
    private const int KeySize = 32;
    private const int Iterations = 100_000;

    public static (byte[] Hash, byte[] Salt) HashSecret(string secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
            throw new ArgumentException("Secret cannot be empty.", nameof(secret));

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = DeriveKey(secret, salt);
        return (hash, salt);
    }

    public static bool Verify(string secret, byte[] hash, byte[] salt)
    {
        if (hash is null || salt is null)
            return false;

        var computed = DeriveKey(secret, salt);
        return CryptographicOperations.FixedTimeEquals(computed, hash);
    }

    private static byte[] DeriveKey(string secret, byte[] salt)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(secret, salt, Iterations, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(KeySize);
    }
}

