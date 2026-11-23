using GymRatService.API.Cookies;
using GymRatService.API.Extensions;
using GymRatService.Common.DTOs.Auth;
using GymRatService.Service.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GymRatService.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class AuthController(IAuthService authService, RefreshTokenCookie refreshTokenCookie) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("register")]
    [EnableRateLimiting(RateLimitingExtensions.RegisterPolicy)]
    public async Task<IActionResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        await authService.RegisterAsync(request, cancellationToken);

        return Created();
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [EnableRateLimiting(RateLimitingExtensions.LoginPolicy)]
    public async Task<IActionResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var response = await authService.LoginAsync(request, cancellationToken);

        refreshTokenCookie.Write(Response, response);

        return Ok(response.Login);
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    [EnableRateLimiting(RateLimitingExtensions.RefreshPolicy)]
    public async Task<IActionResult> RefreshAsync(CancellationToken cancellationToken)
    {
        var response = await authService.RefreshAsync(refreshTokenCookie.Read(Request), cancellationToken);

        refreshTokenCookie.Write(Response, response);

        return Ok(response.Login);
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    [EnableRateLimiting(RateLimitingExtensions.RefreshPolicy)]
    public async Task<IActionResult> LogoutAsync(CancellationToken cancellationToken)
    {
        await authService.LogoutAsync(refreshTokenCookie.Read(Request), cancellationToken);

        refreshTokenCookie.Delete(Response);

        return NoContent();
    }
}
