using GymRatService.API.Extensions;
using GymRatService.Common.DTOs.Users;
using GymRatService.Service.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymRatService.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class UsersController(IUsersService usersService) : ControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> GetUserProfileAsync(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        var response = await usersService.GetProfileAsync(userId, cancellationToken);

        return Ok(response);
    }

    [HttpPatch("me/settings")]
    public async Task<IActionResult> UpdateUserSettingsAsync(UpdateUserSettingsRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        await usersService.UpdateSettingsAsync(userId, request, cancellationToken);

        return NoContent();
    }

[HttpPost("me/password")]
    public async Task<IActionResult> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        await usersService.ChangePasswordAsync(userId, request, cancellationToken);

        return NoContent();
    }

    [HttpDelete("me")]
    public async Task<IActionResult> DeleteAccountAsync(DeleteAccountRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        await usersService.DeleteAccountAsync(userId, request, cancellationToken);

        return NoContent();
    }
}
