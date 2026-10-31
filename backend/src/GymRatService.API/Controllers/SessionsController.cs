using GymRatService.API.Extensions;
using GymRatService.Common.DTOs.Sessions;
using GymRatService.Service.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymRatService.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class SessionsController(ISessionsService sessionsService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> StartSessionAsync(StartSessionRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        var response = await sessionsService.StartSessionAsync(userId, request, cancellationToken);

        return Created($"{Request.Path}/{response.Id}", response);
    }

    [HttpPut("{id}/exercises/{exerciseId}/sets")]
    public async Task<IActionResult> SaveExerciseSetsAsync(Guid id, Guid exerciseId, SaveExerciseSetsRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        await sessionsService.SaveExerciseSetsAsync(userId, id, exerciseId, request, cancellationToken);

        return NoContent();
    }

    [HttpPost("{id}/finish")]
    public async Task<IActionResult> FinishSessionAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        await sessionsService.FinishSessionAsync(userId, id, cancellationToken);

        return NoContent();
    }
}
