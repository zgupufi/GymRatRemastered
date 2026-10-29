using GymRatService.API.Extensions;
using GymRatService.Common.DTOs.Split;
using GymRatService.Service.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymRatService.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class SplitController(ISplitService splitService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetSplitAsync(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        var response = await splitService.GetSplitAsync(userId, cancellationToken);

        return Ok(response);
    }

    [HttpPut("{day}")]
    public async Task<IActionResult> AssignSplitDayAsync(int day, AssignSplitDayRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        await splitService.AssignSplitDayAsync(userId, day, request, cancellationToken);

        return NoContent();
    }
}
