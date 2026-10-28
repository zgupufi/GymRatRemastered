using System.Security.Claims;
using GymRatService.Common.Errors;
using GymRatService.Service.Exceptions;
using Microsoft.IdentityModel.JsonWebTokens;

namespace GymRatService.API.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var subject = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return Guid.TryParse(subject, out var userId)
            ? userId
            : throw new BusinessLogicException(ErrorCodes.UnauthorizedAccess);
    }
}
