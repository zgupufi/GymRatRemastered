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
        var response = await usersService.GetProfileAsync(User.GetUserId(), cancellationToken);

        return Ok(response);
    }

    [HttpPatch("me/settings")]
    public async Task<IActionResult> UpdateUserSettingsAsync(UpdateUserSettingsRequest request, CancellationToken cancellationToken)
    {
        await usersService.UpdateSettingsAsync(User.GetUserId(), request, cancellationToken);

        return NoContent();
    }
}
