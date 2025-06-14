using Chat.Api.DTOs;
namespace Chat.Api.Interfaces;

public interface IConversationService
{
    Task<List<ConversationResponseDto>> GetConversationsAsync(int userId);
    Task<OpenConversationResponseDto> OpenConversationAsync(int conversationId, int userId);
    Task DeleteConversationAsync(int conversationId, int userId);
    Task<List<SearchResponseDto>> SearchMessagesAsync(int userId, string query);
}
