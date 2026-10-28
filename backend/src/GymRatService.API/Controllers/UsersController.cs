using GymRatService.API.Extensions;
using GymRatService.Service.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymRatService.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class UsersController(IUsersService usersService) : ControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> GetMeAsync(CancellationToken cancellationToken)
    {
        var response = await usersService.GetProfileAsync(User.GetUserId(), cancellationToken);

        return Ok(response);
    }
}
