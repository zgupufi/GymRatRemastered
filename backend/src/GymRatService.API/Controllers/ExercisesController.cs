using GymRatService.Common.DTOs.Exercises;
using GymRatService.Common.Errors;
using GymRatService.Service.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GymRatService.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExercisesController(IExercisesService exercisesService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ExerciseDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExercisesAsync(CancellationToken cancellationToken)
    {
        return Ok(await exercisesService.GetExercisesAsync(cancellationToken));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<ExerciseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetExerciseAsync(int id, CancellationToken cancellationToken)
    {
        return Ok(await exercisesService.GetExerciseAsync(id, cancellationToken));
    }
}
