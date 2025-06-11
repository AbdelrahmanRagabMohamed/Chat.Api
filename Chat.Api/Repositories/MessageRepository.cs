using Chat.Api.Data;
using ChatApi.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ChatApi.Repositories;

public class MessageRepository : IMessageRepository
{
    private readonly AppDbContext _context;

    public MessageRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Message>> GetMessagesByConversationIdAsync(int conversationId)
    {
        return await _context.Messages
            .Where(m => m.ConversationId == conversationId)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Message> GetMessageByIdAsync(int messageId)
    {
        return await _context.Messages
            .Include(m => m.Conversation)
            .FirstOrDefaultAsync(m => m.Id == messageId);
    }

    public async Task AddMessageAsync(Message message)
    {
        _context.Messages.Add(message);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateMessageAsync(Message message)
    {
        _context.Messages.Update(message);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteMessageAsync(int messageId)
    {
        var message = await _context.Messages.FindAsync(messageId);
        if (message != null)
            _context.Messages.Remove(message);
        await _context.SaveChangesAsync();
    }

    public async Task<List<Message>> SearchMessagesAsync(int userId, string query)
    {
        return await _context.Messages
            .Where(m => (m.SenderId == userId || m.ReceiverId == userId) && m.Content.Contains(query))
            .Include(m => m.Sender)
            .Include(m => m.Conversation)
            .OrderByDescending(m => m.SentAt)
            .AsNoTracking()
            .ToListAsync();
    }
}