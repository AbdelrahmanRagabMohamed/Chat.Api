using ChatApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ChatApi.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ConversationsController : ControllerBase
{
    private readonly ConversationService _conversationService;

    public ConversationsController(ConversationService conversationService)
    {
        _conversationService = conversationService;
    }

    [HttpGet]
    public async Task<IActionResult> GetConversations()
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
        var response = await _conversationService.GetConversationsAsync(userId);
        return Ok(response);
    }

    [HttpGet("open/{conversationId}")]
    public async Task<IActionResult> OpenConversation(int conversationId)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
        var response = await _conversationService.OpenConversationAsync(conversationId, userId);
        return response == null ? NotFound() : Ok(response);
    }

    [HttpDelete("delete/{conversationId}")]
    public async Task<IActionResult> DeleteConversation(int conversationId)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
        await _conversationService.DeleteConversationAsync(conversationId, userId);
        return Ok("Conversation deleted for current user.");
    }

    [HttpGet("search")]
    public async Task<IActionResult> SearchMessages([FromQuery] string query)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
        var response = await _conversationService.SearchMessagesAsync(userId, query);
        return Ok(response);
    }
}