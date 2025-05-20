using ChatApi.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ChatApi.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly AppDbContext _context;

    public ChatHub(IHubContext<ChatHub> hubContext, AppDbContext context)
    {
        _hubContext = hubContext;
        _context = context;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, userId);
            Console.WriteLine($"User {userId} connected and added to group {userId}");

            var user = await _context.Users.FindAsync(int.Parse(userId));
            if (user != null)
            {
                user.LastSeen = DateTime.UtcNow;
                Console.WriteLine($"Attempting to update LastSeen for user {userId} to {user.LastSeen}");
                try
                {
                    await _context.SaveChangesAsync();
                    Console.WriteLine($"LastSeen updated successfully for user {userId}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to update LastSeen for user {userId}: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine($"User {userId} not found in the database");
            }

            await Clients.All.SendAsync("UserStatusChanged", userId, true);
        }
        await base.OnConnectedAsync();
    }


    public override async Task OnDisconnectedAsync(Exception exception)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, userId);
            Console.WriteLine($"User {userId} disconnected and removed from group {userId}");

            var user = await _context.Users.FindAsync(int.Parse(userId));
            if (user != null)
            {
                user.LastSeen = DateTime.UtcNow;
                Console.WriteLine($"Attempting to update LastSeen for user {userId} to {user.LastSeen}");
                try
                {
                    await _context.SaveChangesAsync();
                    Console.WriteLine($"LastSeen updated successfully for user {userId}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to update LastSeen for user {userId}: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine($"User {userId} not found in the database");
            }

            await Clients.All.SendAsync("UserStatusChanged", userId, false);
        }
        await base.OnDisconnectedAsync(exception);
    }


    public async Task SendMessage(int conversationId, string content)
    {
        var userId = int.Parse(Context.User.FindFirst(ClaimTypes.NameIdentifier).Value);
        var conversation = await _context.Conversations
            .FirstOrDefaultAsync(c => c.Id == conversationId);

        if (conversation == null) return;

        var sender = await _context.Users.FindAsync(userId);
        var message = new
        {
            ConversationId = conversationId,
            SenderId = userId,
            SenderName = sender.UserName,
            Content = content,
            SentAt = DateTime.UtcNow
        };

        var otherUserId = conversation.User1Id == userId ? conversation.User2Id : conversation.User1Id;
        await _hubContext.Clients.Group(otherUserId.ToString()).SendAsync("ReceiveMessage", message);
    }



    public async Task MarkMessageAsSeen(int messageId)
    {
        var userId = int.Parse(Context.User.FindFirst(ClaimTypes.NameIdentifier).Value);
        var message = await _context.Messages
            .Include(m => m.Conversation)
            .FirstOrDefaultAsync(m => m.Id == messageId);

        if (message == null) return;

        var otherUserId = message.Conversation.User1Id == message.SenderId ? message.Conversation.User2Id : message.Conversation.User1Id;
        if (userId != otherUserId) return;

        message.IsSeen = true;
        await _context.SaveChangesAsync();

        await _hubContext.Clients.Group(message.SenderId.ToString()).SendAsync("MessageSeen", messageId);
    }
}