using GymRatService.API.Extensions;
using GymRatService.Common.DTOs.Chat;
using GymRatService.Service.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymRatService.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ChatController(IChatService chatService) : ControllerBase
{
    [HttpPost("messages")]
    public async Task<IActionResult> SendMessageAsync(ChatMessageRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        var response = await chatService.SendMessageAsync(userId, request, cancellationToken);

        return Ok(response);
    }
}
