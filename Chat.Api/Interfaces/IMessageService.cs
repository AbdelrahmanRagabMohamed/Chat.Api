using Chat.Api.DTOs;

namespace Chat.Api.Interfaces;

public interface IMessageService

{
    Task<SendMessageResponseDto> SendMessageAsync(int senderId, SendMessageDto dto);
    Task<MessageResponseDto> EditMessageAsync(int messageId, int userId, string newContent);
    Task DeleteMessageAsync(int messageId, int userId);
    Task<object> GetMessageSeenStatusAsync(int messageId, int userId);
}
