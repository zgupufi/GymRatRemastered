using GymRatService.API.Extensions;
using GymRatService.Common.DTOs.Sessions;
using GymRatService.Common.DTOs.Targets;
using GymRatService.Common.DTOs.Workouts;
using GymRatService.Common.Filtering;
using GymRatService.Service.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymRatService.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class WorkoutsController(IWorkoutsService workoutsService, ISessionsService sessionsService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<WorkoutSummaryDto>>> GetWorkoutSummariesAsync([FromQuery] WorkoutFilter filter, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        var response = await workoutsService.GetWorkoutSummariesAsync(userId, filter, cancellationToken);

        return Ok(response);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<WorkoutDto>> GetWorkoutAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        var response = await workoutsService.GetWorkoutAsync(userId, id, cancellationToken);

        return Ok(response);
    }

    [HttpGet("{id}/last-session")]
    public async Task<ActionResult<LastSessionDto>> GetLastSessionAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        var response = await sessionsService.GetLastSessionAsync(userId, id, cancellationToken);

        return Ok(response);
    }

    [HttpGet("{id}/targets")]
    public async Task<ActionResult<IReadOnlyList<ExerciseTargetDto>>> GetTargetsAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        var response = await sessionsService.GetTargetsAsync(userId, id, cancellationToken);

        return Ok(response);
    }

    [HttpPost]
    public async Task<ActionResult<CreateWorkoutResponse>> CreateWorkoutAsync(CreateWorkoutRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        var response = await workoutsService.CreateWorkoutAsync(userId, request, cancellationToken);

        return Created($"{Request.Path}/{response.Id}", response);
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> RenameWorkoutAsync(Guid id, RenameWorkoutRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        await workoutsService.RenameWorkoutAsync(userId, id, request, cancellationToken);

        return NoContent();
    }

    [HttpPut("{id}/exercises")]
    public async Task<IActionResult> ReplaceWorkoutExercisesAsync(Guid id, ReplaceWorkoutExercisesRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        await workoutsService.ReplaceWorkoutExercisesAsync(userId, id, request, cancellationToken);

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteWorkoutAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        await workoutsService.DeleteWorkoutAsync(userId, id, cancellationToken);

        return NoContent();
    }
}
