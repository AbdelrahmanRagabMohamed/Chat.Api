using Chat.Api.DTOs;
using ChatApi.Data;
using ChatApi.Hubs;
using ChatApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ChatApi.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class MessagesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly UserManager<User> _userManager;

    public MessagesController(AppDbContext context, IHubContext<ChatHub> hubContext, UserManager<User> userManager)
    {
        _context = context;
        _hubContext = hubContext;
        _userManager = userManager;
    }

    [HttpPost("send")]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageDto dto)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

        Conversation conversation;
        try
        {
            conversation = await GetOrCreateConversation(userId, dto.ReceiverId);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }

        var otherUserId = conversation.User1Id == userId ? conversation.User2Id : conversation.User1Id;

        var receiver = await _context.Users.FindAsync(otherUserId);
        if (receiver == null)
            return NotFound($"Receiver with ID {otherUserId} does not exist.");

        var message = new Message
        {
            ConversationId = conversation.Id,
            SenderId = userId,
            ReceiverId = otherUserId,
            Content = dto.Content,
            SentAt = DateTime.UtcNow,
            IsSent = true,
            IsReceived = ChatHub.IsUserOnline(otherUserId),
            IsSeen = false
        };

        _context.Messages.Add(message);
        await _context.SaveChangesAsync();

        var sender = await _context.Users.FindAsync(userId);

        if (ChatHub.IsUserOnline(otherUserId))
        {
            await _hubContext.Clients.Group(otherUserId.ToString()).SendAsync("ReceiveMessage", new
            {
                Id = message.Id,
                ConversationId = message.ConversationId,
                SenderId = message.SenderId,
                SenderName = sender.UserName,
                ReceiverId = otherUserId,
                Content = message.Content,
                SentAt = message.SentAt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                IsSent = message.IsSent,
                IsReceived = message.IsReceived,
                IsSeen = message.IsSeen
            });

            await _hubContext.Clients.Group(otherUserId.ToString()).SendAsync("NewMessageNotification", sender.UserName, dto.Content);

            await _hubContext.Clients.Group(userId.ToString()).SendAsync("MessageReceived", message.Id);
        }

        return Ok(new SendMessageResponseDto
        {
            ConversationId = message.ConversationId,
            SenderId = message.SenderId,
            Message = new MessageResponseDto
            {
                Id = message.Id,
                Content = message.Content,
                SentAt = message.SentAt,
                IsSent = message.IsSent,
                IsReceived = message.IsReceived,
                IsSeen = message.IsSeen
            }
        });
    }

    [HttpPut("edit/{messageId}")]
    public async Task<IActionResult> EditMessage(int messageId, [FromBody] EditMessageDto dto)
    {
        int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

        var message = await _context.Messages
            .Include(m => m.Conversation)
            .FirstOrDefaultAsync(m => m.Id == messageId);

        if (message == null)
            return NotFound("Message not found.");

        if (message.SenderId != userId)
            return Unauthorized("You are not authorized to edit this message.");

        message.Content = dto.Content;
        message.SentAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var otherUserId = message.Conversation.User1Id == userId ? message.Conversation.User2Id : message.Conversation.User1Id;

        if (ChatHub.IsUserOnline(otherUserId))
        {
            await _hubContext.Clients.Group(otherUserId.ToString()).SendAsync("MessageEdited", message.Id, dto.Content);
        }

        return Ok(new MessageResponseDto
        {
            Id = message.Id,
            Content = message.Content,
            SentAt = message.SentAt,
            IsSent = message.IsSent,
            IsReceived = message.IsReceived,
            IsSeen = message.IsSeen
        });
    }

    [HttpDelete("delete/{messageId}")]
    public async Task<IActionResult> DeleteMessage(int messageId)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

        var message = await _context.Messages
            .Include(m => m.Conversation)
            .FirstOrDefaultAsync(m => m.Id == messageId);

        if (message == null)
            return NotFound("Message not found.");

        if (message.SenderId != userId)
            return Unauthorized("You are not authorized to delete this message.");

        _context.Messages.Remove(message);
        await _context.SaveChangesAsync();

        var otherUserId = message.Conversation.User1Id == userId ? message.Conversation.User2Id : message.Conversation.User1Id;
        if (ChatHub.IsUserOnline(otherUserId))
        {
            await _hubContext.Clients.Group(otherUserId.ToString()).SendAsync("MessageDeleted", messageId);
        }

        return Ok("Message Deleted Successfully");
    }

    [HttpGet("{messageId}/seen-status")]
    public async Task<IActionResult> GetMessageSeenStatus(int messageId)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

        var message = await _context.Messages
            .Include(m => m.Conversation)
            .FirstOrDefaultAsync(m => m.Id == messageId);

        if (message == null)
            return NotFound("Message not found.");

        if (message.SenderId != userId)
            return Unauthorized("You are not authorized to check the status of this message.");

        return Ok(new
        {
            MessageId = message.Id,
            IsSent = message.IsSent,
            IsReceived = message.IsReceived,
            IsSeen = message.IsSeen,
            SenderId = message.SenderId,
            ReceiverId = message.ReceiverId,
            StatusMessage = message.IsSeen
                ? "😉 كده هو شاف المسدج انزل بالعلامة الزرقاء يا هشام يا علي"
                : message.IsReceived
                    ? "كده هو استلم المسدج بس لسه مشافش، نزل علامتين صح يا هشام يا علي"
                    : "كده هو لسه ما استلمهاش، نزل علامة صح واحدة يا هشام يا علي"
        });
    }


    private async Task<Conversation> GetOrCreateConversation(int senderId, int receiverId)
    {
        var receiver = await _userManager.FindByIdAsync(receiverId.ToString());
        if (receiver == null)
            throw new Exception("Receiver does not exist.");

        if (senderId == receiverId)
            throw new Exception("Cannot start a conversation with yourself.");

        var existingConversation = await _context.Conversations
            .FirstOrDefaultAsync(c =>
                (c.User1Id == senderId && c.User2Id == receiverId) ||
                (c.User1Id == receiverId && c.User2Id == senderId));

        if (existingConversation != null)
            return existingConversation;

        var conversation = new Conversation
        {
            User1Id = senderId,
            User2Id = receiverId,
            CreatedAt = DateTime.UtcNow,
            IsDeleted_ForUser_1 = false,
            IsDeleted_ForUser_2 = false
        };

        _context.Conversations.Add(conversation);
        await _context.SaveChangesAsync();

        return conversation;
    }

}