using Chat.Api.DTOs;
using ChatApi.Hubs;
using ChatApi.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;

namespace ChatApi.Services;

public class ConversationService
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ConversationService> _logger;

    public ConversationService(
        IConversationRepository conversationRepository,
        IMessageRepository messageRepository,
        IHubContext<ChatHub> hubContext,
        IMemoryCache cache,
        ILogger<ConversationService> logger)
    {
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _hubContext = hubContext;
        _cache = cache;
        _logger = logger;
    }

    public async Task<List<ConversationResponseDto>> GetConversationsAsync(int userId)
    {
        _logger.LogInformation("Fetching conversations for user {UserId}", userId);

        var cacheKey = $"Conversations_{userId}";
        if (_cache.TryGetValue(cacheKey, out List<ConversationResponseDto> cachedConversations))
        {
            _logger.LogInformation("Conversations retrieved from cache for user {UserId}", userId);
            return cachedConversations;
        }

        var conversations = await _conversationRepository.GetUserConversationsAsync(userId);
        var response = conversations.Select(c => new ConversationResponseDto
        {
            Id = c.Id,
            OtherUserId = c.User1Id == userId ? c.User2Id : c.User1Id,
            OtherUserName = c.User1Id == userId ? c.User2.UserName : c.User1.UserName,
            OtherUserProfileImage = c.User1Id == userId ? c.User2.Image : c.User1.Image,
            LastMessage = c.Messages.Any()
                ? new LastMessageDto
                {
                    Content = c.Messages.OrderByDescending(m => m.SentAt).First().Content,
                    SentAt = c.Messages.OrderByDescending(m => m.SentAt).First().SentAt,
                    Status = c.Messages.OrderByDescending(m => m.SentAt).First().IsSeen ? "Seen" :
                             c.Messages.OrderByDescending(m => m.SentAt).First().IsReceived ? "Received" : "Sent"
                }
                : null,
            UnreadCount = c.Messages.Count(m => m.ReceiverId == userId && !m.IsSeen)
        }).ToList();

        _cache.Set(cacheKey, response, TimeSpan.FromMinutes(5));
        _logger.LogInformation("Conversations cached for user {UserId}", userId);
        return response;
    }

    public async Task<OpenConversationResponseDto> OpenConversationAsync(int conversationId, int userId)
    {
        _logger.LogInformation("Opening conversation {ConversationId} for user {UserId}", conversationId, userId);

        var conversation = await _conversationRepository.GetConversationByIdAsync(conversationId);
        _logger.LogInformation("Conversation {ConversationId} found: {Result}, User1Id: {User1Id}, User2Id: {User2Id}",
            conversationId, conversation != null, conversation?.User1Id, conversation?.User2Id);
        if (conversation == null || (conversation.User1Id != userId && conversation.User2Id != userId) ||
            (conversation.User1Id == userId && conversation.IsDeleted_ForUser_1) ||
            (conversation.User2Id == userId && conversation.IsDeleted_ForUser_2))
            return null;

        var otherUserId = conversation.User1Id == userId ? conversation.User2Id : conversation.User1Id;
        var otherUser = conversation.User1Id == userId ? conversation.User2 : conversation.User1;

        var unseenMessages = conversation.Messages.Where(m => m.ReceiverId == userId && !m.IsSeen).ToList();
        if (unseenMessages.Any())
        {
            foreach (var message in unseenMessages)
            {
                message.IsReceived = true;
                message.IsSeen = true;
            }
            await _conversationRepository.SaveChangesAsync();
            if (ChatHub.IsUserOnline(otherUserId))
            {
                await _hubContext.Clients.Group(otherUserId.ToString())
                    .SendAsync("MessagesSeen", unseenMessages.Select(m => m.Id).ToList());
            }
            _logger.LogInformation("Marked {Count} messages as seen in conversation {ConversationId}", unseenMessages.Count, conversationId);
        }

        var messages = await _messageRepository.GetMessagesByConversationIdAsync(conversationId);
        return new OpenConversationResponseDto
        {
            ConversationId = conversation.Id,
            OtherUserId = otherUserId,
            OtherUserName = otherUser.UserName,
            OtherUserImage = otherUser.Image,
            Messages = messages.Select(m => new MessageResponseDto
            {
                Id = m.Id,
                Content = m.Content,
                SentAt = m.SentAt,
                IsSent = m.IsSent,
                IsReceived = m.IsReceived,
                IsSeen = m.IsSeen
            }).ToList()
        };
    }

    public async Task DeleteConversationAsync(int conversationId, int userId)
    {
        _logger.LogInformation("Deleting conversation {ConversationId} for user {UserId}", conversationId, userId);

        var conversation = await _conversationRepository.GetConversationByIdAsync(conversationId);
        _logger.LogInformation("Conversation {ConversationId} found: {Result}, User1Id: {User1Id}, User2Id: {User2Id}",
            conversationId, conversation != null, conversation?.User1Id, conversation?.User2Id);
        if (conversation == null || (conversation.User1Id != userId && conversation.User2Id != userId))
            throw new KeyNotFoundException("Conversation not found or you don't have access to it.");

        if (conversation.User1Id == userId)
            conversation.IsDeleted_ForUser_1 = true;
        else
            conversation.IsDeleted_ForUser_2 = true;

        await _conversationRepository.SaveChangesAsync();

        if (conversation.IsDeletedFromBoth)
        {
            await _conversationRepository.DeleteConversationAsync(conversationId, userId);
            _logger.LogInformation("Conversation {ConversationId} fully deleted", conversationId);
        }

        var otherUserId = conversation.User1Id == userId ? conversation.User2Id : conversation.User1Id;
        if (ChatHub.IsUserOnline(otherUserId))
        {
            await _hubContext.Clients.Group(otherUserId.ToString())
                .SendAsync(conversation.IsDeletedFromBoth ? "ConversationFullyDeleted" : "ConversationDeletedByOther", conversationId);
        }
    }

    public async Task<List<SearchResponseDto>> SearchMessagesAsync(int userId, string query)
    {
        _logger.LogInformation("Searching messages for user {UserId} with query '{Query}'", userId, query);
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Search query is required.");

        var messages = await _messageRepository.SearchMessagesAsync(userId, query);
        return messages.Select(m => new SearchResponseDto
        {
            Id = m.Id,
            ConversationId = m.ConversationId,
            OtherUserId = m.SenderId == userId ? m.ReceiverId : m.SenderId,
            Content = m.Content,
            SentAt = m.SentAt,
            IsSent = m.IsSent,
            IsReceived = m.IsReceived,
            IsSeen = m.IsSeen
        }).ToList();
    }
}