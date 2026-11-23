using System.Security.Cryptography;
using System.Text;

namespace GymRatService.Service.Security;

public sealed class RefreshTokenGenerator : IRefreshTokenGenerator
{
    private const int TokenBytes = 32;

    public GeneratedRefreshToken Generate()
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(TokenBytes));

        return new GeneratedRefreshToken(token, Hash(token));
    }

    public string Hash(string token)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
