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

namespace ChatApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ConversationsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IHubContext<ChatHub> _hubContext;

        public ConversationsController(AppDbContext context, UserManager<User> userManager, IHubContext<ChatHub> hubContext)
        {
            _context = context;
            _userManager = userManager;
            _hubContext = hubContext;
        }

        // Get All Conversations
        // GET => baseUrl/api/Conversations
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
                .Select(c => new ConversationResponseDto
                {
                    Id = c.Id,
                    User1Id = c.User1Id,
                    User1 = new UserResponseDto
                    {
                        Id = c.User1.Id,
                        UserName = c.User1.UserName,
                        Email = c.User1.Email
                    },
                    User2Id = c.User2Id,
                    User2 = new UserResponseDto
                    {
                        Id = c.User2.Id,
                        UserName = c.User2.UserName,
                        Email = c.User2.Email
                    },
                    CreatedAt = c.CreatedAt,
                    Messages = c.Messages != null ? c.Messages.Select(m => new MessageResponseDto
                    {
                        Id = m.Id,
                        ConversationId = m.ConversationId,
                        SenderId = m.SenderId,
                        Sender = new UserResponseDto
                        {
                            Id = m.Sender.Id,
                            UserName = m.Sender.UserName,
                            Email = m.Sender.Email
                        },
                        ReceiverId = m.ReceiverId,
                        Receiver = m.Receiver != null ? new UserResponseDto
                        {
                            Id = m.Receiver.Id,
                            UserName = m.Receiver.UserName,
                            Email = m.Receiver.Email
                        } : null,
                        Content = m.Content,
                        SentAt = m.SentAt,
                        IsSeen = m.IsSeen
                    }).ToList() : null
                })
                .ToListAsync();

            return Ok(conversations);
        }

        // Create New Conversation
        // POST => baseUrl/api/Conversations
        [HttpPost]
        public async Task<IActionResult> CreateConversation([FromBody] CreateConversationDto dto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            // التحقق من وجود المستخدم الآخر
            var otherUser = await _userManager.FindByIdAsync(dto.OtherUserId.ToString());
            if (otherUser == null)
                return BadRequest("Other user ID does not exist.");

            // التحقق من أن المستخدمين مختلفين
            if (userId == dto.OtherUserId)
                return BadRequest("Cannot start a conversation with yourself.");

            // التحقق من وجود المحادثة مسبقاً
            var existingConversation = await _context.Conversations
                .Include(c => c.User1)
                .Include(c => c.User2)
                .Include(c => c.Messages)
                .ThenInclude(m => m.Sender)
                .FirstOrDefaultAsync(c =>
                    (c.User1Id == userId && c.User2Id == dto.OtherUserId) ||
                    (c.User1Id == dto.OtherUserId && c.User2Id == userId));

            if (existingConversation != null)
            {
                return Ok(new ConversationResponseDto
                {
                    Id = existingConversation.Id,
                    User1Id = existingConversation.User1Id,
                    User1 = new UserResponseDto { Id = existingConversation.User1.Id, UserName = existingConversation.User1.UserName },
                    User2Id = existingConversation.User2Id,
                    User2 = new UserResponseDto { Id = existingConversation.User2.Id, UserName = existingConversation.User2.UserName },
                    CreatedAt = existingConversation.CreatedAt,
                    Messages = existingConversation.Messages != null ? existingConversation.Messages.Select(m => new MessageResponseDto
                    {
                        Id = m.Id,
                        ConversationId = m.ConversationId,
                        SenderId = m.SenderId,
                        Sender = new UserResponseDto { Id = m.Sender.Id, UserName = m.Sender.UserName },
                        ReceiverId = m.ReceiverId,
                        Content = m.Content,
                        SentAt = m.SentAt,
                        IsSeen = m.IsSeen
                    }).ToList() : null
                });
            }

            var conversation = new Conversation
            {
                User1Id = userId,
                User2Id = dto.OtherUserId,
                CreatedAt = DateTime.UtcNow,
                IsDeleted_ForUser_1 = false,
                IsDeleted_ForUser_2 = false
            };

            _context.Conversations.Add(conversation);
            await _context.SaveChangesAsync();

            var user1 = await _userManager.FindByIdAsync(userId.ToString());
            var user2 = await _userManager.FindByIdAsync(dto.OtherUserId.ToString());

            return Ok(new ConversationResponseDto
            {
                Id = conversation.Id,
                User1Id = conversation.User1Id,
                User1 = new UserResponseDto { Id = user1.Id, UserName = user1.UserName },
                User2Id = conversation.User2Id,
                User2 = new UserResponseDto { Id = user2.Id, UserName = user2.UserName },
                CreatedAt = conversation.CreatedAt,
                Messages = null
            });
        }

        // Open Exsited Conversation
        // POST => baseUrl/api/Conversations/open/{conversationId}
        [HttpGet("open/{conversationId}")]
        public async Task<IActionResult> OpenConversation(int conversationId)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversationId && (c.User1Id == userId || c.User2Id == userId));

            if (conversation == null)
                return NotFound("Conversation not found or you don't have access to it.");

            // تحديد المستلم (اللي هو الشخص اللي مش المرسل)
            var otherUserId = conversation.User1Id == userId ? conversation.User2Id : conversation.User1Id;

            // جلب الرسايل اللي لسه ما اتشافتش وكاتبها المستخدم الآخر
            var unseenMessages = await _context.Messages
                .Where(m => m.ConversationId == conversationId && m.SenderId == otherUserId && !m.IsSeen)
                .ToListAsync();

            // تحديث حالة IsSeen لكل الرسايل اللي لسه ما اتشافتش
            if (unseenMessages.Any())
            {
                foreach (var message in unseenMessages)
                {
                    message.IsSeen = true;
                }
                await _context.SaveChangesAsync();

                // إرسال إشعار للمرسل إن الرسايل اتشافت
                await _hubContext.Clients.Group(otherUserId.ToString()).SendAsync("MessagesSeen", unseenMessages.Select(m => m.Id).ToList());
            }

            // استرجاع كل الرسايل بعد التحديث
            var messages = await _context.Messages
                .Where(m => m.ConversationId == conversationId)
                .Include(m => m.Sender)
                .Select(m => new MessageResponseDto
                {
                    Id = m.Id,
                    ConversationId = m.ConversationId,
                    SenderId = m.SenderId,
                    Sender = new UserResponseDto { Id = m.Sender.Id, UserName = m.Sender.UserName },
                    ReceiverId = m.ReceiverId,
                    Content = m.Content,
                    SentAt = m.SentAt,
                    IsSeen = m.IsSeen
                })
                .ToListAsync();

            return Ok(messages);
        }

        // Delete Conversation for Current User
        // DELETE => baseUrl/api/Conversations/delete/{conversationId}
        [HttpDelete("delete/{conversationId}")]
        public async Task<IActionResult> DeleteConversation(int conversationId)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            var conversation = await _context.Conversations
                .Include(c => c.User1)
                .Include(c => c.User2)
                .FirstOrDefaultAsync(c => c.Id == conversationId && (c.User1Id == userId || c.User2Id == userId));

            if (conversation == null)
                return NotFound("Conversation not found or you don't have access to it.");

            // تحديد إذا كان المستخدم هو User1 أو User2
            if (conversation.User1Id == userId)
            {
                conversation.IsDeleted_ForUser_1 = true;
            }
            else if (conversation.User2Id == userId)
            {
                conversation.IsDeleted_ForUser_2 = true;
            }

            await _context.SaveChangesAsync();

            // إرسال إشعار للمستخدم الآخر (اختياري)
            var otherUserId = conversation.User1Id == userId ? conversation.User2Id : conversation.User1Id;
            await _hubContext.Clients.Group(otherUserId.ToString()).SendAsync("ConversationDeletedByOther", conversationId);

            return Ok("Conversation deleted for current user.");
        }

        /// Get Users Online Status
        /// GET => baseUrl/api/Conversations/users-status
        /// أظهر دائرة خضراء جنب صورة المستخدم لو أونلاين
        [HttpGet("users-status")]
        public async Task<IActionResult> GetUsersStatus()

        {
            // جلب كل المستخدمين مع حالة الأونلاين
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            var users = await _context.Users
                .Where(u => u.Id != userId)
                .Select(u => new
                {
                    Id = u.Id,
                    UserName = u.UserName,
                    IsOnline = u.LastSeen.HasValue && (DateTime.UtcNow - u.LastSeen.Value).TotalSeconds < 30 // أونلاين لو آخر ظهور خلال 30 ثانية
                })
                .ToListAsync();

            return Ok(users);
        }

        /// Search Messages in Conversations
        /// GET => baseUrl/api/Conversations/search
        [HttpGet("search")]
        public async Task<IActionResult> SearchMessages([FromQuery] string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return BadRequest("Search query is required.");

            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            var messages = await _context.Messages
                .Where(m => (m.SenderId == userId || m.ReceiverId == userId) && m.Content.Contains(query))
                .Include(m => m.Sender)
                .Include(m => m.Conversation)
                .Select(m => new MessageResponseDto
                {
                    Id = m.Id,
                    ConversationId = m.ConversationId,
                    SenderId = m.SenderId,
                    Sender = new UserResponseDto { Id = m.Sender.Id, UserName = m.Sender.UserName, Email = m.Sender.Email },
                    ReceiverId = m.ReceiverId,
                    Receiver = m.Receiver != null ? new UserResponseDto
                    {
                        Id = m.Receiver.Id,
                        UserName = m.Receiver.UserName,
                        Email = m.Receiver.Email
                    } : null,
                    Content = m.Content,
                    SentAt = m.SentAt,
                    IsSeen = m.IsSeen
                })
                .OrderByDescending(m => m.SentAt)
                .ToListAsync();

            return Ok(messages);
        }
    }
}