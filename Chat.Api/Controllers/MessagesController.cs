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
    private readonly UserManager<User> _userManager; // أضفنا UserManager عشان نستخدمه في GetOrCreateConversation

    public MessagesController(AppDbContext context, IHubContext<ChatHub> hubContext, UserManager<User> userManager)
    {
        _context = context;
        _hubContext = hubContext;
        _userManager = userManager;
    }



    /// Send New Message
    /// POST => baseUrl/api/Messages/send
    [HttpPost("send")]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageDto dto)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

        // جلب أو إنشاء المحادثة
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
            IsSeen = false
        };

        _context.Messages.Add(message);
        await _context.SaveChangesAsync();

        var sender = await _context.Users.FindAsync(userId);

        await _hubContext.Clients.Group(otherUserId.ToString()).SendAsync("ReceiveMessage", new
        {
            Id = message.Id,
            ConversationId = message.ConversationId,
            SenderId = message.SenderId,
            SenderName = sender.UserName,
            ReceiverId = otherUserId,
            Content = message.Content,
            SentAt = message.SentAt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            IsSeen = message.IsSeen
        });

        // إشعار انه جاتله رسالة
        await _hubContext.Clients.Group(otherUserId.ToString()).SendAsync("NewMessageNotification", sender.UserName, dto.Content);

        return Ok(new MessageResponseDto
        {
            Id = message.Id,
            ConversationId = message.ConversationId,
            SenderId = message.SenderId,
            Sender = new UserResponseDto { Id = sender.Id, UserName = sender.UserName, Email = sender.Email },
            ReceiverId = message.ReceiverId,
            Receiver = new UserResponseDto { Id = receiver.Id, UserName = receiver.UserName, Email = receiver.Email },
            Content = message.Content,
            SentAt = message.SentAt,
            IsSeen = message.IsSeen
        });
    }

    /// Edit Message
    /// PUT => baseUrl/api/Messages/edit/{messageId}
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

        var sender = await _context.Users.FindAsync(userId);
        var otherUserId = message.Conversation.User1Id == userId ? message.Conversation.User2Id : message.Conversation.User1Id;
        var receiver = await _context.Users.FindAsync(otherUserId);

        await _hubContext.Clients.Group(otherUserId.ToString()).SendAsync("MessageEdited", message.Id, dto.Content);

        return Ok(new MessageResponseDto
        {
            Id = message.Id,
            ConversationId = message.ConversationId,
            SenderId = message.SenderId,
            Sender = new UserResponseDto
            {
                Id = sender.Id,
                UserName = sender.UserName,
                Email = sender.Email
            },
            Content = message.Content,
            SentAt = message.SentAt,
            Receiver = new UserResponseDto
            {
                Id = receiver.Id,
                UserName = receiver.UserName,
                Email = receiver.Email
            }
        });
    }

    /// Delete Message
    /// DELETE => baseUrl/api/Messages/delete/{messageId}
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
        await _hubContext.Clients.Group(otherUserId.ToString()).SendAsync("MessageDeleted", messageId);

        return Ok("Message Deleted Successfully");
    }

    /// Check Message Seen Status
    /// GET => baseUrl/api/Messages/{messageId}/seen-status
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
            IsSeen = message.IsSeen,
            SenderId = message.SenderId,
            ReceiverId = message.ReceiverId,
            StatusMessage = message.IsSeen
                ? "😉 كده هو شاف المسدج انزل بالعلامة الزرقاء يا هشام يا علي"
                : "كده هو لسه مشافش المسدج , شيل العلامة الزرقاء يا هشام يا علي"
        });
    }


    //------------------------------------------//


    // الفانكشن اهي يا نادي //
    /// بتجيب او بتعمل محادثة جديد 
    /// بتاخد senderId و receiverId
    /// وبتشيك لو المحادثة موجودة ولا لا
    /// لو المحادثة موجودة بتجيبها
    /// لو المحادثة مش موجودة بتعمل محادثة جديدة وبتضيفها في الداتا بيز  وبترجع المحادثة

    private async Task<Conversation> GetOrCreateConversation(int senderId, int receiverId)
    {
        // التحقق من وجود المستخدم الآخر
        var receiver = await _userManager.FindByIdAsync(receiverId.ToString());
        if (receiver == null)
            throw new Exception("Receiver does not exist.");

        // التحقق من أن المستخدمين مختلفين
        if (senderId == receiverId)
            throw new Exception("Cannot start a conversation with yourself.");

        // التحقق من وجود المحادثة مسبقاً
        var existingConversation = await _context.Conversations
            .FirstOrDefaultAsync(c =>
                (c.User1Id == senderId && c.User2Id == receiverId) ||
                (c.User1Id == receiverId && c.User2Id == senderId));

        if (existingConversation != null)
            return existingConversation;

        // إنشاء محادثة جديدة لو مفيش محادثة موجودة
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