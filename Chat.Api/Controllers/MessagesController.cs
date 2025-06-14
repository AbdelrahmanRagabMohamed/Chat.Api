using Chat.Api.DTOs;
using Chat.Api.Interfaces;
using ChatApi.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ChatApi.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class MessagesController : ControllerBase
{
    private readonly IMessageService _messageService;
    private readonly ILogger<MessagesController> _logger;

    public MessagesController(IMessageService messageService, ILogger<MessagesController> logger)
    {
        _messageService = messageService;
        _logger = logger;
    }



    [HttpPost("send")]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageDto dto)
    {
        try
        {
            int senderId = int.Parse(User.FindFirst("id")!.Value);
            var result = await _messageService.SendMessageAsync(senderId, dto);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message");
            return StatusCode(500, new ApiResponse(500, "Failed to send message"));
        }
    }

    [HttpPut("edit/{messageId}")]
    public async Task<IActionResult> EditMessage(int messageId, [FromBody] EditMessageDto dto)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
        _logger.LogInformation("User {UserId} editing message {MessageId}", userId, messageId);
        try
        {
            var response = await _messageService.EditMessageAsync(messageId, userId, dto.NewContent);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error editing message {MessageId} by user {UserId}", messageId, userId);
            return StatusCode(500, "An error occurred while editing the message.");
        }
    }

    [HttpDelete("delete/{messageId}")]
    public async Task<IActionResult> DeleteMessage(int messageId)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
        _logger.LogInformation("User {UserId} deleting message {MessageId}", userId, messageId);
        try
        {
            await _messageService.DeleteMessageAsync(messageId, userId);
            return Ok("Message Deleted Successfully");
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Message {MessageId} not found or access denied for user {UserId}", messageId, userId);
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting message {MessageId} by user {UserId}", messageId, userId);
            return StatusCode(500, "An error occurred while deleting the message.");
        }
    }

    [HttpGet("{messageId}/seen-status")]
    public async Task<IActionResult> GetMessageSeenStatus(int messageId)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
        _logger.LogInformation("User {UserId} fetching seen status for message {MessageId}", userId, messageId);
        try
        {
            var response = await _messageService.GetMessageSeenStatusAsync(messageId, userId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching seen status for message {MessageId} by user {UserId}", messageId, userId);
            return StatusCode(500, "An error occurred while retrieving the message seen status.");
        }
    }
}
