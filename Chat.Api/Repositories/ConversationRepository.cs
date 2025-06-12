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
    private readonly ILogger<ConversationRepository> _logger;

    public ConversationRepository(AppDbContext context, UserManager<User> userManager, ILogger<ConversationRepository> logger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<List<Conversation>> GetUserConversationsAsync(int userId)
    {
        var conversations = await _context.Conversations
            .Include(c => c.User1)
            .Include(c => c.User2)
            .Include(c => c.Messages)
            .ThenInclude(m => m.Sender)
            .Where(c => (c.User1Id == userId || c.User2Id == userId) &&
                       !(c.User1Id == userId && c.IsDeleted_ForUser_1) &&
                       !(c.User2Id == userId && c.IsDeleted_ForUser_2))
            .AsNoTracking()
            .ToListAsync();
        _logger.LogInformation("Fetched {Count} conversations for user {UserId}", conversations.Count, userId);
        return conversations;
    }

    public async Task<Conversation> GetConversationByIdAsync(int conversationId)
    {
        var conversation = await _context.Conversations
            .Include(c => c.User1)
            .Include(c => c.User2)
            .Include(c => c.Messages)
            .ThenInclude(m => m.Sender)
            .FirstOrDefaultAsync(c => c.Id == conversationId);
        _logger.LogInformation("Conversation {ConversationId} retrieved: {Result}", conversationId, conversation != null);
        return conversation;
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
        _logger.LogInformation("Created new conversation {ConversationId} for users {User1Id} and {User2Id}", conversation.Id, senderId, receiverId);

        return conversation;
    }

    public async Task DeleteConversationAsync(int conversationId, int userId)
    {
        var conversation = await _context.Conversations
            .Include(c => c.Messages)
            .FirstOrDefaultAsync(c => c.Id == conversationId);
        _logger.LogInformation("Conversation {ConversationId} found for deletion: {Result}", conversationId, conversation != null);

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
        _logger.LogInformation("Deleted conversation {ConversationId} for user {UserId}", conversationId, userId);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }

    public async Task AddConversationAsync(Conversation conversation)
    {
        _context.Conversations.Add(conversation);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Added conversation {ConversationId}", conversation.Id);
    }
}