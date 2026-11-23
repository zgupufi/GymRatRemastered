using GymRatService.API.Extensions;
using GymRatService.API.Results;
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
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> GetUserExportAsync(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        var response = await exportService.GetExportAsync(userId, cancellationToken);

        return Ok(response);
    }

    [HttpGet("me/export/sets")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> GetSetsCsvAsync(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        var response = await exportService.GetSetsCsvLinesAsync(userId, cancellationToken);

        return new CsvStreamResult(response, "gymrat-sets.csv");
    }
}
