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


        /// Get All Conversations
        /// GET => baseUrl/api/Conversations
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


        /// Open Exsited Conversation
        /// POST => baseUrl/api/Conversations/open/{conversationId}
        [HttpGet("open/{conversationId}")]
        public async Task<IActionResult> OpenConversation(int conversationId)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversationId && (c.User1Id == userId || c.User2Id == userId));

            if (conversation == null)
                return NotFound("Conversation not found or you don't have access to it.");

            var otherUserId = conversation.User1Id == userId ? conversation.User2Id : conversation.User1Id;

            var unseenMessages = await _context.Messages
                .Where(m => m.ConversationId == conversationId && m.SenderId == otherUserId && !m.IsSeen)
                .ToListAsync();

            if (unseenMessages.Any())
            {
                foreach (var message in unseenMessages)
                {
                    message.IsSeen = true;
                }
                await _context.SaveChangesAsync();

                await _hubContext.Clients.Group(otherUserId.ToString()).SendAsync("MessagesSeen", unseenMessages.Select(m => m.Id).ToList());
            }

            var messages = await _context.Messages
                .Where(m => m.ConversationId == conversationId)
                .Include(m => m.Sender)
                .Include(m => m.Receiver)
                .Select(m => new MessageResponseDto
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
                })
                .ToListAsync();

            return Ok(messages);
        }


        /// Delete Conversation for Current User
        /// DELETE => baseUrl/api/Conversations/delete/{conversationId}
        [HttpDelete("delete/{conversationId}")]
        public async Task<IActionResult> DeleteConversation(int conversationId)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier).Value;
            if (!int.TryParse(userIdString, out int userId))
            {
                return BadRequest("Invalid user id in token.");
            }

            var conversation = await _context.Conversations
                .Include(c => c.User1)
                .Include(c => c.User2)
                .Include(c => c.Messages)
                .FirstOrDefaultAsync(c =>
                    c.Id == conversationId &&
                    (c.User1Id == userId || c.User2Id == userId)
                );

            if (conversation == null)
                return NotFound("Conversation not found or you don't have access to it.");

            bool bothDeleted = false;
            if (conversation.User1Id == userId)
            {
                conversation.IsDeleted_ForUser_1 = true;
                if (conversation.IsDeleted_ForUser_2)
                    bothDeleted = true;
            }
            else
            {
                conversation.IsDeleted_ForUser_2 = true;
                if (conversation.IsDeleted_ForUser_1)
                    bothDeleted = true;
            }

            if (bothDeleted)
            {
                if (conversation.Messages != null && conversation.Messages.Any())
                    _context.Messages.RemoveRange(conversation.Messages);

                _context.Conversations.Remove(conversation);
                await _context.SaveChangesAsync();

                var otherUserId = conversation.User1Id == userId
                    ? conversation.User2Id
                    : conversation.User1Id;
                await _hubContext.Clients.Group(otherUserId.ToString())
                    .SendAsync("ConversationFullyDeleted", conversationId);

                return Ok("Conversation and its messages have been fully deleted from the database.");
            }

            await _context.SaveChangesAsync();

            var otherUserIdForNotification = conversation.User1Id == userId
                ? conversation.User2Id
                : conversation.User1Id;
            await _hubContext.Clients.Group(otherUserIdForNotification.ToString())
                .SendAsync("ConversationDeletedByOther", conversationId);

            return Ok("Conversation deleted for current user.");
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