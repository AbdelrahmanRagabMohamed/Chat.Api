using Chat.Api.DTOs;
using ChatApi.Hubs;
using ChatApi.Interfaces;
using ChatApi.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;

namespace ChatApi.Services;

public class MessageService
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly UserManager<User> _userManager;
    private readonly ILogger<MessageService> _logger;

    public MessageService(
        IConversationRepository conversationRepository,
        IMessageRepository messageRepository,
        IHubContext<ChatHub> hubContext,
        UserManager<User> userManager,
        ILogger<MessageService> logger)
    {
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _hubContext = hubContext;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<SendMessageResponseDto> SendMessageAsync(int senderId, int receiverId, string content)
    {
        var conversation = await GetOrCreateConversationAsync(senderId, receiverId);
        var otherUserId = conversation.User1Id == senderId ? conversation.User2Id : conversation.User1Id;

        var receiver = await _userManager.FindByIdAsync(otherUserId.ToString());
        if (receiver == null)
            throw new Exception($"Receiver with ID {otherUserId} does not exist.");

        var message = new Message
        {
            ConversationId = conversation.Id,
            SenderId = senderId,
            ReceiverId = otherUserId,
            Content = content,
            SentAt = DateTime.UtcNow,
            IsSent = true,
            IsReceived = ChatHub.IsUserOnline(otherUserId),
            IsSeen = false
        };

        await _messageRepository.AddMessageAsync(message);

        var sender = await _userManager.FindByIdAsync(senderId.ToString());
        if (ChatHub.IsUserOnline(otherUserId))
        {
            await _hubContext.Clients.Group(otherUserId.ToString()).SendAsync("ReceiveMessage", new
            {
                Id = message.Id,
                ConversationId = message.ConversationId,
                SenderId = message.SenderId,
                SenderName = sender?.UserName,
                ReceiverId = otherUserId,
                Content = message.Content,
                SentAt = message.SentAt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                IsSent = message.IsSent,
                IsReceived = message.IsReceived,
                IsSeen = message.IsSeen
            });

            await _hubContext.Clients.Group(otherUserId.ToString()).SendAsync("NewMessageNotification", sender?.UserName, content);
            await _hubContext.Clients.Group(senderId.ToString()).SendAsync("MessageReceived", message.Id);
        }

        return new SendMessageResponseDto
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
        };
    }

    public async Task<MessageResponseDto> EditMessageAsync(int messageId, int userId, string newContent)
    {
        var message = await _messageRepository.GetMessageByIdAsync(messageId);
        if (message == null)
            throw new KeyNotFoundException("Message not found.");

        if (message.SenderId != userId)
            throw new UnauthorizedAccessException("You are not authorized to edit this message.");

        message.Content = newContent;
        message.SentAt = DateTime.UtcNow;
        await _messageRepository.UpdateMessageAsync(message);

        var otherUserId = message.Conversation.User1Id == userId ? message.Conversation.User2Id : message.Conversation.User1Id;
        if (ChatHub.IsUserOnline(otherUserId))
        {
            await _hubContext.Clients.Group(otherUserId.ToString()).SendAsync("MessageEdited", message.Id, newContent);
        }

        return new MessageResponseDto
        {
            Id = message.Id,
            Content = message.Content,
            SentAt = message.SentAt,
            IsSent = message.IsSent,
            IsReceived = message.IsReceived,
            IsSeen = message.IsSeen
        };
    }

    public async Task DeleteMessageAsync(int messageId, int userId)
    {
        var message = await _messageRepository.GetMessageByIdAsync(messageId);
        if (message == null)
            throw new KeyNotFoundException("Message not found.");

        if (message.SenderId != userId)
            throw new UnauthorizedAccessException("You are not authorized to delete this message.");

        await _messageRepository.DeleteMessageAsync(messageId);

        var otherUserId = message.Conversation.User1Id == userId ? message.Conversation.User2Id : message.Conversation.User1Id;
        if (ChatHub.IsUserOnline(otherUserId))
        {
            await _hubContext.Clients.Group(otherUserId.ToString()).SendAsync("MessageDeleted", messageId);
        }
    }

    public async Task<object> GetMessageSeenStatusAsync(int messageId, int userId)
    {
        var message = await _messageRepository.GetMessageByIdAsync(messageId);
        if (message == null)
            throw new KeyNotFoundException("Message not found.");

        if (message.SenderId != userId)
            throw new UnauthorizedAccessException("You are not authorized to check the status of this message.");

        return new
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
        };
    }

    private async Task<Conversation> GetOrCreateConversationAsync(int senderId, int receiverId)
    {
        var receiver = await _userManager.FindByIdAsync(receiverId.ToString());
        if (receiver == null)
            throw new Exception("Receiver does not exist.");

        if (senderId == receiverId)
            throw new Exception("Cannot start a conversation with yourself.");

        var userConversations = await _conversationRepository.GetUserConversationsAsync(senderId);
        var existingConversation = userConversations.FirstOrDefault(c =>
            (c.User1Id == senderId && c.User2Id == receiverId) ||
            (c.User1Id == receiverId && c.User2Id == senderId));

        if (existingConversation != null && existingConversation.IsDeletedFromBoth)
        {
            await _conversationRepository.DeleteConversationAsync(existingConversation.Id, senderId);
            var newConversation = new Conversation
            {
                User1Id = senderId,
                User2Id = receiverId,
                CreatedAt = DateTime.UtcNow,
                IsDeleted_ForUser_1 = false,
                IsDeleted_ForUser_2 = false
            };
            await _conversationRepository.AddConversationAsync(newConversation);
            return newConversation;
        }
        else if (existingConversation == null)
        {
            var newConversation = new Conversation
            {
                User1Id = senderId,
                User2Id = receiverId,
                CreatedAt = DateTime.UtcNow,
                IsDeleted_ForUser_1 = false,
                IsDeleted_ForUser_2 = false
            };
            await _conversationRepository.AddConversationAsync(newConversation);
            return newConversation;
        }

        return existingConversation;
    }
}