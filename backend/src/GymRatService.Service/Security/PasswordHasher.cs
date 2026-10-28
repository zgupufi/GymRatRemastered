using System.Security.Cryptography;
using System.Text;
using GymRatService.Common.Settings;
using Microsoft.Extensions.Options;

namespace GymRatService.Service.Security;

public sealed class PasswordHasher(IOptions<PasswordSettings> options) : IPasswordHasher
{
    private const int SaltSize = 16;

    private const int HashSize = 32;

    private const int Iterations = 210_000;

    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA512;

    public HashedPassword Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Derive(password, salt);

        return new HashedPassword(Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }

    public bool Verify(string password, string hash, string salt)
    {
        var expected = Convert.FromBase64String(hash);
        var actual = Derive(password, Convert.FromBase64String(salt));

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private byte[] Derive(string password, byte[] salt)
    {
        var pepper = options.Value.Pepper;

        if (string.IsNullOrEmpty(pepper))
        {
            throw new InvalidOperationException($"{PasswordSettings.SectionName}:{nameof(PasswordSettings.Pepper)} is not configured.");
        }

        var pepperedPassword = HMACSHA256.HashData(Encoding.UTF8.GetBytes(pepper), Encoding.UTF8.GetBytes(password));

        return Rfc2898DeriveBytes.Pbkdf2(pepperedPassword, salt, Iterations, Algorithm, HashSize);
    }
}
