using Chat.Api.Data;
using ChatApi.Interfaces;
using ChatApi.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ChatApi.Repositories;
public class ConversationRepository : IConversationRepository
{
    private readonly AppDbContext _context;
    private readonly UserManager<User> _userManager;

    public ConversationRepository(AppDbContext context, UserManager<User> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<List<Conversation>> GetUserConversationsAsync(int userId)
    {
        return await _context.Conversations
            .Include(c => c.User1)
            .Include(c => c.User2)
            .Include(c => c.Messages)
            .ThenInclude(m => m.Sender)
            .Where(c => (c.User1Id == userId || c.User2Id == userId) &&
                       !(c.User1Id == userId && c.IsDeleted_ForUser_1) &&
                       !(c.User2Id == userId && c.IsDeleted_ForUser_2))
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Conversation> GetConversationByIdAsync(int conversationId)
    {
        return await _context.Conversations
            .Include(c => c.User1)
            .Include(c => c.User2)
            .Include(c => c.Messages)
            .ThenInclude(m => m.Sender)
            .FirstOrDefaultAsync(c => c.Id == conversationId);
    }

    public async Task<Conversation> GetOrCreateConversationAsync(int senderId, int receiverId)
    {
        var receiver = await _userManager.FindByIdAsync(receiverId.ToString());
        if (receiver == null)
            throw new KeyNotFoundException("Receiver does not exist.");

        if (senderId == receiverId)
            throw new InvalidOperationException("Cannot start a conversation with yourself.");

        var existingConversation = await _context.Conversations
            .FirstOrDefaultAsync(c =>
                (c.User1Id == senderId && c.User2Id == receiverId) ||
                (c.User1Id == receiverId && c.User2Id == senderId));

        if (existingConversation != null)
            return existingConversation;

        var conversation = new Conversation
        {
            User1Id = senderId,
            User2Id = receiverId,
            CreatedAt = DateTime.UtcNow,
            IsDeleted_ForUser_1 = false,
            IsDeleted_ForUser_2 = false
        };

        _context.Conversations.Add(conversation);
        await _context.SaveChangesAsync();

        return conversation;
    }

    public async Task DeleteConversationAsync(int conversationId, int userId)
    {
        var conversation = await _context.Conversations
            .Include(c => c.Messages)
            .FirstOrDefaultAsync(c => c.Id == conversationId);

        if (conversation == null) return;

        if (conversation.User1Id == userId)
            conversation.IsDeleted_ForUser_1 = true;
        else if (conversation.User2Id == userId)
            conversation.IsDeleted_ForUser_2 = true;

        if (conversation.IsDeletedFromBoth)
        {
            _context.Messages.RemoveRange(conversation.Messages);
            _context.Conversations.Remove(conversation);
        }

        await _context.SaveChangesAsync();
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }

    public async Task AddConversationAsync(Conversation conversation) // تنفيذ الـ Method
    {
        _context.Conversations.Add(conversation);
        await _context.SaveChangesAsync();
    }
}