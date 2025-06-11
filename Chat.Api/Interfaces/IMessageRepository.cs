namespace ChatApi.Interfaces;
public interface IMessageRepository
{
    Task<List<Message>> GetMessagesByConversationIdAsync(int conversationId);
    Task<Message> GetMessageByIdAsync(int messageId);
    Task AddMessageAsync(Message message);
    Task UpdateMessageAsync(Message message);
    Task DeleteMessageAsync(int messageId);
    Task<List<Message>> SearchMessagesAsync(int userId, string query);
}
