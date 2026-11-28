using GymRatService.API.Extensions;
using GymRatService.Common.DTOs.Exercises;
using GymRatService.Common.DTOs.Sessions;
using GymRatService.Common.Filtering;
using GymRatService.Service.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymRatService.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ExercisesController(IExercisesService exercisesService, ISessionsService sessionsService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<ExerciseDto>>> GetExercisesAsync([FromQuery] ExerciseFilter filter, CancellationToken cancellationToken)
    {
        var response = await exercisesService.GetExercisesAsync(filter, cancellationToken);

        return Ok(response);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ExerciseDto>> GetExerciseAsync(Guid id, CancellationToken cancellationToken)
    {
        var response = await exercisesService.GetExerciseAsync(id, cancellationToken);

        return Ok(response);
    }

    [HttpGet("{id}/progress")]
    public async Task<ActionResult<IReadOnlyList<ExerciseProgressDto>>> GetExerciseProgressAsync(Guid id, [FromQuery] ProgressFilter filter, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        var response = await sessionsService.GetExerciseProgressAsync(userId, id, filter, cancellationToken);

        return Ok(response);
    }
}
