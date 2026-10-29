using GymRatService.API.Extensions;
using GymRatService.Common.DTOs.Workouts;
using GymRatService.Common.Filtering;
using GymRatService.Service.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymRatService.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class WorkoutsController(IWorkoutsService workoutsService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetWorkoutSummariesAsync([FromQuery] WorkoutFilter filter, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        var response = await workoutsService.GetWorkoutSummariesAsync(userId, filter, cancellationToken);

        return Ok(response);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetWorkoutAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        var response = await workoutsService.GetWorkoutAsync(userId, id, cancellationToken);

        return Ok(response);
    }

    [HttpPost]
    public async Task<IActionResult> CreateWorkoutAsync(CreateWorkoutRequest request, CancellationToken cancellationToken)
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
