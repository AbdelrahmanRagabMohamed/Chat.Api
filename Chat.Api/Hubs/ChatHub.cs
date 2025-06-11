using Chat.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ChatApi.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private static readonly Dictionary<int, string> _connectedUsers = new Dictionary<int, string>();

    private readonly IHubContext<ChatHub> _hubContext;
    private readonly AppDbContext _context;

    public ChatHub(IHubContext<ChatHub> hubContext, AppDbContext context)
    {
        _hubContext = hubContext;
        _context = context;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = int.Parse(Context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        _connectedUsers[userId] = Context.ConnectionId;
        await Groups.AddToGroupAsync(Context.ConnectionId, userId.ToString());
        Console.WriteLine($"User {userId} connected and added to group {userId}");

        // جلب الرسايل المرسلة للمستخدم واللي لسه ما استلمهاش
        var context = Context.GetHttpContext().RequestServices.GetRequiredService<AppDbContext>();
        var unreceivedMessages = await context.Messages
            .Where(m => m.ReceiverId == userId && m.IsSent && !m.IsReceived)
            .ToListAsync();

        if (unreceivedMessages.Any())
        {
            foreach (var message in unreceivedMessages)
            {
                message.IsReceived = true; // تحديث الحالة لـ استلمت
            }
            await context.SaveChangesAsync();

            // إبلاغ المرسلين إن الرسايل بتاعتهم استلمت
            var senderIds = unreceivedMessages.Select(m => m.SenderId).Distinct();
            foreach (var senderId in senderIds)
            {
                if (IsUserOnline(senderId))
                {
                    var senderMessages = unreceivedMessages
                        .Where(m => m.SenderId == senderId)
                        .Select(m => m.Id)
                        .ToList();
                    await Clients.Group(senderId.ToString())
                        .SendAsync("MessagesReceived", senderMessages);
                }
            }
        }

        // إبلاغ المستخدمين الآخرين إن المستخدم ده بقى أونلاين
        var conversations = await context.Conversations
            .Where(c => c.User1Id == userId || c.User2Id == userId)
            .ToListAsync();

        foreach (var conversation in conversations)
        {
            var otherUserId = conversation.User1Id == userId ? conversation.User2Id : conversation.User1Id;
            await Clients.Group(otherUserId.ToString()).SendAsync("UserOnline", userId);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception exception)
    {
        var userId = int.Parse(Context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        _connectedUsers.Remove(userId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, userId.ToString());
        Console.WriteLine($"User {userId} disconnected");

        // إبلاغ المستخدمين الآخرين إن المستخدم ده بقى أوفلاين
        var context = Context.GetHttpContext().RequestServices.GetRequiredService<AppDbContext>();
        var conversations = await context.Conversations
            .Where(c => c.User1Id == userId || c.User2Id == userId)
            .ToListAsync();

        foreach (var conversation in conversations)
        {
            var otherUserId = conversation.User1Id == userId ? conversation.User2Id : conversation.User1Id;
            await Clients.Group(otherUserId.ToString()).SendAsync("UserOffline", userId);
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


    public static bool IsUserOnline(int userId)
    {
        return _connectedUsers.ContainsKey(userId);
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


