using System.Globalization;
using System.Resources;
using System.Security.Claims;
using GymRatService.Common.Errors;
using GymRatService.Service.Exceptions;
using Microsoft.IdentityModel.JsonWebTokens;

namespace GymRatService.API.Extensions;

public static class ClaimsPrincipalExtensions
{
    private static readonly ResourceManager Messages = new(
        "GymRatService.API.Resources.Extensions.ClaimsPrincipalExtensions",
        typeof(ClaimsPrincipalExtensions).Assembly);

    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var subject = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return Guid.TryParse(subject, out var userId)
            ? userId
            : throw new ServiceException(ErrorCodes.InvalidToken, Messages.GetString("InvalidSubject", CultureInfo.CurrentUICulture) ?? string.Empty);
    }
}
