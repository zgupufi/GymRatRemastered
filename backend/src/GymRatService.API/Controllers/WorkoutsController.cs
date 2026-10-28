using GymRatService.API.Extensions;
using GymRatService.Common.DTOs.Workouts;
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
    public async Task<IActionResult> GetWorkoutSummariesAsync(CancellationToken cancellationToken)
    {
        var response = await workoutsService.GetWorkoutSummariesAsync(User.GetUserId(), cancellationToken);

        return Ok(response);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetWorkoutAsync(Guid id, CancellationToken cancellationToken)
    {
        var response = await workoutsService.GetWorkoutAsync(User.GetUserId(), id, cancellationToken);

        return Ok(response);
    }

    [HttpPost]
    public async Task<IActionResult> CreateWorkoutAsync(CreateWorkoutRequest request, CancellationToken cancellationToken)
    {
        var response = await workoutsService.CreateWorkoutAsync(User.GetUserId(), request, cancellationToken);

        return Created($"{Request.Path}/{response.Id}", response);
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> RenameWorkoutAsync(Guid id, RenameWorkoutRequest request, CancellationToken cancellationToken)
    {
        await workoutsService.RenameWorkoutAsync(User.GetUserId(), id, request, cancellationToken);

        return NoContent();
    }

    [HttpPut("{id}/exercises")]
    public async Task<IActionResult> ReplaceWorkoutExercisesAsync(Guid id, ReplaceWorkoutExercisesRequest request, CancellationToken cancellationToken)
    {
        await workoutsService.ReplaceWorkoutExercisesAsync(User.GetUserId(), id, request, cancellationToken);

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteWorkoutAsync(Guid id, CancellationToken cancellationToken)
    {
        await workoutsService.DeleteWorkoutAsync(User.GetUserId(), id, cancellationToken);

        return NoContent();
    }
}
