using Chat.Api.Data;
using Chat.Api.DTOs;
using ChatApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ChatApi.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ConversationsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ConversationService _conversationService;
    private readonly ILogger<ConversationsController> _logger;

    public ConversationsController(AppDbContext context, ConversationService conversationService, ILogger<ConversationsController> logger)
    {
        _context = context;
        _conversationService = conversationService;
        _logger = logger;
    }


    [HttpGet]
    public async Task<IActionResult> GetConversations()
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
        var conversations = await _context.Conversations
            .Where(c => (c.User1Id == userId || c.User2Id == userId) &&
                       !(c.User1Id == userId && c.IsDeleted_ForUser_1) &&
                       !(c.User2Id == userId && c.IsDeleted_ForUser_2))
            .Include(c => c.User1)
            .Include(c => c.User2)
            .Include(c => c.Messages)
            .ThenInclude(m => m.Sender)
            .Select(c => new
            {
                Id = c.Id,
                ReceiverId = c.User1Id == userId ? c.User2Id : c.User1Id,
                ReceiverName = c.User1Id == userId ? c.User2.UserName : c.User1.UserName,
                ReceiverProfileImage = c.User1Id == userId ? c.User2.Image : c.User1.Image,
                LastMessage = c.Messages != null && c.Messages.Any()
                    ? c.Messages.OrderByDescending(m => m.SentAt).First()
                    : null,
                UnreadCount = c.Messages != null ? c.Messages.Count(m => m.ReceiverId == userId && !m.IsSeen) : 0
            })
            .ToListAsync();

        var response = conversations.Select(c => new ConversationResponseDto
        {
            Id = c.Id,
            OtherUserId = c.ReceiverId,
            OtherUserName = c.ReceiverName,
            OtherUserProfileImage = c.ReceiverProfileImage,
            LastMessage = c.LastMessage != null
                ? new LastMessageDto
                {
                    Content = c.LastMessage.Content,
                    SentAt = c.LastMessage.SentAt,
                    Status = c.LastMessage.IsSeen ? "Seen" : c.LastMessage.IsReceived ? "Received" : "Sent"
                }
                : null,
            UnreadCount = c.UnreadCount
        }).ToList();

        return Ok(response);
    }

    [HttpGet("open/{conversationId}")]
    public async Task<IActionResult> OpenConversation(int conversationId)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
        _logger.LogInformation("Opening conversation {ConversationId} for user {UserId}", conversationId, userId);
        try
        {
            var response = await _conversationService.OpenConversationAsync(conversationId, userId);
            return response == null ? NotFound() : Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening conversation {ConversationId} for user {UserId}", conversationId, userId);
            return StatusCode(404, "Conversation Not Found.");
        }
    }

    [HttpDelete("delete/{conversationId}")]
    public async Task<IActionResult> DeleteConversation(int conversationId)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
        _logger.LogInformation("Deleting conversation {ConversationId} for user {UserId}", conversationId, userId);
        try
        {
            await _conversationService.DeleteConversationAsync(conversationId, userId);
            return Ok("Conversation deleted for current user Successfully.");
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Conversation {ConversationId} not found or user {UserId} has no access", conversationId, userId);
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting conversation {ConversationId} for user {UserId}", conversationId, userId);
            return StatusCode(500, "An error occurred while deleting the conversation.");
        }
    }

    [HttpGet("search")]
    public async Task<IActionResult> SearchMessages([FromQuery] string query)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
        _logger.LogInformation("Searching messages for user {UserId} with query '{Query}'", userId, query);
        try
        {
            var response = await _conversationService.SearchMessagesAsync(userId, query);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid search query for user {UserId}", userId);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching messages for user {UserId}", userId);
            return StatusCode(500, "An error occurred while searching messages.");
        }
    }
}