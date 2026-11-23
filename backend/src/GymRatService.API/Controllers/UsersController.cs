using GymRatService.API.Extensions;
using GymRatService.Common.DTOs.Users;
using GymRatService.Service.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymRatService.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class UsersController(IUsersService usersService, IExportService exportService) : ControllerBase
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

    [HttpGet("me/export")]
    public async Task<IActionResult> GetUserExportAsync(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        var response = await exportService.GetExportAsync(userId, cancellationToken);

        return Ok(response);
    }

    [HttpGet("me/export/sets")]
    public async Task<IActionResult> GetSetsCsvAsync(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        var response = await exportService.GetSetsCsvAsync(userId, cancellationToken);

        return File(response, "text/csv; charset=utf-8", "gymrat-sets.csv");
    }
}
