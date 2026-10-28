using System.Security.Claims;
using System.Text;
using GymRatService.Common.DTOs.Auth;
using GymRatService.Common.Settings;
using GymRatService.Repository.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace GymRatService.Service.Security;

public sealed class JwtTokenGenerator(IOptions<JwtSettings> options, TimeProvider timeProvider) : ITokenGenerator
{
    private readonly JsonWebTokenHandler _tokenHandler = new();

    public LoginResponse Generate(User user)
    {
        var settings = options.Value;

        if (string.IsNullOrEmpty(settings.Key))
        {
            throw new InvalidOperationException($"{JwtSettings.SectionName}:{nameof(JwtSettings.Key)} is not configured.");
        }

        var issuedAt = timeProvider.GetUtcNow();
        var expiresAt = issuedAt.AddMinutes(settings.ExpiryMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
            ]),
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            IssuedAt = issuedAt.UtcDateTime,
            NotBefore = issuedAt.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key)),
                SecurityAlgorithms.HmacSha256),
        };

        return new LoginResponse(_tokenHandler.CreateToken(descriptor), expiresAt);
    }
}
