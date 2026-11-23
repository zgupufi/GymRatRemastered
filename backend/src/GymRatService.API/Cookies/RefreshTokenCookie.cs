using GymRatService.API.Extensions;
using GymRatService.Common.DTOs.Auth;
using GymRatService.Common.Settings;
using Microsoft.Extensions.Options;

namespace GymRatService.API.Cookies;

public sealed class RefreshTokenCookie(IOptions<RefreshTokenSettings> options, IHostEnvironment environment)
{
    private const string CookiePath = "/api/auth";

    public string? Read(HttpRequest request)
    {
        return request.Cookies.TryGetValue(options.Value.CookieName, out var value) ? value : null;
    }

    public void Write(HttpResponse response, AuthSession session)
    {
        var cookieOptions = CreateOptions();
        cookieOptions.Expires = session.RefreshTokenExpiresAt;

        response.Cookies.Append(options.Value.CookieName, session.RefreshToken, cookieOptions);
    }

    public void Delete(HttpResponse response)
    {
        response.Cookies.Delete(options.Value.CookieName, CreateOptions());
    }

    private CookieOptions CreateOptions()
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = !environment.AllowsDevelopmentSettings(),
            SameSite = options.Value.SameSite switch
            {
                RefreshCookieSameSite.Lax => SameSiteMode.Lax,
                RefreshCookieSameSite.None => SameSiteMode.None,
                _ => SameSiteMode.Strict,
            },
            Path = CookiePath,
            IsEssential = true,
        };
    }
}
