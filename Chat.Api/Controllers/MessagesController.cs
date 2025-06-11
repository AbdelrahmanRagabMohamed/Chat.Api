using Chat.Api.DTOs;
using ChatApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ChatApi.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class MessagesController : ControllerBase
{
    private readonly MessageService _messageService;

    public MessagesController(MessageService messageService)
    {
        _messageService = messageService;
    }

    [HttpPost("send")]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageDto dto)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
        var response = await _messageService.SendMessageAsync(userId, dto.ReceiverId, dto.Content);
        return Ok(response);
    }

    [HttpPut("edit/{messageId}")]
    public async Task<IActionResult> EditMessage(int messageId, [FromBody] EditMessageDto dto)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
        var response = await _messageService.EditMessageAsync(messageId, userId, dto.Content);
        return Ok(response);
    }

    [HttpDelete("delete/{messageId}")]
    public async Task<IActionResult> DeleteMessage(int messageId)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
        await _messageService.DeleteMessageAsync(messageId, userId);
        return Ok("Message Deleted Successfully");
    }

    [HttpGet("{messageId}/seen-status")]
    public async Task<IActionResult> GetMessageSeenStatus(int messageId)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
        var response = await _messageService.GetMessageSeenStatusAsync(messageId, userId);
        return Ok(response);
    }
}