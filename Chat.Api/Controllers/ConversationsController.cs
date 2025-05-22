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

            var conversation = await _context.Conversations
                .Include(c => c.User1)
                .Include(c => c.User2)
                .FirstOrDefaultAsync(c => c.Id == conversationId && (c.User1Id == userId || c.User2Id == userId));

            if (conversation == null)
                return NotFound("Conversation not found or you don't have access to it.");

            var otherUserId = conversation.User1Id == userId ? conversation.User2Id : conversation.User1Id;
            var otherUser = await _context.Users.FindAsync(otherUserId);

            var unseenMessages = await _context.Messages
                .Where(m => m.ConversationId == conversationId && m.SenderId == otherUserId && !m.IsSeen)
                .ToListAsync();

            if (unseenMessages.Any())
            {
                foreach (var message in unseenMessages)
                {
                    message.IsReceived = true;
                    message.IsSeen = true;
                }
                await _context.SaveChangesAsync();

                if (ChatHub.IsUserOnline(otherUserId))
                {
                    await _hubContext.Clients.Group(otherUserId.ToString())
                        .SendAsync("MessagesSeen", unseenMessages.Select(m => m.Id).ToList());
                }
            }

            var messages = await _context.Messages
                .Where(m => m.ConversationId == conversationId)
                .Select(m => new MessageResponseDto
                {
                    Id = m.Id,
                    Content = m.Content,
                    SentAt = m.SentAt,
                    IsSent = m.IsSent,
                    IsReceived = m.IsReceived,
                    IsSeen = m.IsSeen
                })
                .ToListAsync();

            return Ok(new OpenConversationResponseDto
            {
                ConversationId = conversation.Id,
                OtherUserId = otherUserId,
                OtherUserName = otherUser.UserName,
                OtherUserImage = otherUser.Image,
                Messages = messages
            });
        }

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

                return Ok("Conversation deleted for current user.");
            }

            await _context.SaveChangesAsync();

            var otherUserIdForNotification = conversation.User1Id == userId
                ? conversation.User2Id
                : conversation.User1Id;
            await _hubContext.Clients.Group(otherUserIdForNotification.ToString())
                .SendAsync("ConversationDeletedByOther", conversationId);

            return Ok("Conversation deleted for current user.");
        }

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
                .Select(m => new SearchResponseDto
                {
                    Id = m.Id,
                    ConversationId = m.ConversationId,
                    OtherUserId = m.SenderId == userId ? m.ReceiverId : m.SenderId,
                    Content = m.Content,
                    SentAt = m.SentAt,
                    IsSent = m.IsSent,
                    IsReceived = m.IsReceived,
                    IsSeen = m.IsSeen
                })
                .OrderByDescending(m => m.SentAt)
                .ToListAsync();

            return Ok(messages);
        }
    }
}
