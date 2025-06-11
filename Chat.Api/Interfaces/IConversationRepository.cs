using ChatApi.Models;

namespace ChatApi.Interfaces;
public interface IConversationRepository
{
    Task<List<Conversation>> GetUserConversationsAsync(int userId);
    Task<Conversation> GetConversationByIdAsync(int conversationId);
    Task<Conversation> GetOrCreateConversationAsync(int senderId, int receiverId);
    Task DeleteConversationAsync(int conversationId, int userId);
    Task SaveChangesAsync();
    Task AddConversationAsync(Conversation conversation); // إضافة الـ Method
}