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
        _logger.LogInformation("Sending message from user {SenderId} to user {ReceiverId}", senderId, receiverId);

        // تحقق من وجود محادثة سابقة بين الطرفين
        var conversations = await _conversationRepository.GetUserConversationsAsync(senderId); // استبدال await هنا
        var existingConversation = conversations.FirstOrDefault(c => (c.User1Id == senderId && c.User2Id == receiverId) || (c.User1Id == receiverId && c.User2Id == senderId));

        Conversation conversation;
        if (existingConversation != null && existingConversation.IsDeletedFromBoth)
        {
            await _conversationRepository.DeleteConversationAsync(existingConversation.Id, senderId);
            conversation = new Conversation
            {
                User1Id = senderId,
                User2Id = receiverId,
                CreatedAt = DateTime.UtcNow,
                IsDeleted_ForUser_1 = false,
                IsDeleted_ForUser_2 = false
            };
            await _conversationRepository.AddConversationAsync(conversation); // استخدام AddConversationAsync
        }
        else if (existingConversation == null)
        {
            conversation = new Conversation
            {
                User1Id = senderId,
                User2Id = receiverId,
                CreatedAt = DateTime.UtcNow,
                IsDeleted_ForUser_1 = false,
                IsDeleted_ForUser_2 = false
            };
            await _conversationRepository.AddConversationAsync(conversation); // استخدام AddConversationAsync
        }
        else
        {
            conversation = existingConversation;
        }

        var message = new Message
        {
            ConversationId = conversation.Id,
            SenderId = senderId,
            ReceiverId = receiverId,
            Content = content,
            SentAt = DateTime.UtcNow,
            IsSent = true,
            IsReceived = ChatHub.IsUserOnline(receiverId),
            IsSeen = false
        };

        await _messageRepository.AddMessageAsync(message);

        var sender = await _userManager.FindByIdAsync(senderId.ToString());
        if (ChatHub.IsUserOnline(receiverId))
        {
            await _hubContext.Clients.Group(receiverId.ToString()).SendAsync("ReceiveMessage", new
            {
                Id = message.Id,
                ConversationId = message.ConversationId,
                SenderId = message.SenderId,
                SenderName = sender?.UserName,
                ReceiverId = receiverId,
                Content = message.Content,
                SentAt = message.SentAt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                IsSent = message.IsSent,
                IsReceived = message.IsReceived,
                IsSeen = message.IsSeen
            });

            await _hubContext.Clients.Group(receiverId.ToString()).SendAsync("NewMessageNotification", sender?.UserName, content);
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
        _logger.LogInformation("Editing message {MessageId} by user {UserId}", messageId, userId);

        var message = await _messageRepository.GetMessageByIdAsync(messageId);
        if (message == null)
        {
            _logger.LogWarning("Message {MessageId} not found", messageId);
            throw new KeyNotFoundException("Message not found.");
        }

        if (message.SenderId != userId)
        {
            _logger.LogWarning("User {UserId} is not authorized to edit message {MessageId}", userId, messageId);
            throw new UnauthorizedAccessException("You are not authorized to edit this message.");
        }

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
        _logger.LogInformation("Deleting message {MessageId} by user {UserId}", messageId, userId);

        var message = await _messageRepository.GetMessageByIdAsync(messageId);
        if (message == null)
        {
            _logger.LogWarning("Message {MessageId} not found", messageId);
            throw new KeyNotFoundException("Message not found.");
        }

        if (message.SenderId != userId)
        {
            _logger.LogWarning("User {UserId} is not authorized to delete message {MessageId}", userId, messageId);
            throw new UnauthorizedAccessException("You are not authorized to delete this message.");
        }

        await _messageRepository.DeleteMessageAsync(messageId);

        var otherUserId = message.Conversation.User1Id == userId ? message.Conversation.User2Id : message.Conversation.User1Id;
        if (ChatHub.IsUserOnline(otherUserId))
        {
            await _hubContext.Clients.Group(otherUserId.ToString()).SendAsync("MessageDeleted", messageId);
        }
    }

    public async Task<object> GetMessageSeenStatusAsync(int messageId, int userId)
    {
        _logger.LogInformation("Checking seen status for message {MessageId} by user {UserId}", messageId, userId);

        var message = await _messageRepository.GetMessageByIdAsync(messageId);
        if (message == null)
        {
            _logger.LogWarning("Message {MessageId} not found", messageId);
            throw new KeyNotFoundException("Message not found.");
        }

        if (message.SenderId != userId)
        {
            _logger.LogWarning("User {UserId} is not authorized to check status of message {MessageId}", userId, messageId);
            throw new UnauthorizedAccessException("You are not authorized to check the status of this message.");
        }

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
}