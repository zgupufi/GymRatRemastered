using GymRatService.API.Extensions;
using GymRatService.Common.Filtering;
using GymRatService.Service.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymRatService.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class CoachController(ICoachService coachService) : ControllerBase
{
    [HttpGet("suggestions")]
    public async Task<IActionResult> GetPendingSuggestionsAsync([FromQuery] CoachSuggestionFilter filter, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        var response = await coachService.GetPendingSuggestionsAsync(userId, filter, cancellationToken);

        return Ok(response);
    }

    [HttpPost("suggestions/{id}/apply")]
    public async Task<IActionResult> ApplySuggestionAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        await coachService.ApplySuggestionAsync(userId, id, cancellationToken);

        return NoContent();
    }

    [HttpPost("suggestions/{id}/dismiss")]
    public async Task<IActionResult> DismissSuggestionAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        await coachService.DismissSuggestionAsync(userId, id, cancellationToken);

        return NoContent();
    }
}
