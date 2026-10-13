using GymRatService.Service.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymRatService.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ExercisesController(IExercisesService exercisesService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetExercisesAsync(CancellationToken cancellationToken)
    {
        var response = await exercisesService.GetExercisesAsync(cancellationToken);

        return Ok(response);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetExerciseAsync(Guid id, CancellationToken cancellationToken)
    {
        var response = await exercisesService.GetExerciseAsync(id, cancellationToken);

        return Ok(response);
    }
}
